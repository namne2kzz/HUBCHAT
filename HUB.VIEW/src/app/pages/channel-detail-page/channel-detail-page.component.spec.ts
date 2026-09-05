import { TestBed } from '@angular/core/testing';
import { ChannelDetailPageComponent } from './channel-detail-page.component';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';

describe('ChannelDetailPageComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ChannelDetailPageComponent],
      providers: [
        provideHttpClient(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'test-id' } } } },
      ],
    }).compileComponents();
  });

  it('should create', () => {
    const f = TestBed.createComponent(ChannelDetailPageComponent);
    expect(f.componentInstance).toBeTruthy();
  });
});
