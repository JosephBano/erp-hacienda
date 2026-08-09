import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { ApiService, CreateAnimalGroupRequest, SpeciesDto } from '../../services/api.service';
import { IconComponent } from '../../shared/icon/icon.component';

/**
 * PR3 — `/animal-groups/new`. Form con:
 *  - nombre (required, max 100)
 *  - descripción (opcional, max 500)
 *  - especie (dropdown, opcional)
 *  - modo de seguimiento (radio Individual / Por conteo + texto de ayuda)
 *
 * Submit → POST → navega al detalle del grupo recién creado.
 */
@Component({
  selector: 'app-animal-group-create',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, IconComponent],
  templateUrl: './animal-group-create.component.html',
  styleUrls: ['./animal-group-create.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AnimalGroupCreateComponent implements OnInit {
  private api = inject(ApiService);
  private router = inject(Router);

  name = '';
  description = '';
  speciesId = '';
  trackingMode: 'Individual' | 'Headcount' = 'Individual';

  readonly species = signal<SpeciesDto[]>([]);
  readonly submitting = signal<boolean>(false);
  readonly errorMessage = signal<string>('');

  ngOnInit(): void {
    this.api.getSpecies().subscribe({
      next: (data) => this.species.set(data),
      error: () => this.species.set([]),
    });
  }

  canSubmit(): boolean {
    return this.name.trim().length > 0 && !this.submitting();
  }

  submit(): void {
    if (!this.canSubmit()) return;
    this.submitting.set(true);
    this.errorMessage.set('');

    const body: CreateAnimalGroupRequest = {
      name: this.name.trim(),
      description: this.description.trim() || null,
      speciesId: this.speciesId || null,
      trackingMode: this.trackingMode,
    };

    this.api.createAnimalGroup(body).subscribe({
      next: (resp) => {
        this.submitting.set(false);
        this.router.navigate(['/animal-groups', resp.id]);
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        const e = err as { error?: { detail?: string }; message?: string };
        this.errorMessage.set(e?.error?.detail ?? e?.message ?? 'No se pudo crear el lote.');
      },
    });
  }

  cancel(): void {
    this.router.navigate(['/animal-groups']);
  }
}
