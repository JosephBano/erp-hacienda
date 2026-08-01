import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface Animal {
  id: string;
  farmTag?: string;
  officialTag?: string;
  name?: string;
  birthDate?: string;
  gender: string;
  speciesName?: string;
  breedName?: string;
  categoryName?: string;
  status: string;
  isInWithdrawal: boolean;
  withdrawalUntil?: string;
}

export interface AnimalDetail extends Animal {
  events: AnimalEvent[];
  milkYields: MilkYield[];
}

export interface AnimalEvent {
  id: string;
  eventType: string;
  eventDate: string;
  detailsJson: string;
  recordedBy: string;
}

export interface MilkYield {
  id: string;
  date: string;
  session: string;
  liters: number;
}

export interface MilkingSessionRequest {
  date: string;
  sessionType: string;
  recordedBy: string;
  yields: { animalId: string; liters: number }[];
}

export interface RecordEventRequest {
  eventType: string;
  eventDate: string;
  details: any;
  recordedBy: string;
  milkWithdrawalDays?: number;
  meatWithdrawalDays?: number;
}

export interface SemenStraw {
  id: string;
  code: string;
  bullName: string;
  bullCode?: string;
  breedId: string;
  supplierName?: string;
  initialQuantity: number;
  currentQuantity: number;
  notes?: string;
  createdAt: string;
}

export interface BreedingServiceDto {
  id: string;
  damId: string;
  serviceType: string;
  sireAnimalId?: string;
  strawId?: string;
  serviceDate: string;
  technician?: string;
  notes?: string;
  bodyConditionScore?: number;
  createdAt: string;
}

export interface PregnancyDto {
  id: string;
  damId: string;
  serviceId?: string;
  confirmedAt: string;
  expectedBirthDate: string;
  status: string;
  notes?: string;
  createdAt: string;
}

export interface BirthingDto {
  id: string;
  damId: string;
  pregnancyId?: string;
  birthDate: string;
  difficulty: string;
  totalBorn: number;
  bornAlive: number;
  bornDead: number;
  mummified: number;
  litterWeight?: number;
  notes?: string;
  createdAt: string;
  weanedAt?: string;
  weanedCount?: number;
}

export interface AncestorDto {
  animalId: string;
  farmTag?: string;
  sex: string;
  generationLevel: number;
  role: string;
  motherId?: string;
  fatherAnimalId?: string;
  fatherStrawId?: string;
  fatherStrawBullName?: string;
}

export interface PedigreeDto {
  animalId: string;
  farmTag?: string;
  ancestors: AncestorDto[];
}

export interface DamKpisDto {
  damId: string;
  averageCalvingIntervalDays?: number;
  daysOpen?: number;
  servicesPerConception: number;
  totalBirthings: number;
  totalOffspringAlive: number;
  weanedPerYear: number;
}

export interface AlertDto {
  id: string;
  code: string;
  title: string;
  message: string;
  severity: string;
  targetEntityId?: string;
  isDismissed: boolean;
  createdAt: string;
}

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private http = inject(HttpClient);
  private baseUrl = '/api/v1';

  getAnimals(): Observable<Animal[]> {
    return this.http.get<Animal[]>(`${this.baseUrl}/animals`);
  }

  getAnimalById(id: string): Observable<AnimalDetail> {
    return this.http.get<AnimalDetail>(`${this.baseUrl}/animals/${id}`);
  }

  createAnimal(data: any): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/animals`, data);
  }

  recordAnimalEvent(animalId: string, data: RecordEventRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/animals/${animalId}/events`, {
      eventType: data.eventType,
      occurredAt: data.eventDate,
      recordedBy: data.recordedBy,
      payloadJson: JSON.stringify(data.details),
      milkWithdrawalDays: data.milkWithdrawalDays,
      meatWithdrawalDays: data.meatWithdrawalDays
    });
  }

  recordMilkingSession(data: MilkingSessionRequest): Observable<{ id: string }> {
    const individualYields = data.yields.map((y) => ({ animalId: y.animalId, liters: y.liters }));
    const totalLiters = individualYields.reduce((sum, y) => sum + y.liters, 0);

    return this.http.post<{ id: string }>(`${this.baseUrl}/milking-sessions`, {
      date: data.date,
      shift: data.sessionType,
      recordedBy: data.recordedBy,
      totalLiters,
      individualYields
    });
  }

  // --- Breeding API ---
  getSemenStraws(): Observable<SemenStraw[]> {
    return this.http.get<SemenStraw[]>(`${this.baseUrl}/breeding/semen-straws`);
  }

  createSemenStraw(data: any): Observable<SemenStraw> {
    return this.http.post<SemenStraw>(`${this.baseUrl}/breeding/semen-straws`, data);
  }

  registerBreedingService(data: any): Observable<BreedingServiceDto> {
    return this.http.post<BreedingServiceDto>(`${this.baseUrl}/breeding/services`, data);
  }

  recordPregnancyCheck(data: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/breeding/pregnancy-checks`, data);
  }

  getActivePregnancies(): Observable<PregnancyDto[]> {
    return this.http.get<PregnancyDto[]>(`${this.baseUrl}/breeding/pregnancies/active`);
  }

  recordBirthing(data: any): Observable<BirthingDto> {
    return this.http.post<BirthingDto>(`${this.baseUrl}/breeding/birthings`, data);
  }

  recordWeaning(data: { birthingId: string; weaningDate: string; weanedCount: number; notes?: string }): Observable<BirthingDto> {
    return this.http.post<BirthingDto>(`${this.baseUrl}/breeding/weanings`, data);
  }

  getPedigree(animalId: string): Observable<PedigreeDto> {
    return this.http.get<PedigreeDto>(`${this.baseUrl}/breeding/pedigree/${animalId}`);
  }

  getDamKpis(damId: string): Observable<DamKpisDto> {
    return this.http.get<DamKpisDto>(`${this.baseUrl}/breeding/kpis/dams/${damId}`);
  }

  // --- Alerts API ---
  getAlerts(): Observable<AlertDto[]> {
    return this.http.get<AlertDto[]>(`${this.baseUrl}/alerts`);
  }

  generateAlerts(): Observable<{ generatedAlerts: number }> {
    return this.http.post<{ generatedAlerts: number }>(`${this.baseUrl}/alerts/generate`, {});
  }

  dismissAlert(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/alerts/${id}/dismiss`, {});
  }
}
