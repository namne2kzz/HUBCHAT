import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { DirectoryUser } from '../../models/directory.model';

/**
 * "New group message" dialog: pick 2–7 people to open a group direct message.
 * Emits the chosen member ids; the host opens/creates the group and navigates to it.
 */
@Component({
  selector: 'app-group-dm-dialog',
  standalone: true,
  imports: [],
  templateUrl: './group-dm-dialog.component.html',
  styleUrl: './group-dm-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GroupDmDialogComponent {
  /** Candidate members (workspace members minus the current user). */
  readonly members = input<DirectoryUser[]>([]);
  /** True while the group is being opened (disables Create). */
  readonly creating = input(false);

  readonly close  = output<void>();
  /** Emits the selected member ids when the user confirms. */
  readonly create = output<string[]>();

  /** Minimum/maximum other participants (3–8 total including the caller). */
  protected readonly MIN = 2;
  protected readonly MAX = 7;

  protected readonly query    = signal('');
  protected readonly selected = signal<DirectoryUser[]>([]);

  /** Members matching the search, excluding already-selected ones. */
  protected readonly results = computed(() => {
    const q = this.query().toLowerCase().trim();
    const chosen = new Set(this.selected().map(m => m.id));
    return this.members()
      .filter(m => !chosen.has(m.id))
      .filter(m => !q || m.name.toLowerCase().includes(q) || m.email.toLowerCase().includes(q))
      .slice(0, 50);
  });

  protected readonly canCreate = computed(() => {
    const n = this.selected().length;
    return n >= this.MIN && n <= this.MAX && !this.creating();
  });

  protected onQuery(e: Event): void {
    this.query.set((e.target as HTMLInputElement).value);
  }

  protected add(member: DirectoryUser): void {
    if (this.selected().length >= this.MAX) return;
    this.selected.update(s => [...s, member]);
    this.query.set('');
  }

  protected remove(id: string): void {
    this.selected.update(s => s.filter(m => m.id !== id));
  }

  protected submit(): void {
    if (!this.canCreate()) return;
    this.create.emit(this.selected().map(m => m.id));
  }
}
