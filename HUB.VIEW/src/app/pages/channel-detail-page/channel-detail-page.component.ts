import {
  AfterViewInit, ChangeDetectionStrategy, Component, computed, DestroyRef, effect, inject,
  Injector, OnDestroy, OnInit, signal, ViewChild,
} from '@angular/core';
import { catchError, debounceTime, distinctUntilChanged, map, of, Subject, switchMap, tap } from 'rxjs';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { ChannelService } from '../../services/channel.service';
import { MessageService } from '../../services/message.service';
import { RealtimeService } from '../../services/realtime.service';
import { DirectoryService } from '../../services/directory.service';
import { PresenceService } from '../../services/presence.service';
import { AuthService } from '../../services/auth.service';
import { ChannelDto, ChannelMemberRole, ChannelType } from '../../models/channel.model';
import { MessageDto, ReactionToggle } from '../../models/message.model';
import { applyReaction, removeReaction } from '../../utils/reaction.util';
import { mergeMessages } from '../../utils/message-merge.util';
import { messageFromRealtime } from '../../utils/realtime-message.util';
import { DirectoryUser } from '../../models/directory.model';
import { MessageListComponent } from '../../components/message-list/message-list.component';
import { MessageInputComponent, ComposerSubmit } from '../../components/message-input/message-input.component';
import { TypingIndicatorComponent } from '../../components/typing-indicator/typing-indicator.component';
import { ThreadPanelComponent } from '../../components/thread-panel/thread-panel.component';
import { ChannelFilesPanelComponent } from '../../components/channel-files-panel/channel-files-panel.component';
import { ChannelInfoPanelComponent } from '../../components/channel-info-panel/channel-info-panel.component';

import { DatePipe, DOCUMENT } from '@angular/common';

