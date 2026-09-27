import { inject } from '@angular/core';
import { CanActivateFn, CanActivateChildFn } from '@angular/router';
import { StorageService } from '../core/services/storage.service';
import { StorageKeys } from '../core/constants/storage-keys.constant';
import { ConfigService } from '../core/services/config.service';
import { NavigationService } from '../core/services/navigation.service';

/**
 * Redirects unauthenticated users to DASHBOARD login.
 * DASHBOARD redirects back to HUB with `?t=<token>&r=<refresh>` in the URL.
 * AppComponent reads those params, stores them, and cleans the URL.
 */
const redirectToDashboardLogin = (): boolean => {
  const config     = inject(ConfigService);
  const navigation = inject(NavigationService);
  const returnUrl  = encodeURIComponent(navigation.currentUrl());
  navigation.goTo(`${config.dashboardUrl}/login?returnUrl=${returnUrl}`);
  return false;
};

export const authGuard: CanActivateFn = () => {
  const storage = inject(StorageService);
  return storage.has(StorageKeys.accessToken) || redirectToDashboardLogin();
};

export const authGuardChild: CanActivateChildFn = () => {
  const storage = inject(StorageService);
  return storage.has(StorageKeys.accessToken) || redirectToDashboardLogin();
};
