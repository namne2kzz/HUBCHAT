import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CustomStatusDialogComponent } from './custom-status-dialog.component';
import { PresenceService } from '../../services/presence.service';

/**
 * Covers the custom-status composer (CF-19): presets prefill all three fields, the emoji input keeps a
 * single character, "clear after" resolves to a sane Date (or null), and save/clear reach the service.
 */
describe('CustomStatusDialogComponent', () => {
  let fixture: ComponentFixture<CustomStatusDialogComponent>;
  let component: CustomStatusDialogComponent;
  let presence: jasmine.SpyObj<PresenceService>;

  const inner = () => component as unknown as {
    emoji: { (): string; set(v: string): void };
    text: { (): string; set(v: string): void };
    clearAfterMin: { (): number | null; set(v: number | null): void };
    presets: ReadonlyArray<{ emoji: string; text: string; clearAfterMin: number | null }>;
    applyPreset(p: { emoji: string; text: string; clearAfterMin: number | null }): void;
    onEmojiInput(e: Event): void;
    save(): Promise<void>;
    clear(): Promise<void>;
    canSave(): boolean;
  };

  function mount(): void {
    fixture = TestBed.createComponent(CustomStatusDialogComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('userId', 'me');
    fixture.detectChanges();
  }

  beforeEach(() => {
    presence = jasmine.createSpyObj<PresenceService>('PresenceService', ['getCustomStatus', 'setCustomStatus']);
    presence.getCustomStatus.and.returnValue(null);
    presence.setCustomStatus.and.resolveTo(undefined);

    TestBed.configureTestingModule({
      imports: [CustomStatusDialogComponent],
      providers: [{ provide: PresenceService, useValue: presence }],
    });
  });

  it('prefills emoji, text and clear-after from a preset', () => {
    mount();
    const p = inner().presets[0];
    inner().applyPreset(p);

    expect(inner().emoji()).toBe(p.emoji);
    expect(inner().text()).toBe(p.text);
    expect(inner().clearAfterMin()).toBe(p.clearAfterMin);
  });

  it('keeps only the first character from the emoji input', () => {
    mount();
    inner().onEmojiInput({ target: { value: '🍔🚌' } } as unknown as Event);
    expect([...inner().emoji()].length).toBe(1);
  });

  it('prefills from an existing status', () => {
    presence.getCustomStatus.and.returnValue({ emoji: '🌴', text: 'On holiday', expiresAt: null });
    mount();
    expect(inner().emoji()).toBe('🌴');
    expect(inner().text()).toBe('On holiday');
  });

  it('saves with a ~30-minute expiry when clear-after is 30', async () => {
    mount();
    inner().emoji.set('📅');
    inner().text.set('Heads down');
    inner().clearAfterMin.set(30);

    await inner().save();

    expect(presence.setCustomStatus).toHaveBeenCalled();
    const [emoji, text, when] = presence.setCustomStatus.calls.mostRecent().args as [string, string, Date | null];
    expect(emoji).toBe('📅');
    expect(text).toBe('Heads down');
    expect(when).toBeInstanceOf(Date);
    const deltaMin = ((when as Date).getTime() - Date.now()) / 60_000;
    expect(deltaMin).toBeGreaterThan(29);
    expect(deltaMin).toBeLessThan(31);
  });

  it("saves with a null expiry for \"don't clear\"", async () => {
    mount();
    inner().emoji.set('🏠');
    inner().text.set('Remote');
    inner().clearAfterMin.set(null);

    await inner().save();

    const [, , when] = presence.setCustomStatus.calls.mostRecent().args as [string, string, Date | null];
    expect(when).toBeNull();
  });

  it('does not save an empty status', async () => {
    mount();
    inner().emoji.set('');
    inner().text.set('');
    expect(inner().canSave()).toBeFalse();

    await inner().save();
    expect(presence.setCustomStatus).not.toHaveBeenCalled();
  });

  it('clear() tells the service to empty the status', async () => {
    mount();
    await inner().clear();
    expect(presence.setCustomStatus).toHaveBeenCalledWith('', '', null);
  });
});
