import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChannelMembersPanelComponent } from './channel-members-panel.component';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ConfigService } from '../../core/services/config.service';
import { RealtimeService } from '../../services/realtime.service';
import { Subject } from 'rxjs';

describe('ChannelMembersPanelComponent', () => {
  let fixture: ComponentFixture<ChannelMembersPanelComponent>;

  beforeEach(async () => {
    const rtMock = { presenceChanged$: new Subject() };
    await TestBed.configureTestingModule({
      imports: [ChannelMembersPanelComponent],
      providers: [
        provideHttpClient(), provideHttpClientTesting(),
        { provide: ConfigService, useValue: { apiBaseUrl: '' } },
        { provide: RealtimeService, useValue: rtMock },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ChannelMembersPanelComponent);
    fixture.componentRef.setInput('channelId', 'ch-1');
    fixture.detectChanges();
  });

  it('should create', () => expect(fixture.componentInstance).toBeTruthy());
});
