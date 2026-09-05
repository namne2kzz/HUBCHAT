import {
  ChangeDetectionStrategy, Component, computed, ElementRef, input, output, signal, viewChild,
} from '@angular/core';
import { MessageFormat } from '../../models/message.model';

/** Payload emitted when a message is sent. Fields are reserved so attachments/reply/thread can be
 *  wired later without changing the composer↔page contract. */
export interface ComposerSubmit {
  body: string;
  format: MessageFormat;
  /** Ids of pre-uploaded attachments (file/image/video) — future. */
  attachmentIds?: string[];
  /** Quote-reply target message id — future. */
  replyToId?: string;
  /** Thread parent message id — future. */
  parentId?: string;
}

/**
 * Chat composer with basic rich formatting (Markdown).
 * - Toolbar inserts Markdown around the selection (bold/italic/strike/code/link/lists/quote).
 * - Typing ``` (or the code-block button) switches to a multi-line **code block** mode.
 * - Enter sends (Shift+Enter = newline); in code-block mode Enter = newline, Ctrl/Cmd+Enter sends.
 * Emits a {@link ComposerSubmit} (Markdown) — a stable contract so attachments/reply can be added later.
 */
@Component({
  selector: 'app-message-input',
  standalone: true,
  imports: [],
  templateUrl: './message-input.component.html',
  styleUrl: './message-input.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MessageInputComponent {
  readonly channelName = input<string>('');

  /** Emits the composed message (Markdown) when the user sends. */
  readonly send = output<ComposerSubmit>();
  /** Emits on each keystroke so the parent can broadcast a typing indicator. */
  readonly typing = output<void>();

  private readonly ta = viewChild<ElementRef<HTMLTextAreaElement>>('ta');

  /** Maximum length (characters) of a single message body. */
  protected readonly MAX_LEN = 1000;

  protected readonly body     = signal('');
  /** True when the composer is in fenced code-block mode (monospace, Enter = newline). */
  protected readonly codeMode = signal(false);
  protected readonly codeLang = signal('');
  /** True when the last input was trimmed to {@link MAX_LEN}; drives the inline limit notice. */
  protected readonly limitHit = signal(false);

  protected readonly canSend   = computed(() => this.body().trim().length > 0);
  /** Characters remaining before the limit (may be 0; never negative in practice). */
  protected readonly remaining = computed(() => this.MAX_LEN - this.body().length);

  /** Updates the body; auto-enters code-block mode when the user types ``` on an empty composer. */
  protected onInput(event: Event): void {
    const el = event.target as HTMLTextAreaElement;
    let value = el.value;
    if (!this.codeMode() && value === '```') {
      this.codeMode.set(true);
      this.body.set('');
      this.limitHit.set(false);
      return;
    }
    if (value.length > this.MAX_LEN) {
      value = value.slice(0, this.MAX_LEN);
      el.value = value; // reflect the trim in the DOM immediately (keeps caret at the end)
      this.limitHit.set(true);
    } else {
      this.limitHit.set(false);
    }
    this.body.set(value);
  }

  /** Enter sends (normal) or newline (code mode); Ctrl/Cmd+Enter always sends. Empty backspace exits code mode. */
  protected onKeydown(event: KeyboardEvent): void {
    const send = event.key === 'Enter'
      && (this.codeMode() ? (event.ctrlKey || event.metaKey) : !event.shiftKey);
    if (send) {
      event.preventDefault();
      this.submit();
      return;
    }
    if (this.codeMode() && event.key === 'Backspace' && this.body().length === 0) {
      this.codeMode.set(false);
      this.codeLang.set('');
    }
    this.typing.emit();
  }

  /** Toggles code-block mode from the toolbar. */
  protected toggleCodeBlock(): void {
    this.codeMode.update(v => !v);
    if (!this.codeMode()) this.codeLang.set('');
    setTimeout(() => this.ta()?.nativeElement.focus());
  }

  protected onCodeLang(event: Event): void {
    this.codeLang.set((event.target as HTMLInputElement).value.trim());
  }

  // ── Toolbar (Markdown) ─────────────────────────────────────────────────────
  protected bold(): void      { this.wrap('**', '**'); }
  protected italic(): void    { this.wrap('*', '*'); }
  protected strike(): void    { this.wrap('~~', '~~'); }
  protected inlineCode(): void{ this.wrap('`', '`'); }
  protected link(): void      { this.wrapLink(); }
  protected bullet(): void    { this.prefixLines('- '); }
  protected numbered(): void  { this.prefixLines('{n}. '); }
  protected quote(): void     { this.prefixLines('> '); }

  /** Sets the body, clamping to {@link MAX_LEN} and flagging the inline notice when it trims. */
  private setBodyClamped(value: string): void {
    if (value.length > this.MAX_LEN) {
      value = value.slice(0, this.MAX_LEN);
      this.limitHit.set(true);
    }
    this.body.set(value);
  }

  /** Wraps the current selection with Markdown markers, keeping the selection. */
  private wrap(before: string, after: string): void {
    const ta = this.ta()?.nativeElement;
    if (!ta) return;
    const s = ta.selectionStart, e = ta.selectionEnd, v = ta.value;
    const sel = v.slice(s, e) || 'text';
    this.setBodyClamped(v.slice(0, s) + before + sel + after + v.slice(e));
    setTimeout(() => {
      ta.focus();
      ta.setSelectionRange(s + before.length, s + before.length + sel.length);
    });
  }

  /** Inserts a Markdown link around the selection: [text](url). */
  private wrapLink(): void {
    const ta = this.ta()?.nativeElement;
    if (!ta) return;
    const s = ta.selectionStart, e = ta.selectionEnd, v = ta.value;
    const sel = v.slice(s, e) || 'text';
    const insert = `[${sel}](url)`;
    this.setBodyClamped(v.slice(0, s) + insert + v.slice(e));
    // select the "url" placeholder
    setTimeout(() => {
      ta.focus();
      const urlStart = s + sel.length + 3; // [sel](
      ta.setSelectionRange(urlStart, urlStart + 3);
    });
  }

  /** Prefixes each selected line (or the current line) with a Markdown marker. `{n}` → line number. */
  private prefixLines(prefix: string): void {
    const ta = this.ta()?.nativeElement;
    if (!ta) return;
    const s = ta.selectionStart, e = ta.selectionEnd, v = ta.value;
    const lineStart = v.lastIndexOf('\n', s - 1) + 1;
    const nlAfter = v.indexOf('\n', e);
    const lineEnd = nlAfter === -1 ? v.length : nlAfter;
    const block = v.slice(lineStart, lineEnd)
      .split('\n')
      .map((l, i) => prefix.replace('{n}', String(i + 1)) + l)
      .join('\n');
    this.setBodyClamped(v.slice(0, lineStart) + block + v.slice(lineEnd));
    setTimeout(() => ta.focus());
  }

  /** Sends the composed message (wrapping in a fenced block when in code-block mode). */
  protected submit(): void {
    const raw = this.body();
    if (!raw.trim()) return;
    const body = this.codeMode()
      ? '```' + this.codeLang() + '\n' + raw.replace(/\n+$/, '') + '\n```'
      : raw.trim();
    this.send.emit({ body, format: MessageFormat.Markdown });
    this.body.set('');
    this.codeMode.set(false);
    this.codeLang.set('');
    this.limitHit.set(false);
  }
}
