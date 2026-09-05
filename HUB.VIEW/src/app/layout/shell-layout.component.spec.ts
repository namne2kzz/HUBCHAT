import { TestBed } from '@angular/core/testing';
import { ShellLayoutComponent } from './shell-layout.component';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';

describe('ShellLayoutComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ShellLayoutComponent],
      providers: [provideRouter([]), provideHttpClient()],
    }).compileComponents();
  });

  it('should create', () => {
    const fixture = TestBed.createComponent(ShellLayoutComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });
});
