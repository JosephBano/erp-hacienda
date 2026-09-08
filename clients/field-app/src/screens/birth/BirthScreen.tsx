import React, { useState } from 'react';

import { Notice, Screen, Title } from '../../ui/components';
import { useDraftFlag } from '../../ui/draftGuard';
import { useSingleFlight } from '../../ui/useSingleFlight';
import type { BirthService, OffspringInput, Sex } from '../../services/birthService';
import type { PregnantDam } from '../../services/herdQueries';
import { StepIndicator } from './StepIndicator';
import { Step1PickDam } from './Step1PickDam';
import { BirthDifficulty, Step2ConfirmDetails } from './Step2ConfirmDetails';
import { Step3Offspring } from './Step3Offspring';
import { Step4Review } from './Step4Review';

export interface BirthScreenProps {
  service: BirthService;
  dams: PregnantDam[];
  onRecorded?: () => void;
  onCancel?: () => void;
}

export function BirthScreen({ service, dams, onRecorded, onCancel }: BirthScreenProps) {
  const [step, setStep] = useState<1 | 2 | 3 | 4>(1);
  const [dam, setDam] = useState<PregnantDam | null>(null);
  const [birthDate, setBirthDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [difficulty, setDifficulty] = useState<BirthDifficulty>('Normal');
  const [offspring, setOffspring] = useState<OffspringInput[]>([]);
  const [error, setError] = useState<string | null>(null);
  const { busy, runOnce } = useSingleFlight();

  /**
   * D3 / T5.2 — a chosen dam means the wizard has started and the employee is
   * in the pen recording the birth. Leaving silently throws away the calves
   * they just counted off the ground.
   */
  useDraftFlag(dam !== null);

  const reset = () => {
    setStep(1);
    setDam(null);
    setBirthDate(new Date().toISOString().slice(0, 10));
    setDifficulty('Normal');
    setOffspring([]);
    setError(null);
  };

  const handleSelectDam = (selected: PregnantDam) => {
    setDam(selected);
    setError(null);
    setStep(2);
  };

  const addCalf = (sex: Sex) => setOffspring((curr) => [...curr, { sex }]);
  const removeCalf = (index: number) => setOffspring((curr) => curr.filter((_, i) => i !== index));
  const toggleCalfSex = (index: number) =>
    setOffspring((curr) =>
      curr.map((c, i) => (i === index ? { ...c, sex: c.sex === 'M' ? 'F' : 'M' } : c)),
    );

  const setCalfFarmTag = (index: number, rawText: string) => {
    const trimmed = rawText.trim();
    setOffspring((curr) =>
      curr.map((c, i) => (i === index ? { ...c, farmTag: trimmed || undefined } : c)),
    );
  };

  const setCalfWeight = (index: number, rawText: string) => {
    const cleaned = rawText.replace(',', '.').trim();
    if (cleaned === '') {
      setOffspring((curr) =>
        curr.map((c, i) => (i === index ? { ...c, birthWeightKg: undefined } : c)),
      );
      setError(null);
      return;
    }
    const parsed = Number(cleaned);
    if (!Number.isFinite(parsed) || parsed <= 0) {
      setError('El peso al nacer debe ser un valor positivo (en kilogramos).');
      return;
    }
    setError(null);
    setOffspring((curr) =>
      curr.map((c, i) => (i === index ? { ...c, birthWeightKg: parsed } : c)),
    );
  };

  const isDamObsolete = Boolean(
    dam && !dams.some((d) => d.animalId === dam.animalId && d.pregnancyId === dam.pregnancyId),
  );

  /*
   * A birth is the most expensive duplicate in the app: two "Registrar parto"
   * taps used to enqueue two birthings, each with its own calves, against one
   * pregnancy. Undoing that is a correction event per animal that never
   * existed (regla dura 1). `runOnce` refuses the second tap synchronously,
   * before `recordBirth` yields (D2).
   *
   * On success the screen navigates home, so this component is gone by the
   * time the operation settles; the hook only touches `busy` while mounted, so
   * the interrupted transition neither warns nor leaves the latch shut (T4.5).
   */
  const submit = () =>
    runOnce(async () => {
      if (!dam || isDamObsolete) return;
      setError(null);
      try {
        await service.recordBirth({
          damId: dam.animalId,
          pregnancyId: dam.pregnancyId,
          birthDate,
          difficulty,
          offspring,
        });
        reset();
        onRecorded?.();
      } catch (caught) {
        setError((caught as Error).message);
      }
    });

  const handleCancel = () => {
    reset();
    onCancel?.();
  };

  return (
    <Screen testID="birth-screen">
      <Title>Parto</Title>
      <StepIndicator currentStep={step} />
      {error ? <Notice text={error} /> : null}

      {step === 1 ? (
        <Step1PickDam dams={dams} onSelectDam={handleSelectDam} />
      ) : step === 2 && dam ? (
        <Step2ConfirmDetails
          dam={dam}
          birthDate={birthDate}
          onChangeBirthDate={setBirthDate}
          difficulty={difficulty}
          onChangeDifficulty={setDifficulty}
          onNext={() => setStep(3)}
          onBack={() => setStep(1)}
        />
      ) : step === 3 && dam ? (
        <Step3Offspring
          offspring={offspring}
          onAdd={addCalf}
          onRemove={removeCalf}
          onToggleSex={toggleCalfSex}
          onSetFarmTag={setCalfFarmTag}
          onSetWeight={setCalfWeight}
          onNext={() => setStep(4)}
          onBack={() => setStep(2)}
        />
      ) : step === 4 && dam ? (
        <Step4Review
          dam={dam}
          birthDate={birthDate}
          difficulty={difficulty}
          offspring={offspring}
          busy={busy}
          isDamObsolete={isDamObsolete}
          onChangeDam={() => {
            setDam(null);
            setStep(1);
            setError(null);
          }}
          onSubmit={() => void submit()}
          onBack={() => setStep(3)}
          onCancel={handleCancel}
        />
      ) : null}
    </Screen>
  );
}
