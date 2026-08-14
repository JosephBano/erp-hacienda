import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

export interface Animal {
  id: string;
  farmTag?: string;
  officialTag?: string;
  name?: string;
  birthDate?: string;
  /**
   * Initial weight (kg) recorded at birth. Null for animals whose birth was not
   * weighed or were registered before 3.5a.4 (PLAN-FASE-3-5-PORCINO.md sec.3.5a.4
   * task 3). Surface only on detail views — not on list rows — to keep the list
   * lean.
   */
  birthWeightKg?: number | null;
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
  /**
   * Whether the field app lets the employee record milking sessions for animals of this
   * species. Defaults to false (fail-closed) on creation: a species the operator has not
   * opted in cannot be milked. See `docs/GLOSSARY.md` for the Art. 8 rationale.
   */
  isMilkable: boolean;
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

export interface SpeciesLactationDto {
  speciesId: string;
  daysOfLactation?: number | null;
  cohortWindowDays?: number | null;
}

export interface MortalityCauseDto {
  id: string;
  name: string;
  isActive: boolean;
}

export interface AdministrationRouteDto {
  id: string;
  key: string;
  labelEs: string;
  isActive: boolean;
}

export interface TreatmentReasonDto {
  id: string;
  key: string;
  labelEs: string;
  isActive: boolean;
}

export interface InventoryItemDto {
  id: string;
  name: string;
  category: string;
  unit: string;
  minStock: number;
  description?: string | null;
  totalStock: number;
}

export interface InventoryItemDetailDto {
  id: string;
  name: string;
  category: string;
  unit: string;
  minStock: number;
  description?: string | null;
  feedStageId?: string | null;
}

export interface InventoryBatchDto {
  id: string;
  batchNumber: string;
  quantity: number;
  costPerUnit: number;
  expirationDate?: string | null;
  // ADR-0026 Decisión 1+2: campos de recepción (todos opcionales para no romper filas
  // previas al backfill, excepto `receivedAt` que el backend siempre rellena).
  receivedAt?: string;
  supplierLabel?: string | null;
  invoiceReference?: string | null;
  notes?: string | null;
  recordedByLabel?: string | null;
  createdAt?: string;
}

export type InventoryBatchSummaryDto = InventoryBatchDto;

export interface CreateInventoryBatchRequest {
  BatchNumber: string;
  Quantity: number;
  CostPerUnit: number;
  ExpirationDate: string;
}

// ADR-0026 Decisión 3: DTO del endpoint canónico POST /receptions. Reemplaza a
// `CreateInventoryBatchRequest` para entradas de stock normales — `AddBatch` queda
// solo como ajuste técnico (ver Decisión 6 y el banner amarillo del componente).
export interface RecordInventoryReceptionRequest {
  BatchNumber: string;
  Quantity: number;
  Unit: string;
  CostPerUnit: number;
  ExpirationDate?: string;
  ReceivedAt: string;
  SupplierLabel?: string;
  InvoiceReference?: string;
  Notes?: string;
  RecordedById?: string;
  RecordedByLabel?: string;
}

export interface InventoryUnitConversionDto {
  id: string;
  fromUnit: string;
  toUnit: string;
  factor: number;
}

export interface RegisterUnitConversionRequest {
  FromUnit: string;
  ToUnit: string;
  Factor: number;
}

export interface FeedStageDto {
  id: string;
  key: string;
  labelEs: string;
  isActive: boolean;
}

export interface FarmModuleDto {
  key: string;
  enabled: boolean;
  disabledReason?: string | null;
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

export interface MilkingSessionDto {
  id: string;
  date: string;
  shift: string;
  groupId?: string;
  totalLiters: number;
  recordedBy: string;
  notes?: string;
  yields: { id: string; animalId: string; liters: number }[];
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

export interface BirthingListOffspring {
  animalId: string;
  farmTag?: string | null;
  sex: string;
  birthWeightKg?: number | null;
}

export interface BirthingListItem {
  id: string;
  damId: string;
  damFarmTag?: string | null;
  birthDate: string;
  difficulty: string;
  totalBorn: number;
  bornAlive: number;
  bornDead: number;
  mummified: number;
  litterWeight?: number | null;
  notes?: string | null;
  nursingCohortId?: string | null;
  weanedAt?: string | null;
  weanedCount?: number | null;
  offspring: BirthingListOffspring[];
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

// --- ADR-0025: Animal groups (lotes / grupos de manejo) ---
//
// Server shape from PR1 (AnimalGroupDto in src/Modules/Livestock/.../GetAnimalGroupQueries.cs).
// LiveHeadCount and SpeciesName are server-side derived fields; do not rely on the
// client to compute them.

export interface GroupMembershipDto {
  id: string;
  animalId: string;
  joinedAt: string;     // ISO date (DateOnly)
  leftAt?: string | null;
  isActive: boolean;
}

export interface AnimalGroupDto {
  id: string;
  name: string;
  description?: string | null;
  speciesId?: string | null;
  speciesName?: string | null;       // PR1 server-side
  isActive: boolean;
  trackingMode: 'Individual' | 'Headcount';
  liveHeadCount: number;             // PR1 server-side
  memberships: GroupMembershipDto[];
}

export interface AnimalGroupSummaryDto {
  groupId: string;
  liveHeadCount: number;
  headsAffectedByDiagnosis: number;
  lastVaccinationAt?: string | null;
  lastDisposalAt?: string | null;
  lastTreatmentAt?: string | null;
}

export interface CreateAnimalGroupRequest {
  name: string;
  description?: string | null;
  speciesId?: string | null;
  trackingMode?: 'Individual' | 'Headcount';
}

export interface UpdateAnimalGroupRequest {
  name: string;
  description?: string | null;
  speciesId?: string | null;
}

export interface ChangeTrackingModeRequest {
  trackingMode: 'Individual' | 'Headcount';
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

