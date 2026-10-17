import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ChannelInfoPanelComponent } from './channel-info-panel.component';
import { ChannelService } from '../../services/channel.service';
import { ConfirmDialogService } from '../../services/confirm-dialog.service';
import { ChannelDto, ChannelMemberRole, ChannelType } from '../../models/channel.model';

/**
 * Covers the archive controls on the info modal (CF-13). The point under test is who may archive and
 * that the destructive direction is gated behind a confirm, while reopening is not — archiving hides a
 * channel from the team, reopening is harmless.
 */
describe('ChannelInfoPanelComponent — archive', () => {
  let fixture: ComponentFixture<ChannelInfoPanelComponent>;
  let component: ChannelInfoPanelComponent;
  let channelSvc: jasmine.SpyObj<ChannelService>;
  let confirm: jasmine.SpyObj<ConfirmDialogService>;

  const channel = (over: Partial<ChannelDto> = {}): ChannelDto => ({
    id: 'c1', workspaceId: 'w1', name: 'General', slug: 'general', type: ChannelType.Public,
    topic: '', isPrivate: false, isArchived: false, memberCount: 3,
    createdAt: new Date().toISOString(), linkType: null, linkExternalKey: null, linkUrl: '',
    isMember: true, otherUserId: null, myRole: ChannelMemberRole.Owner, ...over,
  } as ChannelDto);

  function mount(ch: ChannelDto): void {
    fixture = TestBed.createComponent(ChannelInfoPanelComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('channel', ch);
    fixture.componentRef.setInput('channelId', ch.id);
    fixture.componentRef.setInput('currentUserId', 'u1');
    fixture.detectChanges();
  }

  beforeEach(() => {
    channelSvc = jasmine.createSpyObj<ChannelService>('ChannelService', ['archive', 'unarchive']);
    confirm = jasmine.createSpyObj<ConfirmDialogService>('ConfirmDialogService', ['confirm']);

    TestBed.configureTestingModule({
      imports: [ChannelInfoPanelComponent],
      providers: [
        { provide: ChannelService, useValue: channelSvc },
        { provide: ConfirmDialogService, useValue: confirm },
      ],
    });
  });

  describe('canArchive', () => {
    it('is true for an owner', () => {
      mount(channel({ myRole: ChannelMemberRole.Owner }));
      expect((component as unknown as { canArchive: boolean }).canArchive).toBeTrue();
    });

    it('is true for an admin', () => {
      mount(channel({ myRole: ChannelMemberRole.Admin }));
      expect((component as unknown as { canArchive: boolean }).canArchive).toBeTrue();
    });

    it('is false for a plain member', () => {
      mount(channel({ myRole: ChannelMemberRole.Member }));
      expect((component as unknown as { canArchive: boolean }).canArchive).toBeFalse();
    });

    it('is false when the role is unknown', () => {
      mount(channel({ myRole: null }));
      expect((component as unknown as { canArchive: boolean }).canArchive).toBeFalse();
    });
  });

  describe('toggleArchive', () => {
    it('confirms, archives, and emits when the user agrees', async () => {
      confirm.confirm.and.resolveTo(true);
      channelSvc.archive.and.returnValue(of(void 0));
      mount(channel({ isArchived: false }));
      const emitted: boolean[] = [];
      component.archivedChanged.subscribe(v => emitted.push(v));

      await (component as unknown as { toggleArchive(): Promise<void> }).toggleArchive();

      expect(confirm.confirm).toHaveBeenCalled();
      expect(channelSvc.archive).toHaveBeenCalledWith('c1');
      expect(emitted).toEqual([true]);
    });

    it('does nothing when the user cancels the confirm', async () => {
      confirm.confirm.and.resolveTo(false);
      mount(channel({ isArchived: false }));
      const emitted: boolean[] = [];
      component.archivedChanged.subscribe(v => emitted.push(v));

      await (component as unknown as { toggleArchive(): Promise<void> }).toggleArchive();

      expect(channelSvc.archive).not.toHaveBeenCalled();
      expect(emitted).toEqual([]);
    });

    it('reopens without a confirm and emits false', async () => {
      channelSvc.unarchive.and.returnValue(of(void 0));
      mount(channel({ isArchived: true }));
      const emitted: boolean[] = [];
      component.archivedChanged.subscribe(v => emitted.push(v));

      await (component as unknown as { toggleArchive(): Promise<void> }).toggleArchive();

      expect(confirm.confirm).not.toHaveBeenCalled();
      expect(channelSvc.unarchive).toHaveBeenCalledWith('c1');
      expect(emitted).toEqual([false]);
    });

    it('clears the in-flight flag and does not emit when the call fails', async () => {
      confirm.confirm.and.resolveTo(true);
      channelSvc.archive.and.returnValue(throwError(() => new Error('boom')));
      mount(channel({ isArchived: false }));
      const emitted: boolean[] = [];
      component.archivedChanged.subscribe(v => emitted.push(v));

      await (component as unknown as { toggleArchive(): Promise<void> }).toggleArchive();

      expect(emitted).toEqual([]);
      expect((component as unknown as { archiving(): boolean }).archiving()).toBeFalse();
    });
  });
});
