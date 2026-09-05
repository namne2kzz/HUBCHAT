import { TestBed } from '@angular/core/testing';
import { SignedOutPageComponent } from './signed-out-page.component';
import { provideHttpClient } from '@angular/common/http';

describe('SignedOutPageComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SignedOutPageComponent],
      providers: [provideHttpClient()],
    }).compileComponents();
  });

  it('should create', () => {
    const f = TestBed.createComponent(SignedOutPageComponent);
    expect(f.componentInstance).toBeTruthy();
  });
});
