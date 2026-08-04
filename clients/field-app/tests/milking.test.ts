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

  beforeEach(() => {
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
    const record = await service.recordIndividualYield('cow-1', 'Morning', 12.5);

    const pending = await outbox.pending();
    expect(pending).toHaveLength(1);
    expect(pending[0].operationType).toBe('recordMilking');
    expect(record.liters).toBe(12.5);
  });

  it('records the day total per group in a single operation', async () => {
    await service.recordGroupMilking('group-1', 'Morning', 240);

    const [entry] = await outbox.pending();
    expect(entry.payload).toMatchObject({ groupId: 'group-1', totalLiters: 240 });
  });

  it('refuses a negative volume', async () => {
    await expect(service.recordIndividualYield('cow-1', 'Morning', -1)).rejects.toThrow(
      /mayor o igual a cero/i,
    );
  });

  /**
   * The blocking rule of Art. 19, decided offline. The old implementation read an
   * in-memory list that nothing ever filled, so the block could never fire on a real
   * phone.
   */
  it('blocks milk from a cow under an active withdrawal period', async () => {
    await giveWithdrawal('cow-treated', 'Milk', '2000-01-01', '2999-12-31');

    await expect(service.recordIndividualYield('cow-treated', 'Morning', 10)).rejects.toThrow(
      /retiro/i,
    );

    expect(await outbox.pending()).toHaveLength(0);
  });

  it('lets the milk through once the withdrawal period has ended', async () => {
    await giveWithdrawal('cow-recovered', 'Milk', '2000-01-01', '2000-01-10');

    await service.recordIndividualYield('cow-recovered', 'Morning', 10);

    expect(await outbox.pending()).toHaveLength(1);
  });

  it('ignores a meat-only withdrawal when recording milk', async () => {
    await giveWithdrawal('cow-meat-only', 'Meat', '2000-01-01', '2999-12-31');

    await service.recordIndividualYield('cow-meat-only', 'Morning', 10);

    expect(await outbox.pending()).toHaveLength(1);
  });

  it('reports which cows are withheld so the screen can mark them before a tap', async () => {
    await giveWithdrawal('cow-treated', 'Both', '2000-01-01', '2999-12-31');

    const status = await service.withdrawalStatus('cow-treated');

    expect(status.isWithheld).toBe(true);
    expect(status.endsAt).toBe('2999-12-31');
  });

  it('adds up the day so the employee can check the round before leaving', async () => {
    await service.recordIndividualYield('cow-1', 'Morning', 10);
    await service.recordIndividualYield('cow-2', 'Morning', 8.5);

    const summary = await service.dailySummary(today);

    expect(summary.recordsCount).toBe(2);
    expect(summary.totalLiters).toBe(18.5);
  });

  /**
   * Art. 1: history is never edited. Correcting today's figure before it leaves the phone
   * is fine; once the server has it, a correction is a new event.
   */
  it('allows correcting a figure that has not synced yet', async () => {
    const record = await service.recordIndividualYield('cow-1', 'Morning', 10);

    await service.correctTodayYield(record.clientOperationId, 11);

    const summary = await service.dailySummary(today);
    expect(summary.totalLiters).toBe(11);
  });

  it('refuses to edit a figure the server already has', async () => {
    const record = await service.recordIndividualYield('cow-1', 'Morning', 10);
    await outbox.markSynced(record.clientOperationId, 'server-ref');

    await expect(service.correctTodayYield(record.clientOperationId, 11)).rejects.toThrow(
      /sincroniz/i,
    );
  });
});
