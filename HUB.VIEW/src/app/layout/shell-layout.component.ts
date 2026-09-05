import {
  ChangeDetectionStrategy, Component, computed, DestroyRef, effect, inject,
  OnInit, signal,
} from '@angular/core';
import { DatePipe, DOCUMENT, NgClass } from '@angular/common';
import { Router, RouterOutlet } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthService } from '../services/auth.service';
import { ChannelService } from '../services/channel.service';
import { DirectoryService } from '../services/directory.service';
import { NotificationService } from '../services/notification.service';
import { PresenceService } from '../services/presence.service';
import { UserSettingsService } from '../services/user-settings.service';
import { ThemeService } from '../core/services/theme.service';
import { UserStatus } from '../models/user-settings.model';
import { RealtimeService } from '../services/realtime.service';
import { StorageService } from '../core/services/storage.service';
import { StorageKeys } from '../core/constants/storage-keys.constant';
import { ChannelDto, ChannelType } from '../models/channel.model';
import { DirectoryUser, UserMemberships } from '../models/directory.model';
import { NotificationDto, NotificationType } from '../models/notification.model';
import { CreateChannelDialogComponent } from '../components/create-channel-dialog/create-channel-dialog.component';
import { ConfirmDialogComponent } from '../components/confirm-dialog/confirm-dialog.component';

export type SideTab = 'channels' | 'notifications' | 'settings';

