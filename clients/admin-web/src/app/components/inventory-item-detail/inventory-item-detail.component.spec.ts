import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ApiService, FeedStageDto, InventoryBatchDto, InventoryItemDetailDto, InventoryUnitConversionDto } from '../../services/api.service';
import { InventoryItemDetailComponent } from './inventory-item-detail.component';

describe('InventoryItemDetailComponent', () => {
  const item: InventoryItemDetailDto = { id: 'i1', name: 'Maíz', category: 'Feed', unit: 'kg', minStock: 10, description: 'Ración', feedStageId: 'f1' };
  const batches: InventoryBatchDto[] = [{ id: 'b1', batchNumber: 'L-1', quantity: 20, costPerUnit: 1.5, expirationDate: '2027-01-01' }];
  const conversions: InventoryUnitConversionDto[] = [{ id: 'c1', fromUnit: 'kg', toUnit: 'g', factor: 1000 }];
  const stages: FeedStageDto[] = [{ id: 'f1', key: 'starter', labelEs: 'Inicio', isActive: true }];
  let apiStub: Partial<ApiService>;
  beforeEach(async () => {
    apiStub = { getInventoryItemById: () => of(item), getInventoryBatches: () => of(batches), getInventoryUnitConversions: () => of(conversions), getFeedStages: () => of(stages), createInventoryBatch: () => of({ id: 'b2' }), registerInventoryUnitConversion: () => of({ id: 'c2' }), setInventoryItemFeedStage: () => of(undefined) };
    await TestBed.configureTestingModule({ imports: [InventoryItemDetailComponent], providers: [provideRouter([]), { provide: ApiService, useValue: apiStub }, { provide: ActivatedRoute, useValue: { snapshot: { paramMap: new Map([['id', 'i1']]) } } }] }).compileComponents();
  });
  it('renders title and sections', () => { const fixture = TestBed.createComponent(InventoryItemDetailComponent); fixture.detectChanges(); expect(fixture.nativeElement.querySelector('.page-title').textContent).toContain('Maíz'); expect(fixture.nativeElement.textContent).toContain('Lotes'); expect(fixture.nativeElement.textContent).toContain('Conversiones de unidad'); });
  it('shows not found for a 404', () => { apiStub.getInventoryItemById = () => throwError(() => ({ status: 404 })); const fixture = TestBed.createComponent(InventoryItemDetailComponent); fixture.detectChanges(); expect(fixture.nativeElement.textContent).toContain('Ítem no encontrado'); });
  it('renders feed stage only for feed items', () => { const fixture = TestBed.createComponent(InventoryItemDetailComponent); fixture.detectChanges(); expect(fixture.nativeElement.textContent).toContain('Etapa de alimento'); });
  it('has no emoji in visible text', () => { const fixture = TestBed.createComponent(InventoryItemDetailComponent); fixture.detectChanges(); expect(/[\u{1F000}-\u{1FAFF}\u{2600}-\u{27BF}]/u.test(fixture.nativeElement.textContent)).toBe(false); });
});
