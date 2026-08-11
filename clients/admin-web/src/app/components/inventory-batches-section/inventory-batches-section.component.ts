import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService, InventoryBatchDto } from '../../services/api.service';
import { CatalogAction, CatalogColumn, CatalogTableComponent } from '../../shared/catalog-table/catalog-table.component';
import { IconComponent } from '../../shared/icon/icon.component';

@Component({
  selector: 'app-inventory-batches-section',
  standalone: true,
  imports: [CommonModule, FormsModule, CatalogTableComponent, IconComponent],
  template: `
    <div class="card-header">
      <h2>Lotes</h2>
      <button type="button" class="btn-primary btn-sm" (click)="showForm = !showForm">
        <app-icon name="plus" size="sm" ariaLabel="Crear lote"></app-icon> Crear lote
      </button>
    </div>
    <div *ngIf="showForm" class="inline-form mini-form">
      <div class="form-grid">
        <label class="form-label" for="batch-number">Número de lote</label>
        <input id="batch-number" class="form-input" [(ngModel)]="batchNumber" name="batchNumber" required>
        <label class="form-label" for="batch-quantity">Cantidad</label>
        <input id="batch-quantity" class="form-input" type="number" [(ngModel)]="quantity" name="quantity" min="0" required>
        <label class="form-label" for="batch-cost">Costo por unidad</label>
        <input id="batch-cost" class="form-input" type="number" [(ngModel)]="costPerUnit" name="costPerUnit" min="0" step="0.01" required>
        <label class="form-label" for="batch-expiration">Fecha de expiración</label>
        <input id="batch-expiration" class="form-input" type="date" [(ngModel)]="expirationDate" name="expirationDate" required>
      </div>
      <div class="form-actions">
        <button type="button" class="btn-secondary" (click)="showForm = false">Cancelar</button>
        <button type="button" class="btn-primary" (click)="create()" [disabled]="submitting">Guardar lote</button>
      </div>
    </div>
    <div class="alert alert-danger" *ngIf="errorMessage">{{ errorMessage }}</div>
    <app-catalog-table [rows]="batches" [columns]="columns" [actions]="actions" emptyMessage="No hay lotes registrados."></app-catalog-table>
  `,
  styleUrls: ['./inventory-batches-section.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InventoryBatchesSectionComponent {
  private api = inject(ApiService);
  @Input({ required: true }) itemId = '';
  @Input({ required: true }) batches: InventoryBatchDto[] = [];
  @Output() changed = new EventEmitter<void>();
  showForm = false;
  submitting = false;
  errorMessage = '';
  batchNumber = '';
  quantity: number | null = null;
  costPerUnit: number | null = null;
  expirationDate = '';
  readonly columns: CatalogColumn<InventoryBatchDto>[] = [
    { key: 'batchNumber', label: 'Número de lote' },
    { key: 'quantity', label: 'Cantidad' },
    { key: 'costPerUnit', label: 'Costo por unidad' },
    { key: 'expirationDate', label: 'Expiración', render: row => this.formatDate(row.expirationDate) },
  ];
  readonly actions: CatalogAction<InventoryBatchDto>[] = [];
  create(): void {
    if (!this.batchNumber.trim() || this.quantity === null || this.costPerUnit === null || !this.expirationDate) {
      this.errorMessage = 'Completa todos los campos del lote.';
      return;
    }
    this.submitting = true;
    this.api.createInventoryBatch(this.itemId, {
      BatchNumber: this.batchNumber.trim(), Quantity: this.quantity, CostPerUnit: this.costPerUnit, ExpirationDate: this.expirationDate,
    }).subscribe({ next: () => { this.submitting = false; this.showForm = false; this.reset(); this.changed.emit(); }, error: () => { this.submitting = false; this.errorMessage = 'No se pudo crear el lote.'; } });
  }
  private reset(): void { this.batchNumber = ''; this.quantity = null; this.costPerUnit = null; this.expirationDate = ''; this.errorMessage = ''; }
  private formatDate(value?: string | null): string { if (!value) return '—'; return new Date(value).toLocaleDateString('es-EC'); }
}
