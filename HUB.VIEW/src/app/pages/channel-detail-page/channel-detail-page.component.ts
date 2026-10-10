import {
  AfterViewInit, ChangeDetectionStrategy, Component, computed, DestroyRef, inject,
  Injector, OnDestroy, OnInit, signal, ViewChild,
} from '@angular/core';
import { distinctUntilChanged } from 'rxjs';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { ChannelService } from '../../services/channel.service';
import { MessageService } from '../../services/message.service';
import { RealtimeService } from '../../services/realtime.service';
import { DirectoryService } from '../../services/directory.service';
import { AuthService } from '../../services/auth.service';
import { ChannelDto, ChannelType } from '../../models/channel.model';
import { MessageDto } from '../../models/message.model';
import { applyReaction } from '../../utils/reaction.util';
import { mergeMessages } from '../../utils/message-merge.util';
import { messageFromRealtime } from '../../utils/realtime-message.util';
import { DirectoryUser } from '../../models/directory.model';
import { MessageListComponent } from '../../components/message-list/message-list.component';
import { MessageInputComponent, ComposerSubmit } from '../../components/message-input/message-input.component';
import { TypingIndicatorComponent } from '../../components/typing-indicator/typing-indicator.component';
import { ChannelInfoPanelComponent } from '../../components/channel-info-panel/channel-info-panel.component';
import { DatePipe } from '@angular/common';

