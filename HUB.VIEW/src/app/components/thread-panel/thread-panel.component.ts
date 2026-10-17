import {
  ChangeDetectionStrategy, Component, computed, DestroyRef, inject, input, OnChanges, output, signal, SimpleChanges,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MessageService } from '../../services/message.service';
import { DirectoryService } from '../../services/directory.service';
import { MessageDto, ReactionToggle } from '../../models/message.model';
import { DirectoryUser } from '../../models/directory.model';
import { MessageListComponent } from '../message-list/message-list.component';
import { MessageInputComponent, ComposerSubmit } from '../message-input/message-input.component';
import { applyReaction, removeReaction } from '../../utils/reaction.util';

/**
 * Side panel showing one message's thread: the root message plus its replies, with a composer that posts
 * replies (parentId = root). Slack-style right-hand pane. Reuses MessageListComponent for rendering and
 * MessageInputComponent for composing, so threads inherit mentions, reactions, edit/delete for free.
 */
@Component({
  selector: 'app-thread-panel',
  standalone: true,
  imports: [MessageListComponent, MessageInputComponent],
  templateUrl: './thread-panel.component.html',
  styleUrl: './thread-panel.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ThreadPanelComponent implements OnChanges {
  private readonly messageSvc = inject(MessageService);
  private readonly directory  = inject(DirectoryService);
  private readonly destroyRef = inject(DestroyRef);

  /** The top-level message the thread hangs off. */
  readonly root          = input.required<MessageDto>();
  readonly channelId     = input.required<string>();
  readonly currentUserId = input.required<string>();
  /** Whether the viewer may post/react (channel member). */
  readonly canPost       = input<boolean>(false);
  readonly canModerate   = input<boolean>(false);
  readonly channelName   = input<string>('');

  readonly close = output<void>();
  /** Emits (parentId, replyCount) after a reply is posted, so the parent can update the thread summary. */
  readonly replyCountChanged = output<{ parentId: string; count: number }>();

  /** Root + replies, oldest first — the list the panel renders. */
  protected readonly messages = signal<MessageDto[]>([]);
  protected readonly authors  = signal<Record<string, DirectoryUser>>({});
  protected readonly loading  = signal(false);

  /** Replies only (messages after the root). */
  protected readonly replyCount = computed(() => this.messages().filter(m => m.parentId).length);

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['root'] && this.root()) this.load();
  }

  private load(): void {
    const root = this.root();
    this.loading.set(true);
    this.messages.set([root]);
    this.resolveAuthors([root]);
    this.messageSvc.listReplies(this.channelId(), root.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: replies => {
          this.messages.set([root, ...replies]);
          this.resolveAuthors(replies);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  /** Posts a reply (parentId = root) and appends it. @param payload Composer output. */
  protected sendReply(payload: ComposerSubmit): void {
    if (!payload.body.trim()) return;
    const rootId = this.root().id;
    this.messageSvc.send(this.channelId(), {
      body: payload.body, format: payload.format, parentId: rootId, mentionedUserIds: payload.mentionedUserIds,
    }).subscribe(msg => {
      this.messages.update(prev => [...prev, msg]);
      this.resolveAuthors([msg]);
      this.replyCountChanged.emit({ parentId: rootId, count: this.replyCount() });
    });
  }

  /** Toggles a reaction on a thread message (optimistic). @param t Message + emoji. */
  protected onToggleReaction(t: ReactionToggle): void {
    const me = this.currentUserId();
    const msg = this.messages().find(m => m.id === t.messageId);
    if (!me || !msg) return;
    const had = msg.reactions.some(r => r.userId === me && r.emoji === t.emoji);
    const apply = (fn: typeof applyReaction) => this.messages.update(prev => prev.map(m => fn(m, t.messageId, me, t.emoji)));
    apply(had ? removeReaction : applyReaction);
    const call = had ? this.messageSvc.unreact(t.messageId, t.emoji) : this.messageSvc.react(t.messageId, t.emoji);
    call.subscribe({ error: () => apply(had ? applyReaction : removeReaction) });
  }

  /** Edits a thread message (optimistic). @param e Message id + new body. */
  protected onEditMessage(e: { messageId: string; body: string }): void {
    const body = e.body.trim();
    const current = this.messages().find(m => m.id === e.messageId);
    if (!current || !body || body === current.body) return;
    const prevBody = current.body;
    this.messages.update(prev => prev.map(m => m.id === e.messageId ? { ...m, body, editedAt: new Date().toISOString() } : m));
    this.messageSvc.edit(e.messageId, body).subscribe({
      next: saved => this.messages.update(prev => prev.map(m => m.id === saved.id ? saved : m)),
      error: () => this.messages.update(prev => prev.map(m => m.id === e.messageId ? { ...m, body: prevBody, editedAt: current.editedAt } : m)),
    });
  }

  /** Deletes a thread message to a tombstone (optimistic). @param messageId Message id. */
  protected onDeleteMessage(messageId: string): void {
    const before = this.messages();
    this.messages.update(prev => prev.map(m =>
      m.id === messageId ? { ...m, isDeleted: true, body: '', reactions: [], attachments: [], editedAt: null } : m));
    this.messageSvc.delete(messageId).subscribe({ error: () => this.messages.set(before) });
  }

  private resolveAuthors(msgs: MessageDto[]): void {
    const known = this.authors();
    const ids = [...new Set(msgs.flatMap(m => [m.authorId, ...(m.mentions ?? [])]))].filter(id => !known[id]);
    if (!ids.length) return;
    this.directory.getUsers(ids).subscribe({
      next: users => this.authors.update(a => ({ ...a, ...Object.fromEntries(users.map(u => [u.id, u])) })),
      error: () => { /* short id fallback */ },
    });
  }
}
