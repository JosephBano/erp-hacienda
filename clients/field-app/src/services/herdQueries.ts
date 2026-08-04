import { Database, Q } from '@nozbe/watermelondb';

import {
  Animal,
  AnimalCategory,
  AnimalGroup,
  AnimalIdentifier,
  Breed,
  InventoryItem,
  WithdrawalPeriod,
} from '../database/models';

export interface HerdMember {
  animalId: string;
  label: string;
  sex: string;
  isWithheld: boolean;
  withheldUntil?: string;
  speciesId: string;
  breedId?: string;
  categoryId?: string;
  birthDate?: string;
}

/**
 * Reads the herd as the field screens need to show it, entirely from local data.
 *
 * The label deserves a note: an animal may have no tag at all (Art. 3 / ADR-0006), so it
 * falls back to a short form of its id rather than being hidden. An animal the app refuses
 * to display is an animal the employee cannot record against.
 */
export async function loadHerd(database: Database, date = todayIso()): Promise<HerdMember[]> {
  const [animals, identifiers, withdrawals] = await Promise.all([
    database.get<Animal>('animals').query().fetch(),
    database.get<AnimalIdentifier>('animal_identifiers').query(Q.where('is_active', true)).fetch(),
    database.get<WithdrawalPeriod>('withdrawal_periods').query().fetch(),
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

  return animals
    .filter((animal) => !animal.isDeleted)
    .map((animal) => ({
      animalId: animal.id,
      sex: animal.sex,
      label: tagByAnimal.get(animal.id) ?? `Sin arete · ${animal.id.slice(0, 6)}`,
      isWithheld: milkBlockByAnimal.has(animal.id),
      withheldUntil: milkBlockByAnimal.get(animal.id),
      speciesId: animal.speciesId,
      breedId: animal.breedId,
      categoryId: animal.categoryId,
      birthDate: animal.birthDate,
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

function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
}
