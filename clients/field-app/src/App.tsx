import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Platform, SafeAreaView, StatusBar, StyleSheet, View } from 'react-native';

import { createDatabase } from './database';
import { AnimalEditService } from './services/animalEditService';
import { AuthService } from './services/authService';
import { BirthService } from './services/birthService';
import { EventService } from './services/eventService';
import { FeedConsumptionService } from './services/feedConsumptionService';
import { HttpAnimalGroupsApi } from './services/animalGroupsApi';
import { HttpSyncApi } from './services/syncApi';
import { MilkingService } from './services/milkingService';
import { ModuleVisibility } from './services/moduleVisibility';
import { Outbox } from './services/outbox';
import { SyncEngine } from './services/syncEngine';
import {
  loadFeedItems,
  loadGroups,
  loadHerd,
  loadMedications,
  loadMortalityCauses,
  loadPregnantDams,
  loadTreatmentProducts,
  type PregnantDam,
} from './services/herdQueries';
import { ActivitiesHub } from './screens/ActivitiesHub';
import { AnimalEditScreen } from './screens/AnimalEditScreen';
import { AnimalSubjectScreen } from './screens/AnimalSubjectScreen';
import { BirthScreen } from './screens/BirthScreen';
import { EventsScreen } from './screens/EventsScreen';
import { LoginScreen } from './screens/LoginScreen';
import { LotEventsScreen } from './screens/LotEventsScreen';
import { LotSubjectScreen, type LotActivity } from './screens/LotSubjectScreen';
import { MilkingScreen } from './screens/MilkingScreen';
import { SyncStatusScreen } from './screens/SyncStatusScreen';
import { TodayScreen } from './screens/TodayScreen';
import { TreatScreen } from './screens/TreatScreen';
import { VaccinateScreen } from './screens/VaccinateScreen';
import type { TabKey } from './screens/navigation';
import { BigButton, Body, Screen, Title } from './ui/components';
import { theme } from './ui/theme';

// Reads the API URL from the EAS build profile's env (see eas.json), falling back to the
// Android emulator's loopback alias for local dev with `expo start`. The URL is baked
// into the JS bundle at build time — it is NOT runtime-configurable. That is on purpose
// (ADR-0010): a runtime override would conflate bundle-misconfig with network-down and
// would require designing a config-delivery channel we do not need yet.
const API_BASE_URL = process.env.EXPO_PUBLIC_API_URL ?? 'http://10.0.2.2:5282';
const DEVICE_ID = 'field-device';

type Tab = TabKey;

/**
 * Composition root of the field app.
 *
 * Navigation is a plain piece of state rather than a router: the app has five
 * destinations, all one tap from the home screen, and every extra layer between a gloved
 * thumb and a record is a layer that can go wrong at 5 AM.
 */
