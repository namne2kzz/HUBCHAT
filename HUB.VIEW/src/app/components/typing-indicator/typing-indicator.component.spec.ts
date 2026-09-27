import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Subject, of } from 'rxjs';
import { TypingIndicatorComponent } from './typing-indicator.component';
import { RealtimeService } from '../../services/realtime.service';
import { DirectoryService } from '../../services/directory.service';
import { DirectoryUser } from '../../models/directory.model';
import { TypingEvent } from '../../models/message.model';

describe('TypingIndicatorComponent', () => {
  let fixture: ComponentFixture<TypingIndicatorComponent>;
  let typingStarted$: Subject<TypingEvent>;
  let typingStopped$: Subject<TypingEvent>;
  let directory: jasmine.SpyObj<DirectoryService>;

  const CHANNEL = 'ch-1';
  const ME = 'me';

  /** Builds a directory profile; only id and name matter to this component. */
  const user = (id: string, name: string): DirectoryUser => ({
    id, name, email: `${id}@x.com`, avatarClass: 'bg-sky-600', isGlobalAdmin: false, isDeleted: false,
  });

  beforeEach(async () => {
    typingStarted$ = new Subject<TypingEvent>();
    typingStopped$ = new Subject<TypingEvent>();

    // The component resolves display names it does not already have through the directory, so this
    // has to be provided even for a test that never types — otherwise constructing it pulls in the
    // real DirectoryService and its HttpClient.
    directory = jasmine.createSpyObj<DirectoryService>('DirectoryService', ['getUser']);
    directory.getUser.and.returnValue(of(user('u1', 'Resolved Name')));

    await TestBed.configureTestingModule({
      imports: [TypingIndicatorComponent],
      providers: [
        { provide: RealtimeService, useValue: { typingStarted$, typingStopped$, presenceChanged$: new Subject() } },
        { provide: DirectoryService, useValue: directory },
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(TypingIndicatorComponent);
    fixture.componentRef.setInput('channelId', CHANNEL);
    fixture.componentRef.setInput('currentUserId', ME);
    fixture.detectChanges();
  });

  /** Reads the component's computed indicator text. */
  const text = () => fixture.componentInstance.typingText();

  it('should create', () => expect(fixture.componentInstance).toBeTruthy());

  it('shows nothing when nobody is typing', () => {
    expect(text()).toBeNull();
  });

  it('names a single typist', () => {
    fixture.componentRef.setInput('authors', { u1: user('u1', 'Alice') });
    typingStarted$.next({ channelId: CHANNEL, userId: 'u1' });

    expect(text()).toBe('Alice is typing…');
  });

  it('names two typists', () => {
    fixture.componentRef.setInput('authors', {
      u1: user('u1', 'Alice'),
      u2: user('u2', 'Bob'),
    });
    typingStarted$.next({ channelId: CHANNEL, userId: 'u1' });
    typingStarted$.next({ channelId: CHANNEL, userId: 'u2' });

    expect(text()).toBe('Alice and Bob are typing…');
  });

  it('collapses three or more typists', () => {
    ['u1', 'u2', 'u3'].forEach(userId => typingStarted$.next({ channelId: CHANNEL, userId }));

    expect(text()).toBe('Several people are typing…');
  });

  it('ignores the current user typing', () => {
    // Otherwise the person typing sees "you are typing…" about themselves.
    typingStarted$.next({ channelId: CHANNEL, userId: ME });

    expect(text()).toBeNull();
  });

  it('ignores events from another channel', () => {
    // One indicator per open channel subscribes to the same stream, so the filter is what stops a
    // message in channel B lighting up channel A.
    typingStarted$.next({ channelId: 'other-channel', userId: 'u1' });

    expect(text()).toBeNull();
  });

  it('does not list the same typist twice', () => {
    // Keystrokes arrive repeatedly and each one re-announces typing, so without the guard a single
    // person would fill the indicator and tip it into "Several people are typing…".
    typingStarted$.next({ channelId: CHANNEL, userId: 'u1' });
    typingStarted$.next({ channelId: CHANNEL, userId: 'u1' });

    expect(text()).toBe('Resolved Name is typing…');
  });

  it('clears a typist on typingStopped', () => {
    typingStarted$.next({ channelId: CHANNEL, userId: 'u1' });
    expect(text()).not.toBeNull();

    typingStopped$.next({ channelId: CHANNEL, userId: 'u1' });

    expect(text()).toBeNull();
  });

  it('resolves an unknown typist name through the directory', () => {
    // Somebody who has not posted in this channel yet is not in the authors map, so the component
    // falls back to a short id and then asks the directory for the real name.
    typingStarted$.next({ channelId: CHANNEL, userId: 'u1' });

    expect(directory.getUser).toHaveBeenCalledWith('u1');
    expect(text()).toBe('Resolved Name is typing…');
  });

  it('does not ask the directory for a name it already has', () => {
    fixture.componentRef.setInput('authors', { u1: user('u1', 'Alice') });

    typingStarted$.next({ channelId: CHANNEL, userId: 'u1' });

    expect(directory.getUser).not.toHaveBeenCalled();
  });
});
