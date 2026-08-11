import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ApiService, InventoryBatchDto } from '../../services/api.service';
import { InventoryBatchesSectionComponent } from './inventory-batches-section.component';

describe('InventoryBatchesSectionComponent', () => {
  const batches: InventoryBatchDto[] = [{ id: 'b1', batchNumber: 'L-1', quantity: 5, costPerUnit: 2, expirationDate: '2027-01-01' }];
  let apiStub: Partial<ApiService>;
  beforeEach(async () => { apiStub = { createInventoryBatch: () => of({ id: 'b2' }) }; await TestBed.configureTestingModule({ imports: [InventoryBatchesSectionComponent], providers: [provideRouter([]), { provide: ApiService, useValue: apiStub }, { provide: ActivatedRoute, useValue: { snapshot: { paramMap: new Map() } } }] }).compileComponents(); });
  it('shows rows and opens creation form', () => { const fixture = TestBed.createComponent(InventoryBatchesSectionComponent); fixture.componentInstance.itemId = 'i1'; fixture.componentInstance.batches = batches; fixture.detectChanges(); expect(fixture.nativeElement.textContent).toContain('L-1'); (fixture.nativeElement.querySelector('button') as HTMLElement).click(); fixture.detectChanges(); expect(fixture.nativeElement.querySelector('#batch-number')).toBeTruthy(); });
  it('calls createInventoryBatch on submit', () => { const fixture = TestBed.createComponent(InventoryBatchesSectionComponent); fixture.componentInstance.itemId = 'i1'; fixture.componentInstance.batchNumber = 'L-2'; fixture.componentInstance.quantity = 4; fixture.componentInstance.costPerUnit = 3; fixture.componentInstance.expirationDate = '2027-02-01'; fixture.detectChanges(); vi.spyOn(apiStub, 'createInventoryBatch'); fixture.componentInstance.create(); expect(apiStub.createInventoryBatch).toHaveBeenCalledWith('i1', { BatchNumber: 'L-2', Quantity: 4, CostPerUnit: 3, ExpirationDate: '2027-02-01' }); });
  it('renders the expiration date column', () => { const fixture = TestBed.createComponent(InventoryBatchesSectionComponent); fixture.componentInstance.batches = batches; fixture.detectChanges(); expect(fixture.nativeElement.textContent).toContain('31/12/2026'); });
  it('does not render emoji', () => { const fixture = TestBed.createComponent(InventoryBatchesSectionComponent); fixture.detectChanges(); expect(/[\u{1F000}-\u{1FAFF}\u{2600}-\u{27BF}]/u.test(fixture.nativeElement.textContent)).toBe(false); });
});
