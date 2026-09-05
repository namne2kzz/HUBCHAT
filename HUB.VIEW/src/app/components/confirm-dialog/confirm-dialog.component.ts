import { ChangeDetectionStrategy, Component, computed, HostListener, inject } from '@angular/core';
import { ConfirmDialogService } from '../../services/confirm-dialog.service';
import { ConfirmIcon, ConfirmTone } from '../../models/confirm-dialog.model';

/**
 * Host for the app-wide confirm dialog. Mount once (in the shell); it renders whatever request is
 * active on {@link ConfirmDialogService}. Confirm/Cancel/Escape/backdrop all settle the request.
 */
@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [],
  templateUrl: './confirm-dialog.component.html',
  styleUrl: './confirm-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConfirmDialogComponent {
  private readonly svc = inject(ConfirmDialogService);

  /** The active request (null = hidden). */
  protected readonly req = this.svc.request;

  /** Confirm button tone (defaults to primary). */
  protected readonly tone = computed<ConfirmTone>(() => this.req()?.tone ?? 'primary');

  /** Effective header icon — explicit `icon`, else derived from the tone. */
  protected readonly icon = computed<ConfirmIcon>(() => {
    const r = this.req();
    if (!r) return 'none';
    if (r.icon) return r.icon;
    switch (r.tone) {
      case 'danger':  return 'danger';
      case 'warning': return 'warning';
      case 'success': return 'success';
      default:        return 'question';
    }
  });

  /** Confirms the dialog. */
  protected confirm(): void { this.svc.respond(true); }

  /** Cancels/dismisses the dialog. */
  protected cancel(): void { this.svc.respond(false); }

  /** Escape closes the dialog as cancelled. */
  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    if (this.req()) this.svc.respond(false);
  }
}
