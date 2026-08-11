import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ApiService, InventoryItemDto } from '../../services/api.service';
import { CatalogsComponent } from './catalogs.component';

describe('CatalogsComponent inventory navigation', () => {
  it('navigates to inventory detail from the inventory action', async () => {
    const items: InventoryItemDto[] = [{ id: 'i1', name: 'Maíz', category: 'Feed', unit: 'kg', minStock: 1, totalStock: 2 }];
    const apiStub: Partial<ApiService> = { getSpecies: () => of([]), getInventoryItems: () => of(items) };
    await TestBed.configureTestingModule({ imports: [CatalogsComponent], providers: [provideRouter([]), { provide: ApiService, useValue: apiStub }] }).compileComponents();
    const fixture = TestBed.createComponent(CatalogsComponent); fixture.componentInstance.setActiveTab('inventory'); fixture.detectChanges();
    const router = TestBed.inject(Router); vi.spyOn(router, 'navigate');
    (fixture.nativeElement.querySelector('[data-action="Detalle"]') as HTMLElement).click();
    expect(router.navigate).toHaveBeenCalledWith(['/inventory/items', 'i1']);
  });
});
