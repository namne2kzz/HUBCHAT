import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { ThreadPanelComponent } from './thread-panel.component';
import { MessageService } from '../../services/message.service';
import { DirectoryService } from '../../services/directory.service';
import { PresenceService } from '../../services/presence.service';
import { MessageDto, MessageFormat } from '../../models/message.model';

/**
 * Covers the thread panel: it loads the root + its replies, and posting a reply carries parentId and
 * bumps the reply count the parent header shows.
 */
describe('ThreadPanelComponent', () => {
  let fixture: ComponentFixture<ThreadPanelComponent>;
  let component: ThreadPanelComponent;
  let messageSvc: jasmine.SpyObj<MessageService>;

  const root: MessageDto = {
    id: 'root', channelId: 'c1', parentId: null, replyToId: null, forwardedFromId: null,
    authorId: 'u1', body: 'root msg', format: MessageFormat.Plain, mentions: [], reactions: [],
    attachments: [], editedAt: null, createdAt: '2026-10-14T09:00:00Z',
  };
  const reply = (id: string): MessageDto => ({ ...root, id, parentId: 'root', body: id });

  beforeEach(async () => {
    messageSvc = jasmine.createSpyObj<MessageService>('MessageService',
      ['listReplies', 'send', 'react', 'unreact', 'edit', 'delete']);
    messageSvc.listReplies.and.returnValue(of([reply('r1')]));

    await TestBed.configureTestingModule({
      imports: [ThreadPanelComponent],
      providers: [
        provideHttpClient(), provideHttpClientTesting(), // embedded composer's FileService needs HttpClient
        { provide: MessageService, useValue: messageSvc },
        { provide: DirectoryService, useValue: { getUsers: () => of([]) } },
        // The embedded message list reads peer custom statuses from presence; stub it.
        { provide: PresenceService, useValue: { getCustomStatus: () => null } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ThreadPanelComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('root', root);
    fixture.componentRef.setInput('channelId', 'c1');
    fixture.componentRef.setInput('currentUserId', 'u1');
    fixture.componentRef.setInput('canPost', true);
    fixture.detectChanges();
  });

  it('loads the root plus its replies', () => {
    expect(messageSvc.listReplies).toHaveBeenCalledWith('c1', 'root');
    // Root is always first; replies follow.
    expect((component as unknown as { messages: () => MessageDto[] }).messages().map(m => m.id)).toEqual(['root', 'r1']);
  });

  it('posts a reply with parentId and reports the new count', () => {
    messageSvc.send.and.returnValue(of(reply('r2')));
    const counts: { parentId: string; count: number }[] = [];
    component.replyCountChanged.subscribe(e => counts.push(e));

    (component as unknown as { sendReply: (p: unknown) => void })
      .sendReply({ body: 'hi', format: MessageFormat.Markdown });

    // The reply must hang off the root, or it would land in the main timeline instead of the thread.
    expect(messageSvc.send).toHaveBeenCalledWith('c1', jasmine.objectContaining({ body: 'hi', parentId: 'root' }));
    expect(counts).toEqual([{ parentId: 'root', count: 2 }]);
  });
});
