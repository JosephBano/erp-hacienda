import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { Observable, of } from 'rxjs';
import { ApiService, InventoryBatchDto } from '../../services/api.service';
import { AuthService, LoginResult } from '../../services/auth.service';
import { InventoryBatchesSectionComponent } from './inventory-batches-section.component';

describe('InventoryBatchesSectionComponent', () => {
  const fullReception: InventoryBatchDto = {
    id: 'b1',
    batchNumber: 'L-1',
    quantity: 5,
    costPerUnit: 2,
    expirationDate: '2027-01-01',
    receivedAt: '2026-08-13T19:00:00Z',
    supplierLabel: 'Agropecuaria XYZ',
    invoiceReference: 'F-001',
    recordedByLabel: 'José Baño',
  };
  const legacyBatch: InventoryBatchDto = {
    id: 'b2',
    batchNumber: 'L-0',
    quantity: 3,
    costPerUnit: 1.5,
    expirationDate: null,
    receivedAt: '2026-08-10T12:00:00Z',
    supplierLabel: null,
    invoiceReference: null,
    recordedByLabel: null,
  };

  let apiStub: Partial<ApiService>;
  let authStub: { currentUser: ReturnType<typeof signal<LoginResult | null>> };
  const noUser: LoginResult | null = null;

  beforeEach(async () => {
    apiStub = {
      recordInventoryReception: vi.fn(() => of('b-new')),
      createInventoryBatch: vi.fn(),
    };
    authStub = { currentUser: signal<LoginResult | null>(noUser) };
    await TestBed.configureTestingModule({
      imports: [InventoryBatchesSectionComponent],
      providers: [
        provideRouter([]),
        { provide: ApiService, useValue: apiStub },
        { provide: AuthService, useValue: authStub },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: new Map() } } },
      ],
    }).compileComponents();
  });

  function createFixture(batches: InventoryBatchDto[]) {
    const fixture = TestBed.createComponent(InventoryBatchesSectionComponent);
    fixture.componentRef.setInput('itemId', 'i1');
    fixture.componentRef.setInput('defaultUnit', 'kg');
    fixture.componentRef.setInput('batches', batches);
    fixture.detectChanges();
    return fixture;
  }

  it('renders the deprecation banner', () => {
    const fixture = createFixture([fullReception]);
    expect(fixture.nativeElement.textContent).toContain('Purchasing (Fase 4)');
    expect(fixture.nativeElement.textContent).toContain('Recibir orden de compra');
  });

  it('shows the "Recibir alimento" button', () => {
    const fixture = createFixture([]);
    const btn = fixture.nativeElement.querySelector('.card-header button') as HTMLButtonElement;
    expect(btn.textContent).toContain('Recibir alimento');
  });

  it('opens the reception form with all required fields', () => {
    const fixture = createFixture([]);
    (fixture.nativeElement.querySelector('.card-header button') as HTMLElement).click();
    fixture.detectChanges();
    for (const id of [
      'reception-batch-number',
      'reception-quantity',
      'reception-unit',
      'reception-cost',
      'reception-expiration',
      'reception-received-at',
      'reception-supplier',
      'reception-invoice',
      'reception-recorded-by',
      'reception-notes',
    ]) {
      expect(fixture.nativeElement.querySelector('#' + id)).toBeTruthy();
    }
  });

  it('prefills the unit with defaultUnit and the operator full name from AuthService', () => {
    authStub.currentUser.set({ fullName: 'José Baño' } as LoginResult);
    const fixture = createFixture([]);
    (fixture.nativeElement.querySelector('.card-header button') as HTMLButtonElement).click();
    fixture.detectChanges();
    const component = fixture.componentInstance;
    expect(component.defaultUnit).toBe('kg');
    expect(component.unit).toBe('kg');
    expect(component.recordedByLabel).toBe('José Baño');
  });

  it('submits the reception via ApiService.recordInventoryReception with UTC ISO receivedAt', () => {
    const fixture = createFixture([]);
    const component = fixture.componentInstance;
    (fixture.nativeElement.querySelector('.card-header button') as HTMLButtonElement).click();
    fixture.detectChanges();
    component.batchNumber = 'L-99';
    component.quantity = 12;
    component.unit = 'kg';
    component.costPerUnit = 3.25;
    component.expirationDate = '2027-02-01';
    component.receivedAtLocal = '2026-08-13T14:00';
    component.supplierLabel = 'Agropecuaria XYZ';
    component.invoiceReference = 'F-123';
    component.recordedByLabel = 'José';
    component.notes = 'Lote de prueba';
    fixture.detectChanges();
    component.create();
    expect(apiStub.recordInventoryReception).toHaveBeenCalledTimes(1);
    const [itemIdArg, body] = (apiStub.recordInventoryReception as ReturnType<typeof vi.fn>).mock.calls[0];
    expect(itemIdArg).toBe('i1');
    expect(body.BatchNumber).toBe('L-99');
    expect(body.Quantity).toBe(12);
    expect(body.Unit).toBe('kg');
    expect(body.CostPerUnit).toBe(3.25);
    expect(body.ExpirationDate).toBe('2027-02-01');
    expect(body.ReceivedAt).toBe(new Date('2026-08-13T14:00').toISOString());
    expect(body.SupplierLabel).toBe('Agropecuaria XYZ');
    expect(body.InvoiceReference).toBe('F-123');
    expect(body.RecordedByLabel).toBe('José');
    expect(body.Notes).toBe('Lote de prueba');
  });

  it('omits optional empty fields from the request payload', () => {
    const fixture = createFixture([]);
    const component = fixture.componentInstance;
    (fixture.nativeElement.querySelector('.card-header button') as HTMLButtonElement).click();
    fixture.detectChanges();
    component.batchNumber = 'L-1';
    component.quantity = 1;
    component.unit = 'kg';
    component.costPerUnit = 0;
    component.receivedAtLocal = '2026-08-13T14:00';
    fixture.detectChanges();
    component.create();
    const [, body] = (apiStub.recordInventoryReception as ReturnType<typeof vi.fn>).mock.calls[0];
    expect(body.ExpirationDate).toBeUndefined();
    expect(body.SupplierLabel).toBeUndefined();
    expect(body.InvoiceReference).toBeUndefined();
    expect(body.RecordedByLabel).toBeUndefined();
    expect(body.Notes).toBeUndefined();
  });

  it('shows backend error.detail on 400', async () => {
    apiStub.recordInventoryReception = vi.fn(
      (): Observable<string> =>
        new Observable<string>((subscriber) => {
          queueMicrotask(() =>
            subscriber.error({ error: { detail: 'No hay conversión definida para "qq" → "kg".' } })
          );
        })
    );
    const fixture = createFixture([]);
    const component = fixture.componentInstance;
    (fixture.nativeElement.querySelector('.card-header button') as HTMLButtonElement).click();
    fixture.detectChanges();
    component.batchNumber = 'L-1';
    component.quantity = 1;
    component.unit = 'qq';
    component.costPerUnit = 1;
    component.receivedAtLocal = '2026-08-13T14:00';
    fixture.detectChanges();
    component.create();
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No hay conversión definida para "qq" → "kg".');
  });

  it('renders the ReceivedAt and SupplierLabel columns with Ecuador local time', () => {
    const fixture = createFixture([fullReception]);
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Agropecuaria XYZ');
    // 2026-08-13T19:00:00Z → 13/08/2026 14:00 in Ecuador (UTC-5).
    expect(text).toContain('13/08/2026');
    expect(text).toContain('14:00');
  });

  it('shows the "Sin declaración completa" badge for legacy batches (heuristic A, issue #93)', () => {
    const fixture = createFixture([legacyBatch]);
    expect(fixture.nativeElement.textContent).toContain('Sin declaración completa');
  });

  it('does not show the badge for fully-declared receptions', () => {
    const fixture = createFixture([fullReception]);
    expect(fixture.nativeElement.textContent).not.toContain('Sin declaración completa');
  });

  it('renders the expiration date column', () => {
    const fixture = createFixture([fullReception]);
    const expected = new Date(fullReception.expirationDate!).toLocaleDateString('es-EC');
    expect(fixture.nativeElement.textContent).toContain(expected);
  });

  it('shows an empty-state message when there are no batches', () => {
    const fixture = createFixture([]);
    expect(fixture.nativeElement.textContent).toContain('No hay lotes registrados.');
  });

  it('does not render emoji', () => {
    const fixture = createFixture([fullReception, legacyBatch]);
    expect(/[\u{1F000}-\u{1FAFF}\u{2600}-\u{27BF}]/u.test(fixture.nativeElement.textContent)).toBe(false);
  });
});
