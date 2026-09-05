import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideZoneChangeDetection,
} from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { provideToastr } from 'ngx-toastr';
import { provideAnimations } from '@angular/platform-browser/animations';

import { authInterceptor } from './interceptors/auth.interceptor';
import { errorInterceptor } from './interceptors/error.interceptor';
import { ThemeService } from './core/services/theme.service';
import { ConfigService } from './core/services/config.service';
import { AuthService } from './services/auth.service';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    // 1. Consume SSO tokens from the URL BEFORE the router runs (guards read storage).
    //    Must be an app initializer, not AppComponent.ngOnInit — otherwise the auth
    //    guard runs first, sees no token, and bounces back to DASHBOARD login.
    //    Loop-breaker: count handoffs inside a rolling window. If DASHBOARD keeps sending
    //    tokens that HUB's gateway rejects (401), the two apps ping-pong forever — after
    //    MAX_SSO_HANDOFFS we stop and land on /signed-out instead. The counter is NOT
    //    cleared when it trips (that would make the very next bounce look like a fresh
    //    attempt and restart the loop); it expires on its own once the window elapses.
    provideAppInitializer(() => {
      const auth = inject(AuthService);
      const params = new URLSearchParams(window.location.search);
      const t = params.get('t');
      const r = params.get('r');
      if (!t || !r) return;

      const MAX_SSO_HANDOFFS = 3;
      const SSO_WINDOW_MS    = 30_000;

      const now   = Date.now();
      const since = Number(sessionStorage.getItem('hub_sso_since') ?? '0');
      const fresh = now - since > SSO_WINDOW_MS;
      const count = (fresh ? 0 : Number(sessionStorage.getItem('hub_sso_count') ?? '0')) + 1;

      if (fresh) sessionStorage.setItem('hub_sso_since', String(now));
      sessionStorage.setItem('hub_sso_count', String(count));

      if (count > MAX_SSO_HANDOFFS) {
        // Ping-pong detected: the token keeps being rejected. Break the loop.
        auth.clearSession();
        window.history.replaceState({}, '', '/signed-out');
        return;
      }

      auth.applyTokens(t, r);
      window.history.replaceState({}, '', window.location.pathname);
    }),
    // 2. Load runtime config — services that read URLs depend on this.
    provideAppInitializer(() => inject(ConfigService).load()),
    // 3. Apply persisted theme before any component renders.
    provideAppInitializer(() => { inject(ThemeService); }),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),
    provideAnimations(),
    provideToastr({
      timeOut: 4000,
      positionClass: 'toast-top-right',
      preventDuplicates: true,
      progressBar: true,
      closeButton: true,
    }),
  ],
};
