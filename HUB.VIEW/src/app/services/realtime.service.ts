import { inject, Injectable, OnDestroy, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { ConfigService } from '../core/services/config.service';
import { AuthService } from './auth.service';
import { MessageDeletedEvent, MessageEditedEvent, MessageReceivedEvent, ReactionAddedEvent, ReactionRemovedEvent, TypingEvent } from '../models/message.model';
import { ChannelAccessRevokedEvent, ChannelReadEvent, ReadReceiptEvent } from '../models/channel.model';
import { NotificationReceivedEvent } from '../models/notification.model';
import { CustomStatusChangedEvent, PresenceChangedEvent } from '../models/presence.model';

export type HubConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

/**
 * Manages the SignalR connection to HUB realtime-service.
 * Singleton — connection lives for the app session.
 */
@Injectable({ providedIn: 'root' })
export class RealtimeService implements OnDestroy {
  private readonly auth   = inject(AuthService);
  private readonly config = inject(ConfigService);
  private connection!: signalR.HubConnection;

  readonly connectionState = signal<HubConnectionState>('disconnected');

  readonly messageReceived$      = new Subject<MessageReceivedEvent>();
  readonly messageEdited$        = new Subject<MessageEditedEvent>();
  readonly messageDeleted$       = new Subject<MessageDeletedEvent>();
  readonly notificationReceived$ = new Subject<NotificationReceivedEvent>();
  readonly presenceChanged$      = new Subject<PresenceChangedEvent>();
  readonly customStatusChanged$  = new Subject<CustomStatusChangedEvent>();
  readonly typingStarted$        = new Subject<TypingEvent>();
  readonly typingStopped$        = new Subject<TypingEvent>();
  readonly reactionAdded$        = new Subject<ReactionAddedEvent>();
  readonly reactionRemoved$      = new Subject<ReactionRemovedEvent>();
  /** This user lost access to a channel (kicked, removed by sprint sync, or left in another tab). */
  readonly channelAccessRevoked$ = new Subject<ChannelAccessRevokedEvent>();
  /** The current user read a channel in another tab/device — clear its unread badge here too. */
  readonly channelRead$ = new Subject<ChannelReadEvent>();
  /** The other DM participant read up to a point — drives the "Seen" receipt (CF-15). */
  readonly readReceipt$ = new Subject<ReadReceiptEvent>();
  /** A message was pinned/unpinned in a channel. */
  readonly messagePinChanged$ = new Subject<{ messageId: string; channelId: string; isPinned: boolean }>();
  /**
   * Emits after an automatic reconnect. Group memberships and anything pushed while disconnected are lost —
   * subscribers re-join what they need and fetch the gap (see channel-detail-page catch-up).
   */
  readonly reconnected$ = new Subject<void>();

  /** Starts the SignalR connection. Call once after successful login. */
  async connect(): Promise<void> {
    if (this.connection) await this.connection.stop();

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(this.config.hubUrl, {
        accessTokenFactory: () => this.auth.getToken() ?? '',
        transport: signalR.HttpTransportType.WebSockets,
        skipNegotiation: true,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.registerHandlers();
    this.connection.onreconnecting(() => this.connectionState.set('reconnecting'));
    this.connection.onreconnected(() => {
      this.connectionState.set('connected');
      this.reconnected$.next();
    });
    this.connection.onclose(() => this.connectionState.set('disconnected'));

    this.connectionState.set('connecting');
    try {
      await this.connection.start();
      this.connectionState.set('connected');
      this.startHeartbeat();
    } catch (err) {
      this.connectionState.set('disconnected');
      console.error('SignalR connect error', err);
    }
  }

  /** Disconnects the SignalR connection. Call on logout. */
  async disconnect(): Promise<void> {
    clearInterval(this.heartbeatTimer);
    await this.connection?.stop();
    this.connectionState.set('disconnected');
  }

  /** Subscribes to a channel's realtime group. @param channelId Channel to join. */
  async joinChannel(channelId: string): Promise<void> {
    if (this.connectionState() !== 'connected') return;
    await this.connection.invoke('JoinChannel', channelId);
  }

  /** Unsubscribes from a channel's realtime group. @param channelId Channel to leave. */
  async leaveChannel(channelId: string): Promise<void> {
    if (this.connectionState() !== 'connected') return;
    await this.connection.invoke('LeaveChannel', channelId);
  }

  /**
   * Broadcasts that the caller started typing in a channel.
   * No-op if not connected.
   * @param channelId Channel id.
   */
  async startTyping(channelId: string): Promise<void> {
    if (this.connection?.state !== signalR.HubConnectionState.Connected) return;
    await this.connection.invoke('StartTyping', channelId).catch(() => {/* silent */});
  }

  /**
   * Broadcasts that the caller stopped typing in a channel.
   * @param channelId Channel id.
   */
  async stopTyping(channelId: string): Promise<void> {
    if (this.connection?.state !== signalR.HubConnectionState.Connected) return;
    await this.connection.invoke('StopTyping', channelId).catch(() => {/* silent */});
  }

  /**
   * Sets the caller's manual presence status on the server (Active/Away/Do Not Disturb).
   * No-op if not connected; the caller should re-apply on (re)connect.
   * @param status Numeric PresenceStatus (1 = Active/auto, 2 = Away, 3 = Do Not Disturb).
   */
  async setStatus(status: number): Promise<void> {
    if (this.connection?.state !== signalR.HubConnectionState.Connected) return;
    await this.connection.invoke('SetStatus', status).catch(() => {/* silent */});
  }

  /**
   * Sets (or clears) the caller's custom status on the server (CF-19).
   * No-op if not connected. Empty emoji+text clears it.
   * @param emoji A single emoji, or ''.
   * @param text The status note, or '' to clear.
   * @param expiresAtUnixMs Auto-clear time in Unix ms, or null for "don't clear".
   */
  async setCustomStatus(emoji: string, text: string, expiresAtUnixMs: number | null): Promise<void> {
    if (this.connection?.state !== signalR.HubConnectionState.Connected) return;
    await this.connection.invoke('SetCustomStatus', emoji, text, expiresAtUnixMs).catch(() => {/* silent */});
  }

  ngOnDestroy(): void {
    clearInterval(this.heartbeatTimer);
    this.connection?.stop();
    this.messageReceived$.complete();
    this.messageEdited$.complete();
    this.messageDeleted$.complete();
    this.notificationReceived$.complete();
    this.presenceChanged$.complete();
    this.customStatusChanged$.complete();
    this.typingStarted$.complete();
    this.typingStopped$.complete();
    this.reactionAdded$.complete();
    this.reactionRemoved$.complete();
    this.channelAccessRevoked$.complete();
    this.channelRead$.complete();
    this.readReceipt$.complete();
    this.messagePinChanged$.complete();
    this.reconnected$.complete();
  }

  private heartbeatTimer: ReturnType<typeof setInterval> | undefined;

  /** Sends periodic heartbeats to keep the presence TTL alive. */
  private startHeartbeat(): void {
    clearInterval(this.heartbeatTimer);
    this.heartbeatTimer = setInterval(async () => {
      if (this.connection?.state === signalR.HubConnectionState.Connected)
        await this.connection.invoke('Heartbeat').catch(() => {/* silent */});
    }, 30_000);
  }

  private registerHandlers(): void {
    this.connection.on('MessageReceived',      (e: MessageReceivedEvent)      => this.messageReceived$.next(e));
    this.connection.on('messageEdited',        (e: MessageEditedEvent)        => this.messageEdited$.next(e));
    this.connection.on('messageDeleted',       (e: MessageDeletedEvent)       => this.messageDeleted$.next(e));
    this.connection.on('NotificationReceived', (e: NotificationReceivedEvent) => this.notificationReceived$.next(e));
    this.connection.on('presenceChanged',      (e: PresenceChangedEvent)      => this.presenceChanged$.next(e));
    this.connection.on('customStatusChanged',  (e: CustomStatusChangedEvent)  => this.customStatusChanged$.next(e));
    this.connection.on('typingStarted',        (e: TypingEvent)               => this.typingStarted$.next(e));
    this.connection.on('typingStopped',        (e: TypingEvent)               => this.typingStopped$.next(e));
    this.connection.on('reactionAdded',        (e: ReactionAddedEvent)        => this.reactionAdded$.next(e));
    this.connection.on('reactionRemoved',      (e: ReactionRemovedEvent)      => this.reactionRemoved$.next(e));
    this.connection.on('channelAccessRevoked', (e: ChannelAccessRevokedEvent) => this.channelAccessRevoked$.next(e));
    this.connection.on('channelRead',          (e: ChannelReadEvent)          => this.channelRead$.next(e));
    this.connection.on('readReceipt',          (e: ReadReceiptEvent)          => this.readReceipt$.next(e));
    this.connection.on('messagePinChanged',    (e: { messageId: string; channelId: string; isPinned: boolean }) => this.messagePinChanged$.next(e));
  }
}
