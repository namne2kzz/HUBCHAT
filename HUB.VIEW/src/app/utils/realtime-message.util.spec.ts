import { messageFromRealtime } from './realtime-message.util';
import { MessageFormat, MessageReceivedEvent } from '../models/message.model';

/**
 * Covers messageFromRealtime: recipients must see the full message (not the 140-char preview), and old
 * events without the full fields must still render.
 */
describe('messageFromRealtime', () => {
  const base: MessageReceivedEvent = {
    messageId: 'm1', channelId: 'c1', authorId: 'u1',
    preview: 'x'.repeat(140), mentions: [], sentAt: '2026-10-08T10:00:00.050Z',
  };

  it('uses the full body, format and creation time when the event carries them', () => {
    const longBody = '**bold** ' + 'x'.repeat(500);
    const m = messageFromRealtime({
      ...base, body: longBody, format: MessageFormat.Markdown, createdAt: '2026-10-08T10:00:00.000Z',
    });

    // The bug this fixes: recipients saw a 140-char, unrendered stub until they reloaded.
    expect(m.body).toBe(longBody);
    expect(m.format).toBe(MessageFormat.Markdown);
    // createdAt (the message's own time) rather than sentAt (the event's) — it is the timeline sort key.
    expect(m.createdAt).toBe('2026-10-08T10:00:00.000Z');
  });

  it('falls back to the preview as plain text for events from before the full fields existed', () => {
    const m = messageFromRealtime(base);
    expect(m.body).toBe(base.preview);
    expect(m.format).toBe(MessageFormat.Plain);
    expect(m.createdAt).toBe(base.sentAt);
  });

  it('keeps the thread parent so a reply can be told apart from a top-level message', () => {
    expect(messageFromRealtime({ ...base, body: 'r', parentId: 'p1' }).parentId).toBe('p1');
    expect(messageFromRealtime({ ...base, body: 't' }).parentId).toBeNull();
  });
});
