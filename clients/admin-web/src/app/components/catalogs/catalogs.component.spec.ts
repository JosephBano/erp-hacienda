import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { CatalogsComponent } from './catalogs.component';
import { ApiService, SpeciesDto } from '../../services/api.service';

describe('CatalogsComponent', () => {
  const species: SpeciesDto[] = [
    { id: 's1', name: 'Bovino', isMilkable: true },
    { id: 's2', name: 'Porcino', isMilkable: false },
  ];

  const apiStub = {
    getSpecies: () => of(species),
    getBreeds: () => of([]),
    getAnimalCategories: () => of([]),
    getMortalityCauses: () => of([]),
    getAdministrationRoutes: () => of([]),
    getTreatmentReasons: () => of([]),
    getInventoryItems: () => of([]),
    getFarmModules: () => of([]),
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CatalogsComponent],
      providers: [
        provideRouter([]),
        { provide: ApiService, useValue: apiStub },
      ],
    }).compileComponents();
  });

  it('mounts and renders the 8 tabs', () => {
    const fixture = TestBed.createComponent(CatalogsComponent);
    fixture.detectChanges();

    const tablist = fixture.nativeElement.querySelector('[role="tablist"]');
    expect(tablist).toBeTruthy();
    const tabs = fixture.nativeElement.querySelectorAll('[role="tab"]');
    expect(tabs.length).toBe(8);
  });

  it('marks the active tab with aria-selected="true"', () => {
    const fixture = TestBed.createComponent(CatalogsComponent);
    fixture.detectChanges();

    const tabs = fixture.nativeElement.querySelectorAll('[role="tab"]');
    const active = Array.from(tabs as NodeListOf<HTMLElement>).filter(
      (t) => t.getAttribute('aria-selected') === 'true');
    expect(active.length).toBe(1);
    expect((active[0] as HTMLElement).getAttribute('data-tab')).toBe('species');
  });

  it('switches the active tab on click', () => {
    const fixture = TestBed.createComponent(CatalogsComponent);
    fixture.detectChanges();

    const tabs = fixture.nativeElement.querySelectorAll('[role="tab"]');
    const mortalityTab = Array.from(tabs as NodeListOf<HTMLElement>).find(
      (t) => t.getAttribute('data-tab') === 'mortality') as HTMLElement;
    mortalityTab.click();
    fixture.detectChanges();

    expect(mortalityTab.getAttribute('aria-selected')).toBe('true');
  });

  it('renders no emoji in the tab labels', () => {
    const fixture = TestBed.createComponent(CatalogsComponent);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    const emojiPattern = /[\u{1F000}-\u{1FAFF}\u{2600}-\u{27BF}]/u;
    expect(emojiPattern.test(text)).toBe(false);
  });
});
