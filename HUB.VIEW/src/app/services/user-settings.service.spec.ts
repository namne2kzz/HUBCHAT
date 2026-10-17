import { TestBed } from '@angular/core/testing';
import { UserSettingsService } from './user-settings.service';
import { RealtimeService } from './realtime.service';

/**
 * Covers the CF-9 notification rules: "turn off until…" (mutedUntil), and "only when mentioned" — the one
 * exception that still notifies while muted. shouldNotify is the gate the shell uses for sound + badge.
 */
describe('UserSettingsService notifications', () => {
  let service: UserSettingsService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        // Realtime is only used to sync presence; a stub keeps the service isolated.
        { provide: RealtimeService, useValue: { connectionState: () => 'disconnected', setStatus: () => Promise.resolve() } },
      ],
    });
    service = TestBed.inject(UserSettingsService);
  });

  it('notifies normally when nothing is muted', () => {
    expect(service.muted()).toBeFalse();
    expect(service.shouldNotify(false)).toBeTrue();
    expect(service.shouldNotify(true)).toBeTrue();
  });

  describe('turn off until…', () => {
    it('mutes for a preset duration and reports the pause as active', () => {
      service.muteFor('1h');
      expect(service.mutedUntilActive()).toBeTrue();
      expect(service.muted()).toBeTrue();
      expect(service.shouldNotify(false)).toBeFalse();
    });

    it('unmute clears the pause', () => {
      service.muteFor('4h');
      service.unmute();
      expect(service.mutedUntilActive()).toBeFalse();
      expect(service.muted()).toBeFalse();
    });

    it('persists the mute to storage so a fresh load stays paused', () => {
      service.muteFor('forever');
      // loadPrefs reads this key on construction, so a reload would come back muted.
      expect(JSON.parse(localStorage.getItem('hub.notifPrefs')!).mutedUntilMs).toBeGreaterThan(Date.now());
    });
  });

  describe('only when mentioned', () => {
    beforeEach(() => service.muteFor('1h')); // muted

    it('stays silent for a non-mention while muted', () => {
      service.setOnlyWhenMentioned(true);
      expect(service.shouldNotify(false)).toBeFalse();
    });

    it('still notifies a mention while muted when the option is on', () => {
      service.setOnlyWhenMentioned(true);
      // The whole point of CF-9: muted everything, but an @mention still breaks through.
      expect(service.shouldNotify(true)).toBeTrue();
    });

    it('silences even mentions when the option is off', () => {
      service.setOnlyWhenMentioned(false);
      expect(service.shouldNotify(true)).toBeFalse();
    });
  });

  describe('shouldSurfaceNotification (panel + badge gate)', () => {
    it('surfaces everything when not muted', () => {
      expect(service.shouldSurfaceNotification(false)).toBeTrue();
      expect(service.shouldSurfaceNotification(true)).toBeTrue();
    });

    it('drops a non-mention notification while muted (the reported bug: config off must silence)', () => {
      service.muteFor('1h');
      service.setOnlyWhenMentioned(false);
      expect(service.shouldSurfaceNotification(false)).toBeFalse();
      expect(service.shouldSurfaceNotification(true)).toBeFalse(); // option off → even mentions silent
    });

    it('lets a mention/DM through while muted only when the option is on', () => {
      service.muteFor('1h');
      service.setOnlyWhenMentioned(true);
      expect(service.shouldSurfaceNotification(true)).toBeTrue();   // Mention / DM
      expect(service.shouldSurfaceNotification(false)).toBeFalse(); // System stays silent
    });
  });
});
