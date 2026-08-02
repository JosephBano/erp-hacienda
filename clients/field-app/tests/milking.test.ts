import { MilkingService } from '../src/services/milkingService';
import { SyncEngine } from '../src/services/syncEngine';

describe('MilkingService Offline Tests', () => {
  let syncEngine: SyncEngine;
  let milkingService: MilkingService;

  beforeEach(() => {
    if (typeof localStorage !== 'undefined') {
      localStorage.clear();
    }
    syncEngine = new SyncEngine();
    milkingService = new MilkingService(syncEngine);
  });

  test('recordIndividualYield enqueues operation and returns yield record', async () => {
    const record = await milkingService.recordIndividualYield('vaca-001', 'Morning', 14.5);

    expect(record.animalId).toBe('vaca-001');
    expect(record.liters).toBe(14.5);
    expect(syncEngine.getOutboxStats().pendingCount).toBe(1);
  });

  test('recordIndividualYield blocks registration if animal has active milk withdrawal', async () => {
    const today = new Date().toISOString().split('T')[0];
    milkingService.setLocalWithdrawals([
      {
        animalId: 'vaca-retiro',
        target: 'Milk',
        startDate: '2026-01-01',
        endDate: '2026-12-31',
      },
    ]);

    await expect(milkingService.recordIndividualYield('vaca-retiro', 'Morning', 10)).rejects.toThrow(
      'La leche no es vendible'
    );
  });

  test('getDailyMilkingSummary sums daily yield correctly', async () => {
    const today = new Date().toISOString().split('T')[0];

    await milkingService.recordIndividualYield('vaca-1', 'Morning', 12, today);
    await milkingService.recordIndividualYield('vaca-2', 'Morning', 15, today);

    const summary = milkingService.getDailyMilkingSummary(today);
    expect(summary.totalLiters).toBe(27);
    expect(summary.recordsCount).toBe(2);
  });

  test('updateTodayMilking allows updating unsynced same-day record', async () => {
    const today = new Date().toISOString().split('T')[0];
    const record = await milkingService.recordIndividualYield('vaca-1', 'Morning', 10, today);

    milkingService.updateTodayMilking(record.id, 12.5);

    const summary = milkingService.getDailyMilkingSummary(today);
    expect(summary.totalLiters).toBe(12.5);
  });
});
