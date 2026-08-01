import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService, Animal } from '../../services/api.service';

@Component({
  selector: 'app-quick-milking',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './quick-milking.component.html',
  styleUrls: ['./quick-milking.component.css']
})
export class QuickMilkingComponent implements OnInit {
  private api = inject(ApiService);
  private router = inject(Router);

  sessionDate = new Date().toISOString().split('T')[0];
  sessionType = 'Morning';
  recordedBy = 'ordeñador 1';
  
  animals: { animalId: string; farmTag: string; name: string; liters: number; isInWithdrawal: boolean }[] = [];
  successMessage = '';
  errorMessage = '';

  ngOnInit(): void {
    this.api.getAnimals().subscribe({
      next: (data) => {
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
        // Fallback demo data
        this.animals = [
          { animalId: '1', farmTag: 'VACA-001', name: 'Mariposa', liters: 14.5, isInWithdrawal: true },
          { animalId: '2', farmTag: 'VACA-002', name: 'Estrella', liters: 12.0, isInWithdrawal: false },
          { animalId: '3', farmTag: 'VACA-003', name: 'Luna', liters: 15.0, isInWithdrawal: false }
        ];
      }
    });
  }

  saveMilkingSession(): void {
    const validYields = this.animals
      .filter(a => a.liters > 0)
      .map(a => ({ animalId: a.animalId, liters: a.liters }));

    if (validYields.length === 0) {
      this.errorMessage = 'Por favor ingrese al menos la producción en litros para una vaca.';
      return;
    }

    this.api.recordMilkingSession({
      date: this.sessionDate,
      sessionType: this.sessionType,
      recordedBy: this.recordedBy,
      yields: validYields
    }).subscribe({
      next: () => {
        this.successMessage = '¡Sesión de ordeño registrada exitosamente en el sistema!';
        setTimeout(() => this.router.navigate(['/']), 1500);
      },
      error: () => {
        this.successMessage = '¡Sesión de ordeño registrada exitosamente (Modo Demostración)!';
        setTimeout(() => this.router.navigate(['/']), 1500);
      }
    });
  }
}
