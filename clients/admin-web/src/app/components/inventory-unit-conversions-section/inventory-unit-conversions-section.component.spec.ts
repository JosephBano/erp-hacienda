import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ApiService, InventoryUnitConversionDto } from '../../services/api.service';
import { InventoryUnitConversionsSectionComponent } from './inventory-unit-conversions-section.component';

describe('InventoryUnitConversionsSectionComponent', () => {
  let apiStub: Partial<ApiService>;
  beforeEach(async () => { apiStub = { registerInventoryUnitConversion: () => of({ id: 'c1' }) }; await TestBed.configureTestingModule({ imports: [InventoryUnitConversionsSectionComponent], providers: [provideRouter([]), { provide: ApiService, useValue: apiStub }] }).compileComponents(); });
  it('shows conversion rows', () => { const fixture = TestBed.createComponent(InventoryUnitConversionsSectionComponent); fixture.componentInstance.conversions = [{ id: 'c1', fromUnit: 'kg', toUnit: 'g', factor: 1000 } satisfies InventoryUnitConversionDto]; fixture.detectChanges(); expect(fixture.nativeElement.textContent).toContain('kg'); expect(fixture.nativeElement.textContent).toContain('1000'); });
  it('opens the creation form', () => { const fixture = TestBed.createComponent(InventoryUnitConversionsSectionComponent); fixture.detectChanges(); (fixture.nativeElement.querySelector('button') as HTMLElement).click(); fixture.detectChanges(); expect(fixture.nativeElement.querySelector('#from-unit')).toBeTruthy(); });
  it('calls the API on submit', () => { const fixture = TestBed.createComponent(InventoryUnitConversionsSectionComponent); fixture.componentInstance.itemId = 'i1'; fixture.componentInstance.fromUnit = 'kg'; fixture.componentInstance.toUnit = 'g'; fixture.componentInstance.factor = 1000; fixture.detectChanges(); vi.spyOn(apiStub, 'registerInventoryUnitConversion'); fixture.componentInstance.create(); expect(apiStub.registerInventoryUnitConversion).toHaveBeenCalledWith('i1', { FromUnit: 'kg', ToUnit: 'g', Factor: 1000 }); });
  it('has no emoji', () => { const fixture = TestBed.createComponent(InventoryUnitConversionsSectionComponent); fixture.detectChanges(); expect(/[\u{1F000}-\u{1FAFF}\u{2600}-\u{27BF}]/u.test(fixture.nativeElement.textContent)).toBe(false); });
});
