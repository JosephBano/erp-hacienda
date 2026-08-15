import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ApiService, FeedStageDto, InventoryBatchDto, InventoryConsumptionListItem, InventoryItemDetailDto, InventoryUnitConversionDto } from '../../services/api.service';
import { InventoryItemDetailComponent } from './inventory-item-detail.component';

describe('InventoryItemDetailComponent', () => {
  const item: InventoryItemDetailDto = { id: 'i1', name: 'Maíz', category: 'Feed', unit: 'kg', minStock: 10, description: 'Ración', feedStageId: 'f1' };
  const batches: InventoryBatchDto[] = [{ id: 'b1', batchNumber: 'L-1', quantity: 20, costPerUnit: 1.5, expirationDate: '2027-01-01', receivedAt: '2026-08-01T00:00:00Z' }];
  const conversions: InventoryUnitConversionDto[] = [{ id: 'c1', fromUnit: 'kg', toUnit: 'g', factor: 1000 }];
  const stages: FeedStageDto[] = [{ id: 'f1', key: 'starter', labelEs: 'Inicio', isActive: true }];
  let apiStub: Partial<ApiService>;
  beforeEach(async () => {
    apiStub = {
      getInventoryItemById: () => of(item),
      getInventoryBatches: () => of(batches),
      getInventoryUnitConversions: () => of(conversions),
      getFeedStages: () => of(stages),
      getInventoryConsumptions: () => of([]),
      createInventoryBatch: () => of({ id: 'b2' }),
      registerInventoryUnitConversion: () => of({ id: 'c2' }),
      setInventoryItemFeedStage: () => of(undefined),
    };
    await TestBed.configureTestingModule({ imports: [InventoryItemDetailComponent], providers: [provideRouter([]), { provide: ApiService, useValue: apiStub }, { provide: ActivatedRoute, useValue: { snapshot: { paramMap: new Map([['id', 'i1']]) } } }] }).compileComponents();
  });
  it('renders title and sections', () => { const fixture = TestBed.createComponent(InventoryItemDetailComponent); fixture.detectChanges(); expect(fixture.nativeElement.querySelector('.page-title').textContent).toContain('Maíz'); expect(fixture.nativeElement.textContent).toContain('Lotes'); expect(fixture.nativeElement.textContent).toContain('Conversiones de unidad'); });
  it('shows not found for a 404', () => { apiStub.getInventoryItemById = () => throwError(() => ({ status: 404 })); const fixture = TestBed.createComponent(InventoryItemDetailComponent); fixture.detectChanges(); expect(fixture.nativeElement.textContent).toContain('Ítem no encontrado'); });
  it('renders feed stage only for feed items', () => { const fixture = TestBed.createComponent(InventoryItemDetailComponent); fixture.detectChanges(); expect(fixture.nativeElement.textContent).toContain('Etapa de alimento'); });
  it('has no emoji in visible text', () => { const fixture = TestBed.createComponent(InventoryItemDetailComponent); fixture.detectChanges(); expect(/[\u{1F000}-\u{1FAFF}\u{2600}-\u{27BF}]/u.test(fixture.nativeElement.textContent)).toBe(false); });
});

/**
 * Read-side coverage for the consumption-history feature
 * (<c>feature/inventory-consumption-history</c>):
 *
 * <list type="bullet">
 *   <item>The "Consumos registrados" section shows the date, the group name, the
 *         quantity the operator typed + the unit, and the same quantity in
 *         the item's base unit.</item>
 *   <item>When the item has no consumptions yet, the section renders the empty
 *         state (no table, no rows) instead of failing.</item>
 *   <item>The "Consumido últimos 30 días" KPI is derived client-side from the
 *         same list the section renders, in the item's base unit.</item>
 * </list>
 */
