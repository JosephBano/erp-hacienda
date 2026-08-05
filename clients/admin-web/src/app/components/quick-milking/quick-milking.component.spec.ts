import { of, throwError } from 'rxjs';
import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { ApiService, Animal } from '../../services/api.service';
import { QuickMilkingComponent } from './quick-milking.component';

/**
 * Behavioral RED tests for QuickMilkingComponent.
 *
 * These tests pin down the markup & copy contract for the responsive redesign:
 *   - zero emojis anywhere in the rendered DOM
 *   - page title and primary action use <app-icon>, not emoji glyphs
 *   - the withdrawal safety warning is the exact regulatory copy
 *     "LECHE EN RETIRO (NO MEZCLAR)" (no plural typo, no emoji)
 *   - general copy uses "animal / animales" (not "vaca"), and the table
 *     headers include an "Animal" column
 *   - the production table is responsive-table with data-label on every <td>
 *   - when the API errors out, the component shows an empty list and never
 *     fakes "VACA-001" demo data
 *
 * All tests must fail RED today against the current markup/copy and pass
 * once the responsive redesign is implemented in the component template.
 */

// QuickMilkingComponent filters by Female || !gender, so we only need
// female-shaped fixtures here.
const regularFemale: Animal = {
  id: 'animal-1',
  farmTag: 'F-101',
  officialTag: 'S-101',
  name: 'Luna',
  birthDate: '2022-03-04',
  gender: 'Female',
  speciesName: 'Bovino',
  breedName: 'Holstein',
  categoryName: 'Vaca',
  status: 'Active',
  isInWithdrawal: false,
};

const femaleInWithdrawal: Animal = {
  id: 'animal-2',
  farmTag: 'F-102',
  officialTag: 'S-102',
  name: 'Mariposa',
  birthDate: '2021-09-12',
  gender: 'Female',
  speciesName: 'Bovino',
  breedName: 'Jersey',
  categoryName: 'Vaca',
  status: 'Active',
  isInWithdrawal: true,
  withdrawalUntil: '2026-08-10',
};

const happyStub = {
  getAnimals: () => of([regularFemale, femaleInWithdrawal]),
  recordMilkingSession: () => of({ id: 'milking-1' }),
};

const emojiPattern = /[\u{1F000}-\u{1FAFF}\u{2600}-\u{27BF}]/u;

describe('QuickMilkingComponent (responsive redesign contract)', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [QuickMilkingComponent],
      providers: [
        provideRouter([]),
        { provide: ApiService, useValue: happyStub },
      ],
    }).compileComponents();
  });

  function render(): HTMLElement {
    const fixture = TestBed.createComponent(QuickMilkingComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('should render the milking page without any emoji glyph', () => {
    const root = render();

    expect(root.textContent ?? '').not.toMatch(emojiPattern);
  });

  it('should display an app-icon next to the page title instead of an emoji glyph', () => {
    const root = render();
    const title = root.querySelector('h1.page-title');

    expect(title).not.toBeNull();
    // Implementation note: pick <app-icon name="milk"> for the milking title.
    expect(title!.querySelector('app-icon')).not.toBeNull();
  });

  it('should display an app-icon next to the save button instead of an emoji glyph', () => {
    const root = render();
    const saveButton = [...root.querySelectorAll('button')].find(
      (btn) => btn.classList.contains('btn-primary'),
    );

    expect(saveButton, 'expected a primary save button').toBeDefined();
    // Implementation note: pick <app-icon name="save"> for the save action.
    expect(saveButton!.querySelector('app-icon')).not.toBeNull();
  });

  it('should keep the exact withdrawal warning LECHE EN RETIRO (NO MEZCLAR) when an animal is in withdrawal', () => {
    const root = render();

    const withdrawalBadge = root.querySelector('.row-warning .badge-danger');
    expect(
      withdrawalBadge,
      'expected at least one row flagged with .row-warning for the withdrawal female',
    ).not.toBeNull();

    const badgeText = (withdrawalBadge!.textContent ?? '').toUpperCase();
    // Exact regulatory copy: singular RETIRO, never RETIROS.
    expect(badgeText).toContain('LECHE EN RETIRO (NO MEZCLAR)');
    expect(badgeText).not.toContain('RETIROS');
  });

  it('should use animal/animales in the general copy and include an Animal column header', () => {
    const root = render();

    // General copy (title + subtitle) must talk about "animal / animales",
    // not the old "vaca" wording.
    const headerText = (root.querySelector('.page-header')?.textContent ?? '').toLowerCase();
    expect(headerText).toContain('animal');

    // At least one column header must be the "Animal" column that the
    // responsive redesign introduces (replacing the old "Nombre Vaca").
    const headers = [...root.querySelectorAll('thead th')].map((th) =>
      (th.textContent ?? '').trim(),
    );
    expect(
      headers.some((h) => /^animal$/i.test(h)),
      `expected one table header to be exactly "Animal", got: ${JSON.stringify(headers)}`,
    ).toBe(true);
  });

  it('should render the production table with the responsive-table class and a data-label on every td', () => {
    const root = render();

    const table = root.querySelector('table.responsive-table');
    expect(table, 'expected <table class="responsive-table">').not.toBeNull();

    const cells = [...root.querySelectorAll('tbody tr:first-child td')];
    expect(cells.length, 'expected at least one production row').toBeGreaterThan(0);
    expect(
      cells.every((cell) => (cell.getAttribute('data-label') ?? '').trim().length > 0),
      'every <td> in the production table must carry a non-empty data-label',
    ).toBe(true);

    const headers = [...root.querySelectorAll('thead th')].map((th) =>
      (th.textContent ?? '').trim(),
    );
    const labels = cells.map((cell) => cell.getAttribute('data-label'));
    expect(labels).toEqual(headers);
  });

  it('should render an empty animal list with no fake VACA-001 demo data when the API errors', () => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [QuickMilkingComponent],
      providers: [
        provideRouter([]),
        {
          provide: ApiService,
          useValue: {
            getAnimals: () => throwError(() => new Error('network down')),
            recordMilkingSession: () => of({ id: 'milking-1' }),
          },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(QuickMilkingComponent);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;

    // Honest state: no fake demo rows, no invented identifiers, no emojis.
    expect(root.textContent ?? '').not.toContain('VACA-001');
    expect(root.textContent ?? '').not.toContain('VACA-002');
    expect(root.querySelectorAll('tbody tr').length).toBe(0);
  });
});
