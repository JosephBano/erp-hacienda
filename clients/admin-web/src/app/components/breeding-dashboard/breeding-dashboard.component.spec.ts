import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import {
  ApiService,
  PregnancyDto,
  SemenStraw,
} from '../../services/api.service';
import { BreedingDashboardComponent } from './breeding-dashboard.component';

/**
 * Behavioral RED tests for BreedingDashboardComponent (responsive redesign contract).
 *
 * These tests pin down the markup & copy contract for the breeding dashboard:
 *   - zero emoji glyphs anywhere in the rendered DOM
 *   - page title, every tab and the "Evaluar Alertas" action contain <app-icon>
 *     (NO_ERRORS_SCHEMA keeps an absent <app-icon> a failing assertion,
 *      not a compile error)
 *   - the service-form dam selector is labelled "Animal / Madre (Dam)", never
 *     "Vaca / Madre (Dam)"
 *   - the optional calf-farm-tag input uses the placeholder "p.ej. CRIA-045"
 *   - the tabs nav exposes role="tablist" + role="tab" + aria-selected, with
 *     exactly one tab marked aria-selected="true"
 *   - both data tables (pregnancies, straws) carry the .responsive-table class
 *   - injecting one real PregnancyDto and one real SemenStraw, every dynamic
 *     <td> in both tables carries a non-empty data-label that matches its
 *     column header
 *   - guardrail: across all tabs the breeding vocabulary "Monta / IA",
 *     "Parto / Camada" and "Pajuelas" stays present
 *
 * The first nine expectations are expected to fail RED on today's markup and
 * pass once the responsive redesign is implemented in the component template.
 * The vocabulary guardrail is GREEN today and must stay GREEN.
 */

const pregnancy: PregnancyDto = {
  id: 'preg-1',
  damId: 'animal-1',
  serviceId: 'service-1',
  confirmedAt: '2026-04-01',
  expectedBirthDate: '2027-01-10',
  status: 'Active',
  notes: 'Confirmada por palpación',
  createdAt: '2026-04-01T00:00:00Z',
};

const straw: SemenStraw = {
  id: 'straw-1',
  code: 'HOL-2026-01',
  bullName: 'Kingboy Supreme',
  bullCode: 'KB-001',
  breedId: 'breed-1',
  supplierName: 'Semex',
  initialQuantity: 50,
  currentQuantity: 32,
  notes: '',
  createdAt: '2026-04-01T00:00:00Z',
};

const emptyApiStub = {
  getAlerts: () => of([]),
  getActivePregnancies: () => of([]),
  getSemenStraws: () => of([]),
  getAnimals: () => of([]),
};

const dataApiStub = {
  getAlerts: () => of([]),
  getActivePregnancies: () => of([pregnancy]),
  getSemenStraws: () => of([straw]),
  getAnimals: () => of([]),
};

const emojiPattern = /[\u{1F000}-\u{1FAFF}\u{2600}-\u{27BF}]/u;

