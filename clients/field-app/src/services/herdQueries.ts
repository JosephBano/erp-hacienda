import { Database, Q } from '@nozbe/watermelondb';

import {
  Animal,
  AnimalCategory,
  AnimalGroup,
  AnimalIdentifier,
  Breed,
  InventoryItem,
  MortalityCause,
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

  const tagByAnimal = new Map<string, string>();
  for (const identifier of identifiers) {
    if (!identifier.isDeleted && !tagByAnimal.has(identifier.animalId)) {
      tagByAnimal.set(identifier.animalId, identifier.value);
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
    .map((animal) => ({
      animalId: animal.id,
      sex: animal.sex,
      label: tagByAnimal.get(animal.id) ?? `Sin arete · ${animal.id.slice(0, 6)}`,
      isWithheld: milkBlockByAnimal.has(animal.id),
      withheldUntil: milkBlockByAnimal.get(animal.id),
      speciesId: animal.speciesId,
      speciesIsMilkable: milkableBySpecies.get(animal.speciesId) ?? false,
      breedId: animal.breedId,
      categoryId: animal.categoryId,
      birthDate: animal.birthDate,
      motherId: animal.motherId,
    }))
    .sort((a, b) => a.label.localeCompare(b.label));
}

export async function loadGroups(database: Database) {
  const groups = await database.get<AnimalGroup>('animal_groups').query().fetch();

  return groups
    .filter((group) => !group.isDeleted && group.isActive)
    .map((group) => ({ groupId: group.id, label: group.name }))
    .sort((a, b) => a.label.localeCompare(b.label));
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
    .map((item) => ({ itemId: item.id, name: item.name }))
    .sort((a, b) => a.name.localeCompare(b.name));
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
