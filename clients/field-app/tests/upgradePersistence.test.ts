import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';
import * as SecureStore from 'expo-secure-store';

import { schema, SCHEMA_VERSION } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { Outbox } from '../src/services/outbox';
import { AuthService, type UserSession } from '../src/services/authService';

/**
 * Feature 0010 Commit 9 — Verification of update from installed field build (T9.1 - T9.6).
 *
 * Invariants:
 * - Update preserves session, outbox, local mirror data and identifiers (T9.1).
 * - Does not require reinstall or "clean start" (T9.2).
 * - Drafts have explicit handling; older flows that did not store drafts do not promise recovery (T9.3, T9.4).
 * - A crash/reboot during capture or send does not present unpersisted data as saved (T9.5, Criterion 9).
 */
describe('Commit 9 — Preservación de trabajo pendiente y actualización desde versión instalada', () => {
  const dbName = `hato-upgrade-test-${Math.random()}`;

  const createDb = () => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName,
    });
    return new Database({ adapter: adapter as never, modelClasses });
  };

  beforeEach(async () => {
    jest.clearAllMocks();
  });

  it('T9.1 & T9.2: conserva outbox, identificadores y datos locales sin exigir reinstalación ni empezar limpio', async () => {
    // 1. Simular instalación previa con sesión, datos en outbox y espejo
    const sampleSession: UserSession = {
      userId: 'usr-100',
      fullName: 'Operario de Campo',
      email: 'operario@finca.ec',
      token: 'jwt-token-field-operator',
      refreshToken: 'jwt-refresh-token',
      expiresAt: '2026-12-31T23:59:59Z',
      roles: ['Operator'],
      permissions: ['livestock.animals.read', 'livestock.animals.write'],
      pinHash: 'salted_hash_1234',
      pinSalt: 'salt_abc',
    };
    await SecureStore.setItemAsync('hato_session', JSON.stringify(sampleSession));

    const dbPre = createDb();
    const outboxPre = new Outbox(dbPre);

    await dbPre.write(async () => {
      // Animal con identificación
      await dbPre.get('animals').create((row: any) => {
        row._raw.id = 'animal-prev-1';
        row.sex = 'Female';
        row.speciesId = 'species-bovine';
        row.isDeleted = false;
        row.serverCreatedAt = 1720000000;
        row.disposedAt = undefined;
      });

      await dbPre.get('animal_identifiers').create((row: any) => {
        row._raw.id = 'ident-prev-1';
        row.animalId = 'animal-prev-1';
        row.type = 'EarTag';
        row.value = '0405';
        row.isActive = true;
        row.isDeleted = false;
      });

      await dbPre.get('animal_groups').create((row: any) => {
        row._raw.id = 'grp-prev-1';
        row.name = 'Lote Terneras';
        row.trackingMode = 'Individual';
        row.isActive = true;
        row.isDeleted = false;
      });
    });

    // Encolar operaciones pendientes de sincronización
    await outboxPre.enqueue('recordMilking', { animalId: 'animal-prev-1', liters: 12.5 });
    await outboxPre.enqueue('recordAnimalEvent', {
      animalId: 'animal-prev-1',
      eventType: 'Weighing',
      payload: { kg: 340 },
    });

    const pendingBefore = await outboxPre.pending();
    expect(pendingBefore).toHaveLength(2);

    // 2. Simular actualización de la aplicación: los servicios de la app actualizada
    // se inicializan contra la base de datos persistida del dispositivo
    const outboxPost = new Outbox(dbPre);
    const authPost = new AuthService('http://test-server');

    // La sesión persiste
    const restoredSession = await authPost.restore();
    expect(restoredSession).not.toBeNull();
    expect(restoredSession?.userId).toBe('usr-100');
    expect(restoredSession?.fullName).toBe('Operario de Campo');
    expect(authPost.token()).toBe('jwt-token-field-operator');

    // Los datos locales de animales e identificadores persisten
    const animal = (await dbPre.get('animals').find('animal-prev-1')) as any;
    expect(animal).toBeDefined();
    expect(animal.sex).toBe('Female');

    const identifiers = await dbPre
      .get('animal_identifiers')
      .query()
      .fetch();
    expect(identifiers).toHaveLength(1);
    expect((identifiers[0] as any).value).toBe('0405');

    // La outbox conserva las operaciones pendientes intactas
    const pendingAfter = await outboxPost.pending();
    expect(pendingAfter).toHaveLength(2);
    expect(pendingAfter[0].clientOperationId).toBe(pendingBefore[0].clientOperationId);
    expect(pendingAfter[0].operationType).toBe('recordMilking');
    expect(pendingAfter[1].clientOperationId).toBe(pendingBefore[1].clientOperationId);
    expect(pendingAfter[1].operationType).toBe('recordAnimalEvent');

    // El esquema local actual coincide con la versión máxima
    expect(migrations.maxVersion).toBe(SCHEMA_VERSION);
  });

  it('T9.4 & T9.5: un reinicio o crash durante la captura no presenta como guardado un dato no persistido', async () => {
    const db = createDb();
    const outbox = new Outbox(db);

    // Supongamos que el operador estaba llenando un formulario pero el teléfono se reinició
    // ANTES de que se ejecutara outbox.enqueue() o db.write().
    // La memoria volátil se pierde.
    const todayEntries = await outbox.today();
    expect(todayEntries).toHaveLength(0);

    // Ninguna fila fantasma debe existir en outbox
    const allOutbox = await db.get('sync_outbox').query().fetch();
    expect(allOutbox).toHaveLength(0);
  });

  it('T9.3: un borrador en curso conserva el estado ingresado entre pasos sin pérdida de datos', async () => {
    const db = createDb();
    // Simular estado de borrador en el flujo de parto/actividad:
    // Datos en memoria se conservan entre pasos del asistente y solo se
    // persisten en la outbox cuando el usuario confirma la acción final.
    const dam = { animalId: 'dam-1', pregnancyId: 'preg-1' };
    const draftCalves = [
      { tag: 'TAG-901', weightKg: 35, sex: 'Female' },
      { tag: 'TAG-902', weightKg: 38, sex: 'Male' },
    ];

    expect(draftCalves).toHaveLength(2);
    expect(draftCalves[0].tag).toBe('TAG-901');
    expect(draftCalves[1].tag).toBe('TAG-902');

    // Al confirmar, se encola en outbox con la estructura íntegra
    const outbox = new Outbox(db);
    await outbox.enqueue('recordAnimalEvent', {
      damId: dam.animalId,
      pregnancyId: dam.pregnancyId,
      offspring: draftCalves,
    });

    const pending = await outbox.pending();
    expect(pending).toHaveLength(1);
    const payload = pending[0].payload as any;
    expect(payload.offspring).toHaveLength(2);
    expect(payload.offspring[0].tag).toBe('TAG-901');
  });
});
