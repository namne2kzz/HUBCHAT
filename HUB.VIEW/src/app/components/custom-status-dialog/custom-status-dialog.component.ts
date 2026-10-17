import {
  ChangeDetectionStrategy, Component, computed, inject, input, OnInit, output, signal,
} from '@angular/core';
import { PresenceService } from '../../services/presence.service';

/** A ready-made status a user can pick with one tap (Slack-style). */
interface StatusPreset { emoji: string; text: string; /** minutes until auto-clear, or null */ clearAfterMin: number | null; }

/** An auto-clear option offered in the "Clear after" dropdown. */
interface ClearOption { label: string; minutes: number | null; }

/**
 * Custom status composer (CF-19) — a centered dialog: emoji + note + "clear after", plus one-tap presets.
 * Emits {@link saved} once the status is set (or cleared) so the host can close and refresh its own view.
 */
@Component({
  selector: 'app-custom-status-dialog',
  standalone: true,
  imports: [],
  templateUrl: './custom-status-dialog.component.html',
  styleUrl: './custom-status-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CustomStatusDialogComponent implements OnInit {
  private readonly presence = inject(PresenceService);

  /** Current user's id — so the dialog can prefill from an existing status. */
  readonly userId = input.required<string>();

  readonly close = output<void>();
  /** Emitted after the status is set or cleared. */
  readonly saved = output<void>();

  protected readonly MAX_LEN = 100;

  protected readonly emoji = signal('💬');
  protected readonly text  = signal('');
  protected readonly clearAfterMin = signal<number | null>(60);
  protected readonly saving = signal(false);

  /** A short palette of common status emojis (picking from the OS picker is also fine via the input). */
  protected readonly emojiPalette = ['💬', '📅', '🏠', '🍔', '🤒', '🌴', '🚌', '🎧', '🧠', '☕', '🔕', '✈️'];

  protected readonly presets: ReadonlyArray<StatusPreset> = [
    { emoji: '📅', text: 'In a meeting',     clearAfterMin: 60 },
    { emoji: '🚌', text: 'Commuting',        clearAfterMin: 30 },
    { emoji: '🤒', text: 'Out sick',         clearAfterMin: null },
    { emoji: '🌴', text: 'Vacationing',      clearAfterMin: null },
    { emoji: '🏠', text: 'Working remotely', clearAfterMin: null },
    { emoji: '🍔', text: 'Lunch',            clearAfterMin: 30 },
  ];

  protected readonly clearOptions: ReadonlyArray<ClearOption> = [
    { label: "Don't clear", minutes: null },
    { label: '30 minutes',  minutes: 30 },
    { label: '1 hour',      minutes: 60 },
    { label: '4 hours',     minutes: 240 },
    { label: 'Today',       minutes: -1 },   // sentinel: until end of today
    { label: 'This week',   minutes: -2 },   // sentinel: until end of this week (Sun)
  ];

  protected readonly canSave = computed(() => this.text().trim().length > 0 || this.emoji().trim().length > 0);

  ngOnInit(): void {
    const existing = this.presence.getCustomStatus(this.userId());
    if (existing) {
      if (existing.emoji) this.emoji.set(existing.emoji);
      this.text.set(existing.text);
      this.clearAfterMin.set(null); // keep whatever expiry was set unless the user changes it
    }
  }

  protected onEmojiInput(e: Event): void {
    // Keep only the first grapheme-ish character — a status carries a single emoji.
    const v = (e.target as HTMLInputElement).value.trim();
    this.emoji.set([...v][0] ?? '');
  }

  protected onTextInput(e: Event): void {
    this.text.set((e.target as HTMLInputElement).value.slice(0, this.MAX_LEN));
  }

  protected onClearChange(e: Event): void {
    const raw = (e.target as HTMLSelectElement).value;
    this.clearAfterMin.set(raw === 'null' ? null : Number(raw));
  }

  protected applyPreset(p: StatusPreset): void {
    this.emoji.set(p.emoji);
    this.text.set(p.text);
    this.clearAfterMin.set(p.clearAfterMin);
  }

  /** Resolves the chosen "clear after" option to an absolute Date (or null). */
  private resolveExpiry(): Date | null {
    const m = this.clearAfterMin();
    if (m === null) return null;
    const now = new Date();
    if (m === -1) { const d = new Date(now); d.setHours(23, 59, 59, 0); return d; }
    if (m === -2) {
      const d = new Date(now);
      const daysToSunday = (7 - d.getDay()) % 7; // 0 = Sunday
      d.setDate(d.getDate() + daysToSunday);
      d.setHours(23, 59, 59, 0);
      return d;
    }
    return new Date(now.getTime() + m * 60_000);
  }

  protected async save(): Promise<void> {
    if (this.saving() || !this.canSave()) return;
    this.saving.set(true);
    try {
      await this.presence.setCustomStatus(this.emoji().trim(), this.text().trim(), this.resolveExpiry());
      this.saved.emit();
    } finally {
      this.saving.set(false);
    }
  }

  protected async clear(): Promise<void> {
    this.saving.set(true);
    try {
      await this.presence.setCustomStatus('', '', null);
      this.saved.emit();
    } finally {
      this.saving.set(false);
    }
  }
}
