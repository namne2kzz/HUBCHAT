import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { DirectoryService } from '../services/directory.service';
import { ChannelService } from '../services/channel.service';

/**
 * Resolves the current user's organization alias (from memberships) and redirects any non-aliased
 * URL to its `/{orgAlias}/…` equivalent, preserving the attempted path so DASHBOARD deep-links
 * (e.g. `/channels/{id}`) and the bare root both land correctly. In HUB the workspace IS the
 * organization, so the alias is the tenant prefix on every route.
 */
export const hubOrgRedirectGuard: CanActivateFn = (_route, state) => {
  const directory = inject(DirectoryService);
  const channels  = inject(ChannelService);
  const router    = inject(Router);

  // Preserve a legacy /channels/{id} deep-link (e.g. sprint "Open in HUB") by selecting it;
  // the URL itself stays clean (/{alias}/channels).
  const deep = state.url.match(/\/channels\/([^/?]+)/);
  if (deep) channels.selectChannel(deep[1]);

  return directory.getMyMemberships().pipe(
    map(m => router.parseUrl(m?.orgAlias ? `/${m.orgAlias}/channels` : '/signed-out')),
    catchError(() => of(router.parseUrl('/signed-out'))),
  );
};
