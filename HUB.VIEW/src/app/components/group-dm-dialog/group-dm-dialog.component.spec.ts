import { ComponentFixture, TestBed } from '@angular/core/testing';
import { GroupDmDialogComponent } from './group-dm-dialog.component';
import { DirectoryUser } from '../../models/directory.model';

/**
 * Covers the "New group message" dialog: picking people, the 2–7 range that gates Create, and the ids it
 * emits when confirmed.
 */
describe('GroupDmDialogComponent', () => {
  let fixture: ComponentFixture<GroupDmDialogComponent>;
  let component: GroupDmDialogComponent;

  const user = (id: string): DirectoryUser => ({
    id, name: `User ${id}`, email: `${id}@x.com`, avatarClass: 'bg-sky-600', isGlobalAdmin: false, isDeleted: false,
  });

  const inner = () => component as unknown as {
    selected: () => DirectoryUser[];
    canCreate: () => boolean;
    add(u: DirectoryUser): void;
    remove(id: string): void;
    submit(): void;
    MAX: number;
  };

  function mount(members: DirectoryUser[]): void {
    fixture = TestBed.createComponent(GroupDmDialogComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('members', members);
    fixture.detectChanges();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [GroupDmDialogComponent] });
  });

  it('cannot create with fewer than two people selected', () => {
    mount([user('a'), user('b')]);
    inner().add(user('a'));
    expect(inner().canCreate()).toBeFalse(); // only one
    inner().add(user('b'));
    expect(inner().canCreate()).toBeTrue();  // two → valid
  });

  it('stops adding beyond the maximum', () => {
    const many = Array.from({ length: 10 }, (_, i) => user(`u${i}`));
    mount(many);
    many.forEach(m => inner().add(m));
    expect(inner().selected().length).toBe(inner().MAX); // capped at 7
  });

  it('removing a selected person updates the state', () => {
    mount([user('a'), user('b')]);
    inner().add(user('a'));
    inner().add(user('b'));
    inner().remove('a');
    expect(inner().selected().map(m => m.id)).toEqual(['b']);
  });

  it('emits the selected ids on create', () => {
    mount([user('a'), user('b'), user('c')]);
    const emitted: string[][] = [];
    component.create.subscribe(ids => emitted.push(ids));

    inner().add(user('a'));
    inner().add(user('b'));
    inner().submit();

    expect(emitted).toEqual([['a', 'b']]);
  });

  it('does not emit when below the minimum', () => {
    mount([user('a')]);
    const emitted: string[][] = [];
    component.create.subscribe(ids => emitted.push(ids));

    inner().add(user('a'));
    inner().submit();

    expect(emitted).toEqual([]);
  });
});
