import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AndroidAppsComponent } from './android-apps.component';
import {
  ApiService,
  MobileBuildRequestDto,
  MobileReleaseDto,
} from '../../services/api.service';
import { AuthService } from '../../services/auth.service';

describe('AndroidAppsComponent', () => {
  const sampleReleases: MobileReleaseDto[] = [
    {
      id: 'rel-1',
      buildRequestId: 'req-1',
      channel: 'Stage',
      versionName: '1.2.0',
      versionCode: 45,
      commitSha: 'a1b2c3d4e5f6789012345678901234567890abcd',
      packageName: 'com.joemandev.hatofieldapp.stage',
      applicationId: 'com.joemandev.hatofieldapp.stage',
      sha256Digest: 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855',
      fileSizeBytes: 45000000,
      storagePath: '/var/hato/artifacts/stage/hato-1.2.0-stage-b45-a1b2c3d.apk',
      status: 'Available',
      isCurrentStable: false,
      isLastGood: false,
      publishedAtUtc: '2026-09-29T10:00:00Z',
      publishedBy: 'admin@hato.ec',
    },
    {
      id: 'rel-2',
      buildRequestId: 'req-2',
      channel: 'Production',
      versionName: '1.1.0',
      versionCode: 40,
      commitSha: 'c3d4e5f6789012345678901234567890abcdef12',
      packageName: 'com.joemandev.hatofieldapp',
      applicationId: 'com.joemandev.hatofieldapp',
      sha256Digest: 'f4c8996fb92427ae41e4649b934ca495991b7852b855e3b0c44298fc1c149afb',
      fileSizeBytes: 44000000,
      storagePath: '/var/hato/artifacts/prod/hato-1.1.0-prod-b40-c3d4e5f.apk',
      status: 'Active',
      isCurrentStable: true,
      isLastGood: true,
      publishedAtUtc: '2026-09-28T14:00:00Z',
      publishedBy: 'admin@hato.ec',
    },
  ];

  const sampleRequests: MobileBuildRequestDto[] = [
    {
      id: 'req-1',
      channel: 'Stage',
      gitRef: 'develop',
      commitSha: 'a1b2c3d4e5f6789012345678901234567890abcd',
      attempt: 1,
      versionCode: 45,
      versionName: '1.2.0',
      status: 'Completed',
      requestedBy: 'admin@hato.ec',
      requestedAtUtc: '2026-09-29T09:50:00Z',
      gitHubRunId: 123456,
      gitHubRunUrl: 'https://github.com/org/repo/actions/runs/123456',
    },
  ];

  let apiStub: Partial<ApiService>;
  let authStub: Partial<AuthService>;

  beforeEach(async () => {
    apiStub = {
      getMobileReleases: () => of(sampleReleases),
      getMobileBuildRequests: () => of(sampleRequests),
      createMobileBuildRequest: (payload) =>
        of({
          id: 'req-new',
          channel: payload.channel,
          gitRef: payload.gitRef,
          commitSha: payload.commitSha,
          attempt: 1,
          versionCode: 50,
          versionName: '1.3.0',
          status: 'Pending',
          requestedBy: 'test@hato.ec',
          requestedAtUtc: new Date().toISOString(),
        }),
      cancelMobileBuildRequest: () =>
        of({
          id: 'req-1',
          channel: 'Stage',
          gitRef: 'develop',
          commitSha: 'a1b2c3d',
          attempt: 1,
          versionCode: 45,
          versionName: '1.2.0',
          status: 'Canceled',
          requestedBy: 'admin@hato.ec',
          requestedAtUtc: '2026-09-29T09:50:00Z',
        }),
      downloadMobileRelease: () => of(new Blob(['fake-apk-binary'], { type: 'application/vnd.android.package-archive' })),
      publishMobileRelease: (id, payload) =>
        of({
          ...sampleReleases[0],
          status: 'Active',
          releaseNotes: payload.releaseNotes,
        }),
      withdrawMobileRelease: (id, payload) =>
        of({
          ...sampleReleases[1],
          status: 'Withdrawn',
          withdrawnReason: payload.reason,
        }),
    };

    authStub = {
      hasPermission: (code: string) => true, // default all permissions granted
    };

    await TestBed.configureTestingModule({
      imports: [AndroidAppsComponent],
      providers: [
        provideRouter([]),
        { provide: ApiService, useValue: apiStub },
        { provide: AuthService, useValue: authStub },
      ],
    }).compileComponents();
  });

  it('mounts and renders summary cards and releases table', () => {
    const fixture = TestBed.createComponent(AndroidAppsComponent);
    fixture.detectChanges();

    const titleEl = fixture.nativeElement.querySelector('.page-title');
    expect(titleEl?.textContent).toContain('Aplicaciones Android');

    const rows = fixture.nativeElement.querySelectorAll('.releases-card tbody tr');
    expect(rows.length).toBe(2);
  });

  it('renders no emoji glyphs anywhere', () => {
    const fixture = TestBed.createComponent(AndroidAppsComponent);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    const emojiPattern = /[\u{1F000}-\u{1FAFF}\u{2600}-\u{27BF}]/u;
    expect(emojiPattern.test(text)).toBe(false);
  });

  it('filters releases by channel', () => {
    const fixture = TestBed.createComponent(AndroidAppsComponent);
    const comp = fixture.componentInstance;
    fixture.detectChanges();

    comp.selectedChannelFilter.set('Stage');
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('.releases-card tbody tr');
    expect(rows.length).toBe(1);
    expect(rows[0].textContent).toContain('Stage');
    expect(rows[0].textContent).toContain('1.2.0');
  });

  it('executes streaming download when Download button is clicked without token in URL', () => {
    const fixture = TestBed.createComponent(AndroidAppsComponent);
    const comp = fixture.componentInstance;
    fixture.detectChanges();

    const createObjectURLSpy = vi.spyOn(window.URL, 'createObjectURL').mockReturnValue('blob:mock-url');
    const revokeObjectURLSpy = vi.spyOn(window.URL, 'revokeObjectURL').mockImplementation(() => {});

    comp.downloadRelease(sampleReleases[0]);

    expect(createObjectURLSpy).toHaveBeenCalled();
    expect(revokeObjectURLSpy).toHaveBeenCalledWith('blob:mock-url');

    createObjectURLSpy.mockRestore();
    revokeObjectURLSpy.mockRestore();
  });

  it('handles partial status reporting when Generar Ambas has one success and one failure', () => {
    const fixture = TestBed.createComponent(AndroidAppsComponent);
    const comp = fixture.componentInstance;
    fixture.detectChanges();

    let callCount = 0;
    apiStub.createMobileBuildRequest = (payload) => {
      callCount++;
      if (payload.channel === 'Stage') {
        return of({
          id: 'req-stage',
          channel: 'Stage',
          gitRef: 'develop',
          commitSha: 'HEAD',
          attempt: 1,
          versionCode: 51,
          versionName: '1.2.1',
          status: 'Pending',
          requestedBy: 'user@hato.ec',
          requestedAtUtc: new Date().toISOString(),
        });
      } else {
        return throwError(() => ({
          error: { detail: 'Workflow dispatch failed: branch not found' },
        }));
      }
    };

    comp.generateBoth();
    fixture.detectChanges();

    expect(comp.partialStatusMessage()).toContain('Stage: Solicitado con éxito');
    expect(comp.partialStatusMessage()).toContain('Producción: Error');
  });

  it('opens publish modal and publishes a release with release notes', () => {
    const fixture = TestBed.createComponent(AndroidAppsComponent);
    const comp = fixture.componentInstance;
    fixture.detectChanges();

    comp.openPublishModal(sampleReleases[0]);
    fixture.detectChanges();

    expect(comp.showPublishModal()).toBe(true);

    comp.publishNotes = 'Corrección crítica de ordeño';
    comp.confirmPublish();
    fixture.detectChanges();

    expect(comp.showPublishModal()).toBe(false);
    expect(comp.successMessage()).toContain('publicada con éxito');
  });

  it('opens withdraw modal and withdraws a release requiring a reason', () => {
    const fixture = TestBed.createComponent(AndroidAppsComponent);
    const comp = fixture.componentInstance;
    fixture.detectChanges();

    comp.openWithdrawModal(sampleReleases[1]);
    fixture.detectChanges();

    expect(comp.showWithdrawModal()).toBe(true);

    // Empty reason should fail
    comp.withdrawReason = '';
    comp.confirmWithdraw();
    expect(comp.errorMessage()).toContain('Debe especificar un motivo');

    // Valid reason
    comp.withdrawReason = 'Regresión detectada en sincronización';
    comp.confirmWithdraw();
    fixture.detectChanges();

    expect(comp.showWithdrawModal()).toBe(false);
    expect(comp.successMessage()).toContain('retirada del servicio');
  });

  it('hides build action card if user lacks delivery.builds.manage permission', () => {
    authStub.hasPermission = (code: string) => code !== 'delivery.builds.manage';

    const fixture = TestBed.createComponent(AndroidAppsComponent);
    fixture.detectChanges();

    const buildForm = fixture.nativeElement.querySelector('.form-card');
    expect(buildForm).toBeNull();
  });
});
