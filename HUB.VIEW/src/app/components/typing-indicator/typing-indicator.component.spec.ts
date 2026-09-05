import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TypingIndicatorComponent } from './typing-indicator.component';
import { RealtimeService } from '../../services/realtime.service';
import { Subject } from 'rxjs';

describe('TypingIndicatorComponent', () => {
  let fixture: ComponentFixture<TypingIndicatorComponent>;

  beforeEach(async () => {
    const rtMock = { typingStarted$: new Subject(), typingStopped$: new Subject(), presenceChanged$: new Subject() };
    await TestBed.configureTestingModule({
      imports: [TypingIndicatorComponent],
      providers: [{ provide: RealtimeService, useValue: rtMock }],
    }).compileComponents();

    fixture = TestBed.createComponent(TypingIndicatorComponent);
    fixture.componentRef.setInput('channelId', 'ch-1');
    fixture.detectChanges();
  });

  it('should create', () => expect(fixture.componentInstance).toBeTruthy());
});
