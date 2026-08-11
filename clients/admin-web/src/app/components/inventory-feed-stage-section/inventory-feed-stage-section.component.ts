import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService, FeedStageDto } from '../../services/api.service';
import { IconComponent } from '../../shared/icon/icon.component';

@Component({
  selector: 'app-inventory-feed-stage-section',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent],
  template: `
    <div class="card-header"><h2>Etapa de alimento</h2></div>
    <div class="form-grid"><label class="form-label" for="feed-stage">Etapa actual</label><select id="feed-stage" class="form-input" [(ngModel)]="selectedStageId" name="selectedStageId"><option value="">Sin etapa</option><option *ngFor="let stage of stages" [value]="stage.id">{{ stage.labelEs }}</option></select></div>
    <div class="form-actions"><button type="button" class="btn-primary" (click)="save()" [disabled]="submitting"><app-icon name="check" size="sm" ariaLabel="Guardar"></app-icon> Guardar etapa</button></div>
    <div class="alert alert-success" *ngIf="successMessage">{{ successMessage }}</div><div class="alert alert-danger" *ngIf="errorMessage">{{ errorMessage }}</div>
  `,
  styleUrls: ['./inventory-feed-stage-section.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InventoryFeedStageSectionComponent {
  private api = inject(ApiService);
  @Input({ required: true }) itemId = '';
  @Input({ required: true }) stages: FeedStageDto[] = [];
  @Input() feedStageId?: string | null;
  @Output() changed = new EventEmitter<void>();
  selectedStageId = ''; submitting = false; successMessage = ''; errorMessage = '';
  ngOnChanges(): void { this.selectedStageId = this.feedStageId ?? ''; }
  save(): void { this.submitting = true; this.errorMessage = ''; this.api.setInventoryItemFeedStage(this.itemId, { feedStageId: this.selectedStageId || null }).subscribe({ next: () => { this.submitting = false; this.successMessage = 'Etapa de alimento actualizada.'; this.changed.emit(); }, error: () => { this.submitting = false; this.errorMessage = 'No se pudo actualizar la etapa de alimento.'; } }); }
}
