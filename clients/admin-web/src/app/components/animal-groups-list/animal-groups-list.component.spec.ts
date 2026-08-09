import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AnimalGroupsListComponent } from './animal-groups-list.component';
import { ApiService, AnimalGroupDto } from '../../services/api.service';

describe('AnimalGroupsListComponent', () => {
  const groups: AnimalGroupDto[] = [
    {
      id: 'g1',
      name: 'Vacas en ordeño',
      description: 'Lote principal',
      speciesId: 's1',
      speciesName: 'Bovino',
      isActive: true,
      trackingMode: 'Individual',
      liveHeadCount: 18,
      memberships: [],
    },
    {
      id: 'g2',
      name: 'Engorde marzo-2026',
      description: null,
      speciesId: null,
      speciesName: null,
      isActive: false,
      trackingMode: 'Headcount',
      liveHeadCount: 0,
      memberships: [],
    },
  ];

  let apiStub: Partial<ApiService>;

  beforeEach(async () => {
    apiStub = {
      getAnimalGroups: () => of(groups),
      deactivateAnimalGroup: () => of(undefined),
      activateAnimalGroup: () => of(undefined),
    };
    await TestBed.configureTestingModule({
      imports: [AnimalGroupsListComponent],
      providers: [
        provideRouter([]),
        { provide: ApiService, useValue: apiStub },
      ],
    }).compileComponents();
  });

  it('mounts and renders rows from the API', () => {
    const fixture = TestBed.createComponent(AnimalGroupsListComponent);
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    // Two data rows + one empty-row fallback is hidden when rows > 0.
    expect(rows.length).toBe(2);
  });

  it('renders no emoji anywhere', () => {
    const fixture = TestBed.createComponent(AnimalGroupsListComponent);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    const emojiPattern = /[\u{1F000}-\u{1FAFF}\u{2600}-\u{27BF}]/u;
    expect(emojiPattern.test(text)).toBe(false);
  });

  it('shows the empty row when the API returns no groups', () => {
    apiStub.getAnimalGroups = () => of([]);
    const fixture = TestBed.createComponent(AnimalGroupsListComponent);
    fixture.detectChanges();

    const emptyRow = fixture.nativeElement.querySelector('.empty-row');
    expect(emptyRow).toBeTruthy();
    expect(emptyRow.textContent).toContain('No hay lotes');
  });

  it('navigates to the detail page when the Edit action is clicked', () => {
    const fixture = TestBed.createComponent(AnimalGroupsListComponent);
    fixture.detectChanges();

    const router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate');

    // Find the edit button on the first row (g1).
    const editButtons = fixture.nativeElement.querySelectorAll(
      '[data-action="Editar"]',
    );
    (editButtons[0] as HTMLElement).click();

    expect(router.navigate).toHaveBeenCalledWith(['/animal-groups', 'g1']);
  });

  it('shows the deactivate confirm bar and calls the API on confirm', () => {
    const fixture = TestBed.createComponent(AnimalGroupsListComponent);
    fixture.detectChanges();

    vi.spyOn(apiStub, 'deactivateAnimalGroup');

    const deactivateButtons = fixture.nativeElement.querySelectorAll(
      '[data-action="Desactivar"]',
    );
    expect(deactivateButtons.length).toBe(1); // only the active group
    (deactivateButtons[0] as HTMLElement).click();
    fixture.detectChanges();

    const confirmBar = fixture.nativeElement.querySelector(
      '[data-testid="confirm-deactivate"]',
    );
    expect(confirmBar).toBeTruthy();

    (confirmBar as HTMLElement).click();
    expect(apiStub.deactivateAnimalGroup).toHaveBeenCalledWith('g1');
  });

  it('exposes Reactivate only for inactive groups', () => {
    const fixture = TestBed.createComponent(AnimalGroupsListComponent);
    fixture.detectChanges();

    const reactivateButtons = fixture.nativeElement.querySelectorAll(
      '[data-action="Reactivar"]',
    );
    expect(reactivateButtons.length).toBe(1); // only g2 is inactive
  });

  it('shows an error message when the load fails', () => {
    apiStub.getAnimalGroups = () => throwError(() => ({ status: 500, message: 'boom' }));
    const fixture = TestBed.createComponent(AnimalGroupsListComponent);
    fixture.detectChanges();

    const alert = fixture.nativeElement.querySelector('.alert-danger');
    expect(alert).toBeTruthy();
  });
});
