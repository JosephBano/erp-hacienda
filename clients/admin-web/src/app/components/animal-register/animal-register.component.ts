import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService, AnimalCategoryDto, BreedDto, SpeciesDto } from '../../services/api.service';
import { IconComponent } from '../../shared/icon/icon.component';

/**
 * Registers a new animal. Nothing on this screen existed before: `POST /api/v1/animals`
 * worked, but there was no way to see what species/breeds/categories exist to register
 * it against, and no form calling it. A brand-new species also has neither breeds nor
 * categories yet, so both are created inline here rather than sent to a separate screen —
 * the whole point is that a first-time setup takes one visit, not three.
 */
@Component({
  selector: 'app-animal-register',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent],
  templateUrl: './animal-register.component.html',
  styleUrls: ['./animal-register.component.css']
})
export class AnimalRegisterComponent implements OnInit {
  private api = inject(ApiService);
  private router = inject(Router);

  species: SpeciesDto[] = [];
  breeds: BreedDto[] = [];
  categories: AnimalCategoryDto[] = [];

  selectedSpeciesId = '';
  selectedBreedId = '';
  selectedCategoryId = '';
  sex: 'Female' | 'Male' = 'Female';
  birthDate = '';
  farmTag = '';

  showNewSpeciesForm = false;
  newSpeciesName = '';
  newSpeciesGestationDays: number | null = null;

  showNewBreedForm = false;
  newBreedName = '';

  showNewCategoryForm = false;
  newCategoryName = '';

  busy = false;
  successMessage = '';
  errorMessage = '';

  ngOnInit(): void {
    this.loadSpecies();
  }

  loadSpecies(): void {
    this.api.getSpecies().subscribe({
      next: (data) => {
        this.species = data;
        if (data.length > 0 && !this.selectedSpeciesId) {
          this.selectedSpeciesId = data[0].id;
          this.onSpeciesChange();
        }
      },
      error: () => {
        this.errorMessage = 'No se pudo cargar el catálogo de especies.';
      }
    });
  }

  onSpeciesChange(): void {
    this.selectedBreedId = '';
    this.selectedCategoryId = '';
    this.breeds = [];
    this.categories = [];

    if (!this.selectedSpeciesId) return;

    this.api.getBreeds(this.selectedSpeciesId).subscribe({ next: (data) => (this.breeds = data) });
    this.api.getAnimalCategories(this.selectedSpeciesId).subscribe({ next: (data) => (this.categories = data) });
  }

  saveNewSpecies(): void {
    if (!this.newSpeciesName.trim()) {
      this.errorMessage = 'El nombre de la especie es obligatorio.';
      return;
    }

    this.api.createSpecies({
      name: this.newSpeciesName.trim(),
      gestationDays: this.newSpeciesGestationDays ?? undefined
    }).subscribe({
      next: (result) => {
        this.newSpeciesName = '';
        this.newSpeciesGestationDays = null;
        this.showNewSpeciesForm = false;
        this.errorMessage = '';
        this.api.getSpecies().subscribe({
          next: (data) => {
            this.species = data;
            this.selectedSpeciesId = result.id;
            this.onSpeciesChange();
          }
        });
      },
      error: (err) => {
        this.errorMessage = err?.error?.detail || 'No se pudo crear la especie.';
      }
    });
  }

  saveNewBreed(): void {
    if (!this.newBreedName.trim() || !this.selectedSpeciesId) return;

    this.api.createBreed({ speciesId: this.selectedSpeciesId, name: this.newBreedName.trim() }).subscribe({
      next: (result) => {
        this.newBreedName = '';
        this.showNewBreedForm = false;
        this.api.getBreeds(this.selectedSpeciesId).subscribe({
          next: (data) => {
            this.breeds = data;
            this.selectedBreedId = result.id;
          }
        });
      },
      error: (err) => {
        this.errorMessage = err?.error?.detail || 'No se pudo crear la raza.';
      }
    });
  }

  saveNewCategory(): void {
    if (!this.newCategoryName.trim() || !this.selectedSpeciesId) return;

    this.api.createAnimalCategory({ speciesId: this.selectedSpeciesId, name: this.newCategoryName.trim() }).subscribe({
      next: (result) => {
        this.newCategoryName = '';
        this.showNewCategoryForm = false;
        this.api.getAnimalCategories(this.selectedSpeciesId).subscribe({
          next: (data) => {
            this.categories = data;
            this.selectedCategoryId = result.id;
          }
        });
      },
      error: (err) => {
        this.errorMessage = err?.error?.detail || 'No se pudo crear la categoría.';
      }
    });
  }

  registerAnimal(): void {
    this.errorMessage = '';
    this.successMessage = '';

    if (!this.selectedSpeciesId) {
      this.errorMessage = 'Debe seleccionar o crear una especie.';
      return;
    }

    this.busy = true;

    this.api.registerAnimal({
      speciesId: this.selectedSpeciesId,
      sex: this.sex,
      breedId: this.selectedBreedId || undefined,
      categoryId: this.selectedCategoryId || undefined,
      birthDate: this.birthDate || undefined
    }).subscribe({
      next: (result) => {
        if (this.farmTag.trim()) {
          this.api.assignAnimalIdentifier(result.id, {
            type: 'FarmTag',
            value: this.farmTag.trim(),
            validFrom: this.birthDate || new Date().toISOString().split('T')[0]
          }).subscribe({
            complete: () => this.finishRegistration(result.id)
          });
        } else {
          this.finishRegistration(result.id);
        }
      },
      error: (err) => {
        this.busy = false;
        this.errorMessage = err?.error?.detail || 'No se pudo registrar el animal.';
      }
    });
  }

  private finishRegistration(animalId: string): void {
    this.busy = false;
    this.successMessage = 'Animal registrado correctamente.';
    this.router.navigate(['/animals', animalId]);
  }
}
