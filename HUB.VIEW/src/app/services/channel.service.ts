import { inject, Injectable, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ConfigService } from '../core/services/config.service';
import {
  AddChannelMemberRequest, ChannelDto, ChannelMemberDto,
  CreateChannelRequest, OpenLinkedThreadRequest, UpdateChannelRequest,
} from '../models/channel.model';

@Injectable({ providedIn: 'root' })
export class ChannelService {
  private readonly http   = inject(HttpClient);
  private readonly config = inject(ConfigService);
  private get apiUrl() { return `${this.config.apiBaseUrl}/channels`; }

  // ── Shared cache ─────────────────────────────────────────────────────────
  // Shell populates this after loading. Channel-detail-page reads it to
  // resolve slugs → channel objects without an extra network call.

  private readonly _channelsCache = signal<ChannelDto[] | null>(null);

  /** Null until the first workspace load completes; then the full channel list. */
  readonly channelsCache = this._channelsCache.asReadonly();

  // ── Selected channel (kept in state, not the URL — URL stays /{org}/channels) ──
  private static readonly SELECTION_KEY = 'hub.selectedChannel';
  private readonly _selectedId = signal<string | null>(this.readStoredSelection());

  /** The currently open channel id. Persisted so a refresh reopens it. */
  readonly selectedId = this._selectedId.asReadonly();

  /** Selects (opens) a channel by id and persists the choice. @param id Channel id. */
  selectChannel(id: string): void {
    this._selectedId.set(id);
    try { localStorage.setItem(ChannelService.SELECTION_KEY, id); } catch { /* storage unavailable */ }
  }

  private readStoredSelection(): string | null {
    try { return localStorage.getItem(ChannelService.SELECTION_KEY); } catch { return null; }
  }

  /** Stores the loaded channel list — called by shell after each successful list(). */
  updateCache(channels: ChannelDto[]): void { this._channelsCache.set(channels); }

  /**
   * Finds a channel by its slug in the local cache.
   * @param slug URL-safe channel slug (spaces → hyphens).
   * @returns The channel, or undefined if not found or cache not yet populated.
   */
  findBySlug(slug: string): ChannelDto | undefined {
    return this._channelsCache()?.find(c => c.slug === slug);
  }

  /**
   * Finds a channel by its id in the local cache.
   * @param id Channel id (UUID).
   * @returns The channel, or undefined if not found or cache not yet populated.
   */
  findById(id: string): ChannelDto | undefined {
    return this._channelsCache()?.find(c => c.id === id);
  }

  /**
   * Lists channels in a workspace visible to the caller.
   * @param workspaceId Workspace (DASHBOARD repository) id.
   * @returns Observable of channels.
   */
  list(workspaceId: string): Observable<ChannelDto[]> {
    const params = new HttpParams().set('workspaceId', workspaceId);
    return this.http.get<ChannelDto[]>(this.apiUrl, { params });
  }

  /** Gets a single channel by id. @param id Channel id. */
  get(id: string): Observable<ChannelDto> {
    return this.http.get<ChannelDto>(`${this.apiUrl}/${id}`);
  }

  /** Creates a new channel; caller becomes owner. @param request Channel payload. */
  create(request: CreateChannelRequest): Observable<ChannelDto> {
    return this.http.post<ChannelDto>(this.apiUrl, request);
  }

  /** Updates a channel's name and/or topic (admin/owner only). @param id Channel id. @param request Fields to update. */
  update(id: string, request: UpdateChannelRequest): Observable<ChannelDto> {
    return this.http.patch<ChannelDto>(`${this.apiUrl}/${id}`, request);
  }

  /** Switches a channel between Public and Private (owner only). @param id Channel id. @param isPrivate Target visibility. */
  changeVisibility(id: string, isPrivate: boolean): Observable<ChannelDto> {
    return this.http.patch<ChannelDto>(`${this.apiUrl}/${id}/visibility`, { isPrivate });
  }

  /** Transfers channel ownership to another member (owner only). @param id Channel id. @param newOwnerUserId New owner. */
  transferOwnership(id: string, newOwnerUserId: string): Observable<ChannelDto> {
    return this.http.put<ChannelDto>(`${this.apiUrl}/${id}/owner`, { newOwnerUserId });
  }

  /** Find-or-creates a channel linked to a DASHBOARD resource (POST /channels/linked). @param request Link payload. */
  openLinkedThread(request: OpenLinkedThreadRequest): Observable<ChannelDto> {
    return this.http.post<ChannelDto>(`${this.apiUrl}/linked`, request);
  }

  /** Adds the caller to a public channel (POST /channels/{id}/members). @param id Channel id. */
  join(id: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${id}/members`, {});
  }

  /** Removes the caller from a channel (DELETE /channels/{id}/members/me). @param id Channel id. */
  leave(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}/members/me`);
  }

  /** Lists members of a channel. @param id Channel id. @returns Observable of member DTOs. */
  listMembers(id: string): Observable<ChannelMemberDto[]> {
    return this.http.get<ChannelMemberDto[]>(`${this.apiUrl}/${id}/members`);
  }

  /**
   * Adds another user to a channel (admin/owner only). Idempotent — safe if already a member.
   * Uses PUT /channels/{id}/members/{userId} to avoid collision with the self-join POST endpoint.
   * @param id      Channel id.
   * @param request Contains the user id to add.
   */
  addMember(id: string, request: AddChannelMemberRequest): Observable<ChannelMemberDto> {
    return this.http.put<ChannelMemberDto>(`${this.apiUrl}/${id}/members/${request.userId}`, {});
  }

  /**
   * Removes a member from a channel (admin/owner or ManageChannels permission only).
   * Idempotent — safe if the user is not a member.
   * @param channelId Channel id.
   * @param userId    User to remove.
   */
  removeMember(channelId: string, userId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${channelId}/members/${userId}`);
  }

  /**
   * Opens (find-or-create) the canonical direct-message channel with a user. The backend matches
   * a DM by its two members, so both participants always resolve to the same channel.
   * @param workspaceId   Workspace used if a new DM must be created.
   * @param targetUserId  The user to start a DM with.
   * @returns Observable of the DM channel (with otherUserId set).
   */
  openDm(workspaceId: string, targetUserId: string): Observable<ChannelDto> {
    return this.http.post<ChannelDto>(
      `${this.apiUrl}/dm/${targetUserId}`, {}, { params: { workspaceId } });
  }
}
