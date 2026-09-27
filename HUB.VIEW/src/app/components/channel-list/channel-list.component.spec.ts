import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChannelListComponent } from './channel-list.component';
import { ChannelDto, ChannelType } from '../../models/channel.model';

/**
 * Covers the sidebar channel list: what it renders and what it emits.
 *
 * A pure display component, so the tests go through the rendered DOM rather than the class — that is
 * the only place its behaviour actually lives.
 */
describe('ChannelListComponent', () => {
  let fixture: ComponentFixture<ChannelListComponent>;
  let clicked: string[];

  const channel = (over: Partial<ChannelDto> = {}): ChannelDto => ({
    id: 'c1', workspaceId: 'w1', name: 'General', slug: 'general', type: ChannelType.Public,
    topic: '', isPrivate: false, isArchived: false, memberCount: 1,
    createdAt: new Date().toISOString(), linkType: null, linkExternalKey: null, linkUrl: '',
    isMember: true, otherUserId: null, myRole: null, ...over,
  } as ChannelDto);

  const render = (channels: ChannelDto[]) => {
    fixture.componentRef.setInput('channels', channels);
    fixture.detectChanges();
  };

  /** Every clickable channel row currently rendered. */
  const rows = (): HTMLElement[] =>
    Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLElement>('.chan-row'));

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [ChannelListComponent] }).compileComponents();

    fixture = TestBed.createComponent(ChannelListComponent);
    clicked = [];
    fixture.componentInstance.channelClick.subscribe(id => clicked.push(id));

    fixture.componentRef.setInput('channels', []);
    fixture.detectChanges();
  });

  it('should create', () => expect(fixture.componentInstance).toBeTruthy());

  it('renders nothing for an empty list', () => {
    render([]);

    expect(rows().length).toBe(0);
  });

  it('renders a row per channel', () => {
    render([channel({ id: 'c1', name: 'General' }), channel({ id: 'c2', name: 'Random' })]);

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('General');
    expect(text).toContain('Random');
  });

  it('emits the channel id when a row is clicked', () => {
    render([channel({ id: 'c1', name: 'General' })]);

    const row = rows()[0];
    row.click();
    fixture.detectChanges();

    // The id rather than the index or the name: the parent looks the channel up by id, and names are
    // not unique across workspaces.
    expect(clicked).toEqual(['c1']);
  });

  it('emits the right id when there are several channels', () => {
    render([
      channel({ id: 'c1', name: 'General' }),
      channel({ id: 'c2', name: 'Random' }),
      channel({ id: 'c3', name: 'Design' }),
    ]);

    rows()[1].click();
    fixture.detectChanges();

    expect(clicked).toEqual(['c2']);
  });

  it('shows the member count', () => {
    render([channel({ memberCount: 7 })]);

    expect(fixture.nativeElement.textContent).toContain('7');
  });

  it('shows the topic when there is one', () => {
    render([channel({ topic: 'All things eng' })]);

    expect(fixture.nativeElement.textContent).toContain('All things eng');
  });

  it('shows the linked resource key when there is no topic', () => {
    // A discussion thread has no topic of its own, so the DASHBOARD key it was opened from is what
    // tells the two apart in the sidebar.
    render([channel({ topic: '', linkExternalKey: 'DASH-142' })]);

    expect(fixture.nativeElement.textContent).toContain('DASH-142');
  });

  it('prefers the topic over the linked key when both are present', () => {
    render([channel({ topic: 'Real topic', linkExternalKey: 'DASH-142' })]);

    expect(fixture.nativeElement.textContent).toContain('Real topic');
    expect(fixture.nativeElement.textContent).not.toContain('DASH-142');
  });

  it('re-renders when the list changes', () => {
    render([channel({ id: 'c1', name: 'General' })]);
    expect(fixture.nativeElement.textContent).toContain('General');

    render([channel({ id: 'c2', name: 'Random' })]);

    // OnPush with a signal input: a replaced array has to repaint, or the sidebar keeps showing the
    // previous workspace's channels after a switch.
    expect(fixture.nativeElement.textContent).not.toContain('General');
    expect(fixture.nativeElement.textContent).toContain('Random');
  });
});
