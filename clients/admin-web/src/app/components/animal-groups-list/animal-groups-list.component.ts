import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { ApiService, AnimalGroupDto } from '../../services/api.service';
import { CatalogTableComponent, CatalogColumn, CatalogAction } from '../../shared/catalog-table/catalog-table.component';
import { IconComponent } from '../../shared/icon/icon.component';

/**
 * PR3 (ADR-0025) — Pantalla dedicada `/animal-groups`.
 *
 * Lista de grupos/lotes con su `LiveHeadCount` y `SpeciesName` (server-side,
 * PR1). Las acciones por fila son:
 *  - "Editar": navega al detalle (que tiene su propio flujo de edición).
 *  - "Desactivar" / "Reactivar": flujo de dos pasos inline, mismo patrón que
 *    `CatalogsComponent` para catálogos: sin modal nuevo (queda en backlog).
 */
@Component({
  selector: 'app-animal-groups-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, CatalogTableComponent, IconComponent],
  templateUrl: './animal-groups-list.component.html',
  styleUrls: ['./animal-groups-list.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AnimalGroupsListComponent implements OnInit {
  private api = inject(ApiService);
  private router = inject(Router);

  readonly groups = signal<AnimalGroupDto[]>([]);
  readonly includeInactive = signal<boolean>(false);
  readonly loading = signal<boolean>(false);
  readonly loadError = signal<boolean>(false);
  readonly successMessage = signal<string>('');
  readonly errorMessage = signal<string>('');

  // Two-step deactivate/reactivate confirmation (mirrors the CatalogComponent pattern;
  // a reusable ConfirmDialogComponent is on the backlog).
  readonly confirmingDeactivateId = signal<string | null>(null);
  readonly confirmingReactivateId = signal<string | null>(null);

  readonly columns = computed<CatalogColumn<AnimalGroupDto>[]>(() => [
    { key: 'name', label: 'Nombre' },
    { key: 'speciesName', label: 'Especie', render: (g) => g.speciesName ?? '—' },
    {
      key: 'trackingMode',
      label: 'Modo',
      render: (g) => g.trackingMode === 'Headcount' ? 'Por conteo' : 'Individual',
    },
    {
      key: 'liveHeadCount',
      label: 'Cabezas vivas',
      render: (g) => g.liveHeadCount == null ? '—' : String(g.liveHeadCount),
    },
    { key: 'isActive', label: 'Activo', boolean: true },
  ]);

  readonly actions: CatalogAction<AnimalGroupDto>[] = [
    { label: 'Editar', iconName: 'edit' },
    {
      label: 'Desactivar',
      iconName: 'close',
      showWhen: (g) => g.isActive,
    },
    {
      label: 'Reactivar',
      iconName: 'check',
      showWhen: (g) => !g.isActive,
    },
  ];

  ngOnInit(): void {
    this.loadGroups();
  }

  loadGroups(): void {
    this.loading.set(true);
    this.loadError.set(false);
    this.api.getAnimalGroups(this.includeInactive()).subscribe({
      next: (data) => {
        this.groups.set(data);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loadError.set(true);
        this.loading.set(false);
        this.errorMessage.set(this.errorFrom(err, 'cargar los lotes'));
      },
    });
  }

  onIncludeInactiveChange(value: boolean): void {
    this.includeInactive.set(value);
    this.loadGroups();
  }

  goNew(): void {
    this.router.navigate(['/animal-groups/new']);
  }

  isInactive = (g: AnimalGroupDto): boolean => !g.isActive;

  onAction(event: { action: CatalogAction<AnimalGroupDto>; row: AnimalGroupDto }): void {
    switch (event.action.label) {
      case 'Editar':
        this.router.navigate(['/animal-groups', event.row.id]);
        break;
      case 'Desactivar':
        this.confirmingDeactivateId.set(event.row.id);
        break;
      case 'Reactivar':
        this.confirmingReactivateId.set(event.row.id);
        break;
    }
  }

  cancelDeactivate(): void {
    this.confirmingDeactivateId.set(null);
  }

  cancelReactivate(): void {
    this.confirmingReactivateId.set(null);
  }

  confirmDeactivate(): void {
    const id = this.confirmingDeactivateId();
    if (!id) return;
    this.api.deactivateAnimalGroup(id).subscribe({
      next: () => {
        this.confirmingDeactivateId.set(null);
        this.successMessage.set('Lote desactivado.');
        this.loadGroups();
      },
      error: (err: unknown) => this.errorMessage.set(this.errorFrom(err, 'desactivar el lote')),
    });
  }

  confirmReactivate(): void {
    const id = this.confirmingReactivateId();
    if (!id) return;
    this.api.activateAnimalGroup(id).subscribe({
      next: () => {
        this.confirmingReactivateId.set(null);
        this.successMessage.set('Lote reactivado.');
        this.loadGroups();
      },
      error: (err: unknown) => this.errorMessage.set(this.errorFrom(err, 'reactivar el lote')),
    });
  }

  private errorFrom(err: unknown, what: string): string {
    const e = err as { error?: { detail?: string }; message?: string };
    return e?.error?.detail ?? e?.message ?? `No se pudo ${what}.`;
  }
}
