import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../../services/api.service';
import { IconComponent } from '../../shared/icon/icon.component';

@Component({
  selector: 'app-quick-milking',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent],
  templateUrl: './quick-milking.component.html',
  styleUrls: ['./quick-milking.component.css']
})
export class QuickMilkingComponent implements OnInit {
  private api = inject(ApiService);
  private router = inject(Router);

  sessionDate = new Date().toISOString().split('T')[0];
  sessionType = 'Morning';
  recordedBy = 'operario 1';

  animals: { animalId: string; farmTag: string; name: string; liters: number; isInWithdrawal: boolean }[] = [];
  successMessage = '';
  errorMessage = '';
  loadError = false;

  ngOnInit(): void {
    this.api.getAnimals().subscribe({
      next: (data) => {
        this.loadError = false;
        const females = data.filter(a => a.gender === 'Female' || !a.gender);
        this.animals = females.map(a => ({
          animalId: a.id,
          farmTag: a.farmTag || 'S/A',
          name: a.name || 'Sin nombre',
          liters: 0,
          isInWithdrawal: a.isInWithdrawal
        }));
      },
      error: () => {
        this.loadError = true;
        this.animals = [];
        this.errorMessage = 'No se pudo cargar el listado de animales. Intente nuevamente.';
      }
    });
  }

  saveMilkingSession(): void {
    this.successMessage = '';
    this.errorMessage = '';

    const validYields = this.animals
      .filter(a => a.liters > 0)
      .map(a => ({ animalId: a.animalId, liters: a.liters }));

    if (validYields.length === 0) {
      this.errorMessage = 'Ingrese la producción en litros para al menos un animal.';
      return;
    }

    this.api.recordMilkingSession({
      date: this.sessionDate,
      sessionType: this.sessionType,
      recordedBy: this.recordedBy,
      yields: validYields
    }).subscribe({
      next: () => {
        this.successMessage = 'Sesión de ordeño registrada exitosamente.';
        setTimeout(() => this.router.navigate(['/']), 1500);
      },
      error: () => {
        this.errorMessage = 'No se pudo registrar la sesión de ordeño. Revise la conexión e intente nuevamente.';
      }
    });
  }
}