export default function App() {
  const database = useMemo(() => createDatabase(), []);
  const auth = useMemo(() => new AuthService(API_BASE_URL), []);
  const outbox = useMemo(() => new Outbox(database), [database]);

  const api = useMemo(
    () =>
      new HttpSyncApi({
        baseUrl: API_BASE_URL,
        getToken: () => auth.token(),
        refreshToken: () => auth.refresh(),
        deviceId: DEVICE_ID,
      }),
    [auth],
  );

  const engine = useMemo(() => new SyncEngine(database, api), [api, database]);

  const milking = useMemo(() => new MilkingService(database), [database]);
  const events = useMemo(() => new EventService(database), [database]);
  const feedConsumption = useMemo(() => new FeedConsumptionService(database), [database]);
  const births = useMemo(() => new BirthService(database), [database]);
  const animalEdits = useMemo(() => new AnimalEditService(database), [database]);
  // 3.5a.7 task 6 (already delivered server-side): best-effort read of the lot record.
  // Never on the path of a registration — LotEventsScreen never touches this (Art. 9).
  const animalGroupsApi = useMemo(
    () => new HttpAnimalGroupsApi({ baseUrl: API_BASE_URL, getToken: () => auth.token() }),
    [auth],
  );

  const [ready, setReady] = useState(false);
  const [authenticated, setAuthenticated] = useState(false);
  const [hasCachedSession, setHasCachedSession] = useState(false);
  const [tab, setTab] = useState<Tab>('home');
  const [pending, setPending] = useState(0);
  const [herd, setHerd] = useState<Awaited<ReturnType<typeof loadHerd>>>([]);
  const [groups, setGroups] = useState<Awaited<ReturnType<typeof loadGroups>>>([]);
  const [treatmentProducts, setTreatmentProducts] = useState<Awaited<ReturnType<typeof loadTreatmentProducts>>>([]);
  const [medications, setMedications] = useState<Awaited<ReturnType<typeof loadMedications>>>([]);
  const [mortalityCauses, setMortalityCauses] = useState<Awaited<ReturnType<typeof loadMortalityCauses>>>([]);
  const [feedItems, setFeedItems] = useState<Awaited<ReturnType<typeof loadFeedItems>>>([]);
  const [pregnantDams, setPregnantDams] = useState<PregnantDam[]>([]);
  // ADR-0019: the production module is on by default; the pull flips it off for the
  // pig pilot. ModuleVisibility answers from the local DB with no network, so this is
  // offline-safe by construction.
  const [productionOn, setProductionOn] = useState(true);
  // 3.5a.9-B: state that lives at the App level so the activity tree can lean on it.
  // selectedAnimalId is set when the operator picks an animal in the picker screen;
  // todayEntries drives "Lo que registré hoy". eventsInitialAnimalId and
  // eventsInitialActivity let the activity tree (3.5a.9-B) skip both the animal
  // picker and the activity menu when entering EventsScreen.
  const [selectedAnimalId, setSelectedAnimalId] = useState<string | null>(null);
  const [eventsInitialAnimalId, setEventsInitialAnimalId] = useState<string | undefined>(undefined);
  const [eventsInitialActivity, setEventsInitialActivity] = useState<'weight' | 'move' | 'disposal' | undefined>(undefined);
  // 3.5a.7: same shape as the animal-subject state above, for the "Un lote" branch.
  const [selectedGroupId, setSelectedGroupId] = useState<string | null>(null);
  const [lotEventsGroupId, setLotEventsGroupId] = useState<string | undefined>(undefined);
  const [lotEventsActivity, setLotEventsActivity] = useState<LotActivity | undefined>(undefined);
  const [todayEntries, setTodayEntries] = useState<
    {
      clientOperationId: string;
      operationType: string;
      occurredAt: string;
      status: 'pending' | 'synced' | 'rejected' | 'cancelled';
      resultRef?: string;
    }[]
  >([]);

  const visibility = useMemo(() => new ModuleVisibility(database, api), [database, api]);

  const refresh = useCallback(async () => {
    const [nextHerd, nextGroups, nextTreatmentProducts, nextMedications, nextMortalityCauses, nextFeedItems, nextPregnantDams, stats, productionVisible, today] = await Promise.all([
      loadHerd(database),
      loadGroups(database),
      loadTreatmentProducts(database),
      loadMedications(database),
      loadMortalityCauses(database),
      loadFeedItems(database),
      loadPregnantDams(database),
      outbox.stats(),
      visibility.canShow('production'),
      outbox.today(),
    ]);

    setHerd(nextHerd);
    setGroups(nextGroups);
    setTreatmentProducts(nextTreatmentProducts);
    setMedications(nextMedications);
    setMortalityCauses(nextMortalityCauses);
    setFeedItems(nextFeedItems);
    setPregnantDams(nextPregnantDams);
    setPending(stats.pending);
    setProductionOn(productionVisible);
    setTodayEntries(
      today.map((entry) => ({
        clientOperationId: entry.clientOperationId,
        operationType: entry.operationType,
        occurredAt: entry.occurredAt,
        status: entry.status as 'pending' | 'synced' | 'rejected' | 'cancelled',
        resultRef: entry.resultRef,
      })),
    );
  }, [database, outbox, visibility]);

  useEffect(() => {
    void (async () => {
      const cached = await auth.restore();
      setHasCachedSession(Boolean(cached?.pinHash));
      await refresh();
      setReady(true);
    })();
  }, [auth, refresh]);

  useEffect(() => {
    if (!authenticated) return undefined;

    // Opportunistic sync: fires as soon as the phone finds signal again.
    engine.start();

    const unsubscribe = engine.subscribe((result) => {
      if (result.pulled > 0 || result.pushed > 0 || result.rejected > 0) {
        void refresh();
      }
    });

    void engine.syncNow().then(refresh);

    return () => {
      unsubscribe();
      engine.stop();
    };
  }, [authenticated, engine, refresh]);

  if (!ready) {
    return (
      <SafeAreaView style={styles.root}>
        <StatusBar barStyle="light-content" backgroundColor={theme.color.background} translucent={false} />
        <Screen>
          <Title>HATO</Title>
          <Body muted>Abriendo la base local…</Body>
        </Screen>
      </SafeAreaView>
    );
  }

  if (!authenticated) {
    return (
      <SafeAreaView style={styles.root}>
        <StatusBar barStyle="light-content" backgroundColor={theme.color.background} translucent={false} />
        <LoginScreen
          auth={auth}
          hasCachedSession={hasCachedSession}
          onAuthenticated={() => setAuthenticated(true)}
        />
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={styles.root}>
      <StatusBar barStyle="light-content" backgroundColor={theme.color.background} translucent={false} />

      <View style={styles.content}>
        {tab === 'home' ? (
          <ActivitiesHub
            pending={pending}
            onSelect={(route) => {
              // ActivitiesHub's routes map 1:1 to the existing TabKey vocabulary, with
              // the two new subjects (animal-subject, today) declared in navigation.ts.
              // Module visibility continues to gate MilkingScreen at the render level,
              // not here: the route exists in the nav vocabulary regardless of state.
              setTab(route as Tab);
              if (route !== 'animal-subject') {
                setSelectedAnimalId(null);
              }
              // Pre-selection is one-shot. As soon as the operator leaves the events
              // tab for any reason, the next arrival there should be a clean slate.
              if (route !== 'events') {
                setEventsInitialAnimalId(undefined);
                setEventsInitialActivity(undefined);
              }
              // 'lot-events' is never a hub route (ActivitiesHub only ever emits
              // 'lot-subject'; the picker's own onActivity is what advances to
              // 'lot-events'), so any hub selection other than 'lot-subject' means the
              // operator left the lot branch and the pre-selection should reset.
              if (route !== 'lot-subject') {
                setSelectedGroupId(null);
                setLotEventsGroupId(undefined);
                setLotEventsActivity(undefined);
              }
            }}
          />
        ) : null}

        {tab === 'animal-subject' ? (
          <AnimalSubjectScreen
            animals={herd.map((member) => ({ animalId: member.animalId, label: member.label }))}
            recentIds={[]}
            selectedAnimalId={selectedAnimalId ?? undefined}
            onSelectAnimal={(animalId) => setSelectedAnimalId(animalId)}
            onClearSelection={() => setSelectedAnimalId(null)}
            onActivity={(animalId, activity) => {
              // weight/move/disposal route through EventsScreen with both the animal
              // and the activity pre-selected. Plan: the operator has already chosen
              // subject + animal + activity on the activity tree; the form they reach
              // here is the same one they would have reached by tapping through the
              // menu — fewer steps, no behaviour change. 'disposal' joined this path in
              // 3.5a.3 once the mortality causes catalog existed to back it.
              //
              // 'treatment' is the one activity AnimalSubjectScreen still offers that
              // moved out of EventsScreen in 3.5a.2-C: it now opens TreatScreen
              // directly. TreatScreen owns its own animal picker (it needs to, to
              // keep its four-tap budget testable in isolation), so the
              // pre-selection is not threaded through here — a known, accepted
              // UX gap tracked in BACKLOG.md, not a broken flow.
              setSelectedAnimalId(animalId);
              if (activity === 'treatment') {
                setTab('treat');
                return;
              }
              setEventsInitialAnimalId(animalId);
              setEventsInitialActivity(activity);
              setTab('events');
            }}
          />
        ) : null}

        {tab === 'lot-subject' ? (
          <LotSubjectScreen
            lots={groups.map((group) => ({ groupId: group.groupId, label: group.label }))}
            selectedGroupId={selectedGroupId ?? undefined}
            onSelectLot={(groupId) => setSelectedGroupId(groupId)}
            onClearSelection={() => setSelectedGroupId(null)}
            onActivity={(groupId, activity) => {
              // Same shape as the animal-subject → EventsScreen handoff: the operator
              // already chose subject + lot + activity, so the destination form opens
              // with both pre-selected.
              setSelectedGroupId(groupId);
              setLotEventsGroupId(groupId);
              setLotEventsActivity(activity);
              setTab('lot-events');
            }}
            animalGroupsApi={animalGroupsApi}
          />
        ) : null}

        {tab === 'lot-events' && lotEventsGroupId && lotEventsActivity ? (
          <LotEventsScreen
            service={events}
            feedService={feedConsumption}
            database={database}
            lots={groups.map((group) => ({
              groupId: group.groupId,
              label: group.label,
              speciesId: group.speciesId ?? undefined,
            }))}
            feedItems={feedItems}
            medications={medications}
            mortalityCauses={mortalityCauses}
            groupId={lotEventsGroupId}
            activity={lotEventsActivity}
            onRecorded={refresh}
            onBack={() => setTab('lot-subject')}
          />
        ) : null}

        {tab === 'today' ? (
          <TodayScreen
            entries={todayEntries}
            outbox={outbox}
            events={events}
            onChanged={() => void refresh()}
          />
        ) : null}

        {/*
          Defense in depth: ActivitiesHub hides the entry, but if `tab === 'milking'` ever
          ended up set while the module was off — a stale state across a sign-out, a
          deep-link we have not built yet — we still do not render the screen. The data
          path stays open (OutboxService is independent of this branch).
        */}
        {tab === 'milking' && productionOn ? (
          <MilkingScreen
            service={milking}
            database={database}
            candidates={herd}
            recordedBy={auth.currentSession()?.email ?? 'field-app'}
            onRecorded={refresh}
          />
        ) : null}

        {tab === 'events' ? (
          <EventsScreen
            service={events}
            database={database}
            animals={herd}
            groups={groups}
            mortalityCauses={mortalityCauses}
            onRecorded={refresh}
            initialAnimalId={eventsInitialAnimalId}
            initialActivity={eventsInitialActivity}
          />
        ) : null}

        {tab === 'vaccinate' ? (
          <VaccinateScreen
            service={events}
            database={database}
            animals={herd}
            products={treatmentProducts}
            onRecorded={refresh}
            onCancel={() => setTab('home')}
          />
        ) : null}

        {tab === 'treat' ? (
          <TreatScreen
            service={events}
            database={database}
            animals={herd}
            products={treatmentProducts}
            onRecorded={refresh}
            onCancel={() => setTab('home')}
          />
        ) : null}

        {tab === 'birth' ? (
          <BirthScreen
            service={births}
            dams={pregnantDams}
            onRecorded={() => {
              refresh();
              setTab('home');
            }}
            onCancel={() => setTab('home')}
          />
        ) : null}

        {tab === 'editAnimal' ? (
          <AnimalEditScreen database={database} service={animalEdits} animals={herd} onQueued={refresh} />
        ) : null}

        {tab === 'sync' ? (
          <SyncStatusScreen
            engine={engine}
            outbox={outbox}
            visibility={visibility}
            onModulesChanged={refresh}
          />
        ) : null}
      </View>

      {tab !== 'home' ? (
        <View style={styles.footer}>
          <BigButton
            testID="go-home"
            label="Inicio"
            tone="neutral"
            onPress={() => {
              setTab('home');
              setSelectedAnimalId(null);
              setEventsInitialAnimalId(undefined);
              setEventsInitialActivity(undefined);
              setSelectedGroupId(null);
              setLotEventsGroupId(undefined);
              setLotEventsActivity(undefined);
            }}
          />
        </View>
      ) : null}
    </SafeAreaView>
  );
}

const ANDROID_NAV_BAR_PADDING = 48;

const styles = StyleSheet.create({
  root: {
    flex: 1,
    backgroundColor: theme.color.background,
  },
  content: {
    flex: 1,
  },
  /**
   * Holds the "Inicio" / "Volver" button. On Android the system navigation bar (back /
   * home / recent) overlays the bottom edge of the app on edge-to-edge devices, which on
   * SDK 56 means a button placed at the very bottom is half-hidden behind the buttons.
   * Adding `ANDROID_NAV_BAR_PADDING` on Android only keeps the button legible without
   * paying that cost on iOS (where there is no system bar to dodge).
   */
  footer: {
    paddingHorizontal: theme.space.md,
    paddingTop: theme.space.md,
    paddingBottom: Platform.select({
      ios: theme.space.md,
      android: theme.space.md + ANDROID_NAV_BAR_PADDING,
      default: theme.space.md,
    }),
  },
});
