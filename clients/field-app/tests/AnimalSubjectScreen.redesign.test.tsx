import React from 'react';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react-native';

import { AnimalSubjectScreen, type AnimalForSubject, type AnimalHistoryRecord } from '../src/screens/AnimalSubjectScreen';
import type { Outbox, OutboxEntry } from '../src/services/outbox';

describe('AnimalSubjectScreen redesign (feature-0010 Commit 4)', () => {
  const noop = () => undefined;

  const mockAnimals: AnimalForSubject[] = [
    {
      animalId: 'pig-007',
      label: 'Clara (007)',
      tag: '007',
      name: 'Clara',
      sex: 'Female',
      groupName: 'Maternidad 1',
      activeIdentifiers: [{ type: 'FarmTag', value: '007' }],
      isPregnant: true,
      expectedBirthDate: '2026-10-15',
    },
    {
      animalId: 'pig-7',
      label: 'Siete (7)',
      tag: '7',
      name: 'Siete',
      sex: 'Male',
      groupName: 'Crecimiento',
      activeIdentifiers: [{ type: 'FarmTag', value: '7' }],
    },
    {
      animalId: 'pig-008',
      label: 'Macho 008',
      tag: '008',
      sex: 'Male',
      groupName: 'Maternidad 1',
      activeIdentifiers: [{ type: 'FarmTag', value: '008' }],
      isWithheld: true,
      withheldUntil: '2026-09-20',
    },
    {
      animalId: 'pig-disposed',
      label: 'Antigua (009)',
      tag: '009',
      sex: 'Female',
      groupName: 'Bajas',
      disposedAt: '2026-08-30T10:00:00Z',
    },
    {
      animalId: 'pig-untagged-123456',
      label: 'Sin arete · 123456',
      sex: 'Female',
      name: 'Panchita',
      hasPendingTag: true,
    },
  ];

  beforeEach(() => {
    global.fetch = jest.fn(() => {
      throw new Error('AnimalSubjectScreen must not call the network.');
    }) as unknown as typeof fetch;
  });

  describe('T4.1 & T4.2: Search and filtering with leading zeros preservation', () => {
    it('filters herd by sex using filter chips (Todos, Hembras, Machos)', async () => {
      await render(
        <AnimalSubjectScreen
          animals={mockAnimals}
          recentIds={[]}
          onSelectAnimal={noop}
          onActivity={noop}
          onClearSelection={noop}
        />,
      );

      // Initially all animals are shown
      expect(screen.getByTestId('animal-row-pig-007')).toBeTruthy();
      expect(screen.getByTestId('animal-row-pig-7')).toBeTruthy();
      expect(screen.getByTestId('animal-row-pig-008')).toBeTruthy();

      // Tap "Hembras" chip
      await act(async () => {
        fireEvent.press(screen.getByTestId('filter-sex-female'));
      });
      expect(screen.getByTestId('animal-row-pig-007')).toBeTruthy();
      expect(screen.getByTestId('animal-row-pig-untagged-123456')).toBeTruthy();
      expect(screen.queryByTestId('animal-row-pig-7')).toBeNull();
      expect(screen.queryByTestId('animal-row-pig-008')).toBeNull();

      // Tap "Machos" chip
      await act(async () => {
        fireEvent.press(screen.getByTestId('filter-sex-male'));
      });
      expect(screen.getByTestId('animal-row-pig-7')).toBeTruthy();
      expect(screen.getByTestId('animal-row-pig-008')).toBeTruthy();
      expect(screen.queryByTestId('animal-row-pig-007')).toBeNull();

      // Tap "Todos" chip
      await act(async () => {
        fireEvent.press(screen.getByTestId('filter-sex-all'));
      });
      expect(screen.getByTestId('animal-row-pig-007')).toBeTruthy();
      expect(screen.getByTestId('animal-row-pig-7')).toBeTruthy();
    });

    it('filters herd by group using group chips', async () => {
      await render(
        <AnimalSubjectScreen
          animals={mockAnimals}
          recentIds={[]}
          onSelectAnimal={noop}
          onActivity={noop}
          onClearSelection={noop}
        />,
      );

      // Tap "Maternidad 1" group chip
      await act(async () => {
        fireEvent.press(screen.getByTestId('filter-group-Maternidad 1'));
      });
      expect(screen.getByTestId('animal-row-pig-007')).toBeTruthy();
      expect(screen.getByTestId('animal-row-pig-008')).toBeTruthy();
      expect(screen.queryByTestId('animal-row-pig-7')).toBeNull();

      // Tap "Todos los grupos" chip
      await act(async () => {
        fireEvent.press(screen.getByTestId('filter-group-all'));
      });
      expect(screen.getByTestId('animal-row-pig-7')).toBeTruthy();
    });

    it('preserves leading zeros strictly during search: 007 matches 007 and NOT 7 (T4.2)', async () => {
      await render(
        <AnimalSubjectScreen
          animals={mockAnimals}
          recentIds={[]}
          onSelectAnimal={noop}
          onActivity={noop}
          onClearSelection={noop}
        />,
      );

      await act(async () => {
        fireEvent.changeText(screen.getByTestId('animal-subject-search'), '007');
      });

      // Exactly matches 'pig-007'
      expect(screen.getByTestId('animal-row-pig-007')).toBeTruthy();
      // Does NOT match 'pig-7' (leading zero preservation)
      expect(screen.queryByTestId('animal-row-pig-7')).toBeNull();
    });
  });

  describe('T4.3: Ambiguity handling', () => {
    it('shows ambiguity notice when multiple animals have identical tag for conscious choice', async () => {
      const ambiguousAnimals: AnimalForSubject[] = [
        { animalId: 'a1', label: 'Cerda A', tag: '100', sex: 'Female', groupName: 'Lote 1' },
        { animalId: 'a2', label: 'Cerda B', tag: '100', sex: 'Female', groupName: 'Lote 2' },
      ];

      await render(
        <AnimalSubjectScreen
          animals={ambiguousAnimals}
          recentIds={[]}
          onSelectAnimal={noop}
          onActivity={noop}
          onClearSelection={noop}
        />,
      );

      await act(async () => {
        fireEvent.changeText(screen.getByTestId('animal-subject-search'), '100');
      });

      expect(screen.getByText(/Múltiples animales coinciden exactamente/)).toBeTruthy();
      expect(screen.getByTestId('animal-row-a1')).toBeTruthy();
      expect(screen.getByTestId('animal-row-a2')).toBeTruthy();
    });
  });

  describe('T4.4, T4.5 & T4.9: Animal record, backed-only states & untagged identification', () => {
    it('displays prominent TagBadge, name, sex, group and backed-only states (T4.4, T4.5)', async () => {
      await render(
        <AnimalSubjectScreen
          animals={mockAnimals}
          recentIds={[]}
          selectedAnimalId="pig-007"
          onSelectAnimal={noop}
          onActivity={noop}
          onClearSelection={noop}
        />,
      );

      // TagBadge displays '007'
      expect(screen.getByTestId('animal-record-tag-badge')).toBeTruthy();
      expect(screen.getByText('007')).toBeTruthy();

      // Name & internal id
      expect(screen.getByTestId('animal-record-name')).toBeTruthy();
      expect(screen.getByText(/Clara/)).toBeTruthy();
      expect(screen.getByTestId('animal-record-id')).toBeTruthy();

      // Metadata chips: Sexo, Grupo, Gestante
      expect(screen.getByTestId('animal-record-sex-badge')).toBeTruthy();
      expect(screen.getByTestId('animal-record-group-badge')).toBeTruthy();
      expect(screen.getByTestId('animal-record-pregnancy-badge')).toBeTruthy();
      expect(screen.getByText(/Gestante: FPP 2026-10-15/)).toBeTruthy();

      // Backed-only states (T4.5): pig-007 has NO withdrawal and is NOT disposed
      expect(screen.queryByTestId('animal-record-withdrawal-notice')).toBeNull();
      expect(screen.queryByTestId('animal-record-disposed-notice')).toBeNull();
      // Must NOT invent "Sin retiro", "Sano", or "Disponible"
      expect(screen.queryByText(/sin retiro/i)).toBeNull();
      expect(screen.queryByText(/sano/i)).toBeNull();
      expect(screen.queryByText(/disponible/i)).toBeNull();
    });

    it('displays withdrawal notice ONLY when backed by data, and does not invent pregnancy or baja (T4.5)', async () => {
      await render(
        <AnimalSubjectScreen
          animals={mockAnimals}
          recentIds={[]}
          selectedAnimalId="pig-008"
          onSelectAnimal={noop}
          onActivity={noop}
          onClearSelection={noop}
        />,
      );

      // Withdrawal is present
      expect(screen.getByTestId('animal-record-withdrawal-notice')).toBeTruthy();
      expect(screen.getByText(/PERÍODO DE RETIRO ACTIVO HASTA 2026-09-20/)).toBeTruthy();

      // Pregnancy & disposal are absent: must NOT invent "No gestante", "Activo", etc.
      expect(screen.queryByTestId('animal-record-pregnancy-badge')).toBeNull();
      expect(screen.queryByTestId('animal-record-disposed-notice')).toBeNull();
      expect(screen.queryByText(/no gestante/i)).toBeNull();
      expect(screen.queryByText(/sin preñez/i)).toBeNull();
    });

    it('identifies untagged animals with TagBadge "Sin arete" and readable ID/name (T4.9)', async () => {
      await render(
        <AnimalSubjectScreen
          animals={mockAnimals}
          recentIds={[]}
          selectedAnimalId="pig-untagged-123456"
          onSelectAnimal={noop}
          onActivity={noop}
          onClearSelection={noop}
        />,
      );

      expect(screen.getByTestId('animal-record-tag-badge')).toBeTruthy();
      expect(screen.getByText('Sin arete')).toBeTruthy();
      expect(screen.getByTestId('animal-record-name')).toBeTruthy();
      expect(screen.getByText(/Panchita/)).toBeTruthy();
      expect(screen.getByTestId('animal-record-id')).toBeTruthy();
      expect(screen.getByText(/123456/)).toBeTruthy();
    });
  });

  describe('T4.6 & T4.7: History segregation & deduplication', () => {
    it('separates confirmed history from pending local outbox records (T4.6)', async () => {
      const mockOutbox: Outbox = {
        all: jest.fn().mockResolvedValue([
          {
            clientOperationId: 'op-pending-1',
            operationType: 'recordTreatment',
            occurredAt: '2026-09-08T09:15:00Z',
            payload: { animalId: 'pig-007', medicationName: 'Ceftiofur', dose: '5.0', unit: 'ml' },
            status: 'pending',
            attempts: 0,
            queuedAt: Date.now(),
          } as OutboxEntry,
        ]),
      } as unknown as Outbox;

      const historyRecords: AnimalHistoryRecord[] = [
        {
          id: 'evt-confirmed-1',
          eventType: 'weight',
          occurredAt: '2026-08-15T10:00:00Z',
          summary: 'Pesaje',
          details: '84.5 kg',
          status: 'confirmed',
        },
      ];

      await render(
        <AnimalSubjectScreen
          animals={mockAnimals}
          recentIds={[]}
          selectedAnimalId="pig-007"
          onSelectAnimal={noop}
          onActivity={noop}
          onClearSelection={noop}
          outbox={mockOutbox}
          historyRecords={historyRecords}
        />,
      );

      // Confirmed section has the confirmed weight event
      expect(await screen.findByTestId('confirmed-history-section')).toBeTruthy();
      expect(screen.getByTestId('confirmed-history-item-evt-confirmed-1')).toBeTruthy();
      expect(screen.getByText(/84.5 kg/)).toBeTruthy();

      // Pending section has the pending treatment
      expect(await screen.findByTestId('pending-history-section')).toBeTruthy();
      expect(screen.getByTestId('pending-history-item-op-pending-1')).toBeTruthy();
      expect(screen.getByText(/Ceftiofur \(5.0 ml\)/)).toBeTruthy();
      expect(screen.getByText('● Pendiente')).toBeTruthy();
    });

    it('does NOT duplicate events when server confirmation arrives (T4.7)', async () => {
      // Step 1: Outbox has synced entry with resultRef: 'evt-server-99'
      // And historyRecords has the exact same server event 'evt-server-99'
      const mockOutbox: Outbox = {
        all: jest.fn().mockResolvedValue([
          {
            clientOperationId: 'op-local-99',
            operationType: 'recordTreatment',
            occurredAt: '2026-09-08T09:15:00Z',
            payload: { animalId: 'pig-007', medicationName: 'Ceftiofur', dose: '5.0', unit: 'ml' },
            status: 'synced',
            resultRef: 'evt-server-99',
            attempts: 1,
            queuedAt: Date.now(),
          } as OutboxEntry,
        ]),
      } as unknown as Outbox;

      const historyRecords: AnimalHistoryRecord[] = [
        {
          id: 'evt-server-99',
          eventType: 'treatment',
          occurredAt: '2026-09-08T09:15:00Z',
          summary: 'Tratamiento',
          details: 'Ceftiofur 5.0 ml',
          status: 'confirmed',
          clientOperationId: 'op-local-99',
        },
      ];

      await render(
        <AnimalSubjectScreen
          animals={mockAnimals}
          recentIds={[]}
          selectedAnimalId="pig-007"
          onSelectAnimal={noop}
          onActivity={noop}
          onClearSelection={noop}
          outbox={mockOutbox}
          historyRecords={historyRecords}
        />,
      );

      await waitFor(() => expect(screen.getByTestId('confirmed-history-section')).toBeTruthy());

      // No pending events remain
      expect(screen.queryByTestId('pending-history-section')).toBeNull();

      // Exactly ONE confirmed entry is rendered for evt-server-99, not duplicated!
      const items = screen.getAllByTestId(/^confirmed-history-item-/);
      expect(items.length).toBe(1);
      expect(items[0].props.testID).toBe('confirmed-history-item-evt-server-99');
    });
  });

  describe('T4.8: Query & filter persistence on returning from record', () => {
    it('maintains typed text and active filter chips upon navigating back from record', async () => {
      function StatefulWrapper() {
        const [selectedId, setSelectedId] = React.useState<string | undefined>(undefined);
        return (
          <AnimalSubjectScreen
            animals={mockAnimals}
            recentIds={[]}
            selectedAnimalId={selectedId}
            onSelectAnimal={(id) => setSelectedId(id)}
            onClearSelection={() => setSelectedId(undefined)}
            onActivity={noop}
          />
        );
      }

      await render(<StatefulWrapper />);

      // 1. Type query
      await act(async () => {
        fireEvent.changeText(screen.getByTestId('animal-subject-search'), 'Clara');
      });
      // 2. Select sex filter 'Hembras'
      await act(async () => {
        fireEvent.press(screen.getByTestId('filter-sex-female'));
      });

      // Verify row is found and tap to open record
      expect(screen.getByTestId('animal-row-pig-007')).toBeTruthy();
      await act(async () => {
        fireEvent.press(screen.getByTestId('animal-row-pig-007'));
      });

      // Detail screen is shown
      expect(await screen.findByTestId('animal-subject-detail')).toBeTruthy();

      // Tap back button
      await act(async () => {
        fireEvent.press(screen.getByTestId('back-to-animal-picker'));
      });

      // Search picker is restored
      expect(await screen.findByTestId('animal-subject-screen')).toBeTruthy();

      // Query 'Clara' was preserved in the search input
      expect(screen.getByTestId('animal-subject-search').props.value).toBe('Clara');

      // The filtered row is still visible
      expect(screen.getByTestId('animal-row-pig-007')).toBeTruthy();
    });
  });

  describe('T4.10: Aptitude (0005) & Permissions (0008)', () => {
    it('does NOT offer ordinary activities if animal is disposed (baja ya ocurrió)', async () => {
      await render(
        <AnimalSubjectScreen
          animals={mockAnimals}
          recentIds={[]}
          selectedAnimalId="pig-disposed"
          onSelectAnimal={noop}
          onActivity={noop}
          onClearSelection={noop}
        />,
      );

      expect(await screen.findByTestId('animal-record-disposed-notice')).toBeTruthy();
      expect(screen.getByText(/Animal dado de baja\. No hay actividades disponibles\./)).toBeTruthy();

      // Ordinary activities must NOT be offered
      expect(screen.queryByTestId('activity-treatment')).toBeNull();
      expect(screen.queryByTestId('activity-weight')).toBeNull();
      expect(screen.queryByTestId('activity-move')).toBeNull();
      expect(screen.queryByTestId('activity-disposal')).toBeNull();
      expect(screen.queryByTestId('activity-birth')).toBeNull();
    });

    it('offers parto ONLY for eligible females, never for males (0005)', async () => {
      // Female: pig-007 offers activity-birth
      await render(
        <AnimalSubjectScreen
          animals={mockAnimals}
          recentIds={[]}
          selectedAnimalId="pig-007"
          onSelectAnimal={noop}
          onActivity={noop}
          onClearSelection={noop}
        />,
      );

      expect(await screen.findByTestId('activity-birth')).toBeTruthy();
    });

    it('does not offer parto for males (0005)', async () => {
      // Male: pig-7 does NOT offer activity-birth
      await render(
        <AnimalSubjectScreen
          animals={mockAnimals}
          recentIds={[]}
          selectedAnimalId="pig-7"
          onSelectAnimal={noop}
          onActivity={noop}
          onClearSelection={noop}
        />,
      );

      expect(screen.queryByTestId('activity-birth')).toBeNull();
      expect(screen.getByTestId('activity-treatment')).toBeTruthy();
      expect(screen.getByTestId('activity-weight')).toBeTruthy();
    });

    it('respects permissions (0008): hides write activities when lacking livestock.animals.write', async () => {
      await render(
        <AnimalSubjectScreen
          animals={mockAnimals}
          recentIds={[]}
          selectedAnimalId="pig-7"
          onSelectAnimal={noop}
          onActivity={noop}
          onClearSelection={noop}
          permissions={['livestock.animals.read']} // Only read permissions!
        />,
      );

      expect(await screen.findByText(/No tiene permisos para registrar actividades sobre este animal\./)).toBeTruthy();
      expect(screen.queryByTestId('activity-treatment')).toBeNull();
      expect(screen.queryByTestId('activity-weight')).toBeNull();
      expect(screen.queryByTestId('activity-move')).toBeNull();
      expect(screen.queryByTestId('activity-disposal')).toBeNull();
    });
  });
});
