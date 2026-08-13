import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ApiService, AdministrationRouteDto, AnimalCategoryDto, BreedDto, FarmModuleDto, InventoryItemDto, MortalityCauseDto, SpeciesDto, TreatmentReasonDto } from '../../services/api.service';
import { CatalogTableComponent, CatalogColumn, CatalogAction } from '../../shared/catalog-table/catalog-table.component';
import { IconComponent } from '../../shared/icon/icon.component';

type TabKey = 'species' | 'breeds' | 'categories' | 'mortality' | 'routes' | 'reasons' | 'inventory' | 'modules';

interface Tab {
  key: TabKey;
  label: string;
}

/**
 * PR3 — Pantalla genérica de catálogos.
 *
 * Esta pantalla agrupa los catálogos configurables de la finca que antes sólo
 * se podían editar vía curl o SQL (BACKLOG.md "Pantallas de catálogos que
 * faltan"). Cada pestaña usa el <app-catalog-table> compartido con su lista
 * declarativa de columnas y acciones.
 *
 * El módulo "Módulos de la finca" (ADR-0019) usa una presentación distinta —
 * un card-grid con toggle on/off — porque la UX de "activar/desactivar" no
 * encaja en una tabla de filas. La pantalla lo distingue explícitamente.
 */
@Component({
  selector: 'app-catalogs',
  standalone: true,
  imports: [CommonModule, FormsModule, CatalogTableComponent, IconComponent],
  templateUrl: './catalogs.component.html',
  styleUrls: ['./catalogs.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogsComponent implements OnInit {
  private api = inject(ApiService);
  private router = inject(Router);

  readonly tabs: Tab[] = [
    { key: 'species', label: 'Especies' },
    { key: 'breeds', label: 'Razas' },
    { key: 'categories', label: 'Categorías animales' },
    { key: 'mortality', label: 'Causas de mortalidad' },
    { key: 'routes', label: 'Vías de administración' },
    { key: 'reasons', label: 'Motivos de tratamiento' },
    { key: 'inventory', label: 'Ítems de inventario' },
    { key: 'modules', label: 'Módulos de la finca' },
  ];

  readonly activeTab = signal<TabKey>('species');

  // ---- Tab state (signals). Reload after a mutation by reassigning the array.
  readonly species = signal<SpeciesDto[]>([]);
  readonly breeds = signal<BreedDto[]>([]);
  readonly categories = signal<AnimalCategoryDto[]>([]);
  readonly mortalityCauses = signal<MortalityCauseDto[]>([]);
  readonly adminRoutes = signal<AdministrationRouteDto[]>([]);
  readonly treatmentReasons = signal<TreatmentReasonDto[]>([]);
  readonly inventoryItems = signal<InventoryItemDto[]>([]);
  readonly farmModules = signal<FarmModuleDto[]>([]);

  // ---- Filters
  breedsSpeciesFilter = '';
  categoriesSpeciesFilter = '';
  inventoryCategoryFilter = '';

  // ---- Form state (the "+ Nuevo" inline form)
  newMortalityCauseName = '';
  newAdminRouteKey = '';
  newAdminRouteLabel = '';
  newTreatmentReasonKey = '';
  newTreatmentReasonLabel = '';

  // ---- Inventory item create form
  showItemForm = false;
  submittingItem = false;
  newItemName = '';
  newItemCategory: 'Feed' | 'Medicine' | 'Supply' | 'Product' = 'Feed';
  newItemUnit = '';
  newItemMinStock: number | null = null;
  newItemDescription = '';

  // ---- Feedback
  successMessage = '';
  errorMessage = '';

  // ---- Tab visibility helpers
  readonly isSpecies = computed(() => this.activeTab() === 'species');
  readonly isBreeds = computed(() => this.activeTab() === 'breeds');
  readonly isCategories = computed(() => this.activeTab() === 'categories');
  readonly isMortality = computed(() => this.activeTab() === 'mortality');
  readonly isRoutes = computed(() => this.activeTab() === 'routes');
  readonly isReasons = computed(() => this.activeTab() === 'reasons');
  readonly isInventory = computed(() => this.activeTab() === 'inventory');
  readonly isModules = computed(() => this.activeTab() === 'modules');

  // ---- Column / action lists for each tab
  readonly speciesColumns = computed<CatalogColumn<SpeciesDto>[]>(() => [
    { key: 'name', label: 'Nombre' },
    { key: 'isMilkable', label: 'Ordeñable', boolean: true },
  ]);

  readonly breedColumns: CatalogColumn<BreedDto>[] = [
    { key: 'name', label: 'Nombre' },
  ];

  readonly categoryColumns: CatalogColumn<AnimalCategoryDto>[] = [
    { key: 'name', label: 'Nombre' },
  ];

  readonly mortalityColumns: CatalogColumn<MortalityCauseDto>[] = [
    { key: 'name', label: 'Nombre' },
    { key: 'isActive', label: 'Activa', boolean: true },
  ];

  readonly routeColumns: CatalogColumn<AdministrationRouteDto>[] = [
    { key: 'key', label: 'Clave' },
    { key: 'labelEs', label: 'Etiqueta' },
    { key: 'isActive', label: 'Activa', boolean: true },
  ];

  readonly reasonColumns: CatalogColumn<TreatmentReasonDto>[] = [
    { key: 'key', label: 'Clave' },
    { key: 'labelEs', label: 'Etiqueta' },
    { key: 'isActive', label: 'Activo', boolean: true },
  ];

  readonly inventoryColumns: CatalogColumn<InventoryItemDto>[] = [
    { key: 'name', label: 'Nombre' },
    { key: 'category', label: 'Categoría' },
    { key: 'unit', label: 'Unidad' },
    { key: 'totalStock', label: 'Stock' },
  ];

  readonly inventoryActions: CatalogAction<InventoryItemDto>[] = [
    { label: 'Detalle', iconName: 'search' },
  ];

  readonly mortalityActions: CatalogAction<MortalityCauseDto>[] = [
    { label: 'Desactivar', iconName: 'close', showWhen: (r) => r.isActive },
  ];

  readonly routeActions: CatalogAction<AdministrationRouteDto>[] = [
    { label: 'Desactivar', iconName: 'close', showWhen: (r) => r.isActive },
    { label: 'Activar', iconName: 'check', showWhen: (r) => !r.isActive },
  ];

  readonly reasonActions: CatalogAction<TreatmentReasonDto>[] = [
    { label: 'Desactivar', iconName: 'close', showWhen: (r) => r.isActive },
    { label: 'Activar', iconName: 'check', showWhen: (r) => !r.isActive },
  ];

  ngOnInit(): void {
    this.loadSpecies();
  }

  setActiveTab(key: TabKey): void {
    this.activeTab.set(key);
    this.clearMessages();
    this.loadForTab(key);
  }

  private loadForTab(key: TabKey): void {
    switch (key) {
      case 'species': this.loadSpecies(); break;
      case 'breeds': this.loadBreeds(); break;
      case 'categories': this.loadCategories(); break;
      case 'mortality': this.loadMortalityCauses(); break;
      case 'routes': this.loadAdminRoutes(); break;
      case 'reasons': this.loadTreatmentReasons(); break;
      case 'inventory': this.loadInventoryItems(); break;
      case 'modules': this.loadFarmModules(); break;
    }
  }

  private loadSpecies(): void {
    this.api.getSpecies().subscribe({
      next: (data) => this.species.set(data),
      error: (err: unknown) => this.handleError(err, 'especies'),
    });
  }

  private loadBreeds(): void {
    this.api.getBreeds(this.breedsSpeciesFilter || undefined).subscribe({
      next: (data) => this.breeds.set(data),
      error: (err: unknown) => this.handleError(err, 'razas'),
    });
  }

  private loadCategories(): void {
    this.api.getAnimalCategories(this.categoriesSpeciesFilter || undefined).subscribe({
      next: (data) => this.categories.set(data),
      error: (err: unknown) => this.handleError(err, 'categorías'),
    });
  }

  private loadMortalityCauses(): void {
    this.api.getMortalityCauses(true).subscribe({
      next: (data) => this.mortalityCauses.set(data),
      error: (err: unknown) => this.handleError(err, 'causas de mortalidad'),
    });
  }

  private loadAdminRoutes(): void {
    this.api.getAdministrationRoutes(true).subscribe({
      next: (data) => this.adminRoutes.set(data),
      error: (err: unknown) => this.handleError(err, 'vías de administración'),
    });
  }

  private loadTreatmentReasons(): void {
    this.api.getTreatmentReasons(true).subscribe({
      next: (data) => this.treatmentReasons.set(data),
      error: (err: unknown) => this.handleError(err, 'motivos de tratamiento'),
    });
  }

  private loadInventoryItems(): void {
    this.api.getInventoryItems(this.inventoryCategoryFilter || undefined).subscribe({
      next: (data) => this.inventoryItems.set(data),
      error: (err: unknown) => this.handleError(err, 'ítems de inventario'),
    });
  }

  private loadFarmModules(): void {
    this.api.getFarmModules().subscribe({
      next: (data) => this.farmModules.set(data),
      error: (err: unknown) => this.handleError(err, 'módulos de la finca'),
    });
  }

  onInventoryAction(event: { action: CatalogAction<InventoryItemDto>; row: InventoryItemDto }): void {
    if (event.action.label === 'Detalle') this.router.navigate(['/inventory/items', event.row.id]);
  }

  onMortalityAction(event: { action: CatalogAction<MortalityCauseDto>; row: MortalityCauseDto }): void {
    if (event.action.label === 'Desactivar') {
      this.api.deactivateMortalityCause(event.row.id).subscribe({
        next: () => {
          this.successMessage = `Causa de mortalidad "${event.row.name}" desactivada.`;
          this.loadMortalityCauses();
        },
        error: (err: unknown) => this.handleError(err, 'desactivar la causa'),
      });
    }
  }

  onRouteAction(event: { action: CatalogAction<AdministrationRouteDto>; row: AdministrationRouteDto }): void {
    if (event.action.label === 'Desactivar') {
      this.api.deactivateAdministrationRoute(event.row.id).subscribe({
        next: () => {
          this.successMessage = `Vía "${event.row.labelEs}" desactivada.`;
          this.loadAdminRoutes();
        },
        error: (err: unknown) => this.handleError(err, 'desactivar la vía'),
      });
    } else if (event.action.label === 'Activar') {
      this.api.activateAdministrationRoute(event.row.id).subscribe({
        next: () => {
          this.successMessage = `Vía "${event.row.labelEs}" activada.`;
          this.loadAdminRoutes();
        },
        error: (err: unknown) => this.handleError(err, 'activar la vía'),
      });
    }
  }

  onReasonAction(event: { action: CatalogAction<TreatmentReasonDto>; row: TreatmentReasonDto }): void {
    if (event.action.label === 'Desactivar') {
      this.api.deactivateTreatmentReason(event.row.id).subscribe({
        next: () => {
          this.successMessage = `Motivo "${event.row.labelEs}" desactivado.`;
          this.loadTreatmentReasons();
        },
        error: (err: unknown) => this.handleError(err, 'desactivar el motivo'),
      });
    } else if (event.action.label === 'Activar') {
      this.api.activateTreatmentReason(event.row.id).subscribe({
        next: () => {
          this.successMessage = `Motivo "${event.row.labelEs}" activado.`;
          this.loadTreatmentReasons();
        },
        error: (err: unknown) => this.handleError(err, 'activar el motivo'),
      });
    }
  }

  addMortalityCause(): void {
    const name = this.newMortalityCauseName.trim();
    if (!name) {
      this.errorMessage = 'El nombre no puede estar vacío.';
      return;
    }
    this.api.createMortalityCause(name).subscribe({
      next: () => {
        this.successMessage = `Causa de mortalidad "${name}" creada.`;
        this.newMortalityCauseName = '';
        this.loadMortalityCauses();
      },
      error: (err: unknown) => this.handleError(err, 'crear la causa'),
    });
  }

  addAdminRoute(): void {
    const key = this.newAdminRouteKey.trim();
    const label = this.newAdminRouteLabel.trim();
    if (!key || !label) {
      this.errorMessage = 'La clave y la etiqueta son obligatorias.';
      return;
    }
    this.api.createAdministrationRoute({ key, labelEs: label }).subscribe({
      next: () => {
        this.successMessage = `Vía "${label}" creada.`;
        this.newAdminRouteKey = '';
        this.newAdminRouteLabel = '';
        this.loadAdminRoutes();
      },
      error: (err: unknown) => this.handleError(err, 'crear la vía'),
    });
  }

  addTreatmentReason(): void {
    const key = this.newTreatmentReasonKey.trim();
    const label = this.newTreatmentReasonLabel.trim();
    if (!key || !label) {
      this.errorMessage = 'La clave y la etiqueta son obligatorias.';
      return;
    }
    this.api.createTreatmentReason({ key, labelEs: label }).subscribe({
      next: () => {
        this.successMessage = `Motivo "${label}" creado.`;
        this.newTreatmentReasonKey = '';
        this.newTreatmentReasonLabel = '';
        this.loadTreatmentReasons();
      },
      error: (err: unknown) => this.handleError(err, 'crear el motivo'),
    });
  }

  toggleItemForm(): void {
    this.showItemForm = !this.showItemForm;
    if (this.showItemForm) {
      this.resetItemForm();
      this.errorMessage = '';
    }
  }

  private resetItemForm(): void {
    this.newItemName = '';
    this.newItemCategory = 'Feed';
    this.newItemUnit = '';
    this.newItemMinStock = null;
    this.newItemDescription = '';
  }

  createInventoryItem(): void {
    const name = this.newItemName.trim();
    const unit = this.newItemUnit.trim();
    if (!name) {
      this.errorMessage = 'El nombre del ítem es obligatorio.';
      return;
    }
    if (!unit) {
      this.errorMessage = 'La unidad base es obligatoria.';
      return;
    }
    const minStock = this.newItemMinStock ?? undefined;
    if (minStock !== undefined && minStock < 0) {
      this.errorMessage = 'El stock mínimo no puede ser negativo.';
      return;
    }
    const description = this.newItemDescription.trim() || undefined;

    this.submittingItem = true;
    this.api.createInventoryItem({
      name,
      category: this.newItemCategory,
      unit,
      minStock,
      description,
    }).subscribe({
      next: (response) => {
        this.submittingItem = false;
        this.successMessage = `Ítem "${name}" creado.`;
        this.showItemForm = false;
        this.resetItemForm();
        this.loadInventoryItems();
        // Optional: navegar al detalle del ítem recién creado para configurar conversión
        // y registrar una primera recepción sin pasos extra. Lo dejamos como decisión
        // del usuario — la tabla ya muestra el nuevo ítem.
        void response;
      },
      error: (err: unknown) => this.handleError(err, 'crear el ítem'),
    });
  }

  toggleFarmModule(module: FarmModuleDto): void {
    const next = !module.enabled;
    this.api.setFarmModuleEnabled(module.key, next).subscribe({
      next: () => {
        this.successMessage = `Módulo "${module.key}" ${next ? 'activado' : 'desactivado'}.`;
        this.loadFarmModules();
      },
      error: (err: unknown) => this.handleError(err, 'cambiar el módulo'),
    });
  }

  onBreedsFilterChange(): void {
    this.loadBreeds();
  }

  onCategoriesFilterChange(): void {
    this.loadCategories();
  }

  onInventoryFilterChange(): void {
    this.loadInventoryItems();
  }

  isInactive<T extends { isActive?: boolean }>(row: T): boolean {
    return row.isActive === false;
  }

  private clearMessages(): void {
    this.successMessage = '';
    this.errorMessage = '';
  }

  private handleError(err: unknown, what: string): void {
    const detail = (err as { error?: { detail?: string }; message?: string })?.error?.detail
      ?? (err as { message?: string })?.message
      ?? 'Error desconocido';
    this.errorMessage = `No se pudo ${what}: ${detail}`;
  }
}
