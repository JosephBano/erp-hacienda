import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService, SemenStraw, PregnancyDto, AlertDto, Animal } from '../../services/api.service';

@Component({
  selector: 'app-breeding-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './breeding-dashboard.component.html',
  styleUrls: ['./breeding-dashboard.component.css']
})
export class BreedingDashboardComponent implements OnInit {
  private api = inject(ApiService);

  activeTab: 'alerts' | 'pregnancies' | 'service' | 'straws' = 'alerts';

  // Data
  alerts: AlertDto[] = [];
  pregnancies: PregnancyDto[] = [];
  straws: SemenStraw[] = [];
  animals: Animal[] = [];

  // Form states
  loading = false;
  successMessage = '';
  errorMessage = '';

  // Service form
  serviceForm = {
    damId: '',
    serviceType: 'ArtificialInsemination', // 'Natural' | 'ArtificialInsemination'
    serviceDate: new Date().toISOString().substring(0, 10),
    sireAnimalId: '',
    strawId: '',
    technician: '',
    notes: '',
    bodyConditionScore: 3.0
  };

  // Pregnancy check form
  checkForm = {
    serviceId: '',
    checkDate: new Date().toISOString().substring(0, 10),
    method: 'Palpation', // 'Palpation' | 'Ultrasound' | 'NonReturn'
    result: 'Positive', // 'Positive' | 'Negative' | 'Doubtful'
    gestationDays: 283,
    checkedBy: '',
    notes: ''
  };

  // Birthing form
  birthingForm = {
    damId: '',
    pregnancyId: '',
    birthDate: new Date().toISOString().substring(0, 10),
    difficulty: 'Normal',
    bornAlive: 1,
    bornDead: 0,
    mummified: 0,
    litterWeight: 35.0,
    notes: ''
  };

  // Straw form
  strawForm = {
    code: '',
    bullName: '',
    bullCode: '',
    breedId: '00000000-0000-0000-0000-000000000000',
    supplierName: '',
    quantity: 10,
    notes: ''
  };

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading = true;
    this.api.getAlerts().subscribe({
      next: (data) => (this.alerts = data),
      error: (err) => console.error(err)
    });

    this.api.getActivePregnancies().subscribe({
      next: (data) => (this.pregnancies = data),
      error: (err) => console.error(err)
    });

    this.api.getSemenStraws().subscribe({
      next: (data) => (this.straws = data),
      error: (err) => console.error(err)
    });

    this.api.getAnimals().subscribe({
      next: (data) => {
        this.animals = data;
        this.loading = false;
      },
      error: () => (this.loading = false)
    });
  }

  refreshAlerts(): void {
    this.loading = true;
    this.api.generateAlerts().subscribe({
      next: (res) => {
        this.successMessage = `Alertas actualizadas (${res.generatedAlerts} nuevas).`;
        this.loadData();
      },
      error: (err) => {
        this.errorMessage = 'Error generando alertas.';
        this.loading = false;
      }
    });
  }

  dismissAlert(id: string): void {
    this.api.dismissAlert(id).subscribe({
      next: () => {
        this.alerts = this.alerts.filter((a) => a.id !== id);
      }
    });
  }

  submitService(): void {
    this.loading = true;
    this.errorMessage = '';
    this.successMessage = '';

    const payload: any = {
      damId: this.serviceForm.damId,
      serviceType: this.serviceForm.serviceType,
      serviceDate: this.serviceForm.serviceDate,
      technician: this.serviceForm.technician,
      notes: this.serviceForm.notes,
      bodyConditionScore: this.serviceForm.bodyConditionScore
    };

    if (this.serviceForm.serviceType === 'ArtificialInsemination') {
      payload.strawId = this.serviceForm.strawId;
    } else {
      payload.sireAnimalId = this.serviceForm.sireAnimalId;
    }

    this.api.registerBreedingService(payload).subscribe({
      next: () => {
        this.loading = false;
        this.successMessage = 'Servicio reproductivo registrado exitosamente.';
        this.loadData();
      },
      error: (err) => {
        this.loading = false;
        this.errorMessage = err.error?.detail || 'Error al registrar servicio.';
      }
    });
  }

  submitStraw(): void {
    this.loading = true;
    this.errorMessage = '';
    this.successMessage = '';

    this.api.createSemenStraw(this.strawForm).subscribe({
      next: () => {
        this.loading = false;
        this.successMessage = 'Lote de pajuelas registrado exitosamente.';
        this.loadData();
      },
      error: (err) => {
        this.loading = false;
        this.errorMessage = err.error?.detail || 'Error al guardar pajuelas.';
      }
    });
  }

  submitBirthing(): void {
    this.loading = true;
    this.errorMessage = '';
    this.successMessage = '';

    this.api.recordBirthing(this.birthingForm).subscribe({
      next: () => {
        this.loading = false;
        this.successMessage = 'Parto y crías registrados exitosamente.';
        this.loadData();
      },
      error: (err) => {
        this.loading = false;
        this.errorMessage = err.error?.detail || 'Error al registrar parto.';
      }
    });
  }
}
