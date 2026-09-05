import { Routes } from '@angular/router';
import { authGuard, authGuardChild } from './guards/auth.guard';
import { hubOrgRedirectGuard } from './guards/org-redirect.guard';

export const routes: Routes = [
  {
    path: 'signed-out',
    loadComponent: () =>
      import('./pages/signed-out-page/signed-out-page.component').then(m => m.SignedOutPageComponent),
  },
  // ── Legacy non-aliased links → forward to /{orgAlias}/channels (a /channels/{id} deep-link
  //    selects that channel first). Declared before :orgAlias so fixed segments win. ─────────────
  {
    path: 'channels/:slug',
    canActivate: [authGuard, hubOrgRedirectGuard],
    children: [],
  },
  {
    path: 'channels',
    pathMatch: 'full',
    canActivate: [authGuard, hubOrgRedirectGuard],
    children: [],
  },
  {
    path: 'notifications',
    pathMatch: 'full',
    canActivate: [authGuard, hubOrgRedirectGuard],
    children: [],
  },
  // ── Tenant-scoped app — workspace = organization; alias is the route prefix ────
  //    The open channel is app state (persisted), NOT in the URL — URL stays /{alias}/channels.
  {
    path: ':orgAlias',
    loadComponent: () =>
      import('./layout/shell-layout.component').then(m => m.ShellLayoutComponent),
    canActivateChild: [authGuardChild],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'channels' },
      {
        path: 'channels',
        loadComponent: () =>
          import('./pages/channel-detail-page/channel-detail-page.component').then(m => m.ChannelDetailPageComponent),
      },
      {
        path: 'notifications',
        loadComponent: () =>
          import('./pages/notifications-page/notifications-page.component').then(m => m.NotificationsPageComponent),
      },
    ],
  },
  // ── Bare root → resolve the user's org alias, then redirect ───────────────────
  {
    path: '',
    pathMatch: 'full',
    canActivate: [authGuard, hubOrgRedirectGuard],
    children: [],
  },
  { path: '**', redirectTo: '' },
];
