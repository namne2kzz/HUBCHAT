import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { CreateChannelDialogComponent } from './create-channel-dialog.component';
import { ChannelService } from '../../services/channel.service';
import { ChannelDto, ChannelType, CreateChannelRequest } from '../../models/channel.model';

/**
 * Covers the create-channel dialog: what it validates before calling the API, what it sends, and how
 * it behaves while the request is in flight.
 *
 * The loading flag matters more than it looks — it is what stops a second click firing a second
 * create while the first is still running, which would leave two identically named channels.
 */
describe('CreateChannelDialogComponent', () => {
  let fixture: ComponentFixture<CreateChannelDialogComponent>;
  let component: CreateChannelDialogComponent;
  let channels: jasmine.SpyObj<ChannelService>;
  let created: ChannelDto[];
  let closed: number;

  const WORKSPACE = 'ws-1';

  const channel = (over: Partial<ChannelDto> = {}): ChannelDto => ({
    id: 'c1', workspaceId: WORKSPACE, name: 'General', slug: 'general', type: ChannelType.Public,
    topic: '', isPrivate: false, isArchived: false, memberCount: 1,
    createdAt: new Date().toISOString(), linkType: null, linkExternalKey: null, linkUrl: '',
    isMember: true, otherUserId: null, myRole: null, ...over,
  } as ChannelDto);

  /** Reaches the protected members the template binds to. */
  const inner = () => component as unknown as {
    name: { (): string; set(v: string): void };
    topic: { (): string; set(v: string): void };
    type: { (): ChannelType; set(v: ChannelType): void };
    loading: () => boolean;
    error: () => string | null;
    submit(): void;
    setType(t: ChannelType): void;
    onBackdrop(e: MouseEvent): void;
  };

  /** The request body of the most recent create call. */
  const lastRequest = () => channels.create.calls.mostRecent().args[0] as CreateChannelRequest;

  beforeEach(async () => {
    channels = jasmine.createSpyObj<ChannelService>('ChannelService', ['create']);
    channels.create.and.returnValue(of(channel()));

    await TestBed.configureTestingModule({
      imports: [CreateChannelDialogComponent],
      providers: [{ provide: ChannelService, useValue: channels }],
    }).compileComponents();

    fixture = TestBed.createComponent(CreateChannelDialogComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('workspaceId', WORKSPACE);

    created = [];
    closed = 0;
    component.created.subscribe(c => created.push(c));
    component.close.subscribe(() => closed++);

    fixture.detectChanges();
  });

  it('should create', () => expect(component).toBeTruthy());

  describe('validation', () => {
    it('refuses an empty name without calling the API', () => {
      inner().submit();

      expect(channels.create).not.toHaveBeenCalled();
      expect(inner().error()).toContain('required');
    });

    it('refuses a whitespace-only name', () => {
      inner().name.set('   ');

      inner().submit();

      expect(channels.create).not.toHaveBeenCalled();
    });

    it('refuses a name over 100 characters', () => {
      // Matches the server's CreateChannelValidator, so the person sees the problem before a round
      // trip rather than as a generic 400.
      inner().name.set('x'.repeat(101));

      inner().submit();

      expect(channels.create).not.toHaveBeenCalled();
      expect(inner().error()).toContain('100');
    });

    it('accepts a name of exactly 100 characters', () => {
      inner().name.set('x'.repeat(100));

      inner().submit();

      expect(channels.create).toHaveBeenCalled();
    });

    it('clears a previous error on a valid submit', () => {
      inner().submit();
      expect(inner().error()).not.toBeNull();

      inner().name.set('General');
      inner().submit();

      expect(inner().error()).toBeNull();
    });
  });

  describe('what it sends', () => {
    it('sends the trimmed name and the workspace', () => {
      inner().name.set('  General  ');

      inner().submit();

      expect(lastRequest().name).toBe('General');
      expect(lastRequest().workspaceId).toBe(WORKSPACE);
    });

    it('defaults to a public channel', () => {
      inner().name.set('General');

      inner().submit();

      expect(lastRequest().type).toBe(ChannelType.Public);
    });

    it('sends the chosen type', () => {
      inner().name.set('Secret');
      inner().setType(ChannelType.Private);

      inner().submit();

      expect(lastRequest().type).toBe(ChannelType.Private);
    });

    it('omits an empty topic rather than sending a blank string', () => {
      inner().name.set('General');
      inner().topic.set('   ');

      inner().submit();

      expect(lastRequest().topic).toBeUndefined();
    });

    it('sends a trimmed topic when one is given', () => {
      inner().name.set('General');
      inner().topic.set('  All things eng  ');

      inner().submit();

      expect(lastRequest().topic).toBe('All things eng');
    });
  });

  describe('on success', () => {
    it('emits the created channel', () => {
      const result = channel({ id: 'new-channel' });
      channels.create.and.returnValue(of(result));
      inner().name.set('General');

      inner().submit();

      // The shell adds it to the sidebar from this event instead of re-listing the workspace.
      expect(created).toEqual([result]);
    });

    it('stops loading', () => {
      inner().name.set('General');

      inner().submit();

      expect(inner().loading()).toBeFalse();
    });
  });

  describe('on failure', () => {
    beforeEach(() => channels.create.and.returnValue(throwError(() => new Error('boom'))));

    it('shows an error message', () => {
      inner().name.set('General');

      inner().submit();

      expect(inner().error()).toBeTruthy();
    });

    it('stops loading so the person can retry', () => {
      // A stuck loading flag would leave the submit button disabled forever with no way out but a
      // reload.
      inner().name.set('General');

      inner().submit();

      expect(inner().loading()).toBeFalse();
    });

    it('does not emit a created channel', () => {
      inner().name.set('General');

      inner().submit();

      expect(created).toEqual([]);
    });
  });

  describe('closing', () => {
    it('closes when the backdrop itself is clicked', () => {
      const backdrop = document.createElement('div');
      backdrop.classList.add('dialog-backdrop');

      inner().onBackdrop({ target: backdrop } as unknown as MouseEvent);

      expect(closed).toBe(1);
    });

    it('stays open when a click lands inside the dialog', () => {
      // Clicking a field or a button bubbles up to the backdrop handler; only a click on the
      // backdrop element itself means "dismiss".
      const field = document.createElement('input');

      inner().onBackdrop({ target: field } as unknown as MouseEvent);

      expect(closed).toBe(0);
    });
  });
});
