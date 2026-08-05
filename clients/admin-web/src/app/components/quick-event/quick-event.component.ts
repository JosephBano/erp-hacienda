import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService, Animal } from '../../services/api.service';
import { IconComponent } from '../../shared/icon/icon.component';

@Component({
  selector: 'app-quick-event',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent],
  templateUrl: './quick-event.component.html',
  styleUrls: ['./quick-event.component.css']
})
export class QuickEventComponent implements OnInit {
  private api = inject(ApiService);
  private router = inject(Router);

  animals: Animal[] = [];
  selectedAnimalId = '';
  eventType = 'Treatment';
  eventDate = new Date().toISOString().split('T')[0];
  recordedBy = 'veterinario 1';

  // Event specific fields
  weightKg = 120;
  medicationName = '';
  dosage = '';
  withdrawalDays = 0;
  diseaseDiagnosis = '';
  notes = '';

  successMessage = '';
  errorMessage = '';
  loadError = false;

  ngOnInit(): void {
    this.api.getAnimals().subscribe({
      next: (data) => {
        this.loadError = false;
        this.animals = data;
        if (data.length > 0) this.selectedAnimalId = data[0].id;
      },
      error: () => {
        this.loadError = true;
        this.animals = [];
        this.selectedAnimalId = '';
        this.errorMessage = 'No se pudo cargar el listado de animales. Intente nuevamente.';
      }
    });
  }

  saveEvent(): void {
    this.successMessage = '';
    this.errorMessage = '';

    if (!this.selectedAnimalId) {
      this.errorMessage = 'Debe seleccionar un animal.';
      return;
    }

    let detailsPayload: any = {};
    let milkWithdrawalDays: number | undefined;
    if (this.eventType === 'Weighing') {
      detailsPayload = { WeightKg: this.weightKg, Notes: this.notes };
    } else if (this.eventType === 'Treatment') {
      detailsPayload = { MedicationName: this.medicationName, Dosage: this.dosage, WithdrawalDays: this.withdrawalDays, Notes: this.notes };
      milkWithdrawalDays = this.withdrawalDays;
    } else if (this.eventType === 'Diagnosis') {
      detailsPayload = { Disease: this.diseaseDiagnosis, Notes: this.notes };
    }

    this.api.recordAnimalEvent(this.selectedAnimalId, {
      eventType: this.eventType,
      eventDate: this.eventDate,
      details: detailsPayload,
      recordedBy: this.recordedBy,
      milkWithdrawalDays
    }).subscribe({
      next: () => {
        this.successMessage = 'Evento inmutable registrado correctamente.';
        setTimeout(() => this.router.navigate(['/animals', this.selectedAnimalId]), 1500);
      },
      error: () => {
        this.errorMessage = 'No se pudo registrar el evento. Revise la conexión e intente nuevamente.';
      }
    });
  }
}
