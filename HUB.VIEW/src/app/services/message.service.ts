import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, retry, throwError, timer } from 'rxjs';
import { ConfigService } from '../core/services/config.service';
import { CursorPage, MessageDto, PostMessageRequest } from '../models/message.model';

@Injectable({ providedIn: 'root' })
export class MessageService {
  private readonly http   = inject(HttpClient);
  private readonly config = inject(ConfigService);
  private channelUrl(channelId: string) { return `${this.config.apiBaseUrl}/channels/${channelId}/messages`; }

  /**
   * Lists a channel's messages (newest first, keyset pagination).
   * @param channelId Target channel.
   * @param cursor Opaque cursor for the next older page.
   * @param limit Page size (1..100). Default 50.
   * @returns Cursor page of messages.
   */
  list(channelId: string, cursor?: string, limit = 50): Observable<CursorPage<MessageDto>> {
    let params = new HttpParams().set('limit', limit);
    if (cursor) params = params.set('cursor', cursor);
    return this.http.get<CursorPage<MessageDto>>(this.channelUrl(channelId), { params });
  }

  /**
   * Lists top-level messages newer than a given one, oldest first — reconnect catch-up.
   * Page by passing the last returned id until fewer than `limit` items come back.
   * @param channelId Target channel.
   * @param afterMessageId The newest message the client already has.
   * @param limit Page size (1..100). Default 100.
   * @returns Newer messages, oldest first. Errors with 404 when the anchor is unknown (reload instead).
   */
  /**
   * Lists the replies of a thread (GET /channels/{id}/messages/{messageId}/replies), oldest first.
   * @param channelId Channel the thread lives in.
   * @param messageId The top-level message whose thread to open.
   * @returns The thread's replies.
   */
  listReplies(channelId: string, messageId: string): Observable<MessageDto[]> {
    return this.http.get<MessageDto[]>(`${this.channelUrl(channelId)}/${messageId}/replies`);
  }

  /**
   * Forwards a message into another channel (POST /messages/{id}/forward).
   * @param messageId Source message to forward.
   * @param targetChannelId Channel to forward it into.
   * @returns The forwarded message in the target channel.
   */
  forward(messageId: string, targetChannelId: string): Observable<MessageDto> {
    return this.http.post<MessageDto>(`${this.config.apiBaseUrl}/messages/${messageId}/forward`, { targetChannelId });
  }

  /**
   * Searches a channel's full message history (GET /channels/{id}/messages/search).
   * @param channelId Channel to search.
   * @param term Search text.
   * @param limit Max results (default 30).
   * @returns Matching messages, newest first.
   */
  search(channelId: string, term: string, limit = 30): Observable<MessageDto[]> {
    return this.http.get<MessageDto[]>(`${this.channelUrl(channelId)}/search`, {
      params: new HttpParams().set('q', term).set('limit', limit),
    });
  }

  /** Pins a message (POST /messages/{id}/pin). @param messageId Message id. @returns Completes on success. */
  pin(messageId: string): Observable<void> {
    return this.http.post<void>(`${this.config.apiBaseUrl}/messages/${messageId}/pin`, {});
  }

  /** Unpins a message (DELETE /messages/{id}/pin). @param messageId Message id. @returns Completes on success. */
  unpin(messageId: string): Observable<void> {
    return this.http.delete<void>(`${this.config.apiBaseUrl}/messages/${messageId}/pin`);
  }

  /** Lists a channel's pinned messages (GET /channels/{id}/messages/pinned). @param channelId Channel id. */
  listPinned(channelId: string): Observable<MessageDto[]> {
    return this.http.get<MessageDto[]>(`${this.channelUrl(channelId)}/pinned`);
  }

  listAfter(channelId: string, afterMessageId: string, limit = 100): Observable<MessageDto[]> {
    return this.http.get<MessageDto[]>(`${this.channelUrl(channelId)}/after/${afterMessageId}`, {
      params: new HttpParams().set('limit', limit),
    });
  }

  /**
   * Posts a message to a channel (POST /channels/{channelId}/messages), retrying transient failures.
   * One `clientMessageId` is fixed per call and reused on every retry, so if the server committed but the
   * response was lost (network drop, gateway 502/504) the retry returns the original message instead of
   * posting a duplicate.
   * @param channelId Target channel.
   * @param request Message payload; `clientMessageId` is generated when absent.
   * @returns The created (or, on a replayed retry, the original) message.
   */
  send(channelId: string, request: PostMessageRequest): Observable<MessageDto> {
    const body: PostMessageRequest = { ...request, clientMessageId: request.clientMessageId ?? crypto.randomUUID() };
    return this.http.post<MessageDto>(this.channelUrl(channelId), body).pipe(
      retry({
        count: MessageService.sendRetries,
        delay: (err, attempt) => isTransient(err)
          ? timer(MessageService.retryBaseMs * 2 ** (attempt - 1))
          : throwError(() => err),
      }),
    );
  }

  /** Retries after the first attempt; safe only because the key makes the POST idempotent. */
  static readonly sendRetries = 2;

  /** First retry delay; doubles each attempt (500ms, 1s). */
  static readonly retryBaseMs = 500;

  /** Adds an emoji reaction (POST /messages/{id}/reactions). @param messageId Message id. @param emoji Emoji shortcode. */
  react(messageId: string, emoji: string): Observable<void> {
    return this.http.post<void>(`${this.config.apiBaseUrl}/messages/${messageId}/reactions`, { emoji });
  }

  /**
   * Removes the caller's own reaction (DELETE /messages/{id}/reactions?emoji=). Idempotent server-side.
   * @param messageId Message id.
   * @param emoji Emoji to remove (sent as a query param — emoji do not travel well in a path segment).
   * @returns Completes on success.
   */
  unreact(messageId: string, emoji: string): Observable<void> {
    return this.http.delete<void>(`${this.config.apiBaseUrl}/messages/${messageId}/reactions`, {
      params: new HttpParams().set('emoji', emoji),
    });
  }

  /**
   * Edits the caller's own message (PATCH /messages/{id}). Author-only, within the edit window — server enforces.
   * @param messageId Message id.
   * @param body New body.
   * @returns The edited message.
   */
  edit(messageId: string, body: string): Observable<MessageDto> {
    return this.http.patch<MessageDto>(`${this.config.apiBaseUrl}/messages/${messageId}`, { body });
  }

  /**
   * Deletes a message (DELETE /messages/{id}) — author within the window, or a channel moderator.
   * @param messageId Message id.
   * @returns Completes on success.
   */
  delete(messageId: string): Observable<void> {
    return this.http.delete<void>(`${this.config.apiBaseUrl}/messages/${messageId}`);
  }
}

/**
 * True for failures where the request may or may not have reached the server and retrying can succeed:
 * no response at all (status 0) or a gateway/upstream outage (502/503/504). 4xx and 500 are not retried.
 * @param err The error from HttpClient.
 * @returns Whether the send should be retried.
 */
function isTransient(err: unknown): boolean {
  return err instanceof HttpErrorResponse && [0, 502, 503, 504].includes(err.status);
}
