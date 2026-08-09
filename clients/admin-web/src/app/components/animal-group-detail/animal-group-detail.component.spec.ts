import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AnimalGroupDetailComponent } from './animal-group-detail.component';
import { ApiService, AnimalGroupDto, AnimalGroupSummaryDto, SpeciesDto } from '../../services/api.service';

describe('AnimalGroupDetailComponent', () => {
  const group: AnimalGroupDto = {
    id: 'g1',
    name: 'Vacas en ordeño',
    description: 'Lote principal',
    speciesId: 's1',
    speciesName: 'Bovino',
    isActive: true,
    trackingMode: 'Individual',
    liveHeadCount: 12,
    memberships: [
      { id: 'm1', animalId: 'a1', joinedAt: '2026-01-15', leftAt: null, isActive: true },
      { id: 'm2', animalId: 'a2', joinedAt: '2025-11-01', leftAt: '2026-02-01', isActive: false },
    ],
  };

  const summary: AnimalGroupSummaryDto = {
    groupId: 'g1',
    liveHeadCount: 12,
    headsAffectedByDiagnosis: 2,
    lastVaccinationAt: '2026-07-15T10:00:00Z',
    lastDisposalAt: '2026-08-01T10:00:00Z',
    lastTreatmentAt: '2026-08-05T10:00:00Z',
  };

  const species: SpeciesDto[] = [
    { id: 's1', name: 'Bovino', isMilkable: true },
  ];

  let apiStub: Partial<ApiService>;

  beforeEach(async () => {
    apiStub = {
      getAnimalGroupById: () => of(group),
      getAnimalGroupSummary: () => of(summary),
      getSpecies: () => of(species),
      updateAnimalGroup: () => of(undefined),
      deactivateAnimalGroup: () => of(undefined),
      activateAnimalGroup: () => of(undefined),
      changeAnimalGroupTrackingMode: () => of(undefined),
    };
    await TestBed.configureTestingModule({
      imports: [AnimalGroupDetailComponent],
      providers: [
        provideRouter([]),
        { provide: ApiService, useValue: apiStub },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: new Map([['id', 'g1']]) } },
        },
      ],
    }).compileComponents();
  });

  it('mounts and renders the group name + summary cards', () => {
    const fixture = TestBed.createComponent(AnimalGroupDetailComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.page-title')?.textContent).toContain('Vacas en ordeño');
    expect(fixture.nativeElement.querySelector('[data-testid="live-head-count"]')?.textContent).toContain('12');
  });

  it('marks notFound when the API returns 404', () => {
    apiStub.getAnimalGroupById = () => throwError(() => ({ status: 404 }));
    const fixture = TestBed.createComponent(AnimalGroupDetailComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Lote no encontrado');
  });

  it('shows an error alert when the load fails with non-404', () => {
    apiStub.getAnimalGroupById = () => throwError(() => ({ status: 500, message: 'boom' }));
    const fixture = TestBed.createComponent(AnimalGroupDetailComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.alert-danger')).toBeTruthy();
  });

  it('renders no emoji in any visible text', () => {
    const fixture = TestBed.createComponent(AnimalGroupDetailComponent);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    const emojiPattern = /[\u{1F000}-\u{1FAFF}\u{2600}-\u{27BF}]/u;
    expect(emojiPattern.test(text)).toBe(false);
  });

  it('calls deactivateAnimalGroup and reloads on confirm', () => {
    const fixture = TestBed.createComponent(AnimalGroupDetailComponent);
    fixture.detectChanges();

    vi.spyOn(apiStub, 'deactivateAnimalGroup');

    (fixture.nativeElement.querySelector('[data-testid="deactivate-detail"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('[data-testid="confirm-deactivate-detail"]') as HTMLButtonElement).click();

    expect(apiStub.deactivateAnimalGroup).toHaveBeenCalledWith('g1');
  });

  it('calls activateAnimalGroup on reactivate', () => {
    const inactive = { ...group, isActive: false };
    apiStub.getAnimalGroupById = () => of(inactive);
    const fixture = TestBed.createComponent(AnimalGroupDetailComponent);
    fixture.detectChanges();

    vi.spyOn(apiStub, 'activateAnimalGroup');

    (fixture.nativeElement.querySelector('[data-testid="reactivate-detail"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('[data-testid="confirm-reactivate-detail"]') as HTMLButtonElement).click();

    expect(apiStub.activateAnimalGroup).toHaveBeenCalledWith('g1');
  });

  it('calls changeAnimalGroupTrackingMode with a different mode', () => {
    const fixture = TestBed.createComponent(AnimalGroupDetailComponent);
    fixture.detectChanges();

    vi.spyOn(apiStub, 'changeAnimalGroupTrackingMode');

    // The fixture's group is currently 'Individual'; click the "to headcount" button.
    (fixture.nativeElement.querySelector('[data-testid="change-to-headcount"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('[data-testid="confirm-tracking-mode"]') as HTMLButtonElement).click();

    expect(apiStub.changeAnimalGroupTrackingMode).toHaveBeenCalledWith('g1', 'Headcount');
  });

  it('renders members list (active and historical)', () => {
    const fixture = TestBed.createComponent(AnimalGroupDetailComponent);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent ?? '';
    expect(text).toContain('Miembros activos');
    expect(text).toContain('a1');
    expect(text).toContain('Histórico');
    expect(text).toContain('a2');
  });
});
