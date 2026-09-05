import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CreateChannelDialogComponent } from './create-channel-dialog.component';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { ConfigService } from '../../core/services/config.service';

describe('CreateChannelDialogComponent', () => {
  let fixture: ComponentFixture<CreateChannelDialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CreateChannelDialogComponent],
      providers: [
        provideHttpClient(), provideHttpClientTesting(),
        { provide: ConfigService, useValue: { apiBaseUrl: '' } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CreateChannelDialogComponent);
    fixture.componentRef.setInput('workspaceId', 'ws-1');
    fixture.detectChanges();
  });

  it('should create', () => expect(fixture.componentInstance).toBeTruthy());
});
