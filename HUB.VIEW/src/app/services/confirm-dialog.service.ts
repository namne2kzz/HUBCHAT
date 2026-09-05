import { Injectable, signal } from '@angular/core';
import { ConfirmOptions } from '../models/confirm-dialog.model';

/** A pending confirm request: the options plus the promise resolver to settle on the user's choice. */
interface ConfirmRequest extends ConfirmOptions {
  resolve: (result: boolean) => void;
}

/**
 * App-wide confirm dialog. Call {@link confirm} from anywhere to show a themed Yes/No modal and
 * await the user's choice; a single {@link ConfirmDialogComponent} (mounted once in the shell)
 * renders the active request. Replaces the native `window.confirm`.
 */
@Injectable({ providedIn: 'root' })
export class ConfirmDialogService {
  private readonly _request = signal<ConfirmRequest | null>(null);

  /** The active confirm request, or null when no dialog is open. Read by the host component. */
  readonly request = this._request.asReadonly();

  /**
   * Opens a confirm dialog and resolves with the user's choice.
   * @param options Title, description, button labels, tone, icon.
   * @returns True if the user confirmed; false if they cancelled/dismissed.
   */
  confirm(options: ConfirmOptions): Promise<boolean> {
    // If a dialog is already open, resolve it as cancelled before showing the new one.
    this._request()?.resolve(false);
    return new Promise<boolean>(resolve => this._request.set({ ...options, resolve }));
  }

  /**
   * Settles the active request and closes the dialog. Called by the host component.
   * @param result The user's choice.
   */
  respond(result: boolean): void {
    const req = this._request();
    if (!req) return;
    this._request.set(null);
    req.resolve(result);
  }
}
