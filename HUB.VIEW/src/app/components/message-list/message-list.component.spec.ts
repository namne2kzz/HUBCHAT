import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MessageListComponent } from './message-list.component';
import { MessageDto } from '../../models/message.model';
import { DirectoryUser } from '../../models/directory.model';

/**
 * Covers the message list's grouping and rendering logic.
 *
 * Almost everything here feeds one computed: how a flat list of messages becomes rows with headers,
 * day separators and ownership. Getting it wrong is not a crash — it is a transcript that reads
 * wrongly, with somebody's message attributed to the person above them, or every message wearing its
 * own avatar because grouping silently stopped working.
 */
describe('MessageListComponent', () => {
  let fixture: ComponentFixture<MessageListComponent>;
  let component: MessageListComponent;

  const ME = 'me';
  const ALICE = 'alice';
  const BOB = 'bob';

  const BASE = new Date('2026-09-27T12:00:00Z');

  /** Builds a message at an offset in minutes from the base time. */
  const msg = (authorId: string, minutes: number, over: Partial<MessageDto> = {}): MessageDto => ({
    id: `m-${authorId}-${minutes}`,
    channelId: 'c1',
    parentId: null,
    replyToId: null,
    forwardedFromId: null,
    authorId,
    body: 'hello',
    format: 1,
    mentions: [],
    reactions: [],
    attachments: [],
    editedAt: null,
    createdAt: new Date(BASE.getTime() + minutes * 60_000).toISOString(),
    ...over,
  } as MessageDto);

  const user = (id: string, name: string): DirectoryUser => ({
    id, name, email: `${id}@x.com`, avatarClass: 'bg-sky-600', isGlobalAdmin: false, isDeleted: false,
  });

  /** Renders the component with a message list and returns the computed rows. */
  const rowsFor = (messages: MessageDto[], currentUserId = ME) => {
    fixture.componentRef.setInput('messages', messages);
    fixture.componentRef.setInput('currentUserId', currentUserId);
    fixture.detectChanges();
    return component.rows();
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [MessageListComponent] }).compileComponents();

    fixture = TestBed.createComponent(MessageListComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('messages', []);
    fixture.detectChanges();
  });

  it('should create', () => expect(component).toBeTruthy());

  describe('ownership', () => {
    it('marks the current user\'s messages as their own', () => {
      const rows = rowsFor([msg(ME, 0), msg(ALICE, 1)]);

      // Drives which side of the conversation a bubble appears on.
      expect(rows[0].isOwn).toBeTrue();
      expect(rows[1].isOwn).toBeFalse();
    });

    it('treats nobody as the owner when there is no current user', () => {
      const rows = rowsFor([msg(ME, 0)], '');

      expect(rows[0].isOwn).toBeFalse();
    });
  });

  describe('grouping consecutive messages', () => {
    it('shows a header on the first message', () => {
      expect(rowsFor([msg(ALICE, 0)])[0].showHeader).toBeTrue();
    });

    it('hides the header on a quick follow-up from the same author', () => {
      const rows = rowsFor([msg(ALICE, 0), msg(ALICE, 1)]);

      // The visual grouping that stops a burst of messages repeating the same name and avatar.
      expect(rows[1].showHeader).toBeFalse();
    });

    it('shows a header when the author changes', () => {
      const rows = rowsFor([msg(ALICE, 0), msg(BOB, 1)]);

      expect(rows[1].showHeader).toBeTrue();
    });

    it('shows a header again after a gap of more than five minutes', () => {
      const rows = rowsFor([msg(ALICE, 0), msg(ALICE, 6)]);

      // A long pause is a new thought, so it gets its own header even from the same person.
      expect(rows[1].showHeader).toBeTrue();
    });

    it('keeps grouping at exactly five minutes', () => {
      // The boundary is `> 5 minutes`, so five minutes flat still groups.
      const rows = rowsFor([msg(ALICE, 0), msg(ALICE, 5)]);

      expect(rows[1].showHeader).toBeFalse();
    });

    it('marks a message as continued when the same author follows soon after', () => {
      const rows = rowsFor([msg(ALICE, 0), msg(ALICE, 1)]);

      // Suppresses the bubble's bottom corner so a run reads as one block.
      expect(rows[0].isContinued).toBeTrue();
      expect(rows[1].isContinued).toBeFalse();
    });

    it('does not mark a message as continued when the next author differs', () => {
      const rows = rowsFor([msg(ALICE, 0), msg(BOB, 1)]);

      expect(rows[0].isContinued).toBeFalse();
    });

    it('does not mark the last message as continued', () => {
      const rows = rowsFor([msg(ALICE, 0)]);

      expect(rows[0].isContinued).toBeFalse();
    });
  });

  describe('day separators', () => {
    it('labels the first message of the list', () => {
      const rows = rowsFor([msg(ALICE, 0)]);

      expect(rows[0].dateLabel).not.toBeNull();
    });

    it('does not repeat the label within the same day', () => {
      const rows = rowsFor([msg(ALICE, 0), msg(ALICE, 30)]);

      expect(rows[1].dateLabel).toBeNull();
    });

    it('labels the first message of a new day', () => {
      const rows = rowsFor([msg(ALICE, 0), msg(ALICE, 60 * 25)]);

      expect(rows[1].dateLabel).not.toBeNull();
    });

    it('says "Today" for a message sent today', () => {
      const now: MessageDto = { ...msg(ALICE, 0), createdAt: new Date().toISOString() };

      expect(rowsFor([now])[0].dateLabel).toBe('Today');
    });

    it('says "Yesterday" for a message sent yesterday', () => {
      const yesterday = new Date();
      yesterday.setDate(yesterday.getDate() - 1);
      const then: MessageDto = { ...msg(ALICE, 0), createdAt: yesterday.toISOString() };

      expect(rowsFor([then])[0].dateLabel).toBe('Yesterday');
    });

    it('uses a formatted date for anything older', () => {
      const old: MessageDto = { ...msg(ALICE, 0), createdAt: new Date('2020-01-15T12:00:00Z').toISOString() };
      const label = rowsFor([old])[0].dateLabel;

      expect(label).not.toBe('Today');
      expect(label).not.toBe('Yesterday');
      expect(label).toBeTruthy();
    });
  });

  describe('body rendering', () => {
    it('renders the body as markdown', () => {
      const rows = rowsFor([msg(ALICE, 0, { body: '**bold**' })]);

      expect(rows[0].html).toContain('<strong>bold</strong>');
    });

    it('escapes html in the body', () => {
      // The rendered html is bound with [innerHTML], so this is the list's half of the XSS boundary.
      const rows = rowsFor([msg(ALICE, 0, { body: '<script>alert(1)</script>' })]);

      expect(rows[0].html).not.toContain('<script>');
    });
  });

  describe('author display', () => {
    it('uses the resolved directory name', () => {
      fixture.componentRef.setInput('authors', { [ALICE]: user(ALICE, 'Alice Smith') });
      fixture.detectChanges();

      expect(component.authorName(ALICE)).toBe('Alice Smith');
    });

    it('falls back to a short id when the author is unresolved', () => {
      // A message from somebody not in the directory map still has to render with something.
      expect(component.authorName('0123456789abcdef')).toBe('User 01234567');
    });

    it('uses the first letter of the name as the avatar initial', () => {
      fixture.componentRef.setInput('authors', { [ALICE]: user(ALICE, 'alice') });
      fixture.detectChanges();

      expect(component.authorInitial(ALICE)).toBe('A');
    });

    it('falls back to a question mark for an unknown author initial', () => {
      expect(component.authorInitial('nobody')).toBe('?');
    });
  });

  describe('empty state', () => {
    it('produces no rows for an empty list', () => {
      expect(rowsFor([])).toEqual([]);
    });
  });
  describe('@mentions', () => {
    beforeEach(() => {
      fixture.componentRef.setInput('authors', { [ME]: user(ME, 'Me Myself'), [ALICE]: user(ALICE, 'Alice') });
    });

    it('highlights mentioned names using the resolved directory names', () => {
      const [row] = rowsFor([msg(BOB, 0, { body: 'ping @Alice', mentions: [ALICE] })]);
      expect(row.html).toBe('ping <span class="md-mention">@Alice</span>');
      expect(row.mentionsMe).toBeFalse();
    });

    it('flags a message from someone else that mentions me', () => {
      const [row] = rowsFor([msg(BOB, 0, { body: 'hey @Me Myself', mentions: [ME] })]);

      expect(row.mentionsMe).toBeTrue();
      expect(row.html).toContain('md-mention-me');
      expect(fixture.nativeElement.querySelector('.bubble-mentions-me')).not.toBeNull();
    });

    it('does not flag my own message that mentions me', () => {
      const [row] = rowsFor([msg(ME, 0, { body: '@Me Myself reminder', mentions: [ME] })]);
      expect(row.mentionsMe).toBeFalse();
    });

    it('leaves a mention plain until the name is resolved', () => {
      // No directory entry for BOB yet — no guessing a name to highlight.
      const [row] = rowsFor([msg(ALICE, 0, { body: '@Bob hi', mentions: [BOB] })]);
      expect(row.html).toBe('@Bob hi');
    });
  });

  describe('reactions', () => {
    const toggles: { messageId: string; emoji: string }[] = [];
    const chips = () => Array.from(fixture.nativeElement.querySelectorAll('.reaction-chip')) as HTMLButtonElement[];

    beforeEach(() => {
      toggles.length = 0;
      component.toggleReaction.subscribe(t => toggles.push(t));
    });

    it('shows one chip per emoji with its count and marks mine', () => {
      fixture.componentRef.setInput('canReact', true);
      rowsFor([msg(BOB, 0, { reactions: [
        { emoji: '👍', userId: ALICE }, { emoji: '👍', userId: ME }, { emoji: '🎉', userId: BOB },
      ] })]);

      expect(chips().map(c => c.textContent?.replace(/\s+/g, ''))).toEqual(['👍2', '🎉1']);
      expect(chips()[0].classList).toContain('reaction-chip-mine');
      expect(chips()[1].classList).not.toContain('reaction-chip-mine');
    });

    it('clicking a chip asks to toggle that emoji', () => {
      fixture.componentRef.setInput('canReact', true);
      rowsFor([msg(BOB, 0, { reactions: [{ emoji: '👍', userId: ALICE }] })]);

      chips()[0].click();

      expect(toggles).toEqual([{ messageId: 'm-bob-0', emoji: '👍' }]);
    });

    it('the picker offers quick emoji and emits the one picked', () => {
      fixture.componentRef.setInput('canReact', true);
      rowsFor([msg(BOB, 0)]);

      (fixture.nativeElement.querySelector('.reaction-add') as HTMLButtonElement).click();
      fixture.detectChanges();
      const picks = fixture.nativeElement.querySelectorAll('.reaction-pick') as NodeListOf<HTMLButtonElement>;
      expect(picks.length).toBeGreaterThan(0);

      picks[0].click();
      fixture.detectChanges();

      expect(toggles).toEqual([{ messageId: 'm-bob-0', emoji: '👍' }]);
      expect(fixture.nativeElement.querySelector('.reaction-picker')).toBeNull();
    });

    it('a non-member sees reactions but cannot click them or add one', () => {
      fixture.componentRef.setInput('canReact', false);
      rowsFor([msg(BOB, 0, { reactions: [{ emoji: '👍', userId: ALICE }] })]);

      expect(chips()[0].disabled).toBeTrue();
      expect(fixture.nativeElement.querySelector('.reaction-add')).toBeNull();
    });
  });

  describe('edit / delete', () => {
    const fresh = (over = {}) => ({ ...msg(ME, 0, over), createdAt: new Date().toISOString() });
    const edits: { messageId: string; body: string }[] = [];
    const deletes: string[] = [];
    const actionBtns = () => Array.from(fixture.nativeElement.querySelectorAll('.msg-action')) as HTMLButtonElement[];

    beforeEach(() => {
      edits.length = 0; deletes.length = 0;
      fixture.componentRef.setInput('currentUserId', ME);
      component.editMessage.subscribe(e => edits.push(e));
      component.deleteMessage.subscribe(id => deletes.push(id));
    });

    it('offers edit + delete on my own recent message', () => {
      fixture.componentRef.setInput('messages', [fresh()]);
      fixture.detectChanges();
      expect(actionBtns().length).toBe(2);
    });

    it('offers nothing on someone else\'s message when not a moderator', () => {
      fixture.componentRef.setInput('messages', [{ ...msg(ALICE, 0), createdAt: new Date().toISOString() }]);
      fixture.componentRef.setInput('currentUserId', ME);
      fixture.detectChanges();
      expect(actionBtns().length).toBe(0);
    });

    it('lets a moderator delete (but not edit) someone else\'s message', () => {
      fixture.componentRef.setInput('messages', [{ ...msg(ALICE, 0), createdAt: new Date().toISOString() }]);
      fixture.componentRef.setInput('currentUserId', ME);
      fixture.componentRef.setInput('canModerate', true);
      fixture.detectChanges();
      const btns = actionBtns();
      expect(btns.length).toBe(1);
      expect(btns[0].classList).toContain('msg-action-danger');
    });

    it('hides edit once the message is older than the window', () => {
      // msg(ME, 0) uses BASE (2026-09-27) — far past the 24h window.
      fixture.componentRef.setInput('messages', [msg(ME, 0)]);
      fixture.detectChanges();
      expect(actionBtns().length).toBe(0);
    });

    it('emits an edit with the changed body', () => {
      fixture.componentRef.setInput('messages', [fresh({ body: 'original' })]);
      fixture.detectChanges();
      actionBtns()[0].click();                         // edit
      fixture.detectChanges();
      const ta = fixture.nativeElement.querySelector('.msg-edit-input') as HTMLTextAreaElement;
      ta.value = 'original fixed';
      ta.dispatchEvent(new Event('input'));
      (fixture.nativeElement.querySelector('.msg-edit-save') as HTMLButtonElement).click();

      expect(edits).toEqual([{ messageId: 'm-me-0', body: 'original fixed' }]);
    });

    it('does not emit an edit when the body is unchanged', () => {
      fixture.componentRef.setInput('messages', [fresh({ body: 'same' })]);
      fixture.detectChanges();
      actionBtns()[0].click();
      fixture.detectChanges();
      (fixture.nativeElement.querySelector('.msg-edit-save') as HTMLButtonElement).click();
      expect(edits.length).toBe(0);
    });

    it('confirms before emitting a delete', () => {
      fixture.componentRef.setInput('messages', [fresh()]);
      fixture.detectChanges();
      spyOn(window, 'confirm').and.returnValue(true);

      actionBtns()[1].click();                         // delete

      expect(deletes).toEqual(['m-me-0']);
    });

    it('renders a tombstone for a deleted message and no actions', () => {
      fixture.componentRef.setInput('messages', [fresh({ isDeleted: true, body: '' })]);
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector('.bubble-deleted')).not.toBeNull();
      expect(actionBtns().length).toBe(0);
    });
  });
});
