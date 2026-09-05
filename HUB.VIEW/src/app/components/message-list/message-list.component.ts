import {
  ChangeDetectionStrategy, Component, computed, effect, ElementRef, HostListener,
  inject, input, output, signal, untracked,
} from '@angular/core';
import { DatePipe, DOCUMENT } from '@angular/common';
import { MessageDto } from '../../models/message.model';
import { DirectoryUser } from '../../models/directory.model';
import { renderMarkdown } from '../../utils/markdown.util';

interface MessageRow {
  msg: MessageDto;
  /** True when this is the first message in a consecutive same-author group (> 5 min gap). */
  showHeader: boolean;
  /** True when this message was sent by the current user. */
  isOwn: boolean;
  /** True when the next message is from the same author (suppress bottom radius). */
  isContinued: boolean;
  /** Day-separator label (e.g. "Today") shown above this row when the date changes; null otherwise. */
  dateLabel: string | null;
  /** Message body rendered from Markdown to safe HTML. */
  html: string;
}

/**
 * Renders a bubble-style chat list.
 * Own messages appear on the right (accent bubble); others on the left (surface bubble + avatar).
 * Consecutive messages from the same author within 5 minutes are visually grouped.
 */
@Component({
  selector: 'app-message-list',
  standalone: true,
  imports: [DatePipe],
  templateUrl: './message-list.component.html',
  styleUrl: './message-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MessageListComponent {
  private readonly document = inject(DOCUMENT);
  private readonly host     = inject(ElementRef<HTMLElement>);

  /** Whether the view is scrolled to (near) the bottom. Drives auto-follow + the scroll-down button. */
  protected readonly atBottom = signal(true);

  /** True while an older-messages page is loading (set when we trigger loadMore). */
  private loadingOlder = false;
  /** Scroll height + offset captured just before an older-page load, to anchor position after prepend. */
  private preLoadScrollHeight = 0;
  private preLoadScrollTop = 0;

  constructor() {
    effect(() => {
      this.messages(); // track list changes
      const el = this.host.nativeElement;
      if (this.loadingOlder) {
        // Older messages prepended — keep the previously-top message anchored (no jump).
        setTimeout(() => {
          el.scrollTop = el.scrollHeight - this.preLoadScrollHeight + this.preLoadScrollTop;
          this.loadingOlder = false;
        });
      } else if (untracked(() => this.atBottom())) {
        // New message appended while at bottom — follow it.
        setTimeout(() => this.scrollToBottom());
      }
    });
  }

  /**
   * Tracks bottom proximity (auto-follow + scroll-down button) and triggers infinite
   * scroll-up: when the user nears the top and more history exists, request an older page.
   */
  @HostListener('scroll')
  protected onScroll(): void {
    const el = this.host.nativeElement;
    this.atBottom.set(el.scrollHeight - el.scrollTop - el.clientHeight < 150);

    if (el.scrollTop < 120 && this.hasMore() && !this.loadingOlder) {
      this.loadingOlder = true;
      this.preLoadScrollHeight = el.scrollHeight;
      this.preLoadScrollTop = el.scrollTop;
      this.loadMore.emit();
    }
  }

  /** Scrolls the list to the newest message. */
  protected scrollToBottom(): void {
    const el = this.host.nativeElement;
    el.scrollTop = el.scrollHeight;
  }

  readonly messages      = input.required<MessageDto[]>();
  /** Map of authorId → resolved directory profile (name + avatar class). */
  readonly authors       = input<Record<string, DirectoryUser>>({});
  readonly hasMore       = input<boolean>(false);
  /** The current user's id — messages matching this id are shown on the right. */
  readonly currentUserId = input<string>('');
  /**
   * Id of the message to flash-highlight (set by the parent when a search result is clicked).
   * The corresponding bubble row receives the `bubble-row-highlight` class.
   */
  readonly highlightedId = input<string | null>(null);

  /** Emits when older messages are requested. */
  readonly loadMore = output<void>();

  /** Computed rows with grouping metadata. */
  readonly rows = computed<MessageRow[]>(() => {
    const list = this.messages();
    const me   = this.currentUserId();
    return list.map((msg, i) => {
      const prev = list[i - 1];
      const next = list[i + 1];
      const gap  = prev
        ? new Date(msg.createdAt).getTime() - new Date(prev.createdAt).getTime()
        : Infinity;
      const nextGap = next
        ? new Date(next.createdAt).getTime() - new Date(msg.createdAt).getTime()
        : Infinity;

      const showHeader  = !prev || prev.authorId !== msg.authorId || gap > 5 * 60_000;
      const isContinued = !!next && next.authorId === msg.authorId && nextGap <= 5 * 60_000;

      const sameDay   = prev && new Date(prev.createdAt).toDateString() === new Date(msg.createdAt).toDateString();
      const dateLabel = sameDay ? null : this.dayLabel(msg.createdAt);

      return { msg, showHeader, isOwn: msg.authorId === me, isContinued, dateLabel, html: renderMarkdown(msg.body) };
    });
  });

  /** Human day label for a date separator: "Today", "Yesterday", or a formatted date. */
  private dayLabel(iso: string): string {
    const d     = new Date(iso);
    const today = new Date();
    const yest  = new Date(); yest.setDate(today.getDate() - 1);
    if (d.toDateString() === today.toDateString()) return 'Today';
    if (d.toDateString() === yest.toDateString())  return 'Yesterday';
    return d.toLocaleDateString(undefined, { weekday: 'short', month: 'short', day: 'numeric' });
  }

  /** Display name for an author id. @param authorId Author id. */
  authorName(authorId: string): string {
    return this.authors()[authorId]?.name ?? `User ${authorId.slice(0, 8)}`;
  }

  /** Avatar letter for an author (first character of their name). @param authorId Author id. */
  authorInitial(authorId: string): string {
    return (this.authors()[authorId]?.name ?? '?').charAt(0).toUpperCase();
  }

  /**
   * Smoothly scrolls the message list to the bubble with the given message id.
   * Called by the parent component when the user clicks a search result.
   * @param id Message id to scroll to.
   */
  scrollToMessage(id: string): void {
    this.document
      .getElementById('msg-' + id)
      ?.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }
}
