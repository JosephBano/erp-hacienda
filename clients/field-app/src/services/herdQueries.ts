import { Database, Q } from '@nozbe/watermelondb';

import {
  Animal,
  AnimalCategory,
  AnimalGroup,
  AnimalIdentifier,
  Breed,
  BreedingService,
  DoseKind,
  InventoryItem,
  MortalityCause,
  Pregnancy,
  Species,
  WithdrawalPeriod,
} from '../database/models';

export interface HerdMember {
  animalId: string;
  label: string;
  sex: string;
  isWithheld: boolean;
  withheldUntil?: string;
  speciesId: string;
  speciesIsMilkable: boolean;
  breedId?: string;
  categoryId?: string;
  birthDate?: string;
  motherId?: string;
  disposedAt?: string;
}

export interface PregnantDam {
  animalId: string;
  label: string;
  expectedBirthDate?: string;
  sireLabel: string;
  pregnancyId: string;
}

/**
 * Reads the herd as the field screens need to show it, entirely from local data.
 *
 * The label deserves a note: an animal may have no tag at all (Art. 3 / ADR-0006), so it
 * falls back to a short form of its id rather than being hidden. An animal the app refuses
 * to display is an animal the employee cannot record against.
 */
export async function loadHerd(database: Database, date = todayIso()): Promise<HerdMember[]> {
  const [animals, identifiers, withdrawals, speciesList] = await Promise.all([
    database.get<Animal>('animals').query().fetch(),
    database.get<AnimalIdentifier>('animal_identifiers').query(Q.where('is_active', true)).fetch(),
    database.get<WithdrawalPeriod>('withdrawal_periods').query().fetch(),
    database.get<Species>('species').query().fetch(),
  ]);

  const nameByAnimal = new Map<string, string>();
  const tagByAnimal = new Map<string, string>();
  for (const identifier of identifiers) {
    if (identifier.isDeleted) continue;
    const type = (identifier.type ?? '').toLowerCase();
    if (type === 'name' || type === 'nombre') {
      if (!nameByAnimal.has(identifier.animalId)) {
        nameByAnimal.set(identifier.animalId, identifier.value);
      }
    } else {
      if (!tagByAnimal.has(identifier.animalId) || type.includes('farm')) {
        tagByAnimal.set(identifier.animalId, identifier.value);
      }
    }
  }

  const milkBlockByAnimal = new Map<string, string>();
  for (const period of withdrawals) {
    const blocksMilk = period.target === 'Milk' || period.target === 'Both';
    const active = period.startsAt <= date && period.endsAt >= date;
    if (blocksMilk && active && !period.isDeleted) {
      milkBlockByAnimal.set(period.animalId, period.endsAt);
    }
  }

  // The species table carries the `is_milkable` flag — see Art. 8. An animal whose
  // species has is_milkable=false (the default for newly synced species until the
  // operator opts them in from admin-web) is not eligible for milking registration.
  // The model declares isMilkable as optional because WatermelonDB forbids default
  // values on decorated fields, so we coalesce to false (fail-closed) at the boundary.
  const milkableBySpecies = new Map<string, boolean>();
  for (const species of speciesList) {
    if (!species.isDeleted) {
      milkableBySpecies.set(species.id, species.isMilkable ?? false);
    }
  }

  return animals
    .filter((animal) => !animal.isDeleted)
    .map((animal) => {
      const name = nameByAnimal.get(animal.id);
      const tag = tagByAnimal.get(animal.id);
      let label: string;
      if (name && name.trim().length > 0) {
        label = tag ? `${name} (${tag})` : name;
      } else if (tag) {
        label = tag;
      } else {
        label = `Sin arete · ${animal.id.slice(-6)}`;
      }

      return {
        animalId: animal.id,
        sex: animal.sex,
        label,
        isWithheld: milkBlockByAnimal.has(animal.id),
        withheldUntil: milkBlockByAnimal.get(animal.id),
        speciesId: animal.speciesId,
        speciesIsMilkable: milkableBySpecies.get(animal.speciesId) ?? false,
        breedId: animal.breedId,
        categoryId: animal.categoryId,
        birthDate: animal.birthDate,
        motherId: animal.motherId,
        disposedAt: animal.disposedAt,
      };
    })
    .sort((a, b) => a.label.localeCompare(b.label));
}

/**
 * Activity selector for milking (D1, D2, spec sec. 4):
 * Returns only candidates eligible for milking on the given date:
 * - Not deleted
 * - Female sex
 * - Species is flagged is_milkable
 * - Not disposed on or before date
 */
