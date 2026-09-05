import {
  ChangeDetectionStrategy, Component, computed, DestroyRef, inject, input, OnDestroy, signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';
import { RealtimeService } from '../../services/realtime.service';
import { DirectoryService } from '../../services/directory.service';
import { DirectoryUser } from '../../models/directory.model';

interface TypingUser { userId: string; name: string; }

/**
 * Shows "X is typing…" underneath the message list.
 * Auto-clears a user after 5 s of silence (server also sends typingStopped).
 */
@Component({
  selector: 'app-typing-indicator',
  standalone: true,
  imports: [],
  templateUrl: './typing-indicator.component.html',
  styleUrl: './typing-indicator.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TypingIndicatorComponent implements OnDestroy {
  private readonly realtime   = inject(RealtimeService);
  private readonly directory  = inject(DirectoryService);
  private readonly destroyRef = inject(DestroyRef);

  /** Current channel id — used to filter events. */
  readonly channelId = input.required<string>();

  /** Resolved author profiles; used for display names. */
  readonly authors   = input<Record<string, DirectoryUser>>({});

  /** The authenticated user's id — excluded from typing indicator. */
  readonly currentUserId = input<string>('');

  private readonly typingUsers = signal<TypingUser[]>([]);
  private readonly timers = new Map<string, ReturnType<typeof setTimeout>>();

  /** Human-readable "X is typing…" string, or null when nobody is typing. */
  readonly typingText = computed<string | null>(() => {
    const u = this.typingUsers();
    if (!u.length) return null;
    if (u.length === 1) return `${u[0].name} is typing…`;
    if (u.length === 2) return `${u[0].name} and ${u[1].name} are typing…`;
    return 'Several people are typing…';
  });

  constructor() {
    this.realtime.typingStarted$
      .pipe(
        filter(e => e.channelId === this.channelId() && e.userId !== this.currentUserId()),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(e => this.addTyping(e.userId));

    this.realtime.typingStopped$
      .pipe(
        filter(e => e.channelId === this.channelId()),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(e => this.removeTyping(e.userId));
  }

  ngOnDestroy(): void {
    this.timers.forEach(t => clearTimeout(t));
  }

  private addTyping(userId: string): void {
    const known = this.authors()[userId]?.name;
    const name  = known ?? `User …${userId.slice(-4)}`;
    this.typingUsers.update(u => u.some(x => x.userId === userId) ? u : [...u, { userId, name }]);

    // Resolve the real display name if it wasn't in the authors map (typist hasn't posted here yet).
    if (!known) {
      this.directory.getUser(userId).subscribe({
        next: user => this.typingUsers.update(u =>
          u.map(x => x.userId === userId ? { ...x, name: user.name } : x)),
        error: () => { /* keep short-id fallback */ },
      });
    }

    clearTimeout(this.timers.get(userId));
    this.timers.set(userId, setTimeout(() => this.removeTyping(userId), 5_000));
  }

  private removeTyping(userId: string): void {
    clearTimeout(this.timers.get(userId));
    this.timers.delete(userId);
    this.typingUsers.update(u => u.filter(x => x.userId !== userId));
  }
}
