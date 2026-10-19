import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AttachmentViewComponent } from './attachment-view.component';
import { AttachmentUrlService } from '../../services/attachment-url.service';
import { AttachmentDto, AttachmentKind } from '../../models/message.model';

/**
 * Covers the attachment viewer (CF-5), especially the download bug: a file chip never renders the URL in
 * the template, so clicking it must resolve the presigned URL on demand before opening.
 */
describe('AttachmentViewComponent', () => {
  let fixture: ComponentFixture<AttachmentViewComponent>;
  let component: AttachmentViewComponent;
  let urls: jasmine.SpyObj<AttachmentUrlService>;
  let opened: string[];

  const att = (over: Partial<AttachmentDto> = {}): AttachmentDto => ({
    id: 'a1', kind: AttachmentKind.File, url: 'file-1', name: 'doc.pdf',
    size: 2048, mime: 'application/pdf', width: null, height: null, ...over,
  });

  beforeEach(() => {
    urls = jasmine.createSpyObj<AttachmentUrlService>('AttachmentUrlService', ['get']);
    TestBed.configureTestingModule({
      imports: [AttachmentViewComponent],
      providers: [{ provide: AttachmentUrlService, useValue: urls }],
    });
    opened = [];
    spyOn(window, 'open').and.callFake((url?: string | URL) => { opened.push(String(url)); return null; });
  });

  function mount(a: AttachmentDto): void {
    fixture = TestBed.createComponent(AttachmentViewComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('attachment', a);
    fixture.detectChanges();
  }

  it('renders a download chip for a non-media file', () => {
    urls.get.and.returnValue(of('')); // not read for a file chip
    mount(att());
    const chip = fixture.nativeElement.querySelector('.att-file');
    expect(chip).not.toBeNull();
    expect(chip.textContent).toContain('doc.pdf');
  });

  it('resolves the URL on demand and opens it when the file chip is clicked', () => {
    urls.get.and.returnValue(of('https://minio/file-1?sig=abc'));
    mount(att());

    (fixture.nativeElement.querySelector('.att-file') as HTMLButtonElement).click();

    expect(urls.get).toHaveBeenCalledWith('file-1');
    expect(opened).toEqual(['https://minio/file-1?sig=abc']);
  });

  it('does not open when the URL cannot be resolved', () => {
    urls.get.and.returnValue(of(''));
    mount(att());

    (fixture.nativeElement.querySelector('.att-file') as HTMLButtonElement).click();

    expect(window.open).not.toHaveBeenCalled();
  });

  it('renders an inline image for an image attachment', () => {
    urls.get.and.returnValue(of('https://minio/img'));
    mount(att({ kind: AttachmentKind.Image, name: 'pic.png', mime: 'image/png' }));

    const img = fixture.nativeElement.querySelector('.att-image img') as HTMLImageElement;
    expect(img).not.toBeNull();
    expect(img.getAttribute('src')).toBe('https://minio/img');
  });
});
