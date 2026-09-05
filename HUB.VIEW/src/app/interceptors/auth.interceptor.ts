import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { StorageService } from '../core/services/storage.service';
import { StorageKeys } from '../core/constants/storage-keys.constant';

/** Attaches the JWT access token to every outgoing request (except auth endpoints). */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const storage = inject(StorageService);
  const token = storage.getString(StorageKeys.accessToken);

  if (!token) return next(req);

  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};
