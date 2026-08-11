import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ApiService, FeedStageDto, InventoryBatchDto, InventoryItemDetailDto, InventoryUnitConversionDto } from '../../services/api.service';
import { IconComponent } from '../../shared/icon/icon.component';
import { InventoryBatchesSectionComponent } from '../inventory-batches-section/inventory-batches-section.component';
import { InventoryUnitConversionsSectionComponent } from '../inventory-unit-conversions-section/inventory-unit-conversions-section.component';
import { InventoryFeedStageSectionComponent } from '../inventory-feed-stage-section/inventory-feed-stage-section.component';

@Component({
  selector: 'app-inventory-item-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, IconComponent, InventoryBatchesSectionComponent, InventoryUnitConversionsSectionComponent, InventoryFeedStageSectionComponent],
  templateUrl: './inventory-item-detail.component.html',
  styleUrls: ['./inventory-item-detail.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InventoryItemDetailComponent implements OnInit {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  readonly item = signal<InventoryItemDetailDto | null>(null);
  readonly batches = signal<InventoryBatchDto[]>([]);
  readonly conversions = signal<InventoryUnitConversionDto[]>([]);
  readonly feedStages = signal<FeedStageDto[]>([]);
  readonly loading = signal(true);
  readonly notFound = signal(false);
  readonly errorMessage = signal('');

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.notFound.set(true);
      this.loading.set(false);
      return;
    }
    this.load(id);
  }

  load(id: string): void {
    this.loading.set(true);
    forkJoin({
      item: this.api.getInventoryItemById(id),
      batches: this.api.getInventoryBatches(id),
      conversions: this.api.getInventoryUnitConversions(id),
      stages: this.api.getFeedStages(),
    }).subscribe({
      next: (result) => {
        this.item.set(result.item);
        this.batches.set(result.batches);
        this.conversions.set(result.conversions);
        this.feedStages.set(result.stages);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        if ((err as { status?: number })?.status === 404) {
          this.notFound.set(true);
        } else {
          this.errorMessage.set('No se pudo cargar el ítem.');
        }
      },
    });
  }

  reload(): void {
    const id = this.item()?.id;
    if (id) {
      this.load(id);
    }
  }

  goBack(): void {
    this.router.navigate(['/catalogs'], { queryParams: { tab: 'inventory' } });
  }

  categoryLabel(value: string): string {
    return ({
      Medicine: 'Medicamento',
      Feed: 'Alimento',
      Supply: 'Insumo',
      Product: 'Producto',
    } as Record<string, string>)[value] ?? value;
  }
}
