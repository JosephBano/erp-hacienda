import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { Outbox } from '../src/services/outbox';
import { MilkingService } from '../src/services/milkingService';

/**
 * Milking is the 5 AM flow that has to beat the notebook. It runs with no signal, so both
 * the record and the Art. 19 withdrawal block are resolved entirely from local data — the
 * withdrawal periods the pull left behind.
 */
describe('MilkingService', () => {
  let database: Database;
  let outbox: Outbox;
  let service: MilkingService;

  const today = new Date().toISOString().slice(0, 10);
  const recordedBy = 'tester@hato';

  /**
   * Stamps a milkable species in the local DB so the service-level guard accepts the
   * animals we register below. Art. 8: capability is per-species, configured in the DB;
   * the service refuses anything whose species has is_milkable = false.
   */
  const giveMilkableSpecies = async (speciesId: string, name = 'Bovino') => {
    await database.write(async () => {
      await database.get('species').create((row: any) => {
        row._raw.id = speciesId;
        row.name = name;
        row.gestationDays = 283;
        row.isMilkable = true;
        row.isDeleted = false;
      });
    });
  };

  const giveCow = async (animalId: string, speciesId: string) => {
    await database.write(async () => {
      await database.get('animals').create((row: any) => {
        row._raw.id = animalId;
        row.sex = 'Female';
        row.speciesId = speciesId;
        row.isDeleted = false;
        row.serverCreatedAt = Date.now();
      });
    });
  };

  beforeEach(async () => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-milking-${Math.random()}`,
    });

    database = new Database({ adapter: adapter as never, modelClasses });
    outbox = new Outbox(database);
    service = new MilkingService(database);

    await giveMilkableSpecies('species-bovino');
    await giveCow('cow-1', 'species-bovino');
    await giveCow('cow-2', 'species-bovino');
    await giveCow('cow-treated', 'species-bovino');
    await giveCow('cow-recovered', 'species-bovino');
    await giveCow('cow-meat-only', 'species-bovino');
  });

  const giveWithdrawal = async (animalId: string, target: string, from: string, to: string) => {
    await database.write(async () => {
      await database.get('withdrawal_periods').create((row: any) => {
        row._raw.id = `wd-${animalId}`;
        row.animalId = animalId;
        row.eventId = 'event-1';
        row.target = target;
        row.startsAt = from;
        row.endsAt = to;
        row.isDeleted = false;
      });
    });
  };

  it('queues an individual milking that survives with no signal', async () => {
    const record = await service.recordIndividualYield('cow-1', 'Morning', 12.5, recordedBy);

    const pending = await outbox.pending();
    expect(pending).toHaveLength(1);
    expect(pending[0].operationType).toBe('recordMilking');
    expect(pending[0].payload).toMatchObject({ recordedBy });
    expect(record.liters).toBe(12.5);
  });

  it('records the day total per group in a single operation', async () => {
    await service.recordGroupMilking('group-1', 'Morning', 240, recordedBy);

    const [entry] = await outbox.pending();
    expect(entry.payload).toMatchObject({ groupId: 'group-1', totalLiters: 240, recordedBy });
  });

  it('refuses a negative volume', async () => {
    await expect(service.recordIndividualYield('cow-1', 'Morning', -1, recordedBy)).rejects.toThrow(
      /negativo/i,
    );
  });

  /**
   * 0 litres is not a milking — no cow produces exactly nothing. A dry day should not be
   * recorded as a milking at all; if the need exists it will be covered by the plausibility
   * ranges in 3.5a.6 (configurable per species, fails open). Until then, the service
   * refuses 0 outright so the outbox never carries a meaningless record.
   */
  it('refuses a zero volume as not a milking', async () => {
    await expect(service.recordIndividualYield('cow-1', 'Morning', 0, recordedBy)).rejects.toThrow(
      /0|no es un ordeño/i,
    );

    expect(await outbox.pending()).toHaveLength(0);
  });

  /**
   * PLAN 3.5a.0 #4 locked as a test, not as a comment: impossible values are blocked,
   * improbable values pass through. The plausibility ceiling (a 1000-L cow) is NOT this
   * layer's job — it lives in 3.5a.6 with per-species ranges — so a 1-L milking is a
   * legitimate record here even though it is unusual. "Do not punish the operator"
   * means the guardrail rejects the impossible and lets the questionable through; the
   * questionable one is somebody else's problem, with a configurable knob.
   */
  it('locks the input guardrail: improbable values pass, only impossible ones are rejected', async () => {
    // improbable (a 1-L milking): the service lets it through.
    await service.recordIndividualYield('cow-1', 'Morning', 1, recordedBy);
    expect(await outbox.pending()).toHaveLength(1);
  });

  /**
   * Art. 8 defense-in-depth: even if the UI is bypassed (a stale cached screen, a test
   * calling the service directly, an automated script) the service refuses to record a
   * milking for an animal whose species has `is_milkable = false`. Pigs stay pigs.
   */
  it('refuses milk for an animal whose species is not milkable', async () => {
    await giveMilkableSpecies('species-porcino', 'Porcino');
    await giveCow('pig-1', 'species-porcino');
    // Override the default: this species opts out of milking.
    await database.write(async () => {
      const species = await database.get('species').find('species-porcino');
      await species.update((row: any) => {
        row.isMilkable = false;
      });
    });

    await expect(service.recordIndividualYield('pig-1', 'Morning', 5, recordedBy)).rejects.toThrow(
      /no está habilitada para ordeño/i,
    );
  });

  /**
   * The blocking rule of Art. 19, decided offline. The old implementation read an
   * in-memory list that nothing ever filled, so the block could never fire on a real
   * phone.
   */
  it('blocks milk from a cow under an active withdrawal period', async () => {
    await giveWithdrawal('cow-treated', 'Milk', '2000-01-01', '2999-12-31');

    await expect(service.recordIndividualYield('cow-treated', 'Morning', 10, recordedBy)).rejects.toThrow(
      /retiro/i,
    );

    expect(await outbox.pending()).toHaveLength(0);
  });

  it('lets the milk through once the withdrawal period has ended', async () => {
    await giveWithdrawal('cow-recovered', 'Milk', '2000-01-01', '2000-01-10');

    await service.recordIndividualYield('cow-recovered', 'Morning', 10, recordedBy);

    expect(await outbox.pending()).toHaveLength(1);
  });

  it('ignores a meat-only withdrawal when recording milk', async () => {
    await giveWithdrawal('cow-meat-only', 'Meat', '2000-01-01', '2999-12-31');

    await service.recordIndividualYield('cow-meat-only', 'Morning', 10, recordedBy);

    expect(await outbox.pending()).toHaveLength(1);
  });

  it('reports which cows are withheld so the screen can mark them before a tap', async () => {
    await giveWithdrawal('cow-treated', 'Both', '2000-01-01', '2999-12-31');

    const status = await service.withdrawalStatus('cow-treated');

    expect(status.isWithheld).toBe(true);
    expect(status.endsAt).toBe('2999-12-31');
  });

  it('adds up the day so the employee can check the round before leaving', async () => {
    await service.recordIndividualYield('cow-1', 'Morning', 10, recordedBy);
    await service.recordIndividualYield('cow-2', 'Morning', 8.5, recordedBy);

    const summary = await service.dailySummary(today);

    expect(summary.recordsCount).toBe(2);
    expect(summary.totalLiters).toBe(18.5);
  });

  /**
   * Art. 1: history is never edited. Correcting today's figure before it leaves the phone
   * is fine; once the server has it, a correction is a new event.
   */
  it('allows correcting a figure that has not synced yet', async () => {
    const record = await service.recordIndividualYield('cow-1', 'Morning', 10, recordedBy);

    await service.correctTodayYield(record.clientOperationId, 11);

    const summary = await service.dailySummary(today);
    expect(summary.totalLiters).toBe(11);
  });

  it('refuses to edit a figure the server already has', async () => {
    const record = await service.recordIndividualYield('cow-1', 'Morning', 10, recordedBy);
    await outbox.markSynced(record.clientOperationId, 'server-ref');

    await expect(service.correctTodayYield(record.clientOperationId, 11)).rejects.toThrow(
      /sincroniz/i,
    );
  });
});