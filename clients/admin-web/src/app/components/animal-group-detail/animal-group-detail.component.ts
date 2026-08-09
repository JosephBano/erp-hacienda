import { CommonModule, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { ApiService, AnimalGroupDto, AnimalGroupSummaryDto, ChangeTrackingModeRequest, GroupMembershipDto, SpeciesDto, UpdateAnimalGroupRequest } from '../../services/api.service';
import { forkJoin } from 'rxjs';
import { IconComponent } from '../../shared/icon/icon.component';

/**
 * PR3 — `/animal-groups/:id`. Vista detalle de un grupo con:
 *  - encabezado (nombre, descripción, especie, modo, badge activo/inactivo)
 *  - resumen (cabezas vivas, última vacunación, última baja, último tratamiento,
 *    cabezas afectadas por diagnóstico) — datos del endpoint `/summary` (PR1, ADR-0023).
 *  - miembros activos + histórico (lista).
 *  - eventos del grupo (placeholder: solo cuenta).
 *  - edición inline de nombre/descripción/especie.
 *  - cambio de TrackingMode con flujo separado (warning si tiene actividad).
 *  - desactivar / reactivar con confirmación de dos pasos.
 */
@Component({
  selector: 'app-animal-group-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, IconComponent],
  templateUrl: './animal-group-detail.component.html',
  styleUrls: ['./animal-group-detail.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AnimalGroupDetailComponent implements OnInit {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  readonly group = signal<AnimalGroupDto | null>(null);
  readonly summary = signal<AnimalGroupSummaryDto | null>(null);
  readonly species = signal<SpeciesDto[]>([]);
  readonly loading = signal<boolean>(true);
  readonly notFound = signal<boolean>(false);
  readonly successMessage = signal<string>('');
  readonly errorMessage = signal<string>('');

  // --- Edición inline ---
  readonly editing = signal<boolean>(false);
  editName = '';
  editDescription = '';
  editSpeciesId = '';
  readonly editSubmitting = signal<boolean>(false);
  readonly editErrorMessage = signal<string>('');

  // --- Cambio de TrackingMode ---
  readonly requestingTrackingModeChange = signal<{ newMode: 'Individual' | 'Headcount' } | null>(null);
  readonly changingTrackingMode = signal<boolean>(false);

  // --- Desactivar / Reactivar ---
  readonly confirmingDeactivate = signal<boolean>(false);
  readonly confirmingReactivate = signal<boolean>(false);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.notFound.set(true);
      this.loading.set(false);
      return;
    }
    this.load(id);
    this.api.getSpecies().subscribe({
      next: (data) => this.species.set(data),
      error: () => this.species.set([]),
    });
  }

  private load(id: string): void {
    this.loading.set(true);
    this.notFound.set(false);
    forkJoin({
      group: this.api.getAnimalGroupById(id),
      summary: this.api.getAnimalGroupSummary(id),
    }).subscribe({
      next: ({ group, summary }) => {
        this.group.set(group);
        this.summary.set(summary);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        const status = (err as { status?: number })?.status;
        if (status === 404) {
          this.notFound.set(true);
        } else {
          this.errorMessage.set(this.errorFrom(err, 'cargar el lote'));
        }
      },
    });
  }

  reload(): void {
    const g = this.group();
    if (!g) return;
    this.load(g.id);
  }

  // --- Edición ---

  startEdit(): void {
    const g = this.group();
    if (!g) return;
    this.editName = g.name;
    this.editDescription = g.description ?? '';
    this.editSpeciesId = g.speciesId ?? '';
    this.editErrorMessage.set('');
    this.editing.set(true);
  }

  cancelEdit(): void {
    this.editing.set(false);
    this.editErrorMessage.set('');
  }

  saveEdit(): void {
    const g = this.group();
    if (!g) return;
    const name = this.editName.trim();
    if (!name) {
      this.editErrorMessage.set('El nombre no puede quedar vacío.');
      return;
    }
    this.editSubmitting.set(true);
    this.editErrorMessage.set('');

    const body: UpdateAnimalGroupRequest = {
      name,
      description: this.editDescription.trim() || null,
      speciesId: this.editSpeciesId || null,
    };
    this.api.updateAnimalGroup(g.id, body).subscribe({
      next: () => {
        this.editSubmitting.set(false);
        this.editing.set(false);
        this.successMessage.set('Lote actualizado.');
        this.reload();
      },
      error: (err: unknown) => {
        this.editSubmitting.set(false);
        const e = err as { error?: { detail?: string }; message?: string };
        this.editErrorMessage.set(e?.error?.detail ?? e?.message ?? 'No se pudo actualizar el lote.');
      },
    });
  }

  // --- Cambio de TrackingMode ---

  requestTrackingModeChange(newMode: 'Individual' | 'Headcount'): void {
    const g = this.group();
    if (!g || g.trackingMode === newMode) return;
    this.requestingTrackingModeChange.set({ newMode });
  }

  cancelTrackingModeChange(): void {
    this.requestingTrackingModeChange.set(null);
  }

  confirmTrackingModeChange(): void {
    const g = this.group();
    const req = this.requestingTrackingModeChange();
    if (!g || !req) return;

    this.changingTrackingMode.set(true);
    this.api.changeAnimalGroupTrackingMode(g.id, req.newMode).subscribe({
      next: () => {
        this.changingTrackingMode.set(false);
        this.requestingTrackingModeChange.set(null);
        this.successMessage.set('Modo de seguimiento actualizado.');
        this.reload();
      },
      error: (err: unknown) => {
        this.changingTrackingMode.set(false);
        const e = err as { error?: { detail?: string }; message?: string };
        // Mantener el panel de advertencia abierto mostrando el mensaje del backend.
        this.errorMessage.set(e?.error?.detail ?? e?.message ?? 'No se pudo cambiar el modo.');
      },
    });
  }

  // --- Desactivar / Reactivar ---

  startDeactivate(): void { this.confirmingDeactivate.set(true); }
  cancelDeactivate(): void { this.confirmingDeactivate.set(false); }
  confirmDeactivate(): void {
    const g = this.group();
    if (!g) return;
    this.api.deactivateAnimalGroup(g.id).subscribe({
      next: () => {
        this.confirmingDeactivate.set(false);
        this.successMessage.set('Lote desactivado.');
        this.reload();
      },
      error: (err: unknown) => this.errorMessage.set(this.errorFrom(err, 'desactivar el lote')),
    });
  }

  startReactivate(): void { this.confirmingReactivate.set(true); }
  cancelReactivate(): void { this.confirmingReactivate.set(false); }
  confirmReactivate(): void {
    const g = this.group();
    if (!g) return;
    this.api.activateAnimalGroup(g.id).subscribe({
      next: () => {
        this.confirmingReactivate.set(false);
        this.successMessage.set('Lote reactivado.');
        this.reload();
      },
      error: (err: unknown) => this.errorMessage.set(this.errorFrom(err, 'reactivar el lote')),
    });
  }

  // --- UI helpers ---

  goBack(): void {
    this.router.navigate(['/animal-groups']);
  }

  formatDate(iso: string | null | undefined): string {
    if (!iso) return '—';
    const d = new Date(iso);
    if (isNaN(d.getTime())) return '—';
    return d.toLocaleDateString('es-EC', { year: 'numeric', month: 'short', day: '2-digit' });
  }

  /** Texto del botón de cambio de modo (extraído para evitar ICU en el template). */
  trackingModeButtonLabel(newMode: 'Individual' | 'Headcount'): string {
    return `Cambiar a ${newMode === 'Headcount' ? 'Por conteo' : 'Individual'}`;
  }

  /** Texto del badge de estado (extraído para evitar ICU en el template). */
  statusBadgeLabel(isActive: boolean): string {
    return isActive ? 'Activo' : 'Inactivo';
  }

  /** Texto del modo de seguimiento (extraído para evitar ICU en el template). */
  trackingModeLabel(mode: 'Individual' | 'Headcount'): string {
    return mode === 'Headcount' ? 'Por conteo (sin identificar)' : 'Individual';
  }

  activeMemberships(): GroupMembershipDto[] {
    return this.group()?.memberships?.filter((m) => m.isActive) ?? [];
  }

  closedMemberships(): GroupMembershipDto[] {
    return this.group()?.memberships?.filter((m) => !m.isActive) ?? [];
  }

  trackById(_index: number, item: { id: string }): string {
    return item.id;
  }

  private errorFrom(err: unknown, what: string): string {
    const e = err as { error?: { detail?: string }; message?: string };
    return e?.error?.detail ?? e?.message ?? `No se pudo ${what}.`;
  }
}
