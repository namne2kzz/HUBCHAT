/** Visual tone of a confirm dialog — styles the confirm button and the header icon. */
export type ConfirmTone = 'primary' | 'danger' | 'warning' | 'success';

/** Header icon shown in a confirm dialog. `none` hides the icon. */
export type ConfirmIcon = 'question' | 'warning' | 'danger' | 'info' | 'success' | 'none';

/**
 * Options for a reusable confirm dialog opened via {@link ConfirmDialogService.confirm}.
 * Only {@link title} is required; the rest have sensible defaults.
 */
export interface ConfirmOptions {
  /** Bold heading (the question). */
  title: string;
  /** Optional supporting text under the title. */
  description?: string;
  /** Confirm button label (default "Confirm"). */
  confirmText?: string;
  /** Cancel button label (default "Cancel"). */
  cancelText?: string;
  /** Confirm button tone (default "primary"). */
  tone?: ConfirmTone;
  /** Header icon (default derived from tone). */
  icon?: ConfirmIcon;
}
