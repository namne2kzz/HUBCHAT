import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { toObservable } from '@angular/core/rxjs-interop';
import { switchMap } from 'rxjs';
import { ConfigService } from '../../core/services/config.service';
import { WorkItemCacheService } from '../../services/work-item-cache.service';
import { workItemUrl } from '../../utils/work-item-link.util';

/**
 * Preview card for a linked DASHBOARD work item: shows its key, title and state, and opens the item in
 * DASHBOARD when clicked. Resolves the key through a shared cache; renders nothing until (or unless) it
 * resolves.
 */
@Component({
  selector: 'app-work-item-unfurl',
  standalone: true,
  imports: [],
  templateUrl: './work-item-unfurl.component.html',
  styleUrl: './work-item-unfurl.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WorkItemUnfurlComponent {
  private readonly cache  = inject(WorkItemCacheService);
  private readonly config = inject(ConfigService);

  /** The work item key to unfurl (e.g. "NFLOW-142"). */
  readonly key = input.required<string>();

  /** The resolved work item, or null while loading / when not found. */
  protected readonly item = toSignal(
    toObservable(this.key).pipe(switchMap(k => this.cache.get(k))),
    { initialValue: null },
  );

  /** Lowercased state slug for colour theming (e.g. "in-progress"). */
  protected readonly stateClass = computed(() =>
    'wi-state--' + (this.item()?.state ?? '').toLowerCase().replace(/[^a-z0-9]+/g, '-'));

  /** Opens the work item in DASHBOARD. */
  protected open(): void {
    const wi = this.item();
    if (!wi) return;
    window.open(workItemUrl(this.config.dashboardUrl, wi.repositoryCode, wi.key), '_blank', 'noopener');
  }
}
