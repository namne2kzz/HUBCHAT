import { TestBed } from '@angular/core/testing';
import { MessageListComponent } from './message-list.component';

describe('MessageListComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [MessageListComponent] }).compileComponents();
  });

  it('should create', () => {
    const f = TestBed.createComponent(MessageListComponent);
    f.componentRef.setInput('messages', []);
    f.detectChanges();
    expect(f.componentInstance).toBeTruthy();
  });
});
