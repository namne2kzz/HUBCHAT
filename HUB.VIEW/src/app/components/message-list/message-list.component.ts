import {
  ChangeDetectionStrategy, Component, computed, effect, ElementRef, HostListener,
  inject, input, output, signal, untracked,
} from '@angular/core';
import { DatePipe, DOCUMENT, NgTemplateOutlet } from '@angular/common';
import { MessageDto, ReactionGroup, ReactionToggle } from '../../models/message.model';
import { MESSAGE_EDIT_WINDOW_MS } from '../../utils/message-edit.util';
import { DirectoryUser } from '../../models/directory.model';
import { MentionLabel, renderMarkdown } from '../../utils/markdown.util';
import { groupReactions } from '../../utils/reaction.util';
import { ConfirmDialogService } from '../../services/confirm-dialog.service';
import { PresenceService } from '../../services/presence.service';
import { ConfigService } from '../../core/services/config.service';
import { AttachmentViewComponent } from '../attachment-view/attachment-view.component';
import { WorkItemUnfurlComponent } from '../work-item-unfurl/work-item-unfurl.component';
import { extractWorkItemKeys } from '../../utils/work-item-link.util';

interface MessageRow {
  msg: MessageDto;
  /** True when this is the first message in a consecutive same-author group (> 5 min gap). */
  showHeader: boolean;
  /** True when this message was sent by the current user. */
  isOwn: boolean;
  /** True when the next message is from the same author (suppress bottom radius). */
  isContinued: boolean;
  /** Day-separator label (e.g. "Today") shown above this row when the date changes; null otherwise. */
  dateLabel: string | null;
  /** True when someone else's message @mentions the current user — the bubble is tinted. */
  mentionsMe: boolean;
  /** Reactions grouped one chip per emoji, marking the ones the viewer added. */
  reactions: ReactionGroup[];
  /** True when the message is a deletion tombstone. */
  isDeleted: boolean;
  /** True when the viewer may edit it (author, within the window, not deleted). */
  canEdit: boolean;
  /** True when the viewer may delete it (author within window, or a moderator). */
  canDelete: boolean;
  /** The message this one quote-replies to (resolved from messagesById); null if none/unresolved. */
  quoted: MessageDto | null;
  /** True when this message was forwarded from elsewhere. */
  forwarded: boolean;
  /** True when the message is pinned. */
  isPinned: boolean;
  /** Message body rendered from Markdown to safe HTML. */
  html: string;
  /** DASHBOARD work-item keys linked in the body, to unfurl as preview cards; empty when none. */
  workItemKeys: string[];
}

/**
 * Renders a bubble-style chat list.
 * Own messages appear on the right (accent bubble); others on the left (surface bubble + avatar).
 * Consecutive messages from the same author within 5 minutes are visually grouped.
 */
