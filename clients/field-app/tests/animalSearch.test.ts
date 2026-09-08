import { searchAnimals, type AnimalForSubject } from '../src/services/herdQueries';

describe('searchAnimals helper (feature-0007 Commit 1 & 2)', () => {
  it('strictly preserves leading zeros without number coercion ("007" vs "7") (T1.4, Criterion 1)', () => {
    const animal007: AnimalForSubject = {
      animalId: 'a-007',
      label: '007',
      tag: '007',
      activeIdentifiers: [{ type: 'FarmTag', value: '007' }],
      hasPendingTag: false,
    };
    const animal7: AnimalForSubject = {
      animalId: 'a-7',
      label: '7',
      tag: '7',
      activeIdentifiers: [{ type: 'FarmTag', value: '7' }],
      hasPendingTag: false,
    };
    const herd = [animal007, animal7];

    // Searching '007' MUST NOT match '7' via number coercion
    const result007 = searchAnimals(herd, '007');
    expect(result007.exactMatches).toEqual([animal007]);
    expect(result007.partialMatches).toEqual([]);
    expect(result007.allMatches).toEqual([animal007]);

    // Searching '7' matches '7' as exact; '007' is NOT an exact match
    const result7 = searchAnimals(herd, '7');
    expect(result7.exactMatches).toEqual([animal7]);
    // '007' only appears as partial match (string contains '7'), never as exact match
    expect(result7.partialMatches).toEqual([animal007]);

    // Searching '00' finds '007' as partial match, and does not find '7' at all
    const result00 = searchAnimals(herd, '00');
    expect(result00.exactMatches).toEqual([]);
    expect(result00.partialMatches).toEqual([animal007]);
  });

  it('finds an animal with two active identifiers by either one (T1.1, T1.5, D3)', () => {
    const cow: AnimalForSubject = {
      animalId: 'cow-1',
      label: 'Margarita (CRIA-12)',
      name: 'Margarita',
      tag: 'CRIA-12',
      activeIdentifiers: [
        { type: 'FarmTag', value: 'CRIA-12' },
        { type: 'Official', value: 'SIFAE-99901' },
      ],
      hasPendingTag: false,
    };
    const herd = [cow];

    // Found by primary farm tag
    const byTag = searchAnimals(herd, 'CRIA-12');
    expect(byTag.exactMatches).toEqual([cow]);

    // Found by secondary official identifier (not present in primary tag or simple label)
    const byOfficial = searchAnimals(herd, 'SIFAE-99901');
    expect(byOfficial.exactMatches).toEqual([cow]);

    // Case-insensitive search on secondary identifier
    const byOfficialLower = searchAnimals(herd, 'sifae-99901');
    expect(byOfficialLower.exactMatches).toEqual([cow]);

    // Partial search on secondary identifier
    const byOfficialPartial = searchAnimals(herd, 'sifae');
    expect(byOfficialPartial.partialMatches).toEqual([cow]);
  });

  it('distinguishes exact and partial matches in result sets (T1.6)', () => {
    const cow10: AnimalForSubject = {
      animalId: 'cow-10',
      label: 'COW-10',
      tag: 'COW-10',
      activeIdentifiers: [{ type: 'FarmTag', value: 'COW-10' }],
      hasPendingTag: false,
    };
    const cow100: AnimalForSubject = {
      animalId: 'cow-100',
      label: 'COW-100',
      tag: 'COW-100',
      activeIdentifiers: [{ type: 'FarmTag', value: 'COW-100' }],
      hasPendingTag: false,
    };
    const herd = [cow10, cow100];

    const result = searchAnimals(herd, 'COW-10');
    expect(result.exactMatches).toEqual([cow10]);
    expect(result.partialMatches).toEqual([cow100]);
    expect(result.allMatches).toEqual([cow10, cow100]);

    const partialOnly = searchAnimals(herd, 'COW');
    expect(partialOnly.exactMatches).toEqual([]);
    expect(partialOnly.partialMatches).toEqual([cow10, cow100]);
  });

  it('allows untagged animal to be found by alternative readable id and keeps pending tag notice (T1.7, D5)', () => {
    const untagged: AnimalForSubject = {
      animalId: '00000000-0000-5000-8000-111111123456',
      label: 'Sin arete · 123456',
      activeIdentifiers: [],
      hasPendingTag: true,
    };
    const herd = [untagged];

    // Found by the 6-character snippet
    const bySnippet = searchAnimals(herd, '123456');
    expect(bySnippet.exactMatches).toEqual([untagged]);
    expect(bySnippet.exactMatches[0].hasPendingTag).toBe(true);

    // Found by full UUID
    const byUuid = searchAnimals(herd, '00000000-0000-5000-8000-111111123456');
    expect(byUuid.exactMatches).toEqual([untagged]);

    // Found by text "Sin arete"
    const bySinArete = searchAnimals(herd, 'Sin arete');
    expect(bySinArete.partialMatches).toEqual([untagged]);
  });

  it('finds animal by name even when tag is different', () => {
    const animal: AnimalForSubject = {
      animalId: 'a-1',
      label: 'Margarita (TAG-99)',
      name: 'Margarita',
      tag: 'TAG-99',
      activeIdentifiers: [{ type: 'FarmTag', value: 'TAG-99' }],
      hasPendingTag: false,
    };
    const herd = [animal];

    const result = searchAnimals(herd, 'margarita');
    expect(result.exactMatches).toEqual([animal]);
  });

  it('returns all animals in partialMatches when query is empty', () => {
    const animal1: AnimalForSubject = { animalId: 'a-1', label: 'Vaca 1' };
    const animal2: AnimalForSubject = { animalId: 'a-2', label: 'Vaca 2' };
    const herd = [animal1, animal2];

    const result = searchAnimals(herd, '   ');
    expect(result.exactMatches).toEqual([]);
    expect(result.partialMatches).toEqual(herd);
    expect(result.allMatches).toEqual(herd);
  });

  it('searching by previous tag finds animal clearly indicated as historical, never as active (T3.5, D3)', () => {
    const animalWithHistory: AnimalForSubject = {
      animalId: 'a-hist-1',
      label: 'TAG-NEW',
      tag: 'TAG-NEW',
      activeIdentifiers: [{ type: 'FarmTag', value: 'TAG-NEW' }],
      historicalIdentifiers: [{ type: 'FarmTag', value: 'TAG-OLD' }],
      hasPendingTag: false,
    };
    const herd = [animalWithHistory];

    // Searching previous tag 'TAG-OLD'
    const byOldTag = searchAnimals(herd, 'TAG-OLD');
    expect(byOldTag.exactMatches.length).toBe(1);
    const matched = byOldTag.exactMatches[0];
    expect(matched.animalId).toBe('a-hist-1');
    // Clearly indicated as previous tag
    expect(matched.matchedHistoricalTag).toBe('TAG-OLD');
    // Active tag is still TAG-NEW, never presented as having TAG-OLD as active
    expect(matched.tag).toBe('TAG-NEW');

    // Searching current tag 'TAG-NEW' returns without matchedHistoricalTag
    const byCurrentTag = searchAnimals(herd, 'TAG-NEW');
    expect(byCurrentTag.exactMatches.length).toBe(1);
    expect(byCurrentTag.exactMatches[0].matchedHistoricalTag).toBeUndefined();

    // Partial search on previous tag
    const byOldTagPartial = searchAnimals(herd, 'OLD');
    expect(byOldTagPartial.partialMatches.length).toBe(1);
    expect(byOldTagPartial.partialMatches[0].matchedHistoricalTag).toBe('TAG-OLD');
  });

  it('searching previous tag on currently untagged animal preserves pending tag status and marks historical tag (T3.5, D3, D5)', () => {
    const untaggedWithHistory: AnimalForSubject = {
      animalId: '00000000-0000-0000-0000-000000789012',
      label: 'Sin arete · 789012',
      activeIdentifiers: [],
      historicalIdentifiers: [{ type: 'FarmTag', value: 'TAG-LOST-99' }],
      hasPendingTag: true,
    };
    const herd = [untaggedWithHistory];

    const result = searchAnimals(herd, 'TAG-LOST-99');
    expect(result.exactMatches.length).toBe(1);
    const matched = result.exactMatches[0];
    expect(matched.matchedHistoricalTag).toBe('TAG-LOST-99');
    expect(matched.hasPendingTag).toBe(true);
  });
});

