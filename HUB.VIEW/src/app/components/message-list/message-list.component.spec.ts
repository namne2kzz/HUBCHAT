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
});