@Component({
  selector: 'app-channel-detail-page',
  standalone: true,
  imports: [MessageListComponent, MessageInputComponent, TypingIndicatorComponent, ChannelInfoPanelComponent, ThreadPanelComponent, ChannelFilesPanelComponent, DatePipe],
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
  private readonly presence   = inject(PresenceService);
  private readonly auth       = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector   = inject(Injector);
  private readonly document   = inject(DOCUMENT);

  /** Reference to the message list child — used to call `scrollToMessage`. */
  @ViewChild(MessageListComponent) private messageListRef?: MessageListComponent;

  protected readonly channel      = signal<ChannelDto | null>(null);
  protected readonly messages     = signal<MessageDto[]>([]);
  /** Messages keyed by id, so a quote-reply can render a preview of what it quotes. */
  protected readonly messagesById = computed(() => Object.fromEntries(this.messages().map(m => [m.id, m])));
  /** The message being quote-replied to; null when not replying. */
  protected readonly replyingTo = signal<MessageDto | null>(null);
  /** The message being forwarded (drives the forward dialog); null when closed. */
  protected readonly forwarding = signal<MessageDto | null>(null);
  /** Channels the user can forward into (every channel they are a member of, bar the current one). */
  protected readonly forwardTargets = computed(() =>
    (this.channelSvc.channelsCache() ?? []).filter(c => c.isMember && c.id !== this.channelId));
  protected readonly authors      = signal<Record<string, DirectoryUser>>({});
  /** Channel members offered in the composer's @mention list. */
  protected readonly mentionCandidates = signal<DirectoryUser[]>([]);
  protected readonly loading      = signal(true);
  protected readonly hasMore      = signal(false);
  /** Channel "…" info modal (About/Members). */
  protected readonly infoOpen = signal(false);
  /** The top-level message whose thread panel is open; null when closed. */
  protected readonly threadRoot = signal<MessageDto | null>(null);
  protected readonly infoTab  = signal<'about' | 'members'>('about');

  /** Pinned messages for this channel, newest-pinned first (CF-10) — listed in the pinned dialog. */
  protected readonly pinnedMessages = signal<MessageDto[]>([]);
  /** Whether the pinned-messages dialog is open. */
  protected readonly pinnedOpen = signal(false);
  /** Whether the Files & media panel (right side) is open (CF-5). */
  protected readonly filesOpen = signal(false);

  /** In a DM, the other participant's read point in ms — the "Seen" receipt (CF-15); 0 when none/not a DM. */
  protected readonly otherReadAtMs = signal(0);
  /** The DM "Seen" marker to pass to the message list: 0 unless this is a DM with a read point. */
  protected readonly seenAtMs = computed(() => {
    const c = this.channel();
    const isDm = c?.type === ChannelType.Dm || c?.type === ChannelType.GroupDm;
    return isDm ? this.otherReadAtMs() : 0;
  });

  // ── In-channel search ───────────────────────────────────────────────────
  protected readonly searchMode  = signal(false);
  protected readonly searchQuery = signal('');

  /** Id of the message that should flash-highlight after a search result click. */
  protected readonly highlightedMessageId = signal<string | null>(null);

  /**
   * Messages matching the current search query.
   * Non-empty only when search mode is active and a query has been typed.
   */
  /** Server search results for the current query; loaded (debounced) as the user types. */
  protected readonly searchResults = signal<MessageDto[]>([]);
  /** True while a search request is in flight. */
  protected readonly searching = signal(false);
  /** Pushes each keystroke; debounced into one search call. */
  private readonly searchInput$ = new Subject<string>();

  /** Current user's id — passed to message-list for own-message highlighting. */
  protected readonly currentUserId = computed(() => this.auth.currentUser()?.id ?? '');

  /** Whether a channel is currently selected (drives the empty state). */
  protected readonly hasSelection = computed(() => !!this.channelSvc.selectedId());

  /**
   * Whether the current user may post/react in the open channel. Public channels are readable by
   * any repo member, but sending requires membership — non-members see a "join" bar instead of
   * the composer (mirrors the backend, which returns 403 on post/react for non-members).
   */
  protected readonly canPost = computed(() => {
    const c = this.channel();
    return !!c && c.isMember && !c.isArchived; // archived channels are read-only
  });

  /**
   * Whether the viewer may moderate (delete anyone's message). Owner/Admin of the channel; the server also
   * accepts a ManageChannels holder, but the sidebar only knows the channel role, so this is the visible subset.
   */
  protected readonly canModerate = computed(() => {
    const r = this.channel()?.myRole;
    return r === ChannelMemberRole.Owner || r === ChannelMemberRole.Admin;
  });
  /** True while a join request is in flight (disables the Join button). */
  protected readonly joining = signal(false);

  /**
   * Display title for the channel header. For DMs this is the other participant's name
   * (the stored channel name is an internal "dm-{id}-{id}" slug); channels use their name.
   */
  protected readonly channelTitle = computed(() => {
    const ch = this.channel();
    if (!ch) return '';
    // Group DM: join member names (everyone but me).
    if (ch.type === ChannelType.GroupDm && ch.groupMemberIds?.length) {
      const me = this.currentUserId();
      const names = ch.groupMemberIds.filter(id => id !== me).map(id => this.authors()[id]?.name ?? '…');
      return names.length ? names.join(', ') : ch.name;
    }
    if (ch.type === ChannelType.Dm && ch.otherUserId) {
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
    this.subscribeReadTracking();

    // Full-history search runs server-side (not just loaded messages). Debounced so typing is cheap;
    // switchMap drops a stale request when a newer keystroke arrives.
    this.searchInput$.pipe(
      map(q => q.trim()),
      distinctUntilChanged(),
      debounceTime(250),
      tap(q => this.searching.set(q.length > 0)),
      switchMap(q => q.length === 0 ? of([] as MessageDto[])
        : this.messageSvc.search(this.channelId, q).pipe(catchError(() => of([] as MessageDto[])))),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe(results => {
      this.searchResults.set(results);
      this.searching.set(false);
      this.resolveAuthors(results);
    });

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

  /**
   * Adds the viewer's reaction, or removes it when they already have that emoji. Optimistic: the chip
   * changes at once and is rolled back if the API refuses. The realtime echo of this same change is a
   * no-op (applyReaction/removeReaction ignore what is already there).
   * @param t Message and emoji clicked.
   */
  protected onToggleReaction(t: ReactionToggle): void {
    const me = this.currentUserId();
    const msg = this.messages().find(m => m.id === t.messageId);
    if (!me || !msg) return;

    const had = msg.reactions.some(r => r.userId === me && r.emoji === t.emoji);
    const apply  = (fn: typeof applyReaction) =>
      this.messages.update(prev => prev.map(m => fn(m, t.messageId, me, t.emoji)));

    apply(had ? removeReaction : applyReaction);
    const call = had ? this.messageSvc.unreact(t.messageId, t.emoji) : this.messageSvc.react(t.messageId, t.emoji);
    call.subscribe({ error: () => apply(had ? applyReaction : removeReaction) });
  }

  /**
   * Edits a message after confirming the new body differs. Optimistic swap, rolled back if the server refuses
   * (e.g. the edit window passed).
   * @param e The message id and new body from the list.
   */
  protected onEditMessage(e: { messageId: string; body: string }): void {
    const body = e.body.trim();
    const current = this.messages().find(m => m.id === e.messageId);
    if (!current || !body || body === current.body) return;

    const prevBody = current.body;
    this.messages.update(prev => prev.map(m => m.id === e.messageId ? { ...m, body, editedAt: new Date().toISOString() } : m));
    this.messageSvc.edit(e.messageId, body).subscribe({
      next: saved => this.messages.update(prev => prev.map(m => m.id === saved.id ? saved : m)),
      error: () => this.messages.update(prev => prev.map(m => m.id === e.messageId ? { ...m, body: prevBody, editedAt: current.editedAt } : m)),
    });
  }

  /**
   * Deletes a message to a tombstone. Optimistic, rolled back on failure.
   * @param messageId Message id.
   */
  protected onDeleteMessage(messageId: string): void {
    const before = this.messages();
    this.messages.update(prev => prev.map(m =>
      m.id === messageId ? { ...m, isDeleted: true, body: '', reactions: [], attachments: [], editedAt: null } : m));
    this.messageSvc.delete(messageId).subscribe({ error: () => this.messages.set(before) });
  }

  /** Pins/unpins a message (optimistic, rolled back on failure). @param e Message id + desired state. */
  protected onTogglePin(e: { messageId: string; pin: boolean }): void {
    const beforeMsgs = this.messages();
    const beforePinned = this.pinnedMessages();
    this.messages.update(prev => prev.map(m => m.id === e.messageId ? { ...m, isPinned: e.pin } : m));
    this.applyPinToBar(e.messageId, e.pin);
    const call = e.pin ? this.messageSvc.pin(e.messageId) : this.messageSvc.unpin(e.messageId);
    call.subscribe({ error: () => { this.messages.set(beforeMsgs); this.pinnedMessages.set(beforePinned); } });
  }

  /** Jumps the message list to a pinned message and closes the dialog. @param messageId Message id. */
  protected jumpToPinned(messageId: string): void {
    this.pinnedOpen.set(false);
    this.scrollToMessage(messageId);
  }

  /** Jumps to the message an attachment belongs to and closes the Files panel. @param messageId Message id. */
  protected jumpToAttachment(messageId: string): void {
    this.filesOpen.set(false);
    this.scrollToMessage(messageId);
  }

  /** Unpins a message straight from the pinned dialog; closes it if that was the last pin. @param messageId Message id. */
  protected unpinFromBar(messageId: string): void {
    this.onTogglePin({ messageId, pin: false });
    if (this.pinnedMessages().length === 0) this.pinnedOpen.set(false);
  }

  /** Adds/removes a message in the pinned bar (newest-pinned first). @param messageId Message id. @param pin Whether it is now pinned. */
  private applyPinToBar(messageId: string, pin: boolean): void {
    if (!pin) {
      this.pinnedMessages.update(list => list.filter(m => m.id !== messageId));
      return;
    }
    if (this.pinnedMessages().some(m => m.id === messageId)) return;
    const msg = this.messages().find(m => m.id === messageId);
    if (msg) this.pinnedMessages.update(list => [{ ...msg, isPinned: true }, ...list]);
  }

  /** Opens the thread panel for a message. @param messageId Top-level message id. */
  protected openThread(messageId: string): void {
    const root = this.messages().find(m => m.id === messageId);
    if (root) this.threadRoot.set(root);
  }

  /** Updates the thread summary on the root message after a reply is posted. @param e parentId + new count. */
  protected onReplyCountChanged(e: { parentId: string; count: number }): void {
    this.messages.update(prev => prev.map(m =>
      m.id === e.parentId ? { ...m, replyCount: e.count, lastReplyAt: new Date().toISOString() } : m));
    // Keep the open panel's root in sync so its header count updates too.
    this.threadRoot.update(r => r && r.id === e.parentId ? { ...r, replyCount: e.count } : r);
  }

  /** Sends a composed message (Markdown). @param payload Composer output. */
  protected sendMessage(payload: ComposerSubmit): void {
    const hasAttachments = (payload.attachments?.length ?? 0) > 0;
    if (!payload.body.trim() && !hasAttachments) return; // nothing to send
    clearTimeout(this.typingTimer ?? undefined);
    this.realtime.stopTyping(this.channelId);
    const replyToId = this.replyingTo()?.id;
    this.messageSvc.send(this.channelId, {
      body: payload.body, format: payload.format, mentionedUserIds: payload.mentionedUserIds, replyToId,
      attachments: payload.attachments,
    }).subscribe(msg => {
      this.messages.update(prev => mergeMessages(prev, [msg], 'replace'));
      this.resolveAuthors([msg]);
    });
    this.replyingTo.set(null);
  }

  /** Starts a quote-reply to a message. @param messageId Message to quote. */
  protected startReply(messageId: string): void {
    const m = this.messages().find(x => x.id === messageId);
    if (m) this.replyingTo.set(m);
  }

  /** Cancels the pending quote-reply. */
  protected cancelReply(): void { this.replyingTo.set(null); }

  /** Opens the forward dialog for a message. @param messageId Message to forward. */
  protected startForward(messageId: string): void {
    const m = this.messages().find(x => x.id === messageId);
    if (!m) return;
    this.forwarding.set(m);
    // Resolve DM-target partner names so the list shows people, not "dm-…" slugs.
    const known = this.authors();
    const missing = [...new Set(
      this.forwardTargets()
        .filter(c => (c.type === ChannelType.Dm || c.type === ChannelType.GroupDm) && c.otherUserId)
        .map(c => c.otherUserId!)
        .filter(id => !known[id]),
    )];
    if (missing.length) {
      this.directory.getUsers(missing).subscribe({
        next: users => this.authors.update(a => ({ ...a, ...Object.fromEntries(users.map(u => [u.id, u])) })),
        error: () => { /* fall back to "Direct message" */ },
      });
    }
  }

  /** Forwards the pending message into a channel, then closes the dialog. @param targetChannelId Destination. */
  protected doForward(targetChannelId: string): void {
    const src = this.forwarding();
    if (!src) return;
    this.messageSvc.forward(src.id, targetChannelId).subscribe({ next: () => this.forwarding.set(null), error: () => this.forwarding.set(null) });
  }

  /** A short preview of a message for the reply bar / forward dialog. @param m The message. */
  protected shortPreview(m: MessageDto): string {
    return m.body.length > 90 ? m.body.slice(0, 90) + '…' : m.body;
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

  /** Reflects archive/reopen in the header (archived channels hide the composer via canPost). @param archived New state. */
  protected onArchivedChanged(archived: boolean): void {
    this.channel.update(c => (c ? { ...c, isArchived: archived } : c));
  }

  /** Toggles in-channel search mode; clears the query when closing. */
  protected toggleSearch(): void {
    this.searchMode.update(v => !v);
    if (!this.searchMode()) {
      this.searchQuery.set('');
      this.searchResults.set([]);
      this.searchInput$.next('');
    }
  }

  /** Updates the search query from native input event. @param event Native input event. */
  protected onSearchInput(event: Event): void {
    const q = (event.target as HTMLInputElement).value;
    this.searchQuery.set(q);
    this.searchInput$.next(q);
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
   * Human label for a forward target: "#channel" for a channel, or the partner's name for a DM
   * (the stored DM name is an internal "dm-…" slug, never shown to the user).
   * @param ch The candidate channel.
   * @returns The label to display in the forward list.
   */
  protected targetLabel(ch: ChannelDto): string {
    if (ch.type === ChannelType.Dm || ch.type === ChannelType.GroupDm) {
      const name = ch.otherUserId ? this.authors()[ch.otherUserId]?.name : null;
      return name ?? 'Direct message';
    }
    return `#${ch.name}`;
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
    this.mentionCandidates.set([]);
    this.channel.set(null);
    this.cursor = null;
    this.hasMore.set(false);
    this.infoOpen.set(false);
    this.searchMode.set(false);
    this.searchQuery.set('');
    this.pinnedMessages.set([]);
    this.pinnedOpen.set(false);
    this.filesOpen.set(false);
    this.otherReadAtMs.set(0);
  }

  private initChannel(id: string): void {
    this.channelId = id;
    this.loadChannel();
    this.loadMessages();
    this.loadMentionCandidates();
    this.loadPinned();
    this.realtime.joinChannel(id);
  }

  /** Loads the channel's pinned messages for the top pinned bar (CF-10). */
  private loadPinned(): void {
    const channelId = this.channelId;
    this.messageSvc.listPinned(channelId).subscribe({
      next: pinned => { if (channelId === this.channelId) { this.pinnedMessages.set(pinned); this.resolveAuthors(pinned); } },
      error: () => { /* non-critical: the bar just stays empty */ },
    });
  }

  /**
   * Loads the channel's members as @mention suggestions (minus the current user). Members only: the server
   * drops mentions of non-members in private channels, so offering them would promise a notification
   * that never goes out.
   */
  private loadMentionCandidates(): void {
    const channelId = this.channelId;
    const me = this.auth.currentUser()?.id;
    this.channelSvc.listMembers(channelId).pipe(
      switchMap(members => {
        // CF-15: seed the DM "Seen" marker from the other member's current read point.
        if (channelId === this.channelId) {
          const other = members.find(m => m.userId !== me);
          this.otherReadAtMs.set(other?.lastReadAt ? Date.parse(other.lastReadAt) : 0);
        }
        const ids = members.map(m => m.userId).filter(id => id !== me);
        return ids.length ? this.directory.getUsers(ids) : of([] as DirectoryUser[]);
      }),
    ).subscribe({
      next: users => { if (channelId === this.channelId) this.mentionCandidates.set(users); },
      error: () => { /* no suggestions — typing @ simply shows nothing */ },
    });
  }

  private loadChannel(): void {
    this.channelSvc.get(this.channelId).subscribe(ch => {
      this.channel.set(ch);
      // Resolve participant names for the header title: a group DM needs all members, a 1-1 DM
      // just the other person.
      if (ch.type === ChannelType.GroupDm && ch.groupMemberIds?.length) {
        this.resolveAuthors(ch.groupMemberIds.map(id => ({ authorId: id } as MessageDto)));
      } else if (ch.type === ChannelType.Dm && ch.otherUserId) {
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

  /** Nudged whenever the newest visible message may have changed; debounced into one markRead call. */
  private readonly readTrigger = new Subject<void>();

  /** Last read point sent per channel, so an unchanged timeline does not re-post. */
  private lastSentRead: Record<string, string> = {};

  /**
   * Persists the read point server-side so unread badges survive reloads and sync across devices.
   * Debounced (a burst of messages = one call), only for members (the API refuses others), and only while
   * the tab is visible — a background tab has not been "read".
   */
  private subscribeReadTracking(): void {
    this.readTrigger.pipe(debounceTime(800), takeUntilDestroyed(this.destroyRef)).subscribe(() => this.markRead());

    const onVisible = () => { if (this.document.visibilityState === 'visible') this.readTrigger.next(); };
    this.document.addEventListener('visibilitychange', onVisible);
    this.destroyRef.onDestroy(() => this.document.removeEventListener('visibilitychange', onVisible));

    // Any change to the newest message (load, send, realtime, catch-up) is a reason to re-check.
    effect(() => {
      this.messages().at(-1)?.id;
      this.canPost();
      this.readTrigger.next();
    }, { injector: this.injector });
  }

  /** Sends the newest message's time as the read point, if anything changed and the user can read it. */
  private markRead(): void {
    const channelId = this.channelId;
    const newest    = this.messages().at(-1);
    if (!channelId || !newest || !this.canPost() || this.document.visibilityState !== 'visible') return;
    if (this.lastSentRead[channelId] === newest.createdAt) return;
    this.lastSentRead[channelId] = newest.createdAt;
    this.channelSvc.markRead(channelId, newest.createdAt).subscribe({
      error: () => { delete this.lastSentRead[channelId]; }, // allow a retry on the next trigger
    });
  }

  /** Fetches display names for author ids not yet cached. @param msgs Messages to resolve. */
  private resolveAuthors(msgs: MessageDto[]): void {
    const known   = this.authors();
    // Mentioned users too, so their @name can be highlighted in the body.
    const ids     = msgs.flatMap(m => [m.authorId, ...(m.mentions ?? [])]);
    const missing = [...new Set(ids)].filter(id => !known[id]);
    if (!missing.length) return;

    // Pull these authors' custom statuses (CF-19) so the emoji shows beside their name.
    this.presence.fetchCustomStatuses(missing).subscribe();

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
        if (e.channelId !== this.channelId) return;
        this.messages.update(prev =>
          prev.map(m => m.id === e.messageId ? { ...m, body: e.newBody, editedAt: e.editedAt } : m)
        );
      });

    this.realtime.messageDeleted$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(e => {
        if (e.channelId !== this.channelId) return;
        // Tombstone in place (keep the slot + thread) rather than removing the row.
        this.messages.update(prev => prev.map(m =>
          m.id === e.messageId
            ? { ...m, isDeleted: true, body: '', reactions: [], attachments: [], editedAt: null }
            : m));
      });

    this.realtime.reactionAdded$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(e => {
        if (e.channelId !== this.channelId) return;
        this.messages.update(prev => prev.map(m => applyReaction(m, e.messageId, e.userId, e.emoji)));
      });

    this.realtime.reactionRemoved$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(e => {
        if (e.channelId !== this.channelId) return;
        this.messages.update(prev => prev.map(m => removeReaction(m, e.messageId, e.userId, e.emoji)));
      });

    // CF-15: the other DM participant read up to a point — advance the "Seen" marker (never backwards).
    this.realtime.readReceipt$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(e => {
        if (e.channelId !== this.channelId || e.userId === this.currentUserId()) return;
        this.otherReadAtMs.update(cur => Math.max(cur, Date.parse(e.readAt)));
      });

    this.realtime.messagePinChanged$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(e => {
        if (e.channelId !== this.channelId) return;
        this.messages.update(prev => prev.map(m => m.id === e.messageId ? { ...m, isPinned: e.isPinned } : m));
        this.applyPinToBar(e.messageId, e.isPinned);
      });

    this.realtime.channelAccessRevoked$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(e => {
        if (e.channelId !== this.channelId) return;
        const ch = this.channel();
        // A public channel stays readable to non-members — swap the composer for the join bar. But NOT when
        // this revoke is a public→private lock-down (nowPrivate): the channel is no longer visible at all.
        if (ch && !ch.isPrivate && !e.nowPrivate) {
          this.channel.update(c => (c ? { ...c, isMember: false, memberCount: Math.max(0, c.memberCount - 1) } : c));
          return;
        }
        // Private (or just locked down): the server already stopped the stream; close the view.
        this.infoOpen.set(false);
        this.channelSvc.selectChannel('');
      });
  }
}
