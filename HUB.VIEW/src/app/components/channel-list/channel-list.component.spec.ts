import { TestBed } from '@angular/core/testing';
import { ChannelListComponent } from './channel-list.component';
import { ComponentRef } from '@angular/core';

describe('ChannelListComponent', () => {
  let ref: ComponentRef<ChannelListComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [ChannelListComponent] }).compileComponents();
    const f = TestBed.createComponent(ChannelListComponent);
    ref = f.componentRef;
    ref.setInput('channels', []);
    f.detectChanges();
  });

  it('should create', () => expect(ref.instance).toBeTruthy());
});
