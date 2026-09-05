import { TestBed } from '@angular/core/testing';
import { ChannelsPageComponent } from './channels-page.component';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';

describe('ChannelsPageComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ChannelsPageComponent],
      providers: [provideHttpClient(), provideRouter([])],
    }).compileComponents();
  });

  it('should create', () => {
    const f = TestBed.createComponent(ChannelsPageComponent);
    expect(f.componentInstance).toBeTruthy();
  });
});
