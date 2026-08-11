import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService, InventoryUnitConversionDto } from '../../services/api.service';
import { CatalogAction, CatalogColumn, CatalogTableComponent } from '../../shared/catalog-table/catalog-table.component';
import { IconComponent } from '../../shared/icon/icon.component';

@Component({
  selector: 'app-inventory-unit-conversions-section',
  standalone: true,
  imports: [CommonModule, FormsModule, CatalogTableComponent, IconComponent],
  template: `
    <div class="card-header"><h2>Conversiones de unidad</h2><button type="button" class="btn-primary btn-sm" (click)="showForm = !showForm"><app-icon name="plus" size="sm" ariaLabel="Crear conversión"></app-icon> Crear conversión</button></div>
    <div *ngIf="showForm" class="inline-form mini-form"><div class="form-grid">
      <label class="form-label" for="from-unit">Unidad origen</label><input id="from-unit" class="form-input" [(ngModel)]="fromUnit" name="fromUnit" required>
      <label class="form-label" for="to-unit">Unidad destino</label><input id="to-unit" class="form-input" [(ngModel)]="toUnit" name="toUnit" required>
      <label class="form-label" for="factor">Factor</label><input id="factor" class="form-input" type="number" [(ngModel)]="factor" name="factor" min="0" step="0.0001" required>
    </div><div class="form-actions"><button type="button" class="btn-secondary" (click)="showForm = false">Cancelar</button><button type="button" class="btn-primary" (click)="create()" [disabled]="submitting">Guardar conversión</button></div></div>
    <div class="alert alert-danger" *ngIf="errorMessage">{{ errorMessage }}</div>
    <app-catalog-table [rows]="conversions" [columns]="columns" [actions]="actions" emptyMessage="No hay conversiones registradas."></app-catalog-table>
  `,
  styleUrls: ['./inventory-unit-conversions-section.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InventoryUnitConversionsSectionComponent {
  private api = inject(ApiService);
  @Input({ required: true }) itemId = '';
  @Input({ required: true }) conversions: InventoryUnitConversionDto[] = [];
  @Output() changed = new EventEmitter<void>();
  showForm = false; submitting = false; errorMessage = ''; fromUnit = ''; toUnit = ''; factor: number | null = null;
  readonly columns: CatalogColumn<InventoryUnitConversionDto>[] = [{ key: 'fromUnit', label: 'Desde' }, { key: 'toUnit', label: 'Hacia' }, { key: 'factor', label: 'Factor' }];
  readonly actions: CatalogAction<InventoryUnitConversionDto>[] = [];
  create(): void { if (!this.fromUnit.trim() || !this.toUnit.trim() || this.factor === null || this.factor <= 0) { this.errorMessage = 'Completa los datos de la conversión.'; return; } this.submitting = true; this.api.registerInventoryUnitConversion(this.itemId, { FromUnit: this.fromUnit.trim(), ToUnit: this.toUnit.trim(), Factor: this.factor }).subscribe({ next: () => { this.submitting = false; this.showForm = false; this.fromUnit = ''; this.toUnit = ''; this.factor = null; this.errorMessage = ''; this.changed.emit(); }, error: () => { this.submitting = false; this.errorMessage = 'No se pudo crear la conversión.'; } }); }
}
