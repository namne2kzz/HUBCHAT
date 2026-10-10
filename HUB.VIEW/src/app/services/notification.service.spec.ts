import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { NotificationService } from './notification.service';
import { ConfigService } from '../core/services/config.service';
import { NotificationDto, NotificationType } from '../models/notification.model';

/**
 * Covers NotificationService.onRealtimeReceived: a realtime push lands once in the list and the badge,
 * even when the broker redelivers it.
 */
describe('NotificationService', () => {
  let service: NotificationService;

  const notif = (id: string, isRead = false): NotificationDto => ({
    id, type: NotificationType.Mention, sourceId: 'm1', channelId: 'c1', byUserId: 'u2',
    preview: 'hi @you', isRead, createdAt: '2026-10-08T10:00:00Z',
  });

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ConfigService, useValue: { apiBaseUrl: 'http://localhost/api/v1', dashboardUrl: '', hubUrl: '' } },
      ],
    });
    service = TestBed.inject(NotificationService);
  });

  it('prepends a pushed notification and counts it as unread', () => {
    service.onRealtimeReceived(notif('n1'));
    service.onRealtimeReceived(notif('n2'));

    expect(service.notifications().map(n => n.id)).toEqual(['n2', 'n1']);
    expect(service.unreadCount()).toBe(2);
  });

  it('ignores a redelivered notification it already shows', () => {
    service.onRealtimeReceived(notif('n1'));
    service.onRealtimeReceived(notif('n1'));

    // Counting it twice would leave a badge that reading every notification can never clear.
    expect(service.notifications().length).toBe(1);
    expect(service.unreadCount()).toBe(1);
  });

  it('does not bump the badge for an already-read notification', () => {
    service.onRealtimeReceived(notif('n1', true));
    expect(service.unreadCount()).toBe(0);
  });
});