  createSpecies(data: {
    name: string;
    gestationDays?: number;
    /**
     * Defaults to false on the backend if omitted. The field app's Milking screen
     * disables animals whose species has `isMilkable=false`, and the MilkingService
     * rejects the registration server-side too — see Art. 8 (config, not code).
     */
    isMilkable?: boolean;
  }): Observable<{ id: string }> {
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

  getMilkingSessions(date?: string): Observable<MilkingSessionDto[]> {
    let httpParams = new HttpParams();
    if (date) httpParams = httpParams.set('date', date);
    return this.http.get<MilkingSessionDto[]>(`${this.baseUrl}/milking-sessions`, { params: httpParams });
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

  getBirthings(): Observable<BirthingListItem[]> {
    return this.http.get<BirthingListItem[]>(`${this.baseUrl}/breeding/birthings`);
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

  // --- Catalog generic methods (PR3) ---

  getMortalityCauses(includeInactive = false): Observable<MortalityCauseDto[]> {
    let params = new HttpParams();
    if (includeInactive) params = params.set('includeInactive', 'true');
    return this.http.get<MortalityCauseDto[]>(`${this.baseUrl}/mortality-causes`, { params });
  }

  createMortalityCause(name: string): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/mortality-causes`, { name });
  }

  deactivateMortalityCause(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/mortality-causes/${id}`);
  }

  getAdministrationRoutes(includeInactive = false): Observable<AdministrationRouteDto[]> {
    let params = new HttpParams();
    if (includeInactive) params = params.set('includeInactive', 'true');
    return this.http.get<AdministrationRouteDto[]>(`${this.baseUrl}/administration-routes`, { params });
  }

  createAdministrationRoute(data: { key: string; labelEs: string }): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/administration-routes`, data);
  }

  deactivateAdministrationRoute(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/administration-routes/${id}`);
  }

  activateAdministrationRoute(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/administration-routes/${id}/activate`, {});
  }

  updateAdministrationRouteLabel(id: string, labelEs: string): Observable<void> {
    return this.http.patch<void>(`${this.baseUrl}/administration-routes/${id}/label`, { labelEs });
  }

  getTreatmentReasons(includeInactive = false): Observable<TreatmentReasonDto[]> {
    let params = new HttpParams();
    if (includeInactive) params = params.set('includeInactive', 'true');
    return this.http.get<TreatmentReasonDto[]>(`${this.baseUrl}/treatment-reasons`, { params });
  }

  createTreatmentReason(data: { key: string; labelEs: string }): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/treatment-reasons`, data);
  }

  deactivateTreatmentReason(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/treatment-reasons/${id}`);
  }

  activateTreatmentReason(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/treatment-reasons/${id}/activate`, {});
  }

  updateTreatmentReasonLabel(id: string, labelEs: string): Observable<void> {
    return this.http.patch<void>(`${this.baseUrl}/treatment-reasons/${id}/label`, { labelEs });
  }

  getInventoryItems(category?: string): Observable<InventoryItemDto[]> {
    let params = new HttpParams();
    if (category) params = params.set('category', category);
    return this.http.get<InventoryItemDto[]>(`${this.baseUrl}/inventory/items`, { params });
  }

  getInventoryItemById(itemId: string): Observable<InventoryItemDetailDto> {
    return this.http.get<InventoryItemDetailDto>(`${this.baseUrl}/inventory/items/${itemId}`);
  }

  getInventoryBatches(itemId: string): Observable<InventoryBatchSummaryDto[]> {
    return this.http.get<InventoryBatchSummaryDto[]>(`${this.baseUrl}/inventory/items/${itemId}/batches`);
  }

