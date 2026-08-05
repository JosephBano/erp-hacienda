import { of } from 'rxjs';
import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { ApiService, Animal } from '../../services/api.service';
import { AnimalListComponent } from './animal-list.component';

const animal: Animal = {
  id: 'animal-1',
  farmTag: 'F-001',
  officialTag: 'S-001',
  name: 'Luna',
  gender: 'Female',
  speciesName: 'Bovino',
  breedName: 'Holstein',
  categoryName: 'Vaca',
  status: 'Active',
  isInWithdrawal: false,
};

const apiStub = {
  getAnimals: () => of([animal]),
};

const emojiPattern = /[\u{1F000}-\u{1FAFF}\u{2600}-\u{27BF}]/u;

describe('AnimalListComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AnimalListComponent],
      providers: [
        provideRouter([]),
        { provide: ApiService, useValue: apiStub },
      ],
    }).compileComponents();
  });

  function renderAnimalList(): HTMLElement {
    const fixture = TestBed.createComponent(AnimalListComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('should title the page as Inventario de animales', () => {
    const root = renderAnimalList();

    expect(root.querySelector('h1')?.textContent?.trim()).toBe('Inventario de animales');
  });

  it('should render one animal row with non-empty labels matching every table header', () => {
    const root = renderAnimalList();
    const headers = [...root.querySelectorAll('thead th')].map((th) => th.textContent?.trim());
    const cells = [...root.querySelectorAll('tbody tr:first-child td')];

    expect(cells.length).toBeGreaterThan(0);
    expect(cells.every((cell) => cell.getAttribute('data-label')?.trim())).toBe(true);
    expect(cells.map((cell) => cell.getAttribute('data-label'))).toEqual(headers);
  });

  it('should render the animal inventory without emoji glyphs', () => {
    const root = renderAnimalList();

    expect(root.textContent ?? '').not.toMatch(emojiPattern);
  });
});