@Component({
  selector: 'app-shell-layout',
  standalone: true,
  imports: [RouterOutlet, CreateChannelDialogComponent, ConfirmDialogComponent, DatePipe, NgClass],
  templateUrl: './shell-layout.component.html',
  styleUrl: './shell-layout.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShellLayoutComponent implements OnInit {
  protected readonly auth         = inject(AuthService);
  protected readonly realtime     = inject(RealtimeService);
  protected readonly notification = inject(NotificationService);
  protected readonly presence     = inject(PresenceService);
  protected readonly settings     = inject(UserSettingsService);
  protected readonly theme        = inject(ThemeService);
  private readonly channelSvc     = inject(ChannelService);
  private readonly directory      = inject(DirectoryService);
  private readonly storage        = inject(StorageService);
  private readonly router         = inject(Router);
  private readonly destroyRef     = inject(DestroyRef);
  private readonly document       = inject(DOCUMENT);

  readonly ChannelType      = ChannelType;
  readonly NotificationType = NotificationType;

  constructor() {
    // Realtime message/typing events are scoped to each channel's SignalR group, so the
    // connection must join every channel the user can see for sidebar unread + typing to work.
    // Re-runs on (re)connect and whenever the channel list changes; JoinChannel is idempotent.
    effect(() => {
      if (this.realtime.connectionState() === 'connected') {
        for (const c of this.channels()) void this.realtime.joinChannel(c.id);
      }
    });

    // Browser tab badge — reflect total unread in the document title (e.g. "(3) Nexus HUB").
    effect(() => {
      const n = this.totalUnread();
      this.document.title = n > 0 ? `(${n > 99 ? '99+' : n}) Nexus HUB` : 'Nexus HUB';
    });

    // Clear the unread badge whenever a channel is opened (covers deep-links + sidebar clicks).
    effect(() => {
      const id = this.channelSvc.selectedId();
      if (id) this.unreadMap.update(m => (m[id] ? { ...m, [id]: 0 } : m));
    });
  }

  private audioCtx?: AudioContext;
  private lastBeepAt = 0;

  /** Plays a short, subtle notification beep (throttled to once per 2s). */
  private playBeep(): void {
    const now = Date.now();
    if (now - this.lastBeepAt < 2_000) return;
    this.lastBeepAt = now;
    try {
      this.audioCtx ??= new AudioContext();
      const ctx = this.audioCtx;
      if (ctx.state === 'suspended') void ctx.resume();
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.frequency.value = 660;
      gain.gain.setValueAtTime(0.06, ctx.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + 0.25);
      osc.connect(gain).connect(ctx.destination);
      osc.start();
      osc.stop(ctx.currentTime + 0.25);
    } catch { /* audio not available — ignore */ }
  }

  // ── State ────────────────────────────────────────────────────────────────
  protected readonly activeTab         = signal<SideTab>('channels');
  protected readonly channels          = signal<ChannelDto[]>([]);
  protected readonly workspaceId       = signal<string>('');
  protected readonly workspaceName     = signal('Workspace');
  /** The currently open channel id (app state, not the URL). Drives sidebar active highlight. */
  protected readonly selectedChannelId = this.channelSvc.selectedId;
  protected readonly unreadMap         = signal<Record<string, number>>({});
  protected readonly createDialogOpen  = signal(false);
  protected readonly loadingChannels   = signal(true);
  protected readonly loadingNotifs     = signal(false);

  // ── Search ───────────────────────────────────────────────────────────────
  protected readonly searchQuery = signal('');

  // ── Current-user memberships (for permission checks) ─────────────────────
  /** Cached memberships response; populated once on init. */
  private readonly myMemberships = signal<UserMemberships | null>(null);

  /**
   * True when the current user may create new public channels.
   * Requires global-admin status OR the ManageChannels workspace privilege.
   */
  protected readonly canCreateChannel = computed(() => {
    const m = this.myMemberships();
    if (!m) return false;
    if (m.isGlobalAdmin) return true;
    const wsId = this.workspaceId();
    const repo  = m.repositories.find(r => r.repositoryId === wsId);
    return repo?.permissions?.includes('ManageChannels') ?? false;
  });

  // ── Workspace members (full list — used for People search results) ────────
  /** Full list of org members (union across the user's repos); powers People search + Online list. */
  private readonly workspaceMembers  = signal<DirectoryUser[]>([]);
  /** Id of the member currently being opened as a DM (shows a spinner). */
  protected readonly openingDmForId  = signal<string | null>(null);

  // ── Repo groups (collapse/expand) ─────────────────────────────────────────
  /** Per-repo collapsed state: repoId → true when collapsed. */
  private readonly collapsedRepos = signal<Record<string, boolean>>({});
  /** repoId being targeted by the New Channel dialog. */
  private readonly createRepoId   = signal<string>('');

  // ── Typing indicators (sidebar, across all channels) ──────────────────────
  /** channelId → list of other users currently typing. */
  private readonly typingMap    = signal<Record<string, string[]>>({});
  private readonly typingTimers = new Map<string, ReturnType<typeof setTimeout>>();

  // ── DM activity tracking (client-side; updated on realtime events) ────────
  /** Maps channelId → last-message timestamp (ms epoch). Used to sort DMs by recency. */
  private readonly dmLastActivity = signal<Record<string, number>>({});

  // ── Derived ──────────────────────────────────────────────────────────────
  readonly publicChannels = computed(() =>
    this.channels().filter(c => c.type !== ChannelType.Dm && c.type !== ChannelType.GroupDm)
  );
  readonly dmChannels = computed(() =>
    this.channels().filter(c => c.type === ChannelType.Dm || c.type === ChannelType.GroupDm)
  );
  readonly totalUnread = computed(() =>
    Object.values(this.unreadMap()).reduce((s, n) => s + n, 0)
  );

  /**
   * Public channels filtered by the sidebar search query.
   * Matches on channel name (case-insensitive).
   */
  readonly filteredPublicChannels = computed(() => {
    const q = this.searchQuery().toLowerCase().trim();
    const chs = this.publicChannels();
    return q ? chs.filter(c => c.name.toLowerCase().includes(q)) : chs;
  });

  /**
   * Workspace members whose name or email matches the sidebar search query.
   * Empty when there is no query — only used to power the People search section.
   * The current user is excluded from results.
   */
  readonly filteredMembers = computed(() => {
    const q      = this.searchQuery().toLowerCase().trim();
    const selfId = this.auth.currentUser()?.id ?? '';
    if (!q) return [];
    return this.workspaceMembers()
      .filter(m => m.id !== selfId && !m.isDeleted)
      .filter(m =>
        m.name.toLowerCase().includes(q) ||
        m.email.toLowerCase().includes(q)
      );
  });

  /**
   * DM channels filtered by search query AND sorted by:
   *  1. Online status first
   *  2. Most recent message activity
   */
  readonly filteredDmChannels = computed(() => {
    const q      = this.searchQuery().toLowerCase().trim();
    const map    = this.dmLastActivity();
    const sorted = [...this.dmChannels()]
      .sort((a, b) => {
        const aOnline = this.presence.getStatus(this.dmUserId(a)) === 'Online' ? 0 : 1;
        const bOnline = this.presence.getStatus(this.dmUserId(b)) === 'Online' ? 0 : 1;
        if (aOnline !== bOnline) return aOnline - bOnline;
        // Fall back to most-recent-message then channel creation date
        const aTime = map[a.id] ?? new Date(a.createdAt).getTime();
        const bTime = map[b.id] ?? new Date(b.createdAt).getTime();
        return bTime - aTime;
      });
    return q ? sorted.filter(c => this.dmName(c).toLowerCase().includes(q)) : sorted;
  });

  /**
   * Public channels grouped by repository (the workspace = org contains many repos).
   * Only repos the user belongs to are shown; when searching, empty groups are hidden.
   */
  readonly repoGroups = computed(() => {
    const m = this.myMemberships();
    if (!m) return [];
    const q   = this.searchQuery().toLowerCase().trim();
    const pub = this.publicChannels();
    return m.repositories
      .filter(r => !r.isArchived)
      .map(r => ({
        repo: r,
        channels: pub
          .filter(c => c.workspaceId === r.repositoryId)
          .filter(c => !q || c.name.toLowerCase().includes(q)),
      }))
      .filter(g => !q || g.channels.length > 0);
  });

  /**
   * Org members who are currently online AND don't already have a DM (those surface in the
   * Direct Messages list instead, sorted online-first). Excludes self.
   */
  readonly onlinePeople = computed(() => {
    const selfId  = this.auth.currentUser()?.id ?? '';
    const dmNames = new Set(this.dmChannels().map(c => c.name.toLowerCase()));
    return this.workspaceMembers()
      .filter(u => u.id !== selfId && !u.isDeleted
                && this.presence.getStatus(u.id) === 'Online'
                && !dmNames.has(u.name.toLowerCase()))
      .sort((a, b) => a.name.localeCompare(b.name));
  });

  /**
   * The other participant's user id for a DM channel (from the server's OtherUserId),
   * used to key presence and resolve the display name.
   * @param ch DM channel.
   */
  protected dmUserId(ch: ChannelDto): string {
    return ch.otherUserId ?? ch.id;
  }

  /**
   * Display name for a DM = the other participant's name (the stored channel name is an internal
   * "dm-{id}-{id}" slug). Falls back to the raw name until the member list resolves.
   * @param ch DM channel.
   */
  protected dmName(ch: ChannelDto): string {
    const uid = ch.otherUserId;
    return (uid ? this.workspaceMembers().find(m => m.id === uid)?.name : null) ?? ch.name;
  }

  /** @returns true when the given repo group is collapsed. @param repoId Repository id. */
  protected isRepoCollapsed(repoId: string): boolean {
    return this.collapsedRepos()[repoId] ?? false;
  }

  /** Toggles a repo group's collapsed state. @param repoId Repository id. */
  protected toggleRepo(repoId: string): void {
    this.collapsedRepos.update(m => ({ ...m, [repoId]: !(m[repoId] ?? false) }));
  }

  /** @returns true when someone else is currently typing in the channel. @param channelId Channel id. */
  protected isTyping(channelId: string): boolean {
    return (this.typingMap()[channelId]?.length ?? 0) > 0;
  }

  /** @returns true when the user may create channels in the given repo (admin or ManageChannels). @param repoId Repository id. */
  protected canCreateChannelForRepo(repoId: string): boolean {
    const m = this.myMemberships();
    if (!m) return false;
    if (m.isGlobalAdmin) return true;
    return m.repositories.find(r => r.repositoryId === repoId)?.permissions?.includes('ManageChannels') ?? false;
  }

  ngOnInit(): void {
    this.realtime.connect();
    this.notification.list().subscribe();
    this.resolveWorkspace();
    this.subscribeRealtimeEvents();
  }

  // ── Tab navigation ────────────────────────────────────────────────────────
  /** Sets the active sidebar tab. @param tab Target tab. */
  protected setTab(tab: SideTab): void {
    this.activeTab.set(tab);
    if (tab === 'notifications') this.reloadNotifications();
  }

  // ── Channel navigation ────────────────────────────────────────────────────
  /** The current tenant alias (org) used as the route prefix. */
  private orgAlias(): string {
    return this.myMemberships()?.orgAlias
      ?? this.router.url.split('/').filter(Boolean)[0]
      ?? '';
  }

  /** Navigates to a channel by slug (under the org alias). @param ch Channel to open. */
  protected navigate(ch: ChannelDto): void {
    this.channelSvc.selectChannel(ch.id);
    void this.router.navigate(['/', this.orgAlias(), 'channels']);
  }

  /**
   * Opens a DM channel with the selected member.
   * Reuses an existing DM if one already exists (matched by channel name);
   * otherwise creates a new DM channel and adds the user as a member.
   * @param member The workspace member to DM.
   */
  protected navigateToMember(member: DirectoryUser): void {
    if (this.openingDmForId()) return; // already opening one

    // Backend find-or-creates the canonical DM (matched by both members) and adds both.
    this.openingDmForId.set(member.id);
    this.channelSvc
      .openDm(this.workspaceId(), member.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ch => {
          // Add to local list if not already there.
          if (!this.channels().some(c => c.id === ch.id)) {
            this.channels.update(cs => [...cs, ch]);
            this.channelSvc.updateCache(this.channels());
            // Seed activity map for sort order.
            this.dmLastActivity.update(m => ({
              ...m,
              [ch.id]: new Date(ch.createdAt).getTime(),
            }));
          }
          this.openingDmForId.set(null);
          this.clearSearch();
          this.navigate(ch);
        },
        error: () => this.openingDmForId.set(null),
      });
  }

  // ── Create channel dialog ─────────────────────────────────────────────────
  /** Opens the New Channel dialog for a specific repository. @param repoId Target repository id. */
  protected openCreateDialog(repoId: string): void {
    this.createRepoId.set(repoId);
    this.createDialogOpen.set(true);
  }

  /** The repository id the create-channel dialog targets. */
  protected readonly createChannelRepoId = computed(() => this.createRepoId());

  /** Called when the dialog emits the created channel. @param ch New channel. */
  protected onChannelCreated(ch: ChannelDto): void {
    this.channels.update(cs => [...cs, ch]);
    this.channelSvc.updateCache(this.channels());
    this.createDialogOpen.set(false);
    this.navigate(ch);
  }

  // ── Notifications helpers ─────────────────────────────────────────────────
  /** Human label for a notification type. @param type Notification type enum value. */
  protected typeLabel(type: NotificationType): string {
    switch (type) {
      case NotificationType.Mention:       return 'Mention';
      case NotificationType.DirectMessage: return 'DM';
      default:                             return 'System';
    }
  }

  /**
   * Marks a notification read and navigates to its channel if linked.
   * @param n Notification to open.
   */
  protected openNotification(n: NotificationDto): void {
    if (!n.isRead) {
      this.notification.markRead(n.id).subscribe();
      this.notification.markReadLocally(n.id);
    }
    if (n.channelId) {
      this.channelSvc.selectChannel(n.channelId);
      void this.router.navigate(['/', this.orgAlias(), 'channels']);
    }
  }

  /** Marks all notifications as read. */
  protected markAllNotificationsRead(): void {
    this.notification.markAllRead().subscribe();
    this.notification.markAllReadLocally();
  }

  // ── User settings (status + quiet hours) ─────────────────────────────────
  /** Selectable presence statuses shown in the Settings tab. `dot` is the presence-dot modifier class. */
  protected readonly statusOptions: ReadonlyArray<{ value: UserStatus; label: string; dot: string }> = [
    { value: 'Active',       label: 'Active',         dot: 'presence-online' },
    { value: 'Away',         label: 'Away',           dot: 'presence-away' },
    { value: 'DoNotDisturb', label: 'Do Not Disturb', dot: 'presence-dnd' },
  ];

  /** Sets the current user's presence status. @param status Chosen status. */
  protected setStatus(status: UserStatus): void { this.settings.setStatus(status); }

  /**
   * Presence-dot modifier class for ANOTHER user, reflecting their live status.
   * Online → green, Away → orange, Do Not Disturb → red, otherwise grey (offline).
   * @param userId The user whose presence to render.
   */
  protected presenceDot(userId: string): string {
    switch (this.presence.getStatus(userId)) {
      case 'Online':       return 'presence-online';
      case 'Away':         return 'presence-away';
      case 'DoNotDisturb': return 'presence-dnd';
      default:             return 'presence-offline';
    }
  }

  /** Toggles the quiet-hours schedule on/off. @param event Checkbox change event. */
  protected onQuietToggle(event: Event): void {
    this.settings.setQuietHours({ enabled: (event.target as HTMLInputElement).checked });
  }

  /** Updates a quiet-hours boundary time. @param field Which end. @param event Time input event. */
  protected onQuietTime(field: 'start' | 'end', event: Event): void {
    this.settings.setQuietHours({ [field]: (event.target as HTMLInputElement).value });
  }

  /** Presence-dot modifier class for the current user's own status (drives the rail avatar dot). */
  protected readonly ownStatusDot = computed(() => {
    if (this.realtime.connectionState() !== 'connected') return 'presence-offline';
    return this.statusOptions.find(o => o.value === this.settings.status())?.dot ?? 'presence-online';
  });

  // ── Logout ───────────────────────────────────────────────────────────────
  async logout(): Promise<void> {
    await this.realtime.disconnect();
    this.auth.logout();
  }

  // ── Search ───────────────────────────────────────────────────────────────
  /** Updates the sidebar search query from native input event. @param event Input event. */
  protected onSearchInput(event: Event): void {
    this.searchQuery.set((event.target as HTMLInputElement).value);
  }

  /** Clears the sidebar search. */
  protected clearSearch(): void { this.searchQuery.set(''); }

  // ── Private ──────────────────────────────────────────────────────────────

  /**
   * Resolves the workspace once on init. In the org model the **workspace = organization**,
   * so the header always shows the org name (from memberships), consistently whether or not
   * a repository was cached. Also caches memberships for `canCreateChannel`.
   */
  private resolveWorkspace(): void {
    this.directory.getMyMemberships()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: m => {
          this.myMemberships.set(m);
          // Workspace title = organization name (falls back gracefully for legacy payloads).
          this.workspaceName.set(m?.orgName || 'Workspace');

          const repos = (m?.repositories ?? []).filter(r => !r.isArchived);
          if (!repos.length) { this.channels.set([]); this.loadingChannels.set(false); return; }

          // Default repo for new DMs = cached-or-first.
          const cached = this.storage.getString(StorageKeys.activeWorkspace);
          const active = repos.find(r => r.repositoryId === cached) ?? repos[0];
          this.storage.setString(StorageKeys.activeWorkspace, active.repositoryId);
          this.workspaceId.set(active.repositoryId);

          this.loadAllChannels(repos.map(r => r.repositoryId));
        },
        error: () => this.loadingChannels.set(false),
      });
  }

  /**
   * Loads channels + members across every repository the user belongs to and merges them.
   * The workspace is the organization; channels are grouped per repo in the sidebar.
   * @param repoIds Repositories the user is a member of.
   */
  private loadAllChannels(repoIds: string[]): void {
    this.loadingChannels.set(true);

    // Channels — one list() per repo; the endpoint already filters to public-or-member.
    forkJoin(repoIds.map(id => this.channelSvc.list(id).pipe(catchError(() => of([])))))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: lists => {
          const byId = new Map<string, ChannelDto>();
          lists.flat().forEach(c => byId.set(c.id, c)); // dedupe (a DM can surface under >1 repo)
          const channels = [...byId.values()];
          this.channels.set(channels);
          this.channelSvc.updateCache(channels);

          const init: Record<string, number> = {};
          channels
            .filter(c => c.type === ChannelType.Dm || c.type === ChannelType.GroupDm)
            .forEach(c => { init[c.id] = new Date(c.createdAt).getTime(); });
          this.dmLastActivity.set(init);

          this.loadingChannels.set(false);

          const dmIds = channels
            .filter(c => c.type === ChannelType.Dm || c.type === ChannelType.GroupDm)
            .map(c => c.id);
          if (dmIds.length) this.presence.fetchStatuses(dmIds).subscribe();
        },
        error: () => this.loadingChannels.set(false),
      });

    // Members — union across repos for People search + the Online section.
    forkJoin(repoIds.map(id => this.directory.listWorkspaceMembers(id).pipe(catchError(() => of([])))))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: lists => {
          const byId = new Map<string, DirectoryUser>();
          lists.flat().forEach(u => byId.set(u.id, u));
          const members = [...byId.values()];
          this.workspaceMembers.set(members);
          if (members.length) this.presence.fetchStatuses(members.map(u => u.id)).subscribe();
        },
        error: () => { /* non-critical */ },
      });
  }

  private reloadNotifications(): void {
    if (this.loadingNotifs()) return;
    this.loadingNotifs.set(true);
    this.notification.list()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.loadingNotifs.set(false),
        error: () => this.loadingNotifs.set(false),
      });
  }

  private subscribeRealtimeEvents(): void {
    this.realtime.messageReceived$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(e => {
        const chId = e.channelId;

        // Update DM last-activity map to keep sort order fresh.
        const ch = this.channels().find(c => c.id === chId);
        if (ch?.type === ChannelType.Dm || ch?.type === ChannelType.GroupDm) {
          this.dmLastActivity.update(m => ({
            ...m,
            [chId]: new Date(e.sentAt).getTime(),
          }));
        }

        // Increment unread badge + play a beep for any channel not currently open (not own message).
        const isActive = chId === this.channelSvc.selectedId();
        if (!isActive && e.authorId !== this.auth.currentUser()?.id) {
          this.unreadMap.update(m => ({ ...m, [chId]: (m[chId] ?? 0) + 1 }));
          if (!this.settings.muted()) this.playBeep(); // silenced by Do Not Disturb / quiet hours
        }
      });

    this.realtime.notificationReceived$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(e => this.notification.onRealtimeReceived(e.notification));

    // Sidebar typing indicators — track other users typing per channel (auto-expire).
    this.realtime.typingStarted$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(e => {
        if (e.userId !== this.auth.currentUser()?.id) this.addTyping(e.channelId, e.userId);
      });
    this.realtime.typingStopped$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(e => this.removeTyping(e.channelId, e.userId));
  }

  /** Records a user typing in a channel and schedules auto-clear after 6s of silence. */
  private addTyping(channelId: string, userId: string): void {
    this.typingMap.update(m => {
      const set = new Set(m[channelId] ?? []);
      set.add(userId);
      return { ...m, [channelId]: [...set] };
    });
    const key = `${channelId}|${userId}`;
    clearTimeout(this.typingTimers.get(key));
    this.typingTimers.set(key, setTimeout(() => this.removeTyping(channelId, userId), 6000));
  }

  /** Clears a user's typing state in a channel. */
  private removeTyping(channelId: string, userId: string): void {
    clearTimeout(this.typingTimers.get(`${channelId}|${userId}`));
    this.typingTimers.delete(`${channelId}|${userId}`);
    this.typingMap.update(m => {
      const rest = (m[channelId] ?? []).filter(u => u !== userId);
      const copy = { ...m };
      if (rest.length) copy[channelId] = rest; else delete copy[channelId];
      return copy;
    });
  }
}
