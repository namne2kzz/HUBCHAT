import {
  ChangeDetectionStrategy, Component, computed, DestroyRef, inject,
  input, OnChanges, output, signal, SimpleChanges,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ChannelService } from '../../services/channel.service';
import { ConfirmDialogService } from '../../services/confirm-dialog.service';
import { DirectoryService } from '../../services/directory.service';
import { PresenceService } from '../../services/presence.service';
import { ChannelMemberDto, ChannelMemberRole } from '../../models/channel.model';
import { DirectoryUser } from '../../models/directory.model';

interface MemberRow {
  dto: ChannelMemberDto;
  profile: DirectoryUser | null;
}

/**
 * Right-side panel listing a channel's members with presence dots.
 * Owners, Admins, and users with the ManageChannels workspace permission can:
 *   - Add workspace members via the "+" footer picker.
 *   - Remove any member (except the sole owner) via the "×" row button.
 */
@Component({
  selector: 'app-channel-members-panel',
  standalone: true,
  imports: [],
  templateUrl: './channel-members-panel.component.html',
  styleUrl: './channel-members-panel.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChannelMembersPanelComponent implements OnChanges {
  private readonly channelSvc    = inject(ChannelService);
  private readonly directory     = inject(DirectoryService);
  private readonly destroyRef    = inject(DestroyRef);
  private readonly confirmDialog = inject(ConfirmDialogService);
  readonly presence           = inject(PresenceService);

  /** Id of the current authenticated user (used to determine role + hide self-remove). */
  readonly currentUserId = input.required<string>();
  /** Channel whose members are displayed. */
  readonly channelId = input.required<string>();

  /** Emits the current member count whenever it changes (load / add / remove). */
  readonly memberCountChanged = output<number>();
  /** Emits the new owner's user id after ownership is transferred. */
  readonly ownershipTransferred = output<string>();

  /** User id currently being promoted to owner (shows a spinner on that row). */
  protected readonly transferring = signal<string | null>(null);

  protected readonly members          = signal<MemberRow[]>([]);
  protected readonly loading          = signal(false);
  protected readonly error            = signal(false);

  // ── Permission state ─────────────────────────────────────────────────────
  /** Workspace-level permission strings for the current user (e.g. "ManageChannels"). */
  private readonly myPermissions      = signal<string[]>([]);

  /** Channel role of the current user, derived from the members list. */
  private readonly currentUserRole = computed(() =>
    this.members().find(r => r.dto.userId === this.currentUserId())?.dto.role ?? null
  );

  /** Number of owners in this channel (to guard against removing the last owner). */
  private readonly ownerCount = computed(() =>
    this.members().filter(r => r.dto.role === ChannelMemberRole.Owner).length
  );

  /** True when the current user is the channel Owner — only they can transfer ownership. */
  protected readonly isOwnerViewer = computed(() => this.currentUserRole() === ChannelMemberRole.Owner);

  /**
   * True when the current user may add/remove channel members:
   * they are the channel Owner or Admin, OR they hold the ManageChannels workspace privilege.
   */
  protected readonly canManage = computed(() => {
    const role = this.currentUserRole();
    if (role === ChannelMemberRole.Owner || role === ChannelMemberRole.Admin) return true;
    return this.myPermissions().includes('ManageChannels');
  });

  // ── Add-member state ─────────────────────────────────────────────────────
  /** Whether the add-member picker is expanded. */
  protected readonly addMode           = signal(false);
  /** Full workspace member list — loaded lazily on first picker open. */
  private readonly workspaceMembers    = signal<DirectoryUser[]>([]);
  /** Loading workspace members (first open only). */
  protected readonly loadingCandidates = signal(false);
  /** Filter query inside the picker. */
  protected readonly addQuery          = signal('');
  /** Id of the user currently being added (shows spinner). */
  protected readonly adding            = signal<string | null>(null);
  /** Id of the user currently being removed (shows spinner). */
  protected readonly removing          = signal<string | null>(null);

  /**
   * Workspace members who are not already in the channel and match the search query.
   * Used to populate the add-member picker.
   */
  protected readonly candidates = computed(() => {
    const q       = this.addQuery().toLowerCase().trim();
    const current = new Set(this.members().map(r => r.dto.userId));
    const all     = this.workspaceMembers()
      .filter(m => !current.has(m.id) && !m.isDeleted);
    return q
      ? all.filter(m =>
          m.name.toLowerCase().includes(q) || m.email.toLowerCase().includes(q))
      : all;
  });

  readonly ChannelMemberRole = ChannelMemberRole;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['channelId'] && this.channelId()) {
      // Reset state on channel switch.
      this.addMode.set(false);
      this.addQuery.set('');
      this.workspaceMembers.set([]);
      this.myPermissions.set([]);
      this.load();
      this.loadMyPermissions();
    }
  }

  // ── Add-member actions ────────────────────────────────────────────────────

  /** Toggles the add-member picker; loads workspace members on first open. */
  protected toggleAddMode(): void {
    this.addMode.update(v => !v);
    this.addQuery.set('');

    if (this.addMode() && this.workspaceMembers().length === 0) {
      const wsId = this.channelSvc.findById(this.channelId())?.workspaceId;
      if (!wsId) return;

      this.loadingCandidates.set(true);
      this.directory.listWorkspaceMembers(wsId)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: members => {
            this.workspaceMembers.set(members);
            this.loadingCandidates.set(false);
          },
          error: () => this.loadingCandidates.set(false),
        });
    }
  }

  /** Updates the candidate search query. @param event Native input event. */
  protected onAddQueryInput(event: Event): void {
    this.addQuery.set((event.target as HTMLInputElement).value);
  }

  /**
   * Adds a workspace member to the channel, then appends them to the local list.
   * @param user The workspace member to add.
   */
  protected addMember(user: DirectoryUser): void {
    if (this.adding()) return;
    this.adding.set(user.id);

    this.channelSvc.addMember(this.channelId(), { userId: user.id })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: dto => {
          this.members.update(rows => [...rows, { dto, profile: user }]);
          this.memberCountChanged.emit(this.members().length);
          this.adding.set(null);
        },
        error: () => this.adding.set(null),
      });
  }

  // ── Remove-member actions ─────────────────────────────────────────────────

  /**
   * Returns true if the × button should be enabled for this row.
   * Cannot remove yourself (use Leave instead) or the sole channel owner.
   * @param row Member row to evaluate.
   */
  protected canRemoveRow(row: MemberRow): boolean {
    if (row.dto.userId === this.currentUserId()) return false;
    if (row.dto.role === ChannelMemberRole.Owner && this.ownerCount() <= 1) return false;
    return true;
  }

  /**
   * Removes a member from the channel, then drops them from the local list.
   * @param row The member row to remove.
   */
  protected removeMember(row: MemberRow): void {
    if (this.removing()) return;
    this.removing.set(row.dto.userId);

    this.channelSvc.removeMember(this.channelId(), row.dto.userId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.members.update(rows => rows.filter(r => r.dto.userId !== row.dto.userId));
          this.memberCountChanged.emit(this.members().length);
          this.removing.set(null);
        },
        error: () => this.removing.set(null),
      });
  }

  /** True when the current owner can hand ownership to this row (another non-owner member). @param row Member row. */
  protected canMakeOwner(row: MemberRow): boolean {
    return this.isOwnerViewer()
        && row.dto.userId !== this.currentUserId()
        && row.dto.role !== ChannelMemberRole.Owner;
  }

  /** Promotes a member to Owner (demoting the caller to Admin) after confirmation. @param row Target member. */
  protected async makeOwner(row: MemberRow): Promise<void> {
    if (this.transferring()) return;
    const name = row.profile?.name ?? 'this member';
    const ok = await this.confirmDialog.confirm({
      title: 'Transfer ownership?',
      description: `${name} will become the owner and you will become an admin. This can't be undone by you afterwards.`,
      confirmText: 'Transfer',
      cancelText: 'Cancel',
      tone: 'warning',
      icon: 'warning',
    });
    if (!ok) return;

    this.transferring.set(row.dto.userId);
    this.channelSvc.transferOwnership(this.channelId(), row.dto.userId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.ownershipTransferred.emit(row.dto.userId);
          this.transferring.set(null);
          this.load(); // refresh roles (new Owner + caller now Admin)
        },
        error: () => this.transferring.set(null),
      });
  }

  // ── Private helpers ───────────────────────────────────────────────────────

  private load(): void {
    this.loading.set(true);
    this.error.set(false);
    this.members.set([]);

    this.channelSvc.listMembers(this.channelId()).subscribe({
      next: dtos => {
        const rows: MemberRow[] = dtos.map(dto => ({ dto, profile: null }));
        this.members.set(rows);
        this.memberCountChanged.emit(rows.length);
        this.loading.set(false);

        this.presence.fetchStatuses(dtos.map(d => d.userId)).subscribe();

        dtos.forEach((dto, i) => {
          this.directory.getUser(dto.userId).subscribe({
            next: profile => this.members.update(
              rs => rs.map((r, ri) => ri === i ? { ...r, profile } : r)
            ),
            error: () => { /* keep null profile */ },
          });
        });
      },
      error: () => { this.loading.set(false); this.error.set(true); },
    });
  }

  /**
   * Loads the current user's workspace-level permissions from the directory service.
   * Populates `myPermissions` so `canManage` can check for 'ManageChannels'.
   */
  private loadMyPermissions(): void {
    const wsId = this.channelSvc.findById(this.channelId())?.workspaceId;
    if (!wsId) return;

    this.directory.getMyMemberships()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: m => {
          const repo = m.repositories.find(r => r.repositoryId === wsId);
          this.myPermissions.set(repo?.permissions ?? []);
        },
        error: () => { /* leave empty — canManage falls back to role check only */ },
      });
  }

  /** Display name for a row — falls back to short id. @param row Member row. */
  protected displayName(row: MemberRow): string {
    return row.profile?.name ?? `User …${row.dto.userId.slice(-6)}`;
  }

  /** Avatar initials (first letter). @param row Member row. */
  protected initials(row: MemberRow): string {
    return (row.profile?.name ?? 'U').charAt(0).toUpperCase();
  }

  /** Avatar CSS class from profile. @param row Member row. */
  protected avatarClass(row: MemberRow): string {
    return row.profile?.avatarClass ?? 'avatar-neutral';
  }

  /** Role badge label. @param role Member role. */
  protected roleLabel(role: ChannelMemberRole): string {
    return role === ChannelMemberRole.Owner ? 'Owner'
         : role === ChannelMemberRole.Admin ? 'Admin'
         : '';
  }
}
