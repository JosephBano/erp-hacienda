export interface AnimalGroupSummary {
  groupId: string;
  liveHeadCount: number;
  headsAffectedByDiagnosis: number;
  lastVaccinationAt?: string | null;
  lastDisposalAt?: string | null;
  lastTreatmentAt?: string | null;
}

/**
 * Reads the lot record (docs/spec/plan-0002-fase-3-5/spec-3.5a.md sec.3.5a.7 task 6, already delivered
 * server-side: `GET /api/v1/animal-groups/{id}/summary`). This is a display-only read, not
 * a registration path, so it is exempt from Art. 9 (móvil: ninguna operación de registro
 * puede depender de red) — the five registration screens (`LotEventsScreen`) never call
 * this and enqueue to the outbox regardless of what this returns or whether it fails.
 *
 * Deliberately *not* recomputed client-side: `LiveHeadCount` is a derived aggregate
 * (ADR-0015 sec.4) the server already knows how to compute correctly from every group
 * event kind, and duplicating that logic here would be exactly the kind of drift the
 * single-source-of-truth query exists to avoid.
 */
export interface AnimalGroupsApi {
  getSummary(groupId: string): Promise<AnimalGroupSummary>;
}

export interface HttpAnimalGroupsApiOptions {
  baseUrl: string;
  getToken: () => string | null;
}

export class HttpAnimalGroupsApi implements AnimalGroupsApi {
  constructor(private readonly options: HttpAnimalGroupsApiOptions) {}

  async getSummary(groupId: string): Promise<AnimalGroupSummary> {
    const response = await fetch(`${this.options.baseUrl}/api/v1/animal-groups/${groupId}/summary`, {
      headers: {
        Authorization: `Bearer ${this.options.getToken() ?? ''}`,
      },
    });

    if (!response.ok) {
      throw new Error(`No se pudo obtener la ficha del lote (${response.status}).`);
    }

    return (await response.json()) as AnimalGroupSummary;
  }
}
