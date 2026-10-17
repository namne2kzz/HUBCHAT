import { TestBed } from '@angular/core/testing';
import { ChannelDetailPageComponent } from './channel-detail-page.component';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';

describe('ChannelDetailPageComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ChannelDetailPageComponent],
      providers: [
        provideHttpClient(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'test-id' } } } },
      ],
    }).compileComponents();
  });

  it('should create', () => {
    const f = TestBed.createComponent(ChannelDetailPageComponent);
    expect(f.componentInstance).toBeTruthy();
  });

  describe('pinned bar sync (applyPinToBar)', () => {
    interface PinInternals {
      pinnedMessages: { (): { id: string }[]; set(v: unknown[]): void };
      messages: { set(v: unknown[]): void };
      applyPinToBar(id: string, pin: boolean): void;
    }
    const make = () => {
      const f = TestBed.createComponent(ChannelDetailPageComponent);
      return f.componentInstance as unknown as PinInternals;
    };

    it('removes a message from the bar on unpin', () => {
      const c = make();
      c.pinnedMessages.set([{ id: 'a' }, { id: 'b' }]);
      c.applyPinToBar('a', false);
      expect(c.pinnedMessages().map(m => m.id)).toEqual(['b']);
    });

    it('prepends a newly pinned message (newest first) from the loaded messages', () => {
      const c = make();
      c.messages.set([{ id: 'x', authorId: 'u', body: 'hi' }]);
      c.pinnedMessages.set([{ id: 'b' }]);
      c.applyPinToBar('x', true);
      expect(c.pinnedMessages().map(m => m.id)).toEqual(['x', 'b']);
    });

    it('does not double-add an already-pinned message', () => {
      const c = make();
      c.messages.set([{ id: 'x', authorId: 'u', body: 'hi' }]);
      c.pinnedMessages.set([{ id: 'x' }]);
      c.applyPinToBar('x', true);
      expect(c.pinnedMessages().map(m => m.id)).toEqual(['x']);
    });
  });

  describe('targetLabel (forward list)', () => {
    // ChannelType: Public=0, Private=1, Dm=2, GroupDm=3 (mirrors channel.model).
    const label = (ch: unknown) => {
      const f = TestBed.createComponent(ChannelDetailPageComponent);
      return (f.componentInstance as unknown as { targetLabel(c: unknown): string }).targetLabel(ch);
    };

    it('prefixes a channel with #', () => {
      expect(label({ type: 0, name: 'general', otherUserId: null })).toBe('#general');
    });

    it('shows the partner name for a DM, never the dm-… slug', () => {
      const f = TestBed.createComponent(ChannelDetailPageComponent);
      const c = f.componentInstance as unknown as {
        authors: { set(v: Record<string, { id: string; name: string }>): void };
        targetLabel(ch: unknown): string;
      };
      c.authors.set({ u2: { id: 'u2', name: 'Dev User' } });
      expect(c.targetLabel({ type: 2, name: 'dm-000-000', otherUserId: 'u2' })).toBe('Dev User');
    });

    it('falls back to "Direct message" when the partner is unknown', () => {
      expect(label({ type: 2, name: 'dm-000-000', otherUserId: 'u9' })).toBe('Direct message');
    });
  });
});