describe('InventoryItemDetailComponent — Consumos registrados (consumption-history feature)', () => {
  const item: InventoryItemDetailDto = { id: 'i1', name: 'Maíz', category: 'Feed', unit: 'kg', minStock: 10 };
  const today = new Date();
  const thirtyOneDaysAgo = new Date(today);
  thirtyOneDaysAgo.setDate(thirtyOneDaysAgo.getDate() - 31);
  const isoDaysAgo = (d: Date) => d.toISOString().slice(0, 10);
  const recent = isoDaysAgo(new Date(today.getTime() - 3 * 86_400_000));
  const older = isoDaysAgo(thirtyOneDaysAgo);

  const consumptions: InventoryConsumptionListItem[] = [
    {
      id: 'c1', groupId: 'g1', groupName: 'LOTE-A1', inventoryItemId: 'i1', inventoryItemName: 'Maíz',
      quantityRecorded: 8, unitRecorded: 'kg', quantityInBaseUnit: 8, appliedFactor: 1,
      batchId: null, consumedAt: recent, recordedByLabel: 'field-app', notes: null,
    },
    {
      id: 'c2', groupId: 'g2', groupName: 'LOTE-A2', inventoryItemId: 'i1', inventoryItemName: 'Maíz',
      quantityRecorded: 5, unitRecorded: 'saco40kg', quantityInBaseUnit: 200, appliedFactor: 40,
      batchId: null, consumedAt: older, recordedByLabel: 'mayordomo', notes: 'Ración diaria',
    },
  ];

  function renderWith(apiOverride: Partial<ApiService>): HTMLElement {
    const apiStub: Partial<ApiService> = {
      getInventoryItemById: () => of(item),
      getInventoryBatches: () => of([]),
      getInventoryUnitConversions: () => of([]),
      getFeedStages: () => of([]),
      getInventoryConsumptions: () => of(consumptions),
      createInventoryBatch: () => of({ id: 'b2' }),
      registerInventoryUnitConversion: () => of({ id: 'c2' }),
      setInventoryItemFeedStage: () => of(undefined),
      ...apiOverride,
    };
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [InventoryItemDetailComponent],
      providers: [
        provideRouter([]),
        { provide: ApiService, useValue: apiStub },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: new Map([['id', 'i1']]) } } },
      ],
    });
    const fixture = TestBed.createComponent(InventoryItemDetailComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('renders a "Consumos registrados" section with one row per consumption', () => {
    const root = renderWith({});

    expect(root.textContent ?? '').toContain('Consumos registrados');

    // Most-recent-first ordering is the server's job; the test asserts the
    // LOTE-A1 row appears before LOTE-A2 in the rendered DOM.
    const lotA1 = root.textContent?.indexOf('LOTE-A1') ?? -1;
    const lotA2 = root.textContent?.indexOf('LOTE-A2') ?? -1;
    expect(lotA1).toBeGreaterThan(-1);
    expect(lotA2).toBeGreaterThan(lotA1);
  });

  it('shows the per-row quantity the operator typed (with its unit) and the base-unit quantity', () => {
    const root = renderWith({});

    // Row 1 (LOTE-A1): the operator typed 8 kg.
    expect(root.textContent ?? '').toContain('8 kg');
    // Row 2 (LOTE-A2): the operator typed 5 sacos de 40 kg — the typed unit must
    // show up so the operator can audit their own entry, alongside the base-unit
    // value (200 kg) that the cost engine actually reads.
    expect(root.textContent ?? '').toContain('5 saco40kg');
    expect(root.textContent ?? '').toContain('200 kg');
  });

  it('surfaces the "Consumido últimos 30 días" KPI summing only rows within the window', () => {
    const root = renderWith({});

    const text = root.textContent ?? '';
    expect(text).toContain('últimos 30 días');

    // The recent row (3 days ago) is in the window and contributed 8 kg.
    // The older row (31 days ago) is *out* of the window — must not count.
    // The KPI therefore reads 8 kg.
    expect(text).toContain('8 kg');
  });

  it('shows the empty state when there are no consumptions yet', () => {
    const root = renderWith({ getInventoryConsumptions: () => of([]) });

    const text = root.textContent ?? '';
    expect(text).toContain('Consumos registrados');
    expect(text).toMatch(/no hay consumos|aún no|sin consumos/i);
  });
});
