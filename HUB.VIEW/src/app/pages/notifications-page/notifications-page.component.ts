import { ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { DatePipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NotificationService } from '../../services/notification.service';
import { RealtimeService } from '../../services/realtime.service';
import { ChannelService } from '../../services/channel.service';
import { NotificationType } from '../../models/notification.model';

@Component({
  selector: 'app-notifications-page',
  standalone: true,
  imports: [DatePipe],
  templateUrl: './notifications-page.component.html',
  styleUrl: './notifications-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotificationsPageComponent implements OnInit {
  protected readonly notifSvc = inject(NotificationService);
  private readonly realtime   = inject(RealtimeService);
  private readonly channelSvc = inject(ChannelService);
  private readonly router     = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  /** Reads from the shared service signal — in sync with the sub-sidebar panel. */
  protected readonly notifications = this.notifSvc.notifications;
  protected readonly loading       = signal(false);

  ngOnInit(): void {
    if (this.notifications().length === 0) {
      this.loading.set(true);
      this.notifSvc.list().subscribe({
        next: () => this.loading.set(false),
        error: () => this.loading.set(false),
      });
    }
    this.subscribeRealtime();
  }

  /** Human label for a notification type. @param type Notification type. */
  protected typeLabel(type: NotificationType): string {
    switch (type) {
      case NotificationType.Mention:       return 'Mention';
      case NotificationType.DirectMessage: return 'Direct message';
      default:                             return 'System';
    }
  }

  /**
   * Marks a notification read and navigates to its channel if linked.
   * @param n Notification to open.
   */
  protected open(n: { id: string; isRead: boolean; channelId: string | null }): void {
    if (!n.isRead) {
      this.notifSvc.markRead(n.id).subscribe();
      this.notifSvc.markReadLocally(n.id);
    }
    if (n.channelId) {
      const ch = this.channelSvc.findById(n.channelId);
      const alias = this.router.url.split('/').filter(Boolean)[0] ?? '';
      this.router.navigate(['/', alias, 'channels', ch?.slug ?? n.channelId]);
    }
  }

  /** Marks all notifications as read. */
  protected markAllRead(): void {
    this.notifSvc.markAllRead().subscribe();
    this.notifSvc.markAllReadLocally();
  }

  private subscribeRealtime(): void {
    this.realtime.notificationReceived$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(e => this.notifSvc.onRealtimeReceived(e.notification));
  }
}