export async function loadMilkingCandidates(database: Database, date = todayIso()): Promise<HerdMember[]> {
  const herd = await loadHerd(database, date);
  return herd.filter((member) => {
    if (member.sex?.toLowerCase() !== 'female') return false;
    if (!member.speciesIsMilkable) return false;
    if (member.disposedAt && member.disposedAt.slice(0, 10) <= date) return false;
    return true;
  });
}

/**
 * Activity selector for active individual capture (weighing, treatment, movement, disposal)
 * on a given date (D1, D5):
 * Returns animals that are alive/active on that date:
 * - Not deleted
 * - Not disposed on or before date
 * - Both sexes included
 */
export async function loadActiveHerd(database: Database, date = todayIso()): Promise<HerdMember[]> {
  const herd = await loadHerd(database, date);
  return herd.filter((member) => {
    if (member.disposedAt && member.disposedAt.slice(0, 10) <= date) return false;
    return true;
  });
}

/**
 * Loads pregnant dams whose pregnancy is active and not deleted, joining their
 * breeding service to deduce the sire label.
 */
export async function loadPregnantDams(database: Database): Promise<PregnantDam[]> {
  const [pregnancies, services, herd] = await Promise.all([
    database.get<Pregnancy>('pregnancies').query().fetch(),
    database.get<BreedingService>('breeding_services').query().fetch(),
    loadHerd(database),
  ]);

  const herdByAnimalId = new Map<string, HerdMember>();
  for (const member of herd) {
    herdByAnimalId.set(member.animalId, member);
  }

  const serviceById = new Map<string, BreedingService>();
  for (const service of services) {
    if (!service.isDeleted) {
      serviceById.set(service.id, service);
    }
  }

  const pregnantDams: PregnantDam[] = [];

  for (const pregnancy of pregnancies) {
    if (pregnancy.isDeleted || pregnancy.status !== 'Active') {
      continue;
    }

    const dam = herdByAnimalId.get(pregnancy.damId);
    if (!dam) {
      continue;
    }

    // A dam must be female and not effectively disposed
    if (dam.sex?.toLowerCase() !== 'female') {
      continue;
    }
    if (dam.disposedAt && dam.disposedAt.slice(0, 10) <= todayIso()) {
      continue;
    }

    let sireLabel = 'Padre: sin registrar';
    if (pregnancy.serviceId) {
      const service = serviceById.get(pregnancy.serviceId);
      if (service) {
        if (service.serviceType === 'ArtificialInsemination') {
          sireLabel = 'Padre: Inseminación artificial';
        } else if (service.serviceType === 'Natural') {
          if (service.sireAnimalId) {
            const sire = herdByAnimalId.get(service.sireAnimalId);
            if (sire) {
              sireLabel = `Padre: ${sire.label}`;
            } else {
              sireLabel = 'Padre: sin registrar';
            }
          } else {
            sireLabel = 'Padre: sin registrar';
          }
        }
      }
    }

    pregnantDams.push({
      animalId: dam.animalId,
      label: dam.label,
      expectedBirthDate: pregnancy.expectedBirthDate,
      sireLabel,
      pregnancyId: pregnancy.id,
    });
  }

  return pregnantDams.sort((a, b) => {
    const dateA = a.expectedBirthDate ?? '9999-12-31';
    const dateB = b.expectedBirthDate ?? '9999-12-31';
    const cmp = dateA.localeCompare(dateB);
    if (cmp !== 0) return cmp;
    return a.label.localeCompare(b.label);
  });
}


export async function loadGroups(database: Database) {
  const groups = await database.get<AnimalGroup>('animal_groups').query().fetch();

  return groups
    .filter((group) => !group.isDeleted && group.isActive)
    .map((group) => ({
      groupId: group.id,
      label: group.name,
      // speciesId feeds the group-weighing plausibility check (ADR-0022); trackingMode
      // lets the lot subject screen distinguish a headcount lot (3.5a.7's audience) from
      // an individually-tracked one, without a network round trip (Art. 9).
      speciesId: group.speciesId,
      trackingMode: group.trackingMode,
    }))
    .sort((a, b) => a.label.localeCompare(b.label));
}

/**
 * The feed items catalog (3.5a.7 task 5, ADR-0008 mirror pattern), filtered to the
 * "Feed" category the server's `ItemCategory` enum serializes verbatim through the pull
 * (`SyncPullQueries.cs`: `i.Category.ToString()`). Mirrors `loadMedications`.
 */
