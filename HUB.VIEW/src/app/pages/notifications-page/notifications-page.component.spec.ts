import { TestBed } from '@angular/core/testing';
import { NotificationsPageComponent } from './notifications-page.component';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';

describe('NotificationsPageComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NotificationsPageComponent],
      providers: [provideHttpClient(), provideRouter([])],
    }).compileComponents();
  });

  it('should create', () => {
    const f = TestBed.createComponent(NotificationsPageComponent);
    expect(f.componentInstance).toBeTruthy();
  });
});
