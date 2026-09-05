import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import { catchError, throwError } from 'rxjs';
import { StorageService } from '../core/services/storage.service';
import { StorageKeys } from '../core/constants/storage-keys.constant';
import { ConfigService } from '../core/services/config.service';

/**
 * Handles HTTP errors globally.
 * 401 → clears the stale session and redirects to DASHBOARD login (HUB has no own login).
 * Other errors → surface a toast.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const toastr  = inject(ToastrService);
  const storage = inject(StorageService);
  const config  = inject(ConfigService);

  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401) {
        storage.remove(StorageKeys.accessToken);
        storage.remove(StorageKeys.refreshToken);
        storage.remove(StorageKeys.userProfile);
        const returnUrl = encodeURIComponent(window.location.href);
        window.location.href = `${config.dashboardUrl}/login?returnUrl=${returnUrl}`;
        return throwError(() => err);
      }

      const message =
        err.error?.detail ??
        err.error?.title ??
        err.message ??
        'An unexpected error occurred.';

      if (err.status !== 0) toastr.error(message, `Error ${err.status}`);

      return throwError(() => err);
    })
  );
};