@Component({
  selector: 'app-channel-detail-page',
  standalone: true,
  imports: [MessageListComponent, MessageInputComponent, TypingIndicatorComponent, ChannelInfoPanelComponent, DatePipe],
  templateUrl: './channel-detail-page.component.html',
  styleUrl: './channel-detail-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChannelDetailPageComponent implements OnInit, AfterViewInit, OnDestroy {
  /** Exposes the channel-type enum to the template (for the header icon). */
  protected readonly ChannelType = ChannelType;
  private readonly channelSvc = inject(ChannelService);
  private readonly messageSvc = inject(MessageService);
  private readonly realtime   = inject(RealtimeService);
  private readonly directory  = inject(DirectoryService);
  private readonly auth       = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector   = inject(Injector);

  /** Reference to the message list child — used to call `scrollToMessage`. */
  @ViewChild(MessageListComponent) private messageListRef?: MessageListComponent;

  protected readonly channel      = signal<ChannelDto | null>(null);
  protected readonly messages     = signal<MessageDto[]>([]);
  protected readonly authors      = signal<Record<string, DirectoryUser>>({});
  protected readonly loading      = signal(true);
  protected readonly hasMore      = signal(false);
  /** Channel "…" info modal (About/Members). */
  protected readonly infoOpen = signal(false);
  protected readonly infoTab  = signal<'about' | 'members'>('about');

  // ── In-channel search ───────────────────────────────────────────────────
  protected readonly searchMode  = signal(false);
  protected readonly searchQuery = signal('');

  /** Id of the message that should flash-highlight after a search result click. */
  protected readonly highlightedMessageId = signal<string | null>(null);

  /**
   * Messages matching the current search query.
   * Non-empty only when search mode is active and a query has been typed.
   */
  protected readonly searchResults = computed(() => {
    const q = this.searchQuery().toLowerCase().trim();
    if (!this.searchMode() || !q) return [];
    return this.messages().filter(m => m.body.toLowerCase().includes(q));
  });

  /** Current user's id — passed to message-list for own-message highlighting. */
  protected readonly currentUserId = computed(() => this.auth.currentUser()?.id ?? '');

  /** Whether a channel is currently selected (drives the empty state). */
  protected readonly hasSelection = computed(() => !!this.channelSvc.selectedId());

  /**
   * Whether the current user may post/react in the open channel. Public channels are readable by
   * any repo member, but sending requires membership — non-members see a "join" bar instead of
   * the composer (mirrors the backend, which returns 403 on post/react for non-members).
   */
  protected readonly canPost = computed(() => this.channel()?.isMember ?? false);
  /** True while a join request is in flight (disables the Join button). */
  protected readonly joining = signal(false);

  /**
   * Display title for the channel header. For DMs this is the other participant's name
   * (the stored channel name is an internal "dm-{id}-{id}" slug); channels use their name.
   */
  protected readonly channelTitle = computed(() => {
    const ch = this.channel();
    if (!ch) return '';
    if ((ch.type === ChannelType.Dm || ch.type === ChannelType.GroupDm) && ch.otherUserId) {
      return this.authors()[ch.otherUserId]?.name ?? ch.name;
    }
    return ch.name;
  });

  channelId = '';
  private cursor: string | null = null;
  private typingTimer: ReturnType<typeof setTimeout> | null = null;

  ngAfterViewInit(): void { /* ViewChild resolves here — no-op but required by interface. */ }

  ngOnInit(): void {
    // Subscribe to realtime ONCE — all filters read this.channelId at event time.
    this.subscribeRealtime();

    // The open channel is app state (ChannelService.selectedId), not a URL param — the URL
    // stays /{orgAlias}/channels. React to selection changes to load the chosen channel.
    toObservable(this.channelSvc.selectedId, { injector: this.injector })
      .pipe(distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(id => id ? this.switchToId(id) : this.resetToEmpty());
  }

  async ngOnDestroy(): Promise<void> {
    clearTimeout(this.typingTimer ?? undefined);
    // NOTE: do NOT leaveChannel here — the shell keeps the connection joined to every channel
    // so the sidebar can show unread counts + typing for non-open channels.
    if (this.channelId) await this.realtime.stopTyping(this.channelId);
  }

  protected loadMore(): void {
    if (!this.hasMore() || !this.cursor) return;
    this.messageSvc.list(this.channelId, this.cursor).subscribe(page => {
      this.messages.update(prev => mergeMessages(prev, page.items, 'replace'));
      this.cursor = page.nextCursor;
      this.hasMore.set(page.nextCursor !== null);
      this.resolveAuthors(page.items);
    });
  }

  /** Sends a composed message (Markdown). @param payload Composer output. */
  protected sendMessage(payload: ComposerSubmit): void {
    if (!payload.body.trim()) return;
    clearTimeout(this.typingTimer ?? undefined);
    this.realtime.stopTyping(this.channelId);
    this.messageSvc.send(this.channelId, { body: payload.body, format: payload.format }).subscribe(msg => {
      this.messages.update(prev => mergeMessages(prev, [msg], 'replace'));
      this.resolveAuthors([msg]);
    });
  }

  /** Joins the open (public) channel so the current user can post; swaps the join bar for the composer. */
  protected join(): void {
    const ch = this.channel();
    if (!ch || this.joining()) return;
    this.joining.set(true);
    this.channelSvc.join(ch.id).subscribe({
      next: () => {
        this.channel.update(c => (c ? { ...c, isMember: true, memberCount: c.memberCount + 1 } : c));
        this.realtime.joinChannel(ch.id);
        this.joining.set(false);
      },
      error: () => this.joining.set(false),
    });
  }

  /**
   * Called on every keystroke in the composer.
   * Broadcasts typing start and schedules a stop after 2 s of idle.
   */
  protected onTyping(): void {
    this.realtime.startTyping(this.channelId);
    clearTimeout(this.typingTimer ?? undefined);
    this.typingTimer = setTimeout(() => this.realtime.stopTyping(this.channelId), 2_000);
  }

  /** Toggles the members panel. */
  /** Opens the channel info modal on a given tab. @param tab 'about' | 'members'. */
  protected openInfo(tab: 'about' | 'members'): void {
    this.infoTab.set(tab);
    this.infoOpen.set(true);
  }

  /** Updates the header member count when the members panel adds/removes someone. @param n New count. */
  protected onMemberCount(n: number): void {
    this.channel.update(c => c ? { ...c, memberCount: n } : c);
  }

  /** Applies a topic edit from the info modal to the local channel copy. @param topic New topic. */
  protected onTopicChanged(topic: string): void {
    this.channel.update(c => c ? { ...c, topic } : c);
  }

  /** Called after leaving the channel from the info modal — closes it and clears the selection. */
  protected onChannelLeft(): void {
    this.infoOpen.set(false);
    this.channelSvc.selectChannel('');
  }

  /** After joining from the info panel: mark membership so the composer replaces the join bar. */
  protected onChannelJoined(): void {
    this.infoOpen.set(false);
    this.channel.update(c => (c ? { ...c, isMember: true, memberCount: c.memberCount + 1 } : c));
    this.realtime.joinChannel(this.channelId);
  }

  /** After an owner switches visibility: reflect the new type/icon in the header. @param isPrivate New visibility. */
  protected onVisibilityChanged(isPrivate: boolean): void {
    this.channel.update(c => (c
      ? { ...c, isPrivate, type: isPrivate ? ChannelType.Private : ChannelType.Public }
      : c));
  }

  /** After ownership is transferred away: refetch the channel so the caller's demoted role (Admin) is reflected. */
  protected onOwnershipTransferred(): void {
    this.loadChannel();
  }

  /** Toggles in-channel search mode; clears the query when closing. */
  protected toggleSearch(): void {
    this.searchMode.update(v => !v);
    if (!this.searchMode()) this.searchQuery.set('');
  }

  /** Updates the search query from native input event. @param event Native input event. */
  protected onSearchInput(event: Event): void {
    this.searchQuery.set((event.target as HTMLInputElement).value);
  }

  /**
   * Scrolls the message list to the given message and briefly highlights its bubble.
   * Called when the user clicks a result in the search results panel.
   * @param id Message id to jump to.
   */
  protected scrollToMessage(id: string): void {
    this.highlightedMessageId.set(id);
    this.messageListRef?.scrollToMessage(id);
    // Remove the highlight class after the animation completes (1.8 s).
    setTimeout(() => this.highlightedMessageId.set(null), 2_000);
  }

  /**
   * Display name for a message author, used by the search results panel.
   * Falls back to a short id prefix when the profile has not yet been resolved.
   * @param authorId Author user id.
   */
  protected authorName(authorId: string): string {
    return this.authors()[authorId]?.name ?? `User ${authorId.slice(0, 8)}`;
  }

  /**
   * Splits a message body around the first occurrence of the query so the
   * search results panel can render the matching segment in a highlight `<mark>`.
   * Pre/post segments are trimmed for compact display.
   * Returns null if the query is not found (should not happen in practice).
   * @param text  Full message body.
   * @param query Active search query.
   */
  protected highlightParts(
    text: string,
    query: string,
  ): { pre: string; match: string; post: string } | null {
    const q   = query.toLowerCase();
    const idx = text.toLowerCase().indexOf(q);
    if (idx === -1) return null;
    const pre   = idx > 35 ? '…' + text.slice(idx - 25, idx) : text.slice(0, idx);
    const match = text.slice(idx, idx + query.length);
    const rest  = text.slice(idx + query.length);
    const post  = rest.length > 60 ? rest.slice(0, 60) + '…' : rest;
    return { pre, match, post };
  }

  // ── Private ──────────────────────────────────────────────────────────────

  /** Switches to a channel by id (selection-driven). @param id Channel id. */
  private switchToId(id: string): void {
    // Keep the previous channel joined (shell manages group membership for sidebar unread/typing).
    this.resetState();
    this.loading.set(true);
    this.initChannel(id);
  }

  /** Clears to the "no channel selected" empty state. */
  private resetToEmpty(): void {
    this.resetState();
    this.channelId = '';
    this.loading.set(false);
  }

  /** Resets per-channel view state (messages, panels, search). */
  private resetState(): void {
    clearTimeout(this.typingTimer ?? undefined);
    this.messages.set([]);
    this.channel.set(null);
    this.cursor = null;
    this.hasMore.set(false);
    this.infoOpen.set(false);
    this.searchMode.set(false);
    this.searchQuery.set('');
  }

  private initChannel(id: string): void {
    this.channelId = id;
    this.loadChannel();
    this.loadMessages();
    this.realtime.joinChannel(id);
  }

  private loadChannel(): void {
    this.channelSvc.get(this.channelId).subscribe(ch => {
      this.channel.set(ch);
      // Resolve the other DM participant's name for the header title.
      if ((ch.type === ChannelType.Dm || ch.type === ChannelType.GroupDm) && ch.otherUserId) {
        this.resolveAuthors([{ authorId: ch.otherUserId } as MessageDto]);
      }
    });
  }

  private loadMessages(): void {
    this.messageSvc.list(this.channelId).subscribe(page => {
      // Merge rather than set: a realtime push can land between the query and its response.
      this.messages.update(prev => mergeMessages(prev, page.items, 'replace'));
      this.cursor = page.nextCursor;
      this.hasMore.set(page.nextCursor !== null);
      this.loading.set(false);
      this.resolveAuthors(page.items);
    });
  }

  /** Page size for reconnect catch-up (the API maximum). */
  private static readonly CatchUpPageSize = 100;

  /** Beyond this many catch-up pages the gap is so large that a fresh first page is cheaper. */
  private static readonly CatchUpMaxPages = 5;

  /**
   * Recovers what was pushed while the connection was down. SignalR's Redis backplane does not store
   * messages, so anything sent during the gap is simply gone from the realtime stream.
   * Re-joins the group FIRST: from that moment new messages arrive by push, and everything before it is
   * covered by the fetch — fetching first would leave a window between the two where messages fall through.
   */
  private async catchUp(): Promise<void> {
    const channelId = this.channelId;
    if (!channelId) return;

    await this.realtime.joinChannel(channelId).catch(() => { /* the shell retries joins on reconnect */ });

    const newest = this.messages().at(-1);
    if (!newest) { this.loadMessages(); return; }
    this.fetchAfter(channelId, newest.id, 1);
  }

  /** One catch-up page; recurses until caught up, falls back to a reload when the gap is too large or the anchor is gone. */
  private fetchAfter(channelId: string, afterId: string, page: number): void {
    this.messageSvc.listAfter(channelId, afterId, ChannelDetailPageComponent.CatchUpPageSize).subscribe({
      next: items => {
        if (channelId !== this.channelId) return; // user switched channel meanwhile
        this.messages.update(prev => mergeMessages(prev, items, 'replace'));
        this.resolveAuthors(items);
        if (items.length < ChannelDetailPageComponent.CatchUpPageSize) return; // caught up
        if (page >= ChannelDetailPageComponent.CatchUpMaxPages) { this.reloadMessages(); return; }
        this.fetchAfter(channelId, items[items.length - 1].id, page + 1);
      },
      // 404 = anchor unknown to the server (e.g. a stale stub); any failure → start over from the newest page.
      error: () => { if (channelId === this.channelId) this.reloadMessages(); },
    });
  }

  /** Drops the timeline and loads the newest page again. */
  private reloadMessages(): void {
    this.messages.set([]);
    this.cursor = null;
    this.hasMore.set(false);
    this.loadMessages();
  }

  /** Fetches display names for author ids not yet cached. @param msgs Messages to resolve. */
  private resolveAuthors(msgs: MessageDto[]): void {
    const known   = this.authors();
    const missing = [...new Set(msgs.map(m => m.authorId))].filter(id => !known[id]);
    if (!missing.length) return;

    // One request for the whole page's authors — a page with 15 distinct authors used to fire 15.
    this.directory.getUsers(missing).subscribe({
      next: users => this.authors.update(a => ({
        ...a,
        ...Object.fromEntries(users.map(u => [u.id, u])),
      })),
      error: () => { /* show short id */ },
    });
  }

  private subscribeRealtime(): void {
    this.realtime.reconnected$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => void this.catchUp());

    this.realtime.messageReceived$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(e => {
        if (e.channelId !== this.channelId) return;
        const msg = messageFromRealtime(e);
        // The timeline shows top-level messages only (as the list API does) — a thread reply here would
        // appear now and vanish on reload.
        if (msg.parentId) return;
        // 'keep': never overwrite a copy already in the list — the sender's POST response or a catch-up
        // fetch may carry reactions added since, and old events still deliver only a preview stub.
        this.messages.update(prev => mergeMessages(prev, [msg], 'keep'));
        this.resolveAuthors([msg]);
      });

    this.realtime.messageEdited$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(e => {
        this.messages.update(prev =>
          prev.map(m => m.id === e.messageId ? { ...m, body: e.newBody, editedAt: e.editedAt } : m)
        );
      });

    this.realtime.messageDeleted$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(e => {
        if (e.channelId === this.channelId)
          this.messages.update(prev => prev.filter(m => m.id !== e.messageId));
      });

    this.realtime.reactionAdded$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(e => {
        if (e.channelId !== this.channelId) return;
        this.messages.update(prev => prev.map(m => applyReaction(m, e.messageId, e.userId, e.emoji)));
      });

    this.realtime.channelAccessRevoked$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(e => {
        if (e.channelId !== this.channelId) return;
        const ch = this.channel();
        if (ch && !ch.isPrivate) {
          // A public channel stays readable to non-members — swap the composer for the join bar.
          this.channel.update(c => (c ? { ...c, isMember: false, memberCount: Math.max(0, c.memberCount - 1) } : c));
          return;
        }
        // Private: the server already stopped the stream; close the view instead of leaving it silently frozen.
        this.infoOpen.set(false);
        this.channelSvc.selectChannel('');
      });
  }
}
