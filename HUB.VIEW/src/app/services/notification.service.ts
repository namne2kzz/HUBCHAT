import { inject, Injectable, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { ConfigService } from '../core/services/config.service';
import { NotificationDto } from '../models/notification.model';

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly http   = inject(HttpClient);
  private readonly config = inject(ConfigService);
  private get apiUrl() { return `${this.config.apiBaseUrl}/notifications`; }

  /** Unread count signal — updated by realtime events and API responses. */
  readonly unreadCount = signal<number>(0);

  /** Cached notification list — shared between the sub-sidebar panel and the dedicated page. */
  readonly notifications = signal<NotificationDto[]>([]);

  /**
   * Lists the caller's notifications (newest first).
   * Backend returns a plain array (no pagination wrapper).
   * @param unreadOnly Filter to unread only.
   * @param limit Page size (1..100).
   * @returns Observable of notifications.
   */
  list(unreadOnly = false, limit = 50): Observable<NotificationDto[]> {
    const params = new HttpParams()
      .set('unreadOnly', unreadOnly)
      .set('limit', limit);
    return this.http.get<NotificationDto[]>(this.apiUrl, { params }).pipe(
      tap(items => {
        this.notifications.set(items);
        this.unreadCount.set(items.filter(n => !n.isRead).length);
      })
    );
  }

  /** Marks a single notification as read (POST /{id}/read). @param id Notification id. */
  markRead(id: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${id}/read`, {}).pipe(
      tap(() => this.unreadCount.update(c => Math.max(0, c - 1)))
    );
  }

  /** Marks all notifications as read (POST /read-all). */
  markAllRead(): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/read-all`, {}).pipe(
      tap(() => this.unreadCount.set(0))
    );
  }

  /** Marks a notification read in the cached list without a network call. @param id Notification id. */
  markReadLocally(id: string): void {
    this.notifications.update(ns => ns.map(n => n.id === id ? { ...n, isRead: true } : n));
  }

  /** Marks all notifications read in the cached list without a network call. */
  markAllReadLocally(): void {
    this.notifications.update(ns => ns.map(n => ({ ...n, isRead: true })));
  }

  /** Prepends a realtime notification and bumps unread count. @param notification Received notification. */
  onRealtimeReceived(notification: NotificationDto): void {
    this.notifications.update(ns => [notification, ...ns]);
    if (!notification.isRead) this.unreadCount.update(c => c + 1);
  }
}
