import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AttachmentUrlService } from './attachment-url.service';
import { FileService } from './file.service';

/**
 * Covers AttachmentUrlService (CF-5): it caches presigned URLs per file id so a message's image doesn't
 * refetch on every change detection, and it degrades to '' when a URL can't be resolved.
 */
describe('AttachmentUrlService', () => {
  let service: AttachmentUrlService;
  let files: jasmine.SpyObj<FileService>;

  beforeEach(() => {
    files = jasmine.createSpyObj<FileService>('FileService', ['getDownloadUrl']);
    TestBed.configureTestingModule({
      providers: [AttachmentUrlService, { provide: FileService, useValue: files }],
    });
    service = TestBed.inject(AttachmentUrlService);
  });

  it('resolves and caches a URL, calling the API only once', (done) => {
    files.getDownloadUrl.and.returnValue(of({ url: 'https://minio/f1', expiresInSeconds: 300 }));

    service.get('f1').subscribe(url1 => {
      expect(url1).toBe('https://minio/f1');
      service.get('f1').subscribe(url2 => {
        expect(url2).toBe('https://minio/f1');
        expect(files.getDownloadUrl).toHaveBeenCalledTimes(1); // second read served from cache
        done();
      });
    });
  });

  it('returns an empty string when resolution fails', (done) => {
    files.getDownloadUrl.and.returnValue(throwError(() => new Error('404')));
    service.get('bad').subscribe(url => {
      expect(url).toBe('');
      done();
    });
  });
});
