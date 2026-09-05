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
import { MessageDto, MessageFormat } from '../../models/message.model';
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
      const older = [...page.items].reverse();
      this.messages.update(prev => [...older, ...prev]);
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
      this.messages.update(prev => [...prev, msg]);
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
      this.messages.set([...page.items].reverse());
      this.cursor = page.nextCursor;
      this.hasMore.set(page.nextCursor !== null);
      this.loading.set(false);
      this.resolveAuthors(page.items);
    });
  }

  /** Fetches display names for author ids not yet cached. @param msgs Messages to resolve. */
  private resolveAuthors(msgs: MessageDto[]): void {
    const known   = this.authors();
    const missing = [...new Set(msgs.map(m => m.authorId))].filter(id => !known[id]);
    for (const id of missing) {
      this.directory.getUser(id).subscribe({
        next: user => this.authors.update(a => ({ ...a, [id]: user })),
        error: () => { /* show short id */ },
      });
    }
  }

  private subscribeRealtime(): void {
    this.realtime.messageReceived$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(e => {
        if (e.channelId !== this.channelId) return;
        // The realtime event is flat (id + preview) — build a MessageDto for display.
        const msg: MessageDto = {
          id: e.messageId, channelId: e.channelId, parentId: null,
          replyToId: null, forwardedFromId: null,
          authorId: e.authorId, body: e.preview, format: MessageFormat.Plain,
          mentions: e.mentions ?? [], reactions: [], attachments: [], editedAt: null, createdAt: e.sentAt,
        };
        // Dedupe — the sender already appended its own message from the POST response.
        this.messages.update(prev => prev.some(m => m.id === msg.id) ? prev : [...prev, msg]);
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
  }
}
