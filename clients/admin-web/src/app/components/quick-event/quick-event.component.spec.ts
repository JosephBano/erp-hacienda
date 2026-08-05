import { of, throwError } from 'rxjs';
import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { ApiService, Animal } from '../../services/api.service';
import { QuickEventComponent } from './quick-event.component';

/**
 * Behavioral RED tests for QuickEventComponent.
 *
 * These tests pin down the markup & copy contract for the responsive redesign:
 *   - zero emojis anywhere in the rendered DOM
 *   - page title and primary action use <app-icon>, not emoji glyphs
 *   - the four event type options remain Tratamiento, Pesaje, Diagnóstico
 *     and Vacunación but their text must be emoji-free
 *   - when the API errors out, the component shows an empty list and never
 *     fakes "VACA-001" demo data
 *
 * All tests must fail RED today against the current markup/copy and pass
 * once the responsive redesign is implemented in the component template.
 */

const healthyAnimal: Animal = {
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

const secondAnimal: Animal = {
  id: 'animal-2',
  farmTag: 'F-102',
  officialTag: 'S-102',
  name: 'Estrella',
  birthDate: '2021-09-12',
  gender: 'Female',
  speciesName: 'Bovino',
  breedName: 'Jersey',
  categoryName: 'Vaca',
  status: 'Active',
  isInWithdrawal: false,
};

const happyStub = {
  getAnimals: () => of([healthyAnimal, secondAnimal]),
  recordAnimalEvent: () => of({ id: 'event-1' }),
};

const emojiPattern = /[\u{1F000}-\u{1FAFF}\u{2600}-\u{27BF}]/u;

const expectedEventTypes = [
  { value: 'Treatment', keyword: 'tratamiento' },
  { value: 'Weighing', keyword: 'pesaje' },
  { value: 'Diagnosis', keyword: 'diagnóstico' },
  { value: 'Vaccination', keyword: 'vacunación' },
] as const;

describe('QuickEventComponent (responsive redesign contract)', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [QuickEventComponent],
      providers: [
        provideRouter([]),
        { provide: ApiService, useValue: happyStub },
      ],
    }).compileComponents();
  });

  function render(): HTMLElement {
    const fixture = TestBed.createComponent(QuickEventComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('should render the event page without any emoji glyph', () => {
    const root = render();

    expect(root.textContent ?? '').not.toMatch(emojiPattern);
  });

  it('should display an app-icon next to the page title instead of an emoji glyph', () => {
    const root = render();
    const title = root.querySelector('h1.page-title');

    expect(title).not.toBeNull();
    // Implementation note: pick <app-icon name="activity"> or "file-text"
    // for the immutable-event title.
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

  it('should keep Tratamiento, Pesaje, Diagnóstico and Vacunación as event type options without emoji glyphs', () => {
    const root = render();

    const allOptions = [...root.querySelectorAll('option')];
    const eventOptions = expectedEventTypes.map(({ value, keyword }) => {
      const option = allOptions.find((o) => o.getAttribute('value') === value);
      return { value, keyword, option };
    });

    for (const { value, option } of eventOptions) {
      expect(
        option,
        `expected an <option value="${value}"> to be present`,
      ).toBeDefined();
      expect(option!.textContent ?? '').not.toMatch(emojiPattern);
    }

    const optionTexts = eventOptions.map(({ option }) =>
      (option!.textContent ?? '').toLowerCase(),
    );

    expect(
      optionTexts.some((t) => t.includes('tratamiento')),
      `expected Tratamiento wording in event type options, got: ${JSON.stringify(optionTexts)}`,
    ).toBe(true);
    expect(
      optionTexts.some((t) => t.includes('pesaje')),
      `expected Pesaje wording in event type options, got: ${JSON.stringify(optionTexts)}`,
    ).toBe(true);
    expect(
      optionTexts.some((t) => t.includes('diagnóstico')),
      `expected Diagnóstico wording in event type options, got: ${JSON.stringify(optionTexts)}`,
    ).toBe(true);
    expect(
      optionTexts.some((t) => t.includes('vacunación')),
      `expected Vacunación wording in event type options, got: ${JSON.stringify(optionTexts)}`,
    ).toBe(true);
  });

  it('should render an empty animal list with no fake VACA-001 demo data when the API errors', () => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [QuickEventComponent],
      providers: [
        provideRouter([]),
        {
          provide: ApiService,
          useValue: {
            getAnimals: () => throwError(() => new Error('network down')),
            recordAnimalEvent: () => of({ id: 'event-1' }),
          },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(QuickEventComponent);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;

    // Honest state: no fake demo rows, no invented identifiers, no emojis.
    expect(root.textContent ?? '').not.toContain('VACA-001');
    expect(root.textContent ?? '').not.toContain('VACA-002');
    const animalSelect = root.querySelectorAll('select')[0];
    expect(animalSelect?.querySelectorAll('option').length ?? 0).toBe(0);
  });
});
