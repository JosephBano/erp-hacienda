import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  OnDestroy,
  inject,
  signal,
  computed,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin, of, Subscription, timer } from 'rxjs';
import { catchError } from 'rxjs/operators';
import {
  ApiService,
  MobileBuildRequestDto,
  MobileReleaseDto,
  CreateMobileBuildRequestPayload,
} from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { IconComponent } from '../../shared/icon/icon.component';

/**
 * Mobile Release Distribution Management (Spec 0014 / ADR-0035).
 * Allows authorized operators to view Android releases, trigger builds for Stage/Production,
 * inspect in-flight queue, download signed APKs via authenticated blob streaming,
 * and publish or withdraw releases.
 */
@Component({
  selector: 'app-android-apps',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent],
  templateUrl: './android-apps.component.html',
  styleUrls: ['./android-apps.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AndroidAppsComponent implements OnInit, OnDestroy {
  readonly api = inject(ApiService);
  readonly auth = inject(AuthService);

  readonly releases = signal<MobileReleaseDto[]>([]);
  readonly buildRequests = signal<MobileBuildRequestDto[]>([]);
  readonly loading = signal<boolean>(false);
  readonly generating = signal<boolean>(false);
  readonly downloadingReleaseId = signal<string | null>(null);

  readonly selectedChannelFilter = signal<string>('All');
  readonly selectedStatusFilter = signal<string>('All');

  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly partialStatusMessage = signal<string | null>(null);

  // Build request form inputs
  buildGitRef = 'develop';
  buildCommitSha = '';
  buildNotes = '';

  // Publish Modal State
  readonly showPublishModal = signal<boolean>(false);
  readonly targetReleaseForPublish = signal<MobileReleaseDto | null>(null);
  publishNotes = '';
  publishMinVersion = '';
  readonly isPublishing = signal<boolean>(false);

  // Withdraw Modal State
  readonly showWithdrawModal = signal<boolean>(false);
  readonly targetReleaseForWithdraw = signal<MobileReleaseDto | null>(null);
  withdrawReason = '';
  readonly isWithdrawing = signal<boolean>(false);

  private pollSub: Subscription | null = null;

  readonly filteredReleases = computed(() => {
    const channel = this.selectedChannelFilter();
    const status = this.selectedStatusFilter();

    return this.releases().filter((r) => {
      const matchChannel = channel === 'All' || r.channel.toLowerCase() === channel.toLowerCase();
      const matchStatus = status === 'All' || r.status.toLowerCase() === status.toLowerCase();
      return matchChannel && matchStatus;
    });
  });

  readonly latestStageRelease = computed(() => {
    return this.releases().find((r) => r.channel.toLowerCase() === 'stage') ?? null;
  });

  readonly latestProdRelease = computed(() => {
    return this.releases().find((r) => r.channel.toLowerCase() === 'production') ?? null;
  });

  readonly currentStableRelease = computed(() => {
    return this.releases().find((r) => r.isCurrentStable) ?? null;
  });

  readonly totalStorageBytes = computed(() => {
    return this.releases()
      .filter((r) => r.status.toLowerCase() !== 'pruned')
      .reduce((acc, r) => acc + (r.fileSizeBytes || 0), 0);
  });

  readonly totalStorageMb = computed(() => {
    return (this.totalStorageBytes() / (1024 * 1024)).toFixed(1);
  });

  readonly inFlightBuildRequests = computed(() => {
    return this.buildRequests().filter((r) =>
      ['pending', 'dispatched', 'building', 'importing'].includes(r.status.toLowerCase()),
    );
  });

  ngOnInit(): void {
    this.loadData();
    // Lightweight polling every 10 seconds for build status updates
    this.pollSub = timer(10000, 10000).subscribe(() => {
      if (this.inFlightBuildRequests().length > 0) {
        this.refreshRequests();
      }
    });
  }

  ngOnDestroy(): void {
    this.pollSub?.unsubscribe();
  }

  loadData(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    forkJoin({
      releases: this.api.getMobileReleases(),
      requests: this.api.getMobileBuildRequests(),
    }).subscribe({
      next: ({ releases, requests }) => {
        this.releases.set(releases);
        this.buildRequests.set(requests);
        this.loading.set(false);
      },
      error: (err) => {
        this.errorMessage.set('Error al cargar la información de aplicaciones Android.');
        this.loading.set(false);
      },
    });
  }

  refreshRequests(): void {
    this.api.getMobileBuildRequests().subscribe({
      next: (requests) => this.buildRequests.set(requests),
      error: () => {},
    });
  }

  generateStage(): void {
    const payload: CreateMobileBuildRequestPayload = {
      channel: 'Stage',
      gitRef: this.buildGitRef.trim() || 'develop',
      commitSha: this.buildCommitSha.trim() || 'HEAD',
      notes: this.buildNotes.trim() || null,
    };
    this.generating.set(true);
    this.clearMessages();

    this.api.createMobileBuildRequest(payload).subscribe({
      next: (req) => {
        this.generating.set(false);
        this.successMessage.set(`Solicitud de compilación para Stage registrada (#${req.versionCode}).`);
        this.refreshRequests();
      },
      error: (err) => {
        this.generating.set(false);
        this.errorMessage.set(this.extractErrorMessage(err, 'Error al solicitar compilación para Stage.'));
      },
    });
  }

  generateProduction(): void {
    const payload: CreateMobileBuildRequestPayload = {
      channel: 'Production',
      gitRef: this.buildGitRef.trim() || 'main',
      commitSha: this.buildCommitSha.trim() || 'HEAD',
      notes: this.buildNotes.trim() || null,
    };
    this.generating.set(true);
    this.clearMessages();

    this.api.createMobileBuildRequest(payload).subscribe({
      next: (req) => {
        this.generating.set(false);
        this.successMessage.set(`Solicitud de compilación para Producción registrada (#${req.versionCode}).`);
        this.refreshRequests();
      },
      error: (err) => {
        this.generating.set(false);
        this.errorMessage.set(this.extractErrorMessage(err, 'Error al solicitar compilación para Producción.'));
      },
    });
  }

  generateBoth(): void {
    const gitRef = this.buildGitRef.trim() || 'develop';
    const commitSha = this.buildCommitSha.trim() || 'HEAD';
    const notes = this.buildNotes.trim() || null;

    const stagePayload: CreateMobileBuildRequestPayload = {
      channel: 'Stage',
      gitRef,
      commitSha,
      notes,
    };

    const prodPayload: CreateMobileBuildRequestPayload = {
      channel: 'Production',
      gitRef: gitRef === 'develop' ? 'main' : gitRef,
      commitSha,
      notes,
    };

    this.generating.set(true);
    this.clearMessages();

    forkJoin({
      stage: this.api.createMobileBuildRequest(stagePayload).pipe(
        catchError((err) => of({ isError: true, error: err })),
      ),
      prod: this.api.createMobileBuildRequest(prodPayload).pipe(
        catchError((err) => of({ isError: true, error: err })),
      ),
    }).subscribe({
      next: ({ stage, prod }) => {
        this.generating.set(false);
        const stageFailed = (stage as any).isError;
        const prodFailed = (prod as any).isError;

        if (!stageFailed && !prodFailed) {
          this.successMessage.set('Solicitudes registradas con éxito para ambos canales (Stage y Producción).');
        } else if (!stageFailed && prodFailed) {
          const prodErr = this.extractErrorMessage((prod as any).error, 'fallo desconocido');
          this.partialStatusMessage.set(`Stage: Solicitado con éxito | Producción: Error (${prodErr})`);
        } else if (stageFailed && !prodFailed) {
          const stageErr = this.extractErrorMessage((stage as any).error, 'fallo desconocido');
          this.partialStatusMessage.set(`Producción: Solicitado con éxito | Stage: Error (${stageErr})`);
        } else {
          this.errorMessage.set('Fallaron las solicitudes para ambos canales.');
        }

        this.refreshRequests();
      },
    });
  }

  cancelBuildRequest(request: MobileBuildRequestDto): void {
    this.clearMessages();
    this.api.cancelMobileBuildRequest(request.id, 'Cancelado desde panel web').subscribe({
      next: () => {
        this.successMessage.set(`Solicitud #${request.versionCode} cancelada.`);
        this.refreshRequests();
      },
      error: (err) => {
        this.errorMessage.set(this.extractErrorMessage(err, 'No se pudo cancelar la solicitud.'));
      },
    });
  }

  downloadRelease(release: MobileReleaseDto): void {
    if (release.status.toLowerCase() === 'pruned') {
      this.errorMessage.set('El binario APK de esta versión ya fue depurado por la política de retención.');
      return;
    }

    this.clearMessages();
    this.downloadingReleaseId.set(release.id);

    this.api.downloadMobileRelease(release.id).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        const shortCommit = release.commitSha ? release.commitSha.substring(0, 7) : 'commit';
        a.download = `hato-${release.versionName}-${release.channel.toLowerCase()}-b${release.versionCode}-${shortCommit}.apk`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        window.URL.revokeObjectURL(url);
        this.downloadingReleaseId.set(null);
      },
      error: (err) => {
        this.downloadingReleaseId.set(null);
        this.errorMessage.set('Error al descargar el binario APK. Verifique sus permisos de red o acceso.');
      },
    });
  }

  // --- Publish Modal Actions ---

  openPublishModal(release: MobileReleaseDto): void {
    this.targetReleaseForPublish.set(release);
    this.publishNotes = release.releaseNotes || '';
    this.publishMinVersion = release.minimumSupportedVersion || '';
    this.showPublishModal.set(true);
  }

  closePublishModal(): void {
    this.showPublishModal.set(false);
    this.targetReleaseForPublish.set(null);
    this.publishNotes = '';
    this.publishMinVersion = '';
  }

  confirmPublish(): void {
    const rel = this.targetReleaseForPublish();
    if (!rel) return;

    this.isPublishing.set(true);
    this.clearMessages();

    this.api
      .publishMobileRelease(rel.id, {
        releaseNotes: this.publishNotes.trim() || null,
        minimumSupportedVersion: this.publishMinVersion.trim() || null,
      })
      .subscribe({
        next: (updated) => {
          this.isPublishing.set(false);
          this.closePublishModal();
          this.successMessage.set(`Versión ${updated.versionName} (${updated.channel}) publicada con éxito.`);
          this.loadData();
        },
        error: (err) => {
          this.isPublishing.set(false);
          this.errorMessage.set(this.extractErrorMessage(err, 'Error al publicar la release.'));
        },
      });
  }

  // --- Withdraw Modal Actions ---

  openWithdrawModal(release: MobileReleaseDto): void {
    this.targetReleaseForWithdraw.set(release);
    this.withdrawReason = '';
    this.showWithdrawModal.set(true);
  }

  closeWithdrawModal(): void {
    this.showWithdrawModal.set(false);
    this.targetReleaseForWithdraw.set(null);
    this.withdrawReason = '';
  }

  confirmWithdraw(): void {
    const rel = this.targetReleaseForWithdraw();
    if (!rel) return;

    if (!this.withdrawReason.trim()) {
      this.errorMessage.set('Debe especificar un motivo para retirar la versión.');
      return;
    }

    this.isWithdrawing.set(true);
    this.clearMessages();

    this.api
      .withdrawMobileRelease(rel.id, {
        reason: this.withdrawReason.trim(),
      })
      .subscribe({
        next: (updated) => {
          this.isWithdrawing.set(false);
          this.closeWithdrawModal();
          this.successMessage.set(`Versión ${updated.versionName} (${updated.channel}) retirada del servicio.`);
          this.loadData();
        },
        error: (err) => {
          this.isWithdrawing.set(false);
          this.errorMessage.set(this.extractErrorMessage(err, 'Error al retirar la release.'));
        },
      });
  }

  // --- Helper Methods ---

  formatBytes(bytes: number): string {
    if (!bytes || bytes === 0) return '0 B';
    const mb = bytes / (1024 * 1024);
    if (mb >= 1) return `${mb.toFixed(1)} MB`;
    const kb = bytes / 1024;
    return `${kb.toFixed(1)} KB`;
  }

  shortSha(sha: string): string {
    if (!sha) return '—';
    return sha.length > 7 ? sha.substring(0, 7) : sha;
  }

  formatDate(dateIso?: string | null): string {
    if (!dateIso) return '—';
    try {
      const d = new Date(dateIso);
      return d.toLocaleDateString('es-EC', {
        year: 'numeric',
        month: 'short',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
      });
    } catch {
      return dateIso;
    }
  }

  private clearMessages(): void {
    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.partialStatusMessage.set(null);
  }

  private extractErrorMessage(err: any, fallback: string): string {
    if (err?.error?.detail) return err.error.detail;
    if (err?.error?.title) return err.error.title;
    if (err?.message) return err.message;
    return fallback;
  }
}
