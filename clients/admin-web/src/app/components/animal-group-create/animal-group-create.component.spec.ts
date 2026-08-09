import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AnimalGroupCreateComponent } from './animal-group-create.component';
import { ApiService, SpeciesDto } from '../../services/api.service';

describe('AnimalGroupCreateComponent', () => {
  const species: SpeciesDto[] = [
    { id: 's1', name: 'Bovino', isMilkable: true },
    { id: 's2', name: 'Porcino', isMilkable: false },
  ];

  let apiStub: Partial<ApiService>;

  beforeEach(async () => {
    apiStub = {
      getSpecies: () => of(species),
      createAnimalGroup: () => of({ id: 'new-id' }),
    };
    await TestBed.configureTestingModule({
      imports: [AnimalGroupCreateComponent],
      providers: [
        provideRouter([]),
        { provide: ApiService, useValue: apiStub },
      ],
    }).compileComponents();
  });

  it('mounts and renders the create form', () => {
    const fixture = TestBed.createComponent(AnimalGroupCreateComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="group-name-input"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('[data-testid="group-description-input"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('[data-testid="group-species-select"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('[data-testid="tracking-mode-individual"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('[data-testid="tracking-mode-headcount"]')).toBeTruthy();
  });

  it('renders no emoji in the help text', () => {
    const fixture = TestBed.createComponent(AnimalGroupCreateComponent);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    const emojiPattern = /[\u{1F000}-\u{1FAFF}\u{2600}-\u{27BF}]/u;
    expect(emojiPattern.test(text)).toBe(false);
  });

  it('starts with the submit button disabled (empty name)', () => {
    const fixture = TestBed.createComponent(AnimalGroupCreateComponent);
    fixture.detectChanges();

    const submit = fixture.nativeElement.querySelector(
      '[data-testid="submit-button"]',
    ) as HTMLButtonElement;
    expect(submit.disabled).toBe(true);
  });

  it('submits and navigates to the new detail page on success', () => {
    const fixture = TestBed.createComponent(AnimalGroupCreateComponent);
    fixture.componentInstance.name = 'Engorde abril-2026';
    fixture.componentInstance.speciesId = 's2';
    fixture.componentInstance.trackingMode = 'Headcount';
    fixture.detectChanges();

    vi.spyOn(apiStub, 'createAnimalGroup');
    const router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate');

    (fixture.nativeElement.querySelector('[data-testid="submit-button"]') as HTMLButtonElement).click();

    expect(apiStub.createAnimalGroup).toHaveBeenCalledWith({
      name: 'Engorde abril-2026',
      description: null,
      speciesId: 's2',
      trackingMode: 'Headcount',
    });
    expect(router.navigate).toHaveBeenCalledWith(['/animal-groups', 'new-id']);
  });

  it('shows an error message when the API rejects', () => {
    apiStub.createAnimalGroup = () => throwError(() => ({
      error: { detail: 'El nombre del grupo no puede estar vacío.' },
    }));
    const fixture = TestBed.createComponent(AnimalGroupCreateComponent);
    fixture.componentInstance.name = 'whatever';
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('[data-testid="submit-button"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    const alert = fixture.nativeElement.querySelector('.alert-danger');
    expect(alert).toBeTruthy();
    expect(alert.textContent).toContain('vacío');
  });
});
