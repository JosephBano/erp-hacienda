import { of } from 'rxjs';
import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { ApiService, Animal } from '../../services/api.service';
import { DashboardComponent } from './dashboard.component';

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
  getMilkingSessions: () => of([]),
};

const emojiPattern = /[\u{1F000}-\u{1FAFF}\u{2600}-\u{27BF}]/u;

describe('DashboardComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        provideRouter([]),
        { provide: ApiService, useValue: apiStub },
      ],
    }).compileComponents();
  });

  function renderDashboard(): HTMLElement {
    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('should title the recent animals section as Animales recientes', () => {
    const root = renderDashboard();

    expect(root.querySelector('h2')?.textContent?.trim()).toBe('Animales recientes');
  });

  it('should describe the active animals metric with the desired finca copy', () => {
    const root = renderDashboard();

    expect(root.textContent).toContain('Animales activos en la finca');
  });

  it('should render one recent animal row with labels matching every table header', () => {
    const root = renderDashboard();
    const headers = [...root.querySelectorAll('thead th')].map((th) => th.textContent?.trim());
    const cells = [...root.querySelectorAll('tbody tr:first-child td')];

    expect(cells.length).toBeGreaterThan(0);
    expect(cells.every((cell) => cell.getAttribute('data-label')?.trim())).toBe(true);
    expect(cells.map((cell) => cell.getAttribute('data-label'))).toEqual(headers);
  });

  it('should render the dashboard without emoji glyphs', () => {
    const root = renderDashboard();

    expect(root.textContent ?? '').not.toMatch(emojiPattern);
  });
});