describe('BreedingDashboardComponent (responsive redesign contract)', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BreedingDashboardComponent],
      providers: [
        provideRouter([]),
        { provide: ApiService, useValue: emptyApiStub },
      ],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();
  });

  function render(): ComponentFixture<BreedingDashboardComponent> {
    const fixture = TestBed.createComponent(BreedingDashboardComponent);
    fixture.detectChanges();
    return fixture;
  }

  function tabs(root: HTMLElement): HTMLButtonElement[] {
    return [...root.querySelectorAll<HTMLButtonElement>('.tab-btn')];
  }

  it('should render every breeding tab without any emoji glyph', () => {
    const fixture = render();
    const root = fixture.nativeElement as HTMLElement;

    for (const tab of tabs(root)) {
      tab.click();
      fixture.detectChanges();
      expect(root.textContent ?? '').not.toMatch(emojiPattern);
    }
  });

  it('should display an app-icon next to the page title instead of an emoji glyph', () => {
    const root = render().nativeElement as HTMLElement;
    const title = root.querySelector('h1.page-title');

    expect(title, 'expected an <h1 class="page-title">').not.toBeNull();
    expect(
      title!.querySelector('app-icon'),
      'expected the page title to contain <app-icon> (today it has the 🐄 emoji)',
    ).not.toBeNull();
  });

  it('should render every tab button with an app-icon instead of an emoji glyph', () => {
    const root = render().nativeElement as HTMLElement;
    const tabButtons = tabs(root);

    expect(tabButtons.length).toBeGreaterThanOrEqual(4);
    expect(
      tabButtons.every((tab) => tab.querySelector('app-icon') !== null),
      'every tab button must contain an <app-icon> (today they carry emoji glyphs)',
    ).toBe(true);
  });

  it('should render the Evaluar Alertas action with an app-icon instead of an emoji glyph', () => {
    const root = render().nativeElement as HTMLElement;
    const evalButton = [...root.querySelectorAll('button')].find((btn) =>
      /Evaluar Alertas/i.test(btn.textContent ?? ''),
    );

    expect(evalButton, 'expected the Evaluar Alertas button').toBeDefined();
    expect(
      evalButton!.querySelector('app-icon'),
      'expected the Evaluar Alertas button to contain <app-icon> (today it carries the 🔄 emoji)',
    ).not.toBeNull();
  });

  it('should label the service dam selector as "Animal / Madre (Dam)", not "Vaca"', () => {
    const fixture = render();
    const root = fixture.nativeElement as HTMLElement;
    tabs(root)[2].click();
    fixture.detectChanges();
    const labels = [...root.querySelectorAll('label')].map(
      (l) => l.textContent?.trim() ?? '',
    );

    expect(
      labels,
      'expected a <label> reading exactly "Animal / Madre (Dam)"',
    ).toContain('Animal / Madre (Dam)');

    expect(
      labels,
      'the old "Vaca / Madre (Dam)" label must be replaced',
    ).not.toContain('Vaca / Madre (Dam)');

    expect(
      labels.some((l) => /(^|\s)Vaca(\s|\/|$)/.test(l)),
      'no <label> should still reference "Vaca"',
    ).toBe(false);
  });

  it('should use the placeholder "p.ej. CRIA-045" for the optional calf farm tag input', () => {
    const fixture = render();
    const root = fixture.nativeElement as HTMLElement;
    tabs(root)[2].click();
    fixture.componentInstance.birthingForm.bornAlive = 1;
    fixture.detectChanges();
    const calfTagInput = root.querySelector<HTMLInputElement>('input[name="bCalfTag"]');

    expect(
      calfTagInput,
      'expected the optional calf farm tag input (name="bCalfTag")',
    ).not.toBeNull();
    expect(calfTagInput!.getAttribute('placeholder')).toBe('p.ej. CRIA-045');
  });

  it('should expose the tabs nav as role=tablist with role=tab buttons and aria-selected', () => {
    const root = render().nativeElement as HTMLElement;
    const tablist = root.querySelector<HTMLElement>('[role="tablist"]');

    expect(tablist, 'expected a [role="tablist"] container').not.toBeNull();

    const tabButtons = [
      ...tablist!.querySelectorAll<HTMLButtonElement>('[role="tab"]'),
    ];
    expect(tabButtons.length).toBeGreaterThanOrEqual(4);

    expect(
      tabButtons.every((tab) => tab.hasAttribute('aria-selected')),
      'every tab button must expose aria-selected',
    ).toBe(true);

    const selected = tabButtons.filter(
      (tab) => tab.getAttribute('aria-selected') === 'true',
    );
    expect(
      selected.length,
      'exactly one tab must be aria-selected="true"',
    ).toBe(1);

    expect(tabButtons[0].getAttribute('aria-selected')).toBe('true');
  });

  it('should render each data table with the responsive-table class', () => {
    const fixture = render();
    const root = fixture.nativeElement as HTMLElement;
    const tabButtons = tabs(root);

    tabButtons[1].click();
    fixture.detectChanges();
    expect(root.querySelectorAll('table.responsive-table')).toHaveLength(1);

    tabButtons[3].click();
    fixture.detectChanges();
    expect(root.querySelectorAll('table.responsive-table')).toHaveLength(1);
  });

  it('should render every dynamic td of pregnancies and straws tables with data-label matching the column headers', async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [BreedingDashboardComponent],
      providers: [
        provideRouter([]),
        { provide: ApiService, useValue: dataApiStub },
      ],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();

    const fixture = TestBed.createComponent(BreedingDashboardComponent);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;

    const tabButtons = tabs(root);

    // --- Pregnancies tab -----------------------------------------------------
    const pregTab = tabButtons.find((t) =>
      /Gestaciones Activas/i.test(t.textContent ?? ''),
    );
    expect(pregTab, 'expected the Gestaciones Activas tab').toBeDefined();
    pregTab!.click();
    fixture.detectChanges();

    const pregCells = [
      ...root.querySelectorAll<HTMLTableCellElement>('table tbody tr td'),
    ];
    expect(
      pregCells.length,
      'expected at least one dynamic pregnancy row',
    ).toBeGreaterThan(0);
    expect(
      pregCells.every((c) => (c.getAttribute('data-label') ?? '').trim().length > 0),
      'every dynamic pregnancy <td> must carry a non-empty data-label',
    ).toBe(true);

    const pregHeaders = [...root.querySelectorAll<HTMLTableCellElement>('table thead th')].map(
      (h) => (h.textContent ?? '').trim(),
    );
    expect(pregCells.map((c) => c.getAttribute('data-label'))).toEqual(pregHeaders);

    // --- Straws tab ----------------------------------------------------------
    const strawTab = tabButtons.find((t) =>
      /Catálogo de Pajuelas/i.test(t.textContent ?? ''),
    );
    expect(strawTab, 'expected the Catálogo de Pajuelas tab').toBeDefined();
    strawTab!.click();
    fixture.detectChanges();

    const strawCells = [
      ...root.querySelectorAll<HTMLTableCellElement>('table tbody tr td'),
    ];
    expect(
      strawCells.length,
      'expected at least one dynamic straw row',
    ).toBeGreaterThan(0);
    expect(
      strawCells.every((c) => (c.getAttribute('data-label') ?? '').trim().length > 0),
      'every dynamic straw <td> must carry a non-empty data-label',
    ).toBe(true);

    const strawHeaders = [
      ...root.querySelectorAll<HTMLTableCellElement>('table thead th'),
    ].map((h) => (h.textContent ?? '').trim());
    expect(strawCells.map((c) => c.getAttribute('data-label'))).toEqual(strawHeaders);
  });

  it('should preserve the breeding domain vocabulary across all tabs: Monta / IA, Parto / Camada, Pajuelas', () => {
    const fixture = render();
    const root = fixture.nativeElement as HTMLElement;

    let collected = root.textContent ?? '';
    for (const tab of tabs(root)) {
      tab.click();
      fixture.detectChanges();
      collected += '\n' + (root.textContent ?? '');
    }

    expect(collected).toContain('Monta / IA');
    expect(collected).toContain('Parto / Camada');
    expect(collected).toContain('Pajuelas');
  });
});