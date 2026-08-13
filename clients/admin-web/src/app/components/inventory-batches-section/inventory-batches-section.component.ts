import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService, InventoryBatchDto, RecordInventoryReceptionRequest } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { IconComponent } from '../../shared/icon/icon.component';

// ADR-0026 Decisión 6: el botón "Crear lote" se reemplaza por "Recibir alimento"
// con los campos de trazabilidad de recepción (fecha, proveedor, factura, autor).
// El banner amarillo arriba documenta la vida útil declarada (será reemplazado por
// "Recibir orden de compra" cuando llegue Purchasing — Fase 4).
//
// El listado de batches ahora muestra `receivedAt` y `supplierLabel` (columnas
// adicionales) y un badge amarillo "Sin declaración completa" cuando los tres campos
// de recepción que sólo popula RecordReception están vacíos — heurística A del
// issue #93 para detectar lotes creados por la ruta legacy `AddBatch` (POST /batches).
@Component({
  selector: 'app-inventory-batches-section',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent],
  template: `
    <div class="deprecation-banner" role="status">
      <app-icon name="alert" size="sm" ariaLabel="Aviso"></app-icon>
      <div>
        <strong>Cuando llegue Purchasing (Fase 4),</strong> este flujo se reemplazará por "Recibir orden de compra". Por ahora, registra aquí las entradas de alimento al inventario.
      </div>
    </div>
    <div class="card-header">
      <h2>Lotes</h2>
      <button type="button" class="btn-primary btn-sm" (click)="toggleForm()">
        <app-icon name="plus" size="sm" ariaLabel="Recibir alimento"></app-icon>
        <span>Recibir alimento</span>
      </button>
    </div>
    <div *ngIf="showForm" class="inline-form mini-form">
      <div class="form-grid">
        <label class="form-label" for="reception-batch-number">Número de lote</label>
        <input id="reception-batch-number" class="form-input" [(ngModel)]="batchNumber" name="batchNumber" maxlength="50" placeholder="Ej: L-2026-08" required>

        <label class="form-label" for="reception-quantity">Cantidad</label>
        <input id="reception-quantity" class="form-input" type="number" [(ngModel)]="quantity" name="quantity" min="0.001" step="0.001" placeholder="Ej: 500" required>

        <label class="form-label" for="reception-unit">Unidad</label>
        <input id="reception-unit" class="form-input" [(ngModel)]="unit" name="unit" maxlength="20" placeholder="kg, saco, qq…" required>

        <label class="form-label" for="reception-cost">Costo por unidad</label>
        <input id="reception-cost" class="form-input" type="number" [(ngModel)]="costPerUnit" name="costPerUnit" min="0" step="0.01" placeholder="Ej: 0.45" required>

        <label class="form-label" for="reception-expiration">Fecha de expiración (opcional)</label>
        <input id="reception-expiration" class="form-input" type="date" [(ngModel)]="expirationDate" name="expirationDate">

        <label class="form-label" for="reception-received-at">Fecha de recepción</label>
        <input id="reception-received-at" class="form-input" type="datetime-local" [(ngModel)]="receivedAtLocal" name="receivedAtLocal" min="2020-01-01T00:00" required>

        <label class="form-label" for="reception-supplier">Proveedor (texto libre)</label>
        <input id="reception-supplier" class="form-input" [(ngModel)]="supplierLabel" name="supplierLabel" maxlength="200" placeholder="Ej: Agropecuaria XYZ S.A.">

        <label class="form-label" for="reception-invoice">Factura / Guía (opcional)</label>
        <input id="reception-invoice" class="form-input" [(ngModel)]="invoiceReference" name="invoiceReference" maxlength="100" placeholder="Ej: FAC-001-002-12345">

        <label class="form-label" for="reception-recorded-by">Quién registra</label>
        <input id="reception-recorded-by" class="form-input" [(ngModel)]="recordedByLabel" name="recordedByLabel" maxlength="200" placeholder="Nombre del operario">

        <label class="form-label" for="reception-notes">Notas (opcional)</label>
        <textarea id="reception-notes" class="form-textarea" rows="2" [(ngModel)]="notes" name="notes" maxlength="500" placeholder="Observaciones de la recepción"></textarea>
      </div>
      <div class="form-actions">
        <button type="button" class="btn-secondary" (click)="toggleForm()">Cancelar</button>
        <button type="button" class="btn-primary" (click)="create()" [disabled]="submitting">
          <app-icon name="check" size="sm" ariaLabel="Registrar"></app-icon>
          <span>{{ submitting ? 'Registrando…' : 'Registrar recepción' }}</span>
        </button>
      </div>
    </div>
    <div class="alert alert-danger" *ngIf="errorMessage">{{ errorMessage }}</div>
    <div *ngIf="batches.length === 0" class="empty-state">No hay lotes registrados.</div>
    <div *ngIf="batches.length > 0" class="table-container">
      <table class="responsive-table">
        <thead>
          <tr>
            <th>Número de lote</th>
            <th>Cantidad</th>
            <th>Costo por unidad</th>
            <th>Expiración</th>
            <th>Recibido</th>
            <th>Proveedor</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let row of batches">
            <td data-label="Número de lote">{{ row.batchNumber }}</td>
            <td data-label="Cantidad">{{ row.quantity }}</td>
            <td data-label="Costo por unidad">{{ row.costPerUnit }}</td>
            <td data-label="Expiración">{{ formatDateOnly(row.expirationDate) }}</td>
            <td data-label="Recibido">
              <div>{{ formatReceivedAt(row.receivedAt) }}</div>
              <span *ngIf="isIncompleteReception(row)" class="badge badge-warning reception-flag" title="Creado por la ruta legacy /batches — sin trazabilidad de proveedor/factura/autor">
                Sin declaración completa
              </span>
            </td>
            <td data-label="Proveedor">{{ row.supplierLabel || '—' }}</td>
          </tr>
        </tbody>
      </table>
    </div>
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

  // Heurística A del issue #93: si los tres campos que sólo popula RecordReception están
  // vacíos, el lote fue creado por la ruta legacy AddBatch / POST /batches — el operario
  // nunca completó la declaración completa. El backend hace backfill `received_at = created_at`
  // pero los metadatos quedan null, así que la heurística es 100% precisa para detectar
  // lotes creados por el flujo viejo (post-ADR-0026 los nuevos siempre pasan por aquí).
  isIncompleteReception(row: InventoryBatchDto): boolean {
    return (
      row.supplierLabel == null &&
      row.invoiceReference == null &&
      row.recordedByLabel == null
    );
  }

  formatDateOnly(value?: string | null): string {
    if (!value) return '—';
    return new Date(value).toLocaleDateString('es-EC');
  }

  formatReceivedAt(value?: string | null): string {
    if (!value) return '—';
    return new Intl.DateTimeFormat('es-EC', {
      timeZone: 'America/Guayaquil',
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
    }).format(new Date(value));
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
