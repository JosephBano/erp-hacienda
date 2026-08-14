import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AlertDto, Animal, ApiService, PregnancyDto, SemenStraw } from '../../services/api.service';
import { IconComponent } from '../../shared/icon/icon.component';

export interface OffspringFormItem {
  farmTag: string;
  sex: 'F' | 'M';
  birthWeightKg: number | null;
}

function safeRandomUuid(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    try {
      return crypto.randomUUID();
    } catch {
      // Fallback for non-secure HTTP context
    }
  }
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

@Component({
  selector: 'app-breeding-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent],
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
  // gestationDays is intentionally not part of this form: the API resolves it from the
  // dam's Species.GestationDays (Art. 8) instead of the client guessing a number.
  checkForm = {
    serviceId: '',
    checkDate: new Date().toISOString().substring(0, 10),
    method: 'Palpation', // 'Palpation' | 'Ultrasound' | 'NonReturn'
    result: 'Positive', // 'Positive' | 'Negative' | 'Doubtful'
    checkedBy: '',
    notes: ''
  };

  // Birthing form
  birthingForm = {
    damId: '',
    pregnancyId: '',
    birthDate: new Date().toISOString().substring(0, 10),
    difficulty: 'Normal',
    bornAlive: 0,
    bornDead: 0,
    mummified: 0,
    litterWeight: null as number | null,
    notes: '',
  };

  private _offspringList: OffspringFormItem[] = [];

  get offspringList(): OffspringFormItem[] {
    const alive = Math.max(0, Number(this.birthingForm.bornAlive) || 0);
    while (this._offspringList.length < alive) {
      this._offspringList.push({
        farmTag: '',
        sex: 'F',
        birthWeightKg: null
      });
    }
    if (this._offspringList.length > alive) {
      this._offspringList = this._offspringList.slice(0, alive);
    }
    return this._offspringList;
  }

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

  getAnimalDisplayName(a: Animal): string {
    if (a.name && a.name.trim().length > 0) {
      return a.farmTag ? `${a.name} (${a.farmTag})` : a.name;
    }
    return a.farmTag || a.officialTag || a.id;
  }

  onBornAliveChange(count?: number): void {
    const rawCount = count ?? this.birthingForm.bornAlive;
    const alive = Math.max(0, Number(rawCount) || 0);
    this.birthingForm.bornAlive = alive;
  }

  resetBirthingForm(): void {
    this.birthingForm = {
      damId: '',
      pregnancyId: '',
      birthDate: new Date().toISOString().substring(0, 10),
      difficulty: 'Normal',
      bornAlive: 0,
      bornDead: 0,
      mummified: 0,
      litterWeight: null,
      notes: '',
    };
    this._offspringList = [];
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
      technician: this.serviceForm.technician?.trim() || null,
      notes: this.serviceForm.notes?.trim() || null,
      bodyConditionScore: this.serviceForm.bodyConditionScore
    };

    if (this.serviceForm.serviceType === 'ArtificialInsemination') {
      if (this.serviceForm.strawId && this.serviceForm.strawId.trim() !== '') {
        payload.strawId = this.serviceForm.strawId.trim();
      }
    } else {
      if (this.serviceForm.sireAnimalId && this.serviceForm.sireAnimalId.trim() !== '') {
        payload.sireAnimalId = this.serviceForm.sireAnimalId.trim();
      }
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
    try {
      this.loading = true;
      this.errorMessage = '';
      this.successMessage = '';

      if (!this.birthingForm.damId) {
        this.errorMessage = 'Debe seleccionar una madre para registrar el parto.';
        this.loading = false;
        return;
      }

      const bornAlive = Number(this.birthingForm.bornAlive) || 0;
      const bornDead = Number(this.birthingForm.bornDead) || 0;
      const mummified = Number(this.birthingForm.mummified) || 0;

      if (bornAlive + bornDead + mummified <= 0) {
        this.errorMessage = 'El total de nacidos (vivos, muertos o momias) debe ser mayor a cero.';
        this.loading = false;
        return;
      }

      const payload: any = {
        damId: this.birthingForm.damId,
        birthDate: this.birthingForm.birthDate || new Date().toISOString().substring(0, 10),
        difficulty: this.birthingForm.difficulty || 'Normal',
        bornAlive,
        bornDead,
        mummified
      };

      if (this.birthingForm.pregnancyId && this.birthingForm.pregnancyId.trim() !== '') {
        payload.pregnancyId = this.birthingForm.pregnancyId.trim();
      }

      if (this.birthingForm.litterWeight && Number(this.birthingForm.litterWeight) > 0) {
        payload.litterWeight = Number(this.birthingForm.litterWeight);
      }

      if (this.birthingForm.notes && this.birthingForm.notes.trim() !== '') {
        payload.notes = this.birthingForm.notes.trim();
      }

      if (bornAlive > 0 && this.offspringList.length > 0) {
        payload.offspring = this.offspringList
          .slice(0, bornAlive)
          .map((o) => ({
            childId: safeRandomUuid(),
            farmTag: o.farmTag && o.farmTag.trim() !== '' ? o.farmTag.trim() : null,
            sex: o.sex || 'F',
            birthWeightKg:
              o.birthWeightKg != null &&
              o.birthWeightKg !== ('' as any) &&
              Number(o.birthWeightKg) > 0
                ? Number(o.birthWeightKg)
                : null
          }));
      }

      this.api.recordBirthing(payload).subscribe({
        next: () => {
          this.loading = false;
          this.successMessage = 'Parto y crías registrados exitosamente.';
          this.resetBirthingForm();
          this.loadData();
        },
        error: (err) => {
          this.loading = false;
          this.errorMessage = err.error?.detail || err.message || 'Error al registrar parto.';
        }
      });
    } catch (err: any) {
      this.loading = false;
      this.errorMessage = `Error inesperado: ${err?.message || err}`;
    }
  }
}
