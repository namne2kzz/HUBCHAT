import { HttpContext, HttpContextToken } from '@angular/common/http';

/**
 * When true on a request's context, the global error interceptor skips its error toast for that request.
 * For best-effort calls (e.g. presence custom-status) whose failure the UI already handles gracefully and
 * must not surface a red toast.
 */
export const SKIP_ERROR_TOAST = new HttpContextToken<boolean>(() => false);

/** Convenience: an HttpContext that opts a request out of the global error toast. @returns The context. */
export function skipErrorToast(): HttpContext {
  return new HttpContext().set(SKIP_ERROR_TOAST, true);
}
