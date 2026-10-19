import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, output, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { AttachmentDto, AttachmentKind, ChannelAttachmentDto } from '../../models/message.model';
import { DirectoryUser } from '../../models/directory.model';
import { MessageService } from '../../services/message.service';
import { AttachmentViewComponent } from '../attachment-view/attachment-view.component';

type FilesTab = 'media' | 'files';

/**
 * Right-side panel listing a channel's shared attachments (CF-5), split into Media (images/videos) and
 * Files tabs. Click an item to jump to its message in the timeline.
 */
@Component({
  selector: 'app-channel-files-panel',
  standalone: true,
  imports: [DatePipe, AttachmentViewComponent],
  templateUrl: './channel-files-panel.component.html',
  styleUrl: './channel-files-panel.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChannelFilesPanelComponent implements OnInit {
  private readonly messages = inject(MessageService);

  readonly channelId = input.required<string>();
  /** Author id → profile, for "shared by" names (provided by the page). */
  readonly authors = input<Record<string, DirectoryUser>>({});

  readonly close       = output<void>();
  /** Emitted with a message id when the user clicks an item, so the page scrolls to it. */
  readonly jumpTo      = output<string>();

  readonly AttachmentKind = AttachmentKind;

  protected readonly tab     = signal<FilesTab>('media');
  protected readonly loading = signal(true);
  private   readonly all     = signal<ChannelAttachmentDto[]>([]);

  /** Media (image/video) attachments. */
  protected readonly media = computed(() =>
    this.all().filter(a => a.kind === AttachmentKind.Image || a.kind === AttachmentKind.Video));
  /** Non-media file attachments. */
  protected readonly docs = computed(() => this.all().filter(a => a.kind === AttachmentKind.File));

  ngOnInit(): void {
    this.messages.listAttachments(this.channelId()).subscribe({
      next: items => { this.all.set(items); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  /** Author display name for an id. @param id Author id. @returns The name or a short fallback. */
  protected authorName(id: string): string {
    return this.authors()[id]?.name ?? `User ${id.slice(0, 8)}`;
  }

  /** Adapts a panel row to the AttachmentDto shape the viewer renders. @param a Row. @returns AttachmentDto. */
  protected toAttachment(a: ChannelAttachmentDto): AttachmentDto {
    return { id: a.id, kind: a.kind, url: a.fileId, name: a.name, size: a.size, mime: a.mime, width: a.width, height: a.height };
  }
}
