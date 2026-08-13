import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService, InventoryBatchDto, RecordInventoryReceptionRequest } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { CatalogAction, CatalogColumn, CatalogTableComponent } from '../../shared/catalog-table/catalog-table.component';
import { IconComponent } from '../../shared/icon/icon.component';

// ADR-0026 Decisión 6: el botón "Crear lote" se reemplaza por "Recibir alimento"
// con los campos de trazabilidad de recepción (fecha, proveedor, factura, autor).
// El banner amarillo arriba documenta la vida útil declarada (será reemplazado por
// "Recibir orden de compra" cuando llegue Purchasing — Fase 4).
//
// Las columnas adicionales de la tabla (`receivedAt`, `supplierLabel`, badge "sin
// declaración completa") llegan en el commit siguiente; acá dejamos intacta la tabla
// existente para no romper la revisión incremental.
@Component({
  selector: 'app-inventory-batches-section',
  standalone: true,
  imports: [CommonModule, FormsModule, CatalogTableComponent, IconComponent],
  template: `
    <div class="deprecation-banner" role="status">
      <strong>Cuando llegue Purchasing (Fase 4),</strong> este flujo se reemplazará por "Recibir orden de compra". Por ahora, registra aquí las entradas de alimento al inventario.
    </div>
    <div class="card-header">
      <h2>Lotes</h2>
      <button type="button" class="btn-primary btn-sm" (click)="toggleForm()">
        <app-icon name="plus" size="sm" ariaLabel="Recibir alimento"></app-icon> Recibir alimento
      </button>
    </div>
    <div *ngIf="showForm" class="inline-form mini-form">
      <div class="form-grid">
        <label class="form-label" for="reception-batch-number">Número de lote</label>
        <input id="reception-batch-number" class="form-input" [(ngModel)]="batchNumber" name="batchNumber" maxlength="50" required>

        <label class="form-label" for="reception-quantity">Cantidad</label>
        <input id="reception-quantity" class="form-input" type="number" [(ngModel)]="quantity" name="quantity" min="0.001" step="0.001" required>

        <label class="form-label" for="reception-unit">Unidad</label>
        <input id="reception-unit" class="form-input" [(ngModel)]="unit" name="unit" maxlength="20" required>

        <label class="form-label" for="reception-cost">Costo por unidad</label>
        <input id="reception-cost" class="form-input" type="number" [(ngModel)]="costPerUnit" name="costPerUnit" min="0" step="0.01" required>

        <label class="form-label" for="reception-expiration">Fecha de expiración (opcional)</label>
        <input id="reception-expiration" class="form-input" type="date" [(ngModel)]="expirationDate" name="expirationDate">

        <label class="form-label" for="reception-received-at">Fecha de recepción</label>
        <input id="reception-received-at" class="form-input" type="datetime-local" [(ngModel)]="receivedAtLocal" name="receivedAtLocal" required>

        <label class="form-label" for="reception-supplier">Proveedor (texto libre)</label>
        <input id="reception-supplier" class="form-input" [(ngModel)]="supplierLabel" name="supplierLabel" maxlength="200" placeholder="Ej: Agropecuaria XYZ S.A.">

        <label class="form-label" for="reception-invoice">Factura / Guía (opcional)</label>
        <input id="reception-invoice" class="form-input" [(ngModel)]="invoiceReference" name="invoiceReference" maxlength="100">

        <label class="form-label" for="reception-recorded-by">Quién registra</label>
        <input id="reception-recorded-by" class="form-input" [(ngModel)]="recordedByLabel" name="recordedByLabel" maxlength="200" placeholder="Nombre del operario">

        <label class="form-label" for="reception-notes">Notas (opcional)</label>
        <textarea id="reception-notes" class="form-input" rows="2" [(ngModel)]="notes" name="notes" maxlength="500"></textarea>
      </div>
      <div class="form-actions">
        <button type="button" class="btn-secondary" (click)="toggleForm()">Cancelar</button>
        <button type="button" class="btn-primary" (click)="create()" [disabled]="submitting">Registrar recepción</button>
      </div>
    </div>
    <div class="alert alert-danger" *ngIf="errorMessage">{{ errorMessage }}</div>
    <app-catalog-table
      [rows]="batches"
      [columns]="columns"
      [actions]="actions"
      emptyMessage="No hay lotes registrados."></app-catalog-table>
  `,
  styleUrls: ['./inventory-batches-section.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InventoryBatchesSectionComponent {
  private api = inject(ApiService);
  private auth = inject(AuthService);

  @Input({ required: true }) itemId = '';
  @Input({ required: true }) batches: InventoryBatchDto[] = [];
  /**
   * Unidad base del ítem (e.g. "kg", "saco"). Se usa como valor por defecto del campo
   * Unidad del formulario — el operario puede escribir otra (e.g. "qq") y el backend
   * resuelve la conversión si existe configurada en el ítem.
   */
  @Input() defaultUnit: string | null = null;
  @Output() changed = new EventEmitter<void>();

  showForm = false;
  submitting = false;
  errorMessage = '';

  // Form fields
  batchNumber = '';
  quantity: number | null = null;
  unit = '';
  costPerUnit: number | null = null;
  expirationDate = '';
  // datetime-local input value is a local-time string "YYYY-MM-DDTHH:mm". The browser
  // interprets it as local time; we convert to UTC ISO-8601 before sending.
  receivedAtLocal = '';
  supplierLabel = '';
  invoiceReference = '';
  notes = '';
  recordedByLabel = '';

  // Tabla: la dejamos igual que antes en este commit — las columnas nuevas
  // (receivedAt, supplierLabel, badge "sin declaración completa") entran en el commit
  // siguiente para mantener una revisión incremental legible.
  readonly columns: CatalogColumn<InventoryBatchDto>[] = [
    { key: 'batchNumber', label: 'Número de lote' },
    { key: 'quantity', label: 'Cantidad' },
    { key: 'costPerUnit', label: 'Costo por unidad' },
    { key: 'expirationDate', label: 'Expiración', render: (row) => this.formatDateOnly(row.expirationDate) },
  ];

  readonly actions: CatalogAction<InventoryBatchDto>[] = [];

  toggleForm(): void {
    this.showForm = !this.showForm;
    if (this.showForm) {
      this.reset();
    } else {
      this.errorMessage = '';
    }
  }

  create(): void {
    if (
      !this.batchNumber.trim() ||
      this.quantity === null ||
      this.quantity <= 0 ||
      !this.unit.trim() ||
      this.costPerUnit === null ||
      this.costPerUnit < 0 ||
      !this.receivedAtLocal
    ) {
      this.errorMessage = 'Completa los campos obligatorios (lote, cantidad, unidad, costo, fecha de recepción).';
      return;
    }

    const receivedAtIso = this.toUtcIso(this.receivedAtLocal);
    if (!receivedAtIso) {
      this.errorMessage = 'La fecha de recepción no es válida.';
      return;
    }

    const request: RecordInventoryReceptionRequest = {
      BatchNumber: this.batchNumber.trim(),
      Quantity: this.quantity,
      Unit: this.unit.trim(),
      CostPerUnit: this.costPerUnit,
      ReceivedAt: receivedAtIso,
    };
    if (this.expirationDate) {
      request.ExpirationDate = this.expirationDate;
    }
    const supplier = this.supplierLabel.trim();
    if (supplier) {
      request.SupplierLabel = supplier;
    }
    const invoice = this.invoiceReference.trim();
    if (invoice) {
      request.InvoiceReference = invoice;
    }
    const recordedBy = this.recordedByLabel.trim();
    if (recordedBy) {
      request.RecordedByLabel = recordedBy;
    }
    const notes = this.notes.trim();
    if (notes) {
      request.Notes = notes;
    }

    this.submitting = true;
    this.api.recordInventoryReception(this.itemId, request).subscribe({
      next: () => {
        this.submitting = false;
        this.showForm = false;
        this.reset();
        this.changed.emit();
      },
      error: (err: { error?: { detail?: string; title?: string }; message?: string }) => {
        this.submitting = false;
        // Problem Details (RFC 7807): preferimos `detail` (mensaje legible del backend);
        // caemos a `title`, luego al mensaje genérico de HttpErrorResponse.
        this.errorMessage =
          err?.error?.detail ||
          err?.error?.title ||
          err?.message ||
          'No se pudo registrar la recepción.';
      },
    });
  }

  formatDateOnly(value?: string | null): string {
    if (!value) return '—';
    return new Date(value).toLocaleDateString('es-EC');
  }

  private reset(): void {
    this.batchNumber = '';
    this.quantity = null;
    this.unit = this.defaultUnit ?? '';
    this.costPerUnit = null;
    this.expirationDate = '';
    this.receivedAtLocal = this.defaultReceivedAtLocal();
    this.supplierLabel = '';
    this.invoiceReference = '';
    this.notes = '';
    this.recordedByLabel = this.auth.currentUser()?.fullName ?? '';
    this.errorMessage = '';
  }

  private defaultReceivedAtLocal(): string {
    // datetime-local wants "YYYY-MM-DDTHH:mm" in local time. Sin segundos (la granularidad
    // del control HTML5 es al minuto).
    const now = new Date();
    const pad = (n: number) => n.toString().padStart(2, '0');
    return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}T${pad(now.getHours())}:${pad(now.getMinutes())}`;
  }

  private toUtcIso(localValue: string): string | null {
    const parsed = new Date(localValue);
    return Number.isNaN(parsed.getTime()) ? null : parsed.toISOString();
  }
}
