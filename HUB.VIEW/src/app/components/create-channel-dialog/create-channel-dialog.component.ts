import {
  ChangeDetectionStrategy, Component, inject, input, output, signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ChannelService } from '../../services/channel.service';
import { ChannelDto, ChannelType } from '../../models/channel.model';

/**
 * Modal dialog for creating a public or private channel.
 * Emits the created channel so the shell can add it to the sidebar immediately.
 */
@Component({
  selector: 'app-create-channel-dialog',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './create-channel-dialog.component.html',
  styleUrl: './create-channel-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CreateChannelDialogComponent {
  private readonly channelSvc = inject(ChannelService);

  /** The workspace (DASHBOARD repository) to create the channel in. */
  readonly workspaceId = input.required<string>();

  /** Emits the created channel DTO. */
  readonly created = output<ChannelDto>();

  /** Emits void when the dialog should be closed without creating. */
  readonly close = output<void>();

  readonly ChannelType = ChannelType;

  protected name      = signal('');
  protected topic     = signal('');
  protected type      = signal<ChannelType>(ChannelType.Public);
  protected loading   = signal(false);
  protected error     = signal<string | null>(null);

  /** Switches between Public and Private. @param t Type to set. */
  protected setType(t: ChannelType): void { this.type.set(t); }

  /**
   * Validates and submits the create-channel form.
   * On success emits the new channel and closes the dialog.
   */
  protected submit(): void {
    const n = this.name().trim();
    if (!n) { this.error.set('Channel name is required.'); return; }
    if (n.length > 100) { this.error.set('Name must be 100 characters or fewer.'); return; }

    this.loading.set(true);
    this.error.set(null);

    this.channelSvc.create({
      workspaceId: this.workspaceId(),
      name: n,
      type: this.type(),
      topic: this.topic().trim() || undefined,
    }).subscribe({
      next: ch => { this.loading.set(false); this.created.emit(ch); },
      error: () => { this.loading.set(false); this.error.set('Could not create channel. Try again.'); },
    });
  }

  /** Closes the dialog when backdrop is clicked. @param event Mouse event. */
  protected onBackdrop(event: MouseEvent): void {
    if ((event.target as HTMLElement).classList.contains('dialog-backdrop'))
      this.close.emit();
  }
}
