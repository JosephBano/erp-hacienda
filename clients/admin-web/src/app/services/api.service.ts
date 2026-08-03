import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
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

export interface SpeciesDto {
  id: string;
  name: string;
  gestationDays?: number;
}

export interface BreedDto {
  id: string;
  speciesId: string;
  name: string;
}

export interface AnimalCategoryDto {
  id: string;
  speciesId: string;
  name: string;
}

export interface RegisterAnimalRequest {
  speciesId: string;
  sex: 'Male' | 'Female';
  breedId?: string;
  categoryId?: string;
  birthDate?: string;
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

export interface PermissionDto {
  id: string;
  code: string;
  name: string;
  module: string;
  description: string;
}

export interface RoleDto {
  id: string;
  code: string;
  name: string;
  description: string;
  isSystem: boolean;
  permissions: PermissionDto[];
}

export interface UserDto {
  id: string;
  fullName: string;
  email: string;
  roles: string[];
  isActive: boolean;
}

export interface AuditLogDto {
  id: string;
  userId?: string;
  userEmail?: string;
  userFullName?: string;
  action: string;
  module: string;
  entityName: string;
  entityId: string;
  detailsJson?: string;
  timestamp: string;
}

export interface PagedAuditLogsDto {
  items: AuditLogDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface SyncOperationDto {
  id: string;
  clientOperationId: string;
  operationType: string;
  status: string;
  deviceId: string;
  errorDetails?: string;
  resultRef?: string;
  occurredAt: string;
  receivedAt: string;
}

export interface SyncConflictDto {
  id: string;
  entityType: string;
  entityId: string;
  fieldName: string;
  serverValue?: string;
  attemptedValue?: string;
  resolution: string;
  deviceId?: string;
  detectedAt: string;
}

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private http = inject(HttpClient);
  private baseUrl = '/api/v1';

  // --- Livestock catalogs (species/breeds/categories) ---
  getSpecies(): Observable<SpeciesDto[]> {
    return this.http.get<SpeciesDto[]>(`${this.baseUrl}/species`);
  }

  createSpecies(data: { name: string; gestationDays?: number }): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/species`, data);
  }

  getBreeds(speciesId?: string): Observable<BreedDto[]> {
    let httpParams = new HttpParams();
    if (speciesId) httpParams = httpParams.set('speciesId', speciesId);
    return this.http.get<BreedDto[]>(`${this.baseUrl}/breeds`, { params: httpParams });
  }

  createBreed(data: { speciesId: string; name: string }): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/breeds`, data);
  }

  getAnimalCategories(speciesId?: string): Observable<AnimalCategoryDto[]> {
    let httpParams = new HttpParams();
    if (speciesId) httpParams = httpParams.set('speciesId', speciesId);
    return this.http.get<AnimalCategoryDto[]>(`${this.baseUrl}/animal-categories`, { params: httpParams });
  }

  createAnimalCategory(data: { speciesId: string; name: string }): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/animal-categories`, data);
  }

  registerAnimal(data: RegisterAnimalRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/animals`, data);
  }

  assignAnimalIdentifier(
    animalId: string,
    data: { type: string; value: string; validFrom: string }
  ): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/animals/${animalId}/identifiers`, data);
  }

  // --- Sync tray ---
  getSyncConflicts(entityType?: string): Observable<SyncConflictDto[]> {
    let httpParams = new HttpParams();
    if (entityType) httpParams = httpParams.set('entityType', entityType);
    return this.http.get<SyncConflictDto[]>(`${this.baseUrl}/sync/conflicts`, { params: httpParams });
  }

  getSyncOperations(status?: string): Observable<SyncOperationDto[]> {
    let httpParams = new HttpParams();
    if (status) httpParams = httpParams.set('status', status);
    return this.http.get<SyncOperationDto[]>(`${this.baseUrl}/sync/operations`, { params: httpParams });
  }

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

  // --- People, Roles & Permissions API ---
  getUsers(): Observable<UserDto[]> {
    return this.http.get<UserDto[]>(`${this.baseUrl}/people/users`);
  }

  getRoles(): Observable<RoleDto[]> {
    return this.http.get<RoleDto[]>(`${this.baseUrl}/people/roles`);
  }

  getPermissions(): Observable<PermissionDto[]> {
    return this.http.get<PermissionDto[]>(`${this.baseUrl}/people/permissions`);
  }

  createRole(data: { code: string; name: string; description: string; permissionIds?: string[] }): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/people/roles`, data);
  }

  updateRole(roleId: string, data: { roleId: string; name: string; description: string; permissionIds?: string[] }): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/people/roles/${roleId}`, data);
  }

  assignUserRole(userId: string, roleId: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/people/users/${userId}/roles/${roleId}`, {});
  }

  getAuditLogs(params?: { userId?: string; from?: string; to?: string; page?: number; pageSize?: number }): Observable<PagedAuditLogsDto> {
    let httpParams = new HttpParams();
    if (params?.userId) httpParams = httpParams.set('userId', params.userId);
    if (params?.from) httpParams = httpParams.set('from', params.from);
    if (params?.to) httpParams = httpParams.set('to', params.to);
    if (params?.page) httpParams = httpParams.set('page', params.page);
    if (params?.pageSize) httpParams = httpParams.set('pageSize', params.pageSize);

    return this.http.get<PagedAuditLogsDto>(`${this.baseUrl}/audit`, { params: httpParams });
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