@Component({
  selector: 'app-message-list',
  standalone: true,
  imports: [DatePipe, NgTemplateOutlet, AttachmentViewComponent, WorkItemUnfurlComponent],
  templateUrl: './message-list.component.html',
  styleUrl: './message-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MessageListComponent {
  private readonly document = inject(DOCUMENT);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly presence = inject(PresenceService);
  private readonly config   = inject(ConfigService);
  private readonly host     = inject(ElementRef<HTMLElement>);

  /** Whether the view is scrolled to (near) the bottom. Drives auto-follow + the scroll-down button. */
  protected readonly atBottom = signal(true);

  /** True while an older-messages page is loading (set when we trigger loadMore). */
  private loadingOlder = false;
  /** Scroll height + offset captured just before an older-page load, to anchor position after prepend. */
  private preLoadScrollHeight = 0;
  private preLoadScrollTop = 0;

  constructor() {
    effect(() => {
      this.messages(); // track list changes
      const el = this.host.nativeElement;
      if (this.loadingOlder) {
        // Older messages prepended — keep the previously-top message anchored (no jump).
        setTimeout(() => {
          el.scrollTop = el.scrollHeight - this.preLoadScrollHeight + this.preLoadScrollTop;
          this.loadingOlder = false;
        });
      } else if (untracked(() => this.atBottom())) {
        // New message appended while at bottom — follow it.
        setTimeout(() => this.scrollToBottom());
      }
    });
  }

  /**
   * Tracks bottom proximity (auto-follow + scroll-down button) and triggers infinite
   * scroll-up: when the user nears the top and more history exists, request an older page.
   */
  @HostListener('scroll')
  protected onScroll(): void {
    const el = this.host.nativeElement;
    this.atBottom.set(el.scrollHeight - el.scrollTop - el.clientHeight < 150);

    if (el.scrollTop < 120 && this.hasMore() && !this.loadingOlder) {
      this.loadingOlder = true;
      this.preLoadScrollHeight = el.scrollHeight;
      this.preLoadScrollTop = el.scrollTop;
      this.loadMore.emit();
    }
  }

  /** Scrolls the list to the newest message. */
  protected scrollToBottom(): void {
    const el = this.host.nativeElement;
    el.scrollTop = el.scrollHeight;
  }

  readonly messages      = input.required<MessageDto[]>();
  /** Map of authorId → resolved directory profile (name + avatar class). */
  readonly authors       = input<Record<string, DirectoryUser>>({});
  readonly hasMore       = input<boolean>(false);
  /** The current user's id — messages matching this id are shown on the right. */
  readonly currentUserId = input<string>('');
  /**
   * Id of the message to flash-highlight (set by the parent when a search result is clicked).
   * The corresponding bubble row receives the `bubble-row-highlight` class.
   */
  readonly highlightedId = input<string | null>(null);

  /**
   * DM "Seen" marker (CF-15): the other participant's read point in ms. The last of the viewer's own
   * messages at or before this time shows a "Seen" label. 0 disables it (non-DM, or never read).
   */
  readonly seenAtMs = input<number>(0);

  /** Whether the viewer may react (channel members only — the API refuses non-members). */
  readonly canReact = input<boolean>(false);
  /** Whether the viewer may moderate (delete anyone's message) — Owner/Admin or ManageChannels. */
  readonly canModerate = input<boolean>(false);
  /** All messages keyed by id, so a quote-reply can show a preview of the quoted message. */
  readonly messagesById = input<Record<string, MessageDto>>({});

  /** Emits when older messages are requested. */
  readonly loadMore = output<void>();
  /** Emits when the viewer toggles a reaction (adds it, or removes it when they already have it). */
  readonly toggleReaction = output<ReactionToggle>();
  /** Emits the message id + new body when the viewer saves an edit. */
  readonly editMessage = output<{ messageId: string; body: string }>();
  /** Emits the message id when the viewer deletes a message. */
  readonly deleteMessage = output<string>();
  /** Emits the top-level message id when the viewer opens its thread. */
  readonly openThread = output<string>();
  /** Emits the message id when the viewer picks "Reply" (quote-reply). */
  readonly replyToMessage = output<string>();
  /** Emits the message id when the viewer picks "Forward". */
  readonly forwardMessage = output<string>();
  /** Emits { messageId, pin } when the viewer pins/unpins a message. */
  readonly togglePin = output<{ messageId: string; pin: boolean }>();

  /** Id of the message being edited inline; null when none. */
  protected readonly editingId = signal<string | null>(null);
  /** Working copy of the body while editing. */
  protected readonly editDraft = signal('');

  /** Emoji offered in the quick picker. */
  protected readonly quickEmojis = ['👍', '❤️', '😂', '🎉', '😮', '😢', '👀', '✅'] as const;
  /** Message id whose emoji picker is open; null when closed. */
  protected readonly pickerFor = signal<string | null>(null);

  /**
   * Toggles the viewer's reaction and closes the picker.
   * @param messageId Message reacted to.
   * @param emoji Emoji clicked.
   */
  protected toggle(messageId: string, emoji: string): void {
    if (!this.canReact()) return;
    this.toggleReaction.emit({ messageId, emoji });
    this.pickerFor.set(null);
  }

  /** Opens or closes the emoji picker for a message (closes the … menu if it was open). @param messageId Message id. */
  protected togglePicker(messageId: string): void {
    this.menuFor.set(null);
    this.pickerFor.update(id => (id === messageId ? null : messageId));
  }

  /** Message id whose "more actions" (…) menu is open; null when closed. */
  protected readonly menuFor = signal<string | null>(null);

  /** Opens/closes the … menu for a message (closes the picker if it was open). @param messageId Message id. */
  protected toggleMenu(messageId: string): void {
    this.pickerFor.set(null);
    this.menuFor.update(id => (id === messageId ? null : messageId));
  }

  /**
   * Closes any open reaction picker / … menu when the user clicks outside the message toolbar.
   * Click-outside (not mouseleave) is deliberate: the menu sits a few px below the floating toolbar,
   * and closing on mouseleave made the gap between them impossible to cross — the menu vanished before
   * the cursor reached it.
   * @param target The clicked element.
   */
  @HostListener('document:click', ['$event.target'])
  protected onDocumentClick(target: EventTarget | null): void {
    if (this.pickerFor() === null && this.menuFor() === null) return;
    const el = target as HTMLElement | null;
    if (el?.closest('.msg-toolbar')) return; // click inside the toolbar/menu — leave it open
    this.pickerFor.set(null);
    this.menuFor.set(null);
  }

  /** Picks "Reply" from the menu. @param messageId Message to quote. */
  protected pickReply(messageId: string): void { this.menuFor.set(null); this.replyToMessage.emit(messageId); }

  /** Picks "Forward" from the menu. @param messageId Message to forward. */
  protected pickForward(messageId: string): void { this.menuFor.set(null); this.forwardMessage.emit(messageId); }

  /** Picks "Pin"/"Unpin" from the menu. @param row The row. */
  protected pickPin(row: MessageRow): void { this.menuFor.set(null); this.togglePin.emit({ messageId: row.msg.id, pin: !row.isPinned }); }

  /** A short preview of a quoted message for the inline quote box. @param m The quoted message. */
  protected quotedPreview(m: MessageDto): string {
    if (m.isDeleted) return 'Deleted message';
    return m.body.length > 80 ? m.body.slice(0, 80) + '…' : m.body;
  }

  /** Opens inline edit for a message, seeding the draft with its current body. @param row The row. */
  protected startEdit(row: MessageRow): void {
    this.menuFor.set(null);
    this.editingId.set(row.msg.id);
    this.editDraft.set(row.msg.body);
  }

  /** Cancels inline edit without saving. */
  protected cancelEdit(): void {
    this.editingId.set(null);
    this.editDraft.set('');
  }

  /** Emits the edit when the draft is non-empty and changed; closes edit mode. @param row The row. */
  protected saveEdit(row: MessageRow): void {
    const body = this.editDraft().trim();
    if (body && body !== row.msg.body) this.editMessage.emit({ messageId: row.msg.id, body });
    this.cancelEdit();
  }

  /** Keeps the edit draft in sync with the textarea. @param e Input event. */
  protected onEditInput(e: Event): void {
    this.editDraft.set((e.target as HTMLTextAreaElement).value);
  }

  /** Enter saves, Shift+Enter is a newline, Escape cancels. @param e Key event. @param row The row. */
  protected onEditKeydown(e: KeyboardEvent, row: MessageRow): void {
    if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); this.saveEdit(row); }
    else if (e.key === 'Escape') { e.preventDefault(); this.cancelEdit(); }
  }

  /** Confirms then emits a delete. @param messageId Message id. */
  protected async confirmDelete(messageId: string): Promise<void> {
    this.menuFor.set(null);
    const ok = await this.confirmDialog.confirm({
      title: 'Delete message?',
      description: 'This message will be removed for everyone in the channel. This cannot be undone.',
      confirmText: 'Delete',
      tone: 'danger',
    });
    if (ok) this.deleteMessage.emit(messageId);
  }

  /** Tooltip listing who reacted, e.g. "Alice, Bob". @param g Reaction group. */
  protected reactorNames(g: ReactionGroup): string {
    return g.userIds.map(id => this.authors()[id]?.name ?? 'Someone').join(', ');
  }

  /** Computed rows with grouping metadata. */
  readonly rows = computed<MessageRow[]>(() => {
    const list = this.messages();
    const me   = this.currentUserId();
    return list.map((msg, i) => {
      const prev = list[i - 1];
      const next = list[i + 1];
      const gap  = prev
        ? new Date(msg.createdAt).getTime() - new Date(prev.createdAt).getTime()
        : Infinity;
      const nextGap = next
        ? new Date(next.createdAt).getTime() - new Date(msg.createdAt).getTime()
        : Infinity;

      const showHeader  = !prev || prev.authorId !== msg.authorId || gap > 5 * 60_000;
      const isContinued = !!next && next.authorId === msg.authorId && nextGap <= 5 * 60_000;

      const sameDay   = prev && new Date(prev.createdAt).toDateString() === new Date(msg.createdAt).toDateString();
      const dateLabel = sameDay ? null : this.dayLabel(msg.createdAt);

      const isOwn      = msg.authorId === me;
      const mentionsMe = !isOwn && !!me && (msg.mentions ?? []).includes(me);
      const reactions  = groupReactions(msg.reactions, me);
      const isDeleted  = !!msg.isDeleted;
      // Mirror the server: author may edit/delete within 24h; a moderator may delete anything, anytime.
      const inWindow   = Date.now() - new Date(msg.createdAt).getTime() <= MESSAGE_EDIT_WINDOW_MS;
      const canEdit    = !isDeleted && isOwn && inWindow;
      const canDelete  = !isDeleted && ((isOwn && inWindow) || this.canModerate());
      const html       = isDeleted ? '' : renderMarkdown(msg.body, this.mentionLabels(msg));
      const quoted     = msg.replyToId ? (this.messagesById()[msg.replyToId] ?? null) : null;
      const forwarded  = !!msg.forwardedFromId;
      const isPinned   = !!msg.isPinned;
      const workItemKeys = isDeleted ? [] : extractWorkItemKeys(msg.body, this.config.dashboardUrl);
      return { msg, showHeader, isOwn, isContinued, dateLabel, mentionsMe, reactions, isDeleted, canEdit, canDelete, quoted, forwarded, isPinned, html, workItemKeys };
    });
  });

  /**
   * Id of the viewer's own message under which to show "Seen" (CF-15): their most recent non-deleted
   * message at or before the other participant's read point. Empty when there is nothing seen yet.
   */
  readonly seenMessageId = computed(() => {
    const at = this.seenAtMs();
    if (!at) return '';
    const me = this.currentUserId();
    let seenId = '';
    for (const m of this.messages()) {
      if (m.authorId === me && !m.isDeleted && new Date(m.createdAt).getTime() <= at) seenId = m.id;
    }
    return seenId;
  });

  /** Human day label for a date separator: "Today", "Yesterday", or a formatted date. */
  private dayLabel(iso: string): string {
    const d     = new Date(iso);
    const today = new Date();
    const yest  = new Date(); yest.setDate(today.getDate() - 1);
    if (d.toDateString() === today.toDateString()) return 'Today';
    if (d.toDateString() === yest.toDateString())  return 'Yesterday';
    return d.toLocaleDateString(undefined, { weekday: 'short', month: 'short', day: 'numeric' });
  }

  /**
   * Names to highlight as @mentions in a message — only users the message really mentions, and only once
   * their names are resolved (the page resolves mentioned ids alongside authors).
   * @param msg The message.
   * @returns Labels for renderMarkdown.
   */
  private mentionLabels(msg: MessageDto): MentionLabel[] {
    const me = this.currentUserId();
    const users = this.authors();
    return (msg.mentions ?? [])
      .map(id => ({ name: users[id]?.name ?? '', self: id === me }))
      .filter(m => m.name);
  }

  /** Display name for an author id. @param authorId Author id. */
  authorName(authorId: string): string {
    return this.authors()[authorId]?.name ?? `User ${authorId.slice(0, 8)}`;
  }

  /** Avatar letter for an author (first character of their name). @param authorId Author id. */
  authorInitial(authorId: string): string {
    return (this.authors()[authorId]?.name ?? '?').charAt(0).toUpperCase();
  }

  /** The author's custom-status emoji to show beside their name, or '' when none (CF-19). */
  authorStatusEmoji(authorId: string): string {
    return this.presence.getCustomStatus(authorId)?.emoji ?? '';
  }

  /** The author's custom-status note, for a tooltip on the emoji (CF-19). */
  authorStatusText(authorId: string): string {
    return this.presence.getCustomStatus(authorId)?.text ?? '';
  }

  /**
   * Smoothly scrolls the message list to the bubble with the given message id.
   * Called by the parent component when the user clicks a search result.
   * @param id Message id to scroll to.
   */
  scrollToMessage(id: string): void {
    this.document
      .getElementById('msg-' + id)
      ?.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }
}
