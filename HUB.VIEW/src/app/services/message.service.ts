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