export async function loadFeedItems(database: Database) {
  const items = await database.get<InventoryItem>('inventory_items').query().fetch();

  return items
    .filter((item) => !item.isDeleted && item.category === 'Feed')
    .map((item) => ({ itemId: item.id, name: item.name, unit: item.unit }))
    .sort((a, b) => a.name.localeCompare(b.name));
}

export async function loadBreeds(database: Database, speciesId: string) {
  const breeds = await database.get<Breed>('breeds').query(Q.where('species_id', speciesId)).fetch();

  return breeds
    .filter((breed) => !breed.isDeleted)
    .map((breed) => ({ breedId: breed.id, label: breed.name }))
    .sort((a, b) => a.label.localeCompare(b.label));
}

export async function loadCategories(database: Database, speciesId: string) {
  const categories = await database
    .get<AnimalCategory>('animal_categories')
    .query(Q.where('species_id', speciesId))
    .fetch();

  return categories
    .filter((category) => !category.isDeleted)
    .map((category) => ({ categoryId: category.id, label: category.name }))
    .sort((a, b) => a.label.localeCompare(b.label));
}

export async function loadMedications(database: Database) {
  const items = await database.get<InventoryItem>('inventory_items').query().fetch();

  return items
    .filter((item) => !item.isDeleted && /medic/i.test(item.category))
    // `unit` rides along so VaccinateScreen/TreatScreen (3.5a.2-C) can build a
    // structured dose without asking the operator to type a unit the product
    // already declares (the "unidad es del producto, no del operario" rule).
    .map((item) => ({ itemId: item.id, name: item.name, unit: item.unit }))
    .sort((a, b) => a.name.localeCompare(b.name));
}

/**
 * The full inventory catalog for the treatment/vaccination product picker
 * (3.5a.2-C): unlike `loadMedications`, this is not filtered by category —
 * a "product" here can be a vaccine, a dewormer or anything else the panel
 * stocked, and the catalog does not carry a code-level notion of "vaccine"
 * vs. "medication" (Art. 8: that distinction, if it ever matters, is a data
 * column, not a regex in this file).
 */
export async function loadTreatmentProducts(database: Database) {
  const items = await database.get<InventoryItem>('inventory_items').query().fetch();

  return items
    .filter((item) => !item.isDeleted)
    .map((item) => ({ itemId: item.id, name: item.name, unit: item.unit }))
    .sort((a, b) => a.name.localeCompare(b.name));
}

/**
 * The dose-form catalog (3.5a.2-B/C): `absolute` / `per_weight` / `per_head`.
 * `VaccinateScreen` needs the `per_head` row's id to build a
 * `createTreatmentCourse` payload without a round trip.
 */
export async function loadDoseKinds(database: Database) {
  const kinds = await database.get<DoseKind>('dose_kinds').query().fetch();

  return kinds
    .filter((k) => !k.isDeleted && k.isActive)
    .map((k) => ({ doseKindId: k.id, key: k.key, labelEs: k.labelEs }))
    .sort((a, b) => a.labelEs.localeCompare(b.labelEs));
}

/** The mortality causes catalog (3.5a.3), for the "baja con causa" picker. */
export async function loadMortalityCauses(database: Database) {
  const causes = await database.get<MortalityCause>('mortality_causes').query().fetch();

  return causes
    .filter((cause) => !cause.isDeleted && cause.isActive)
    .map((cause) => ({ causeId: cause.id, name: cause.name }))
    .sort((a, b) => a.name.localeCompare(b.name));
}

/**
 * The administration routes catalog (3.5a.2-A). The picker in
 * TreatScreen / VaccinateScreen uses this to build the structured
 * treatment payload offline.
 */
export async function loadAdministrationRoutes(database: Database) {
  const routes = await database
    .get<import('../database/models').AdministrationRoute>('administration_routes')
    .query()
    .fetch();

  return routes
    .filter((r) => !r.isDeleted && r.isActive)
    .map((r) => ({ routeId: r.id, key: r.key, labelEs: r.labelEs }))
    .sort((a, b) => a.labelEs.localeCompare(b.labelEs));
}

/**
 * The treatment reasons catalog (3.5a.2-A). The three values
 * (scheduled/curative/preventive) are what separate "vacuna de
 * calendario" from "vacuna porque se enfermó" on the herd's history.
 */
export async function loadTreatmentReasons(database: Database) {
  const reasons = await database
    .get<import('../database/models').TreatmentReason>('treatment_reasons')
    .query()
    .fetch();

  return reasons
    .filter((r) => !r.isDeleted && r.isActive)
    .map((r) => ({ reasonId: r.id, key: r.key, labelEs: r.labelEs }))
    .sort((a, b) => a.labelEs.localeCompare(b.labelEs));
}

function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
}
