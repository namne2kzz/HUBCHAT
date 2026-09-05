import { TestBed } from '@angular/core/testing';
import { ComposerSubmit, MessageInputComponent } from './message-input.component';

describe('MessageInputComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [MessageInputComponent] }).compileComponents();
  });

  it('should create', () => {
    const f = TestBed.createComponent(MessageInputComponent);
    f.detectChanges();
    expect(f.componentInstance).toBeTruthy();
  });

  it('should not emit on empty body', () => {
    const f = TestBed.createComponent(MessageInputComponent);
    f.detectChanges();
    const emitted: ComposerSubmit[] = [];
    f.componentInstance.send.subscribe((v: ComposerSubmit) => emitted.push(v));
    f.componentInstance['submit']();
    expect(emitted.length).toBe(0);
  });
});
