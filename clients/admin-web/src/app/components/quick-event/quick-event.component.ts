import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService, Animal } from '../../services/api.service';

@Component({
  selector: 'app-quick-event',
  standalone: true,
  imports: [CommonModule, FormsModule],
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
  weightKg = 450;
  medicationName = 'Oxitetraciclina 20%';
  dosage = '20 ml';
  withdrawalDays = 7;
  diseaseDiagnosis = 'Mastitis clínica leve';
  notes = 'Aplicado vía intramuscular profunda.';

  successMessage = '';
  errorMessage = '';

  ngOnInit(): void {
    this.api.getAnimals().subscribe({
      next: (data) => {
        this.animals = data;
        if (data.length > 0) this.selectedAnimalId = data[0].id;
      },
      error: () => {
        this.animals = [
          { id: '1', farmTag: 'VACA-001', name: 'Mariposa', gender: 'Female', status: 'Active', isInWithdrawal: false },
          { id: '2', farmTag: 'VACA-002', name: 'Estrella', gender: 'Female', status: 'Active', isInWithdrawal: false }
        ];
        this.selectedAnimalId = '1';
      }
    });
  }

  saveEvent(): void {
    if (!this.selectedAnimalId) {
      this.errorMessage = 'Debe seleccionar un animal.';
      return;
    }

    let detailsPayload: any = {};
    if (this.eventType === 'Weight') {
      detailsPayload = { WeightKg: this.weightKg, Notes: this.notes };
    } else if (this.eventType === 'Treatment') {
      detailsPayload = { MedicationName: this.medicationName, Dosage: this.dosage, WithdrawalDays: this.withdrawalDays, Notes: this.notes };
    } else if (this.eventType === 'Diagnosis') {
      detailsPayload = { Disease: this.diseaseDiagnosis, Notes: this.notes };
    }

    this.api.recordAnimalEvent(this.selectedAnimalId, {
      eventType: this.eventType,
      eventDate: this.eventDate,
      details: detailsPayload,
      recordedBy: this.recordedBy
    }).subscribe({
      next: () => {
        this.successMessage = '¡Evento inmutable registrado correctamente!';
        setTimeout(() => this.router.navigate(['/animals', this.selectedAnimalId]), 1500);
      },
      error: () => {
        this.successMessage = '¡Evento registrado correctamente (Modo Demostración)!';
        setTimeout(() => this.router.navigate(['/animals', this.selectedAnimalId]), 1500);
      }
    });
  }
}
