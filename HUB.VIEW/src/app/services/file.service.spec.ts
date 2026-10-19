import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { FileService } from './file.service';
import { ConfigService } from '../core/services/config.service';
import { SKIP_ERROR_TOAST } from '../core/http/http-context';

/**
 * Covers FileService's contract with the Media service (CF-5): the upload ticket includes the
 * workspace/channel the server requires, and the download URL fetch opts out of the global error toast.
 * The MinIO PUT itself (fetch) is not exercised here — only the two HTTP calls we own.
 */
describe('FileService', () => {
  let service: FileService;
  let http: HttpTestingController;
  const API = 'http://localhost:5000/api/v1';

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ConfigService, useValue: { apiBaseUrl: API, dashboardUrl: '', hubUrl: '' } },
      ],
    });
    service = TestBed.inject(FileService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('requests an upload ticket with the workspace and channel', () => {
    const file = new File(['x'], 'a.png', { type: 'image/png' });
    service.upload(file, 'w1', 'c1').subscribe();

    const req = http.expectOne(`${API}/files`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(jasmine.objectContaining({
      workspaceId: 'w1', channelId: 'c1', fileName: 'a.png', contentType: 'image/png',
    }));
    // Leave it pending — the next step is a real fetch() to MinIO which the test harness can't intercept.
  });

  it('fetches a download URL and skips the error toast', () => {
    service.getDownloadUrl('f1').subscribe();

    const req = http.expectOne(`${API}/files/f1`);
    expect(req.request.method).toBe('GET');
    expect(req.request.context.get(SKIP_ERROR_TOAST)).toBeTrue();
    req.flush({ url: 'https://minio/f1', expiresInSeconds: 300 });
  });
});
