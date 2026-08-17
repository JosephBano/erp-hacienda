import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ApiService, AnimalDetail, DamKpisDto, PedigreeDto } from '../../services/api.service';
import { AnimalDetailComponent } from './animal-detail.component';

const animalInWithdrawal: AnimalDetail = {
  id: 'animal-1',
  farmTag: 'F-001',
  officialTag: 'S-001',
  name: 'Luna',
  gender: 'Female',
  speciesName: 'Bovino',
  breedName: 'Holstein',
  categoryName: 'Vaca',
  status: 'Active',
  isInWithdrawal: true,
  withdrawalUntil: '2026-08-10',
  birthWeightKg: 1.45,
  events: [
    {
      id: 'event-1',
      eventType: 'Tratamiento',
      eventDate: '2026-08-01',
      detailsJson: '{"medicine":"example"}',
      recordedBy: 'Veterinario',
    },
  ],
  milkYields: [
    {
      id: 'yield-1',
      date: '2026-07-31',
      session: 'Morning',
      liters: 18.5,
    },
  ],
};

const pedigree: PedigreeDto = {
  animalId: animalInWithdrawal.id,
  farmTag: animalInWithdrawal.farmTag,
  ancestors: [
    {
      animalId: 'ancestor-1',
      farmTag: 'F-000',
      sex: 'Female',
      generationLevel: 1,
      role: 'Mother',
      fatherAnimalId: 'sire-1',
    },
  ],
};

const damKpis: DamKpisDto = {
  damId: animalInWithdrawal.id,
  averageCalvingIntervalDays: 390,
  daysOpen: 80,
  servicesPerConception: 2,
  totalBirthings: 3,
  totalOffspringAlive: 3,
  weanedPerYear: 1,
};

const apiStub = {
  getAnimalById: () => of(animalInWithdrawal),
  getPedigree: () => of(pedigree),
  getDamKpis: () => of(damKpis),
};

const routeStub = {
  snapshot: {
    paramMap: convertToParamMap({ id: animalInWithdrawal.id }),
  },
};

const emojiPattern = /[\u{1F000}-\u{1FAFF}\u{2600}-\u{27BF}]/u;

describe('AnimalDetailComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AnimalDetailComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: routeStub },
        { provide: ApiService, useValue: apiStub },
      ],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();
  });

  function renderAnimalDetail(): ComponentFixture<AnimalDetailComponent> {
    const fixture = TestBed.createComponent(AnimalDetailComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('should render the animal detail without emoji glyphs', () => {
    const fixture = renderAnimalDetail();
    const root = fixture.nativeElement as HTMLElement;

    expect(root.textContent ?? '').not.toMatch(emojiPattern);
  });

  it('should keep four detail tabs with an app-icon in each tab', () => {
    const fixture = renderAnimalDetail();
    const root = fixture.nativeElement as HTMLElement;
    const tabs = [...root.querySelectorAll<HTMLButtonElement>('.tab-btn')];

    expect(tabs).toHaveLength(4);
    expect(tabs.every((tab) => tab.querySelector('app-icon') !== null)).toBe(true);
  });

  it('should render every detail table as responsive with a data-label on each row cell', () => {
    const fixture = renderAnimalDetail();
    const root = fixture.nativeElement as HTMLElement;
    const tabs = [...root.querySelectorAll<HTMLButtonElement>('.tab-btn')];

    for (const tab of tabs.slice(0, 3)) {
      tab.click();
      fixture.detectChanges();

      const tables = [...root.querySelectorAll<HTMLTableElement>('table')];
      const rowCells = [...root.querySelectorAll<HTMLTableCellElement>('tbody tr td')];

      expect(tables.length).toBe(1);
      expect(tables[0].classList.contains('responsive-table')).toBe(true);
      expect(rowCells.length).toBeGreaterThan(0);
      expect(rowCells.every((cell) => cell.getAttribute('data-label')?.trim())).toBe(true);
    }
  });

  it('should preserve the legal SIFAE official tag label in the animal detail', () => {
    const fixture = renderAnimalDetail();
    const root = fixture.nativeElement as HTMLElement;

    expect(root.textContent ?? '').toContain('Arete Oficial SIFAE');
  });

  it('should describe an animal in withdrawal as ANIMAL BAJO TRATAMIENTO instead of VACA', () => {
    const fixture = renderAnimalDetail();
    const root = fixture.nativeElement as HTMLElement;
    const tabs = [...root.querySelectorAll<HTMLButtonElement>('.tab-btn')];

    tabs[3].click();
    fixture.detectChanges();

    const withdrawalAlert = root.querySelector('.withdrawal-alert');
    const heading = withdrawalAlert?.querySelector('h4');
    const withdrawalText = withdrawalAlert?.textContent ?? '';

    expect(heading?.textContent?.trim()).toBe('ANIMAL BAJO TRATAMIENTO');
    expect(withdrawalText).not.toContain('VACA');
  });

  it('should name the milking tab Producción de Leche without hiding or renaming it as a false capability', () => {
    const fixture = renderAnimalDetail();
    const root = fixture.nativeElement as HTMLElement;
    const tabs = [...root.querySelectorAll<HTMLButtonElement>('.tab-btn')];
    const tabLabels = tabs.map((tab) => tab.textContent?.replace(/\s+/g, ' ').trim() ?? '');

    expect(tabLabels.some((label) => label.includes('Producción de Leche'))).toBe(true);
    expect(tabLabels.join(' ')).not.toMatch(/capacidad/i);
  });

  it('should surface the birth weight recorded at the animal\'s registration', () => {
    // Pin the readout that the user asked for: a criar pesó N kg al nacer, the panel
    // must show it on the detail card so the gilt-selection sort key is reachable
    // from a single click (docs/spec/plan-0002-fase-3-5/spec-3.5a.md sec.3.5a.4 task 3).
    const fixture = renderAnimalDetail();
    const root = fixture.nativeElement as HTMLElement;
    const cards = [...root.querySelectorAll<HTMLElement>('.info-card')];
    const birthWeightCard = cards.find((c) =>
      /peso al nacer/i.test(c.textContent ?? ''),
    );

    expect(
      birthWeightCard,
      'expected an info card labelled "Peso al nacer"',
    ).not.toBeUndefined();
    expect(birthWeightCard!.textContent ?? '').toContain('1.45');
    expect(birthWeightCard!.textContent ?? '').toContain('kg');
  });
});