  createInventoryBatch(itemId: string, body: CreateInventoryBatchRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/inventory/items/${itemId}/batches`, body);
  }

  // ADR-0026 Decisión 5: ruta canónica para registrar entradas de stock con
  // trazabilidad de proveedor y factura. Reemplaza `createInventoryBatch` en la UI
  // normal; el legacy `POST /batches` queda solo para ajustes manuales y emite
  // warning en el log del backend.
  recordInventoryReception(itemId: string, body: RecordInventoryReceptionRequest): Observable<string> {
    return this.http
      .post<{ id: string }>(`${this.baseUrl}/inventory/items/${itemId}/receptions`, body)
      .pipe(map((r) => r.id));
  }

  getInventoryUnitConversions(itemId: string): Observable<InventoryUnitConversionDto[]> {
    return this.http.get<InventoryUnitConversionDto[]>(`${this.baseUrl}/inventory/items/${itemId}/unit-conversions`);
  }

  registerInventoryUnitConversion(itemId: string, body: RegisterUnitConversionRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/inventory/items/${itemId}/unit-conversions`, body);
  }

  setInventoryItemFeedStage(itemId: string, body: { feedStageId: string | null }): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/inventory/items/${itemId}/feed-stage`, body);
  }

  getFeedStages(includeInactive = false): Observable<FeedStageDto[]> {
    let params = new HttpParams();
    if (includeInactive) params = params.set('includeInactive', 'true');
    return this.http.get<FeedStageDto[]>(`${this.baseUrl}/inventory/feed-stages`, { params });
  }

  createFeedStage(body: { key: string; labelEs: string }): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/inventory/feed-stages`, body);
  }

  deactivateFeedStage(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/inventory/feed-stages/${id}/deactivate`, {});
  }

  activateFeedStage(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/inventory/feed-stages/${id}/activate`, {});
  }

  createInventoryItem(data: {
    name: string;
    category: string;
    unit: string;
    minStock?: number;
    description?: string;
  }): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/inventory/items`, data);
  }

  getFarmModules(): Observable<FarmModuleDto[]> {
    return this.http.get<FarmModuleDto[]>(`${this.baseUrl}/farm-modules`);
  }

  setFarmModuleEnabled(key: string, enabled: boolean, disabledReason?: string): Observable<FarmModuleDto> {
    return this.http.patch<FarmModuleDto>(`${this.baseUrl}/farm-modules/${key}`, {
      enabled,
      disabledReason,
    });
  }

  getSpeciesLactation(speciesId: string): Observable<SpeciesLactationDto> {
    return this.http.get<SpeciesLactationDto>(`${this.baseUrl}/species/${speciesId}/lactation`);
  }

  updateSpeciesLactation(speciesId: string, data: {
    daysOfLactation?: number | null;
    cohortWindowDays?: number | null;
  }): Observable<void> {
    return this.http.patch<void>(`${this.baseUrl}/species/${speciesId}/lactation`, data);
  }

  // --- Animal Groups (ADR-0025 PR3) ---
  //
  // Endpoint contract from PR2 (AnimalGroupsEndpoints.cs in src/Hato.Api/Endpoints).
  // All write verbs require `livestock.animals.write` (same gate as animals).
  // Reads are open to any authenticated user, matching AnimalsEndpoints.

  getAnimalGroups(includeInactive = false): Observable<AnimalGroupDto[]> {
    let params = new HttpParams();
    if (includeInactive) params = params.set('includeInactive', 'true');
    return this.http.get<AnimalGroupDto[]>(`${this.baseUrl}/animal-groups`, { params });
  }

  getAnimalGroupById(id: string): Observable<AnimalGroupDto> {
    return this.http.get<AnimalGroupDto>(`${this.baseUrl}/animal-groups/${id}`);
  }

  getAnimalGroupSummary(id: string): Observable<AnimalGroupSummaryDto> {
    return this.http.get<AnimalGroupSummaryDto>(`${this.baseUrl}/animal-groups/${id}/summary`);
  }

  createAnimalGroup(data: CreateAnimalGroupRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/animal-groups`, data);
  }

  updateAnimalGroup(id: string, data: UpdateAnimalGroupRequest): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/animal-groups/${id}`, data);
  }

  deactivateAnimalGroup(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/animal-groups/${id}`);
  }

  activateAnimalGroup(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/animal-groups/${id}/activate`, {});
  }

  changeAnimalGroupTrackingMode(id: string, trackingMode: 'Individual' | 'Headcount'): Observable<void> {
    return this.http.patch<void>(`${this.baseUrl}/animal-groups/${id}/tracking-mode`, { trackingMode });
  }
}
