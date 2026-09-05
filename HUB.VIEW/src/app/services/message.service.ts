import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
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
   * Posts a message to a channel (POST /channels/{channelId}/messages).
   * @param channelId Target channel.
   * @param request Message payload.
   * @returns The created message.
   */
  send(channelId: string, request: PostMessageRequest): Observable<MessageDto> {
    return this.http.post<MessageDto>(this.channelUrl(channelId), request);
  }

  /** Adds an emoji reaction (POST /messages/{id}/reactions). @param messageId Message id. @param emoji Emoji shortcode. */
  react(messageId: string, emoji: string): Observable<void> {
    return this.http.post<void>(`${this.config.apiBaseUrl}/messages/${messageId}/reactions`, { emoji });
  }
}
