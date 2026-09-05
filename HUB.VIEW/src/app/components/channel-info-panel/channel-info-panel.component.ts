import {
  ChangeDetectionStrategy, Component, DestroyRef, inject, input, OnInit, output, signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ChannelService } from '../../services/channel.service';
import { ChannelDto, ChannelMemberRole, ChannelType } from '../../models/channel.model';
import { ChannelMembersPanelComponent } from '../channel-members-panel/channel-members-panel.component';

type InfoTab = 'about' | 'members' | 'settings';

/**
 * Channel "…" modal — a centered dialog with tabs (Slack-style).
 * Tab 1 (About): topic (editable), created date, channel id, leave.
 * Tab 2 (Members): reuses {@link ChannelMembersPanelComponent} for add/remove.
 * Designed as a tab container so Files/Pins/Settings can be added later without a rewrite.
 */
@Component({
  selector: 'app-channel-info-panel',
  standalone: true,
  imports: [DatePipe, ChannelMembersPanelComponent],
  templateUrl: './channel-info-panel.component.html',
  styleUrl: './channel-info-panel.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChannelInfoPanelComponent implements OnInit {
  private readonly channelSvc = inject(ChannelService);
  private readonly destroyRef = inject(DestroyRef);

  readonly channel       = input.required<ChannelDto>();
  readonly channelId     = input.required<string>();
  readonly currentUserId = input.required<string>();
  readonly initialTab    = input<InfoTab>('about');

  readonly close             = output<void>();
  readonly memberCountChanged = output<number>();
  /** Emitted after the topic is updated so the parent can refresh its channel copy. */
  readonly topicChanged = output<string>();
  /** Emitted after leaving the channel. */
  readonly left = output<void>();
  /** Emitted after joining the channel. */
  readonly joined = output<void>();
  /** Emitted after the channel visibility changes (true = now private). */
  readonly visibilityChanged = output<boolean>();
  /** Emitted after ownership is transferred (payload = new owner's user id). */
  readonly ownershipTransferred = output<string>();

  /** True while a visibility change is in flight. */
  protected readonly changingVisibility = signal(false);

  /** True when the channel is currently private (drives the Settings toggle). */
  protected get isPrivate(): boolean {
    return this.channel().isPrivate;
  }

  readonly ChannelType = ChannelType;

  protected readonly tab          = signal<InfoTab>('about');
  protected readonly editingTopic = signal(false);
  protected readonly topicDraft   = signal('');
  protected readonly savingTopic  = signal(false);
  protected readonly copied       = signal(false);

  ngOnInit(): void {
    this.tab.set(this.initialTab());
  }

  /** True for DM/GroupDm channels — hides topic editing + leave. */
  protected get isDm(): boolean {
    return this.channel().type === ChannelType.Dm || this.channel().type === ChannelType.GroupDm;
  }

  /** True when the current user is a member — drives the Leave/Join toggle. */
  protected get isMember(): boolean {
    return this.channel().isMember;
  }

  /** True when the current user owns the channel — owners can't leave (must transfer first). */
  protected get isOwner(): boolean {
    return this.channel().myRole === ChannelMemberRole.Owner;
  }

  protected startEditTopic(): void {
    this.topicDraft.set(this.channel().topic ?? '');
    this.editingTopic.set(true);
  }

  protected onTopicInput(event: Event): void {
    this.topicDraft.set((event.target as HTMLInputElement).value);
  }

  protected saveTopic(): void {
    const topic = this.topicDraft().trim();
    this.savingTopic.set(true);
    this.channelSvc.update(this.channelId(), { topic })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.savingTopic.set(false);
          this.editingTopic.set(false);
          this.topicChanged.emit(topic);
        },
        error: () => this.savingTopic.set(false),
      });
  }

  protected copyId(): void {
    try {
      navigator.clipboard?.writeText(this.channelId());
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 1500);
    } catch { /* clipboard unavailable */ }
  }

  protected leaveChannel(): void {
    this.channelSvc.leave(this.channelId())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: () => this.left.emit(), error: () => { /* ignore */ } });
  }

  /** Switches the channel between Public and Private (owner only) and notifies the parent. */
  protected setVisibility(isPrivate: boolean): void {
    if (this.changingVisibility() || this.isPrivate === isPrivate) return;
    this.changingVisibility.set(true);
    this.channelSvc.changeVisibility(this.channelId(), isPrivate)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => { this.visibilityChanged.emit(isPrivate); this.changingVisibility.set(false); },
        error: () => this.changingVisibility.set(false),
      });
  }

  /** Joins the (public) channel, then notifies the parent to swap the composer in. */
  protected joinChannel(): void {
    this.channelSvc.join(this.channelId())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: () => this.joined.emit(), error: () => { /* ignore */ } });
  }
}
