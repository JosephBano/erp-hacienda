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
    return this.http.post<{ id: string }>(`${this.baseUrl}/animals/${animalId}/events`, data);
  }

  recordMilkingSession(data: MilkingSessionRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/production/milking-sessions`, data);
  }
}
