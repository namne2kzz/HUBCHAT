import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ComposerSubmit, MessageInputComponent } from './message-input.component';
import { MessageFormat } from '../../models/message.model';

/**
 * Covers the message composer.
 *
 * The key handling is the substance here: Enter sends in normal mode but inserts a newline in code
 * mode, and Ctrl/Cmd+Enter sends in either. Get one cell of that matrix wrong and either a
 * half-written message goes out, or the send key stops working and nothing says why.
 */
describe('MessageInputComponent', () => {
  let fixture: ComponentFixture<MessageInputComponent>;
  let component: MessageInputComponent;
  let sent: ComposerSubmit[];

  /** Reaches protected members the template drives. */
  const inner = () => component as unknown as {
    body: { (): string; set(v: string): void };
    codeMode: { (): boolean; set(v: boolean): void };
    codeLang: { (): string; set(v: string): void };
    limitHit: () => boolean;
    canSend: () => boolean;
    remaining: () => number;
    MAX_LEN: number;
    submit(): void;
    onInput(e: Event): void;
    onKeydown(e: KeyboardEvent): void;
    toggleCodeBlock(): void;
    bold(): void;
    italic(): void;
    bullet(): void;
    quote(): void;
    numbered(): void;
  };

  /** Types into the real textarea so onInput sees a genuine event target. */
  const type = (value: string) => {
    const ta = fixture.nativeElement.querySelector('textarea') as HTMLTextAreaElement;
    ta.value = value;
    inner().onInput({ target: ta } as unknown as Event);
    fixture.detectChanges();
  };

  const press = (key: string, modifiers: Partial<KeyboardEventInit> = {}) => {
    // cancelable is required for preventDefault to register on a synthetic event; without it
    // defaultPrevented stays false however the handler behaves.
    const event = new KeyboardEvent('keydown', { key, cancelable: true, ...modifiers });
    inner().onKeydown(event);
    fixture.detectChanges();
    return event;
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [MessageInputComponent] }).compileComponents();

    fixture = TestBed.createComponent(MessageInputComponent);
    component = fixture.componentInstance;
    sent = [];
    component.send.subscribe(v => sent.push(v));
    fixture.detectChanges();
  });

  it('should create', () => expect(component).toBeTruthy());

  describe('sending', () => {
    it('emits the typed body', () => {
      type('hello world');

      inner().submit();

      expect(sent.length).toBe(1);
      expect(sent[0].body).toBe('hello world');
      expect(sent[0].format).toBe(MessageFormat.Markdown);
    });

    it('does not emit for an empty body', () => {
      inner().submit();

      expect(sent.length).toBe(0);
    });

    it('does not emit for a whitespace-only body', () => {
      // Otherwise pressing Enter on a stray space posts a blank message the server then rejects.
      type('   \n  ');

      inner().submit();

      expect(sent.length).toBe(0);
    });

    it('trims the body before sending', () => {
      type('  padded  ');

      inner().submit();

      expect(sent[0].body).toBe('padded');
    });

    it('clears the composer after sending', () => {
      type('hello');

      inner().submit();

      expect(inner().body()).toBe('');
      expect(inner().canSend()).toBeFalse();
    });

    it('reports whether there is anything to send', () => {
      expect(inner().canSend()).toBeFalse();

      type('x');
      expect(inner().canSend()).toBeTrue();

      type('   ');
      expect(inner().canSend()).toBeFalse();
    });
  });

  describe('key handling in normal mode', () => {
    it('sends on Enter', () => {
      type('hello');

      const event = press('Enter');

      expect(sent.length).toBe(1);
      // preventDefault stops the newline being inserted as well as sending.
      expect(event.defaultPrevented).toBeTrue();
    });

    it('does not send on Shift+Enter', () => {
      type('hello');

      const event = press('Enter', { shiftKey: true });

      expect(sent.length).toBe(0);
      expect(event.defaultPrevented).toBeFalse();
    });

    it('sends on Ctrl+Enter as well', () => {
      type('hello');

      press('Enter', { ctrlKey: true });

      expect(sent.length).toBe(1);
    });

    it('signals typing on an ordinary keystroke', () => {
      let typingCount = 0;
      component.typing.subscribe(() => typingCount++);

      press('a');

      expect(typingCount).toBe(1);
    });

    it('does not signal typing on the send keystroke', () => {
      // The message is on its way, so broadcasting "still typing" would leave the indicator lit
      // after the message appeared.
      let typingCount = 0;
      component.typing.subscribe(() => typingCount++);
      type('hello');

      press('Enter');

      expect(typingCount).toBe(0);
    });
  });

  describe('code-block mode', () => {
    it('switches on when the composer contains only a fence', () => {
      type('```');

      expect(inner().codeMode()).toBeTrue();
      // The fence itself is consumed — it becomes the wrapper, not part of the body.
      expect(inner().body()).toBe('');
    });

    it('does not switch on for a fence mid-message', () => {
      type('see ```');

      expect(inner().codeMode()).toBeFalse();
    });

    it('inserts a newline on plain Enter instead of sending', () => {
      type('```');
      type('const x = 1;');

      const event = press('Enter');

      // Inverted from normal mode: a code block is multi-line by nature, so Enter has to be free.
      expect(sent.length).toBe(0);
      expect(event.defaultPrevented).toBeFalse();
    });

    it('sends on Ctrl+Enter', () => {
      type('```');
      type('const x = 1;');

      press('Enter', { ctrlKey: true });

      expect(sent.length).toBe(1);
    });

    it('sends on Cmd+Enter for mac keyboards', () => {
      type('```');
      type('const x = 1;');

      press('Enter', { metaKey: true });

      expect(sent.length).toBe(1);
    });

    it('wraps the body in a fenced block when sending', () => {
      type('```');
      type('const x = 1;');

      inner().submit();

      expect(sent[0].body).toBe('```\nconst x = 1;\n```');
    });

    it('includes the language hint when one is set', () => {
      type('```');
      inner().codeLang.set('ts');
      type('const x = 1;');

      inner().submit();

      expect(sent[0].body).toBe('```ts\nconst x = 1;\n```');
    });

    it('leaves code mode after sending', () => {
      type('```');
      type('code');

      inner().submit();

      // Otherwise the next ordinary message would silently go out as a code block.
      expect(inner().codeMode()).toBeFalse();
      expect(inner().codeLang()).toBe('');
    });

    it('exits on backspace in an empty code block', () => {
      type('```');

      press('Backspace');

      // The only way back out with the keyboard, since the fence that opened it was consumed.
      expect(inner().codeMode()).toBeFalse();
    });

    it('does not exit on backspace when the block has content', () => {
      type('```');
      type('code');

      press('Backspace');

      expect(inner().codeMode()).toBeTrue();
    });

    it('toggles from the toolbar', () => {
      inner().toggleCodeBlock();
      expect(inner().codeMode()).toBeTrue();

      inner().toggleCodeBlock();
      expect(inner().codeMode()).toBeFalse();
    });
  });

  describe('length limit', () => {
    it('clamps input to the maximum length', () => {
      type('x'.repeat(inner().MAX_LEN + 50));

      expect(inner().body().length).toBe(inner().MAX_LEN);
    });

    it('raises the limit notice when it trims', () => {
      type('x'.repeat(inner().MAX_LEN + 1));

      // Silently dropping characters would look like the keyboard stopped working.
      expect(inner().limitHit()).toBeTrue();
    });

    it('clears the notice once back under the limit', () => {
      type('x'.repeat(inner().MAX_LEN + 1));
      type('short again');

      expect(inner().limitHit()).toBeFalse();
    });

    it('accepts a body of exactly the maximum length', () => {
      type('x'.repeat(inner().MAX_LEN));

      expect(inner().body().length).toBe(inner().MAX_LEN);
      expect(inner().limitHit()).toBeFalse();
    });

    it('counts down the remaining characters', () => {
      type('hello');

      expect(inner().remaining()).toBe(inner().MAX_LEN - 5);
    });

    it('clears the notice after sending', () => {
      type('x'.repeat(inner().MAX_LEN + 1));

      inner().submit();

      expect(inner().limitHit()).toBeFalse();
    });
  });

  describe('markdown toolbar', () => {
    /** Places a selection in the textarea so the wrapping helpers have something to work on. */
    const select = (value: string, start: number, end: number) => {
      const ta = fixture.nativeElement.querySelector('textarea') as HTMLTextAreaElement;
      ta.value = value;
      ta.setSelectionRange(start, end);
      return ta;
    };

    it('wraps the selection in bold markers', () => {
      select('make this loud', 5, 9);

      inner().bold();

      expect(inner().body()).toBe('make **this** loud');
    });

    it('wraps the selection in italic markers', () => {
      select('make this soft', 5, 9);

      inner().italic();

      expect(inner().body()).toBe('make *this* soft');
    });

    it('inserts a placeholder when nothing is selected', () => {
      select('', 0, 0);

      inner().bold();

      // Gives the person something to type over rather than two empty markers with a caret between.
      expect(inner().body()).toBe('**text**');
    });

    it('prefixes a line for a bullet list', () => {
      select('an item', 0, 0);

      inner().bullet();

      expect(inner().body()).toBe('- an item');
    });

    it('prefixes a line for a quote', () => {
      select('quoted', 0, 0);

      inner().quote();

      expect(inner().body()).toBe('> quoted');
    });

    it('numbers each selected line in order', () => {
      const value = 'first\nsecond\nthird';
      select(value, 0, value.length);

      inner().numbered();

      // The {n} placeholder has to increment per line — a fixed "1." would produce a list that all
      // renders as 1.
      expect(inner().body()).toBe('1. first\n2. second\n3. third');
    });

    it('bullets every selected line', () => {
      const value = 'one\ntwo';
      select(value, 0, value.length);

      inner().bullet();

      expect(inner().body()).toBe('- one\n- two');
    });
  });
});
