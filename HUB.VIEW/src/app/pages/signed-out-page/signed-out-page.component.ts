import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { AuthService } from '../../services/auth.service';

/** Landing page shown after logout. A single Sign In button restarts the SSO flow. */
@Component({
  selector: 'app-signed-out-page',
  standalone: true,
  imports: [],
  templateUrl: './signed-out-page.component.html',
  styleUrl: './signed-out-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SignedOutPageComponent {
  private readonly auth = inject(AuthService);

  /** Redirects to DASHBOARD login (SSO), returning to channels on success. */
  signIn(): void {
    this.auth.signIn('/channels');
  }
}
