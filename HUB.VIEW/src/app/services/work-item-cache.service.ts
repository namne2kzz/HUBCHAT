import { inject, Injectable } from '@angular/core';
import { Observable, map, of, shareReplay } from 'rxjs';
import { DirectoryService } from './directory.service';
import { WorkItemContext } from '../models/directory.model';

/**
 * Caches work-item lookups by key so the same linked work item is fetched once, no matter how many
 * messages reference it. A null result (missing/inaccessible) is cached too, so it isn't retried on
 * every render.
 */
@Injectable({ providedIn: 'root' })
export class WorkItemCacheService {
  private readonly directory = inject(DirectoryService);
  private readonly cache = new Map<string, Observable<WorkItemContext | null>>();

  /**
   * Resolves a work item by key, from cache when already seen.
   * @param key Work item key (e.g. "NFLOW-142").
   * @returns Observable of the context, or null when it cannot be resolved.
   */
  get(key: string): Observable<WorkItemContext | null> {
    const existing = this.cache.get(key);
    if (existing) return existing;

    const req = this.directory.getWorkItemByKey(key).pipe(
      map(wi => wi ?? null),
      shareReplay(1),
    );
    this.cache.set(key, req);
    return req;
  }
}
