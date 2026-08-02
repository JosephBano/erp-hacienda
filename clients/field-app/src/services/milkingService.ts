import { SyncEngine } from './syncEngine';

export type MilkingShift = 'Morning' | 'Afternoon' | 'Evening';

export interface LocalMilkYield {
  id: string;
  animalId: string;
  shift: MilkingShift;
  liters: number;
  date: string;
  synced: boolean;
}

export interface LocalWithdrawal {
  animalId: string;
  target: 'Milk' | 'Meat' | 'Both';
  startDate: string;
  endDate: string;
}

export interface DailyMilkingSummary {
  date: string;
  totalLiters: number;
  recordsCount: number;
  yields: LocalMilkYield[];
}

export class MilkingService {
  private static YIELDS_KEY = 'hato_local_milk_yields';
  private static WITHDRAWALS_KEY = 'hato_local_withdrawals';

  private localYields: LocalMilkYield[] = [];
  private localWithdrawals: LocalWithdrawal[] = [];

  constructor(private syncEngine: SyncEngine) {
    this.loadStorage();
  }

  async recordGroupMilking(
    groupId: string,
    shift: MilkingShift,
    totalLiters: number,
    date: string = new Date().toISOString().split('T')[0]
  ): Promise<LocalMilkYield> {
    if (totalLiters < 0) {
      throw new Error('El volumen de leche debe ser mayor o igual a cero.');
    }

    const payload = {
      date,
      shift,
      groupId,
      totalLiters,
      recordedBy: 'field-user',
    };

    const outboxItem = await this.syncEngine.enqueueOperation('recordMilking', payload);

    const record: LocalMilkYield = {
      id: outboxItem.clientOperationId,
      animalId: `group-${groupId}`,
      shift,
      liters: totalLiters,
      date,
      synced: false,
    };

    this.localYields.push(record);
    this.persistStorage();
    return record;
  }

  async recordIndividualYield(
    animalId: string,
    shift: MilkingShift,
    liters: number,
    date: string = new Date().toISOString().split('T')[0]
  ): Promise<LocalMilkYield> {
    if (liters < 0) {
      throw new Error('El volumen de leche debe ser mayor o igual a cero.');
    }

    const withdrawalStatus = this.checkMilkWithdrawalStatus(animalId, date);
    if (withdrawalStatus.isWithheld) {
      throw new Error(`El animal tiene un período de retiro de leche activo hasta ${withdrawalStatus.endDate}. La leche no es vendible.`);
    }

    const payload = {
      date,
      shift,
      totalLiters: liters,
      recordedBy: 'field-user',
      individualYields: [{ animalId, liters }],
    };

    const outboxItem = await this.syncEngine.enqueueOperation('recordMilking', payload);

    const record: LocalMilkYield = {
      id: outboxItem.clientOperationId,
      animalId,
      shift,
      liters,
      date,
      synced: false,
    };

    this.localYields.push(record);
    this.persistStorage();
    return record;
  }

  checkMilkWithdrawalStatus(animalId: string, date: string = new Date().toISOString().split('T')[0]): { isWithheld: boolean; endDate?: string } {
    const active = this.localWithdrawals.find(
      (w) =>
        w.animalId === animalId &&
        (w.target === 'Milk' || w.target === 'Both') &&
        w.startDate <= date &&
        w.endDate >= date
    );

    if (active) {
      return { isWithheld: true, endDate: active.endDate };
    }
    return { isWithheld: false };
  }

  setLocalWithdrawals(withdrawals: LocalWithdrawal[]): void {
    this.localWithdrawals = withdrawals;
    if (typeof localStorage !== 'undefined') {
      localStorage.setItem(MilkingService.WITHDRAWALS_KEY, JSON.stringify(withdrawals));
    }
  }

  getDailyMilkingSummary(date: string = new Date().toISOString().split('T')[0]): DailyMilkingSummary {
    const dayYields = this.localYields.filter((y) => y.date === date);
    const totalLiters = dayYields.reduce((sum, y) => sum + y.liters, 0);

    return {
      date,
      totalLiters,
      recordsCount: dayYields.length,
      yields: dayYields,
    };
  }

  updateTodayMilking(recordId: string, newLiters: number): void {
    const today = new Date().toISOString().split('T')[0];
    const record = this.localYields.find((y) => y.id === recordId);

    if (!record) {
      throw new Error('Registro de ordeño no encontrado.');
    }

    if (record.date !== today) {
      throw new Error('Solo se puede editar el ordeño del día en curso (Art. 1).');
    }

    if (record.synced) {
      throw new Error('El registro ya fue sincronizado. Las correcciones requieren un nuevo evento.');
    }

    record.liters = newLiters;
    this.persistStorage();
  }

  private persistStorage(): void {
    if (typeof localStorage !== 'undefined') {
      localStorage.setItem(MilkingService.YIELDS_KEY, JSON.stringify(this.localYields));
    }
  }

  private loadStorage(): void {
    if (typeof localStorage !== 'undefined') {
      const rawY = localStorage.getItem(MilkingService.YIELDS_KEY);
      if (rawY) {
        try {
          this.localYields = JSON.parse(rawY);
        } catch {
          this.localYields = [];
        }
      }

      const rawW = localStorage.getItem(MilkingService.WITHDRAWALS_KEY);
      if (rawW) {
        try {
          this.localWithdrawals = JSON.parse(rawW);
        } catch {
          this.localWithdrawals = [];
        }
      }
    }
  }
}
