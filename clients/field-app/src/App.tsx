import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Platform, SafeAreaView, StatusBar, StyleSheet, View } from 'react-native';

import { createDatabase } from './database';
import { AnimalEditService } from './services/animalEditService';
import { AuthService } from './services/authService';
import { BirthService } from './services/birthService';
import { EventService } from './services/eventService';
import { HttpSyncApi } from './services/syncApi';
import { MilkingService } from './services/milkingService';
import { ModuleVisibility } from './services/moduleVisibility';
import { Outbox } from './services/outbox';
import { SyncEngine } from './services/syncEngine';
import { loadGroups, loadHerd, loadMedications } from './services/herdQueries';
import { ActivitiesHub } from './screens/ActivitiesHub';
import { AnimalEditScreen } from './screens/AnimalEditScreen';
import { AnimalSubjectScreen } from './screens/AnimalSubjectScreen';
import { BirthScreen } from './screens/BirthScreen';
import { EventsScreen } from './screens/EventsScreen';
import { HomeScreen } from './screens/HomeScreen';
import { LoginScreen } from './screens/LoginScreen';
import { MilkingScreen } from './screens/MilkingScreen';
import { SyncStatusScreen } from './screens/SyncStatusScreen';
import { TodayScreen } from './screens/TodayScreen';
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

  const engine = useMemo(() => {
    const api = new HttpSyncApi({
      baseUrl: API_BASE_URL,
      getToken: () => auth.token(),
      refreshToken: () => auth.refresh(),
      deviceId: DEVICE_ID,
    });

    return new SyncEngine(database, api);
  }, [auth, database]);

  const milking = useMemo(() => new MilkingService(database), [database]);
  const events = useMemo(() => new EventService(database), [database]);
  const births = useMemo(() => new BirthService(database), [database]);
  const animalEdits = useMemo(() => new AnimalEditService(database), [database]);

  const [ready, setReady] = useState(false);
  const [authenticated, setAuthenticated] = useState(false);
  const [hasCachedSession, setHasCachedSession] = useState(false);
  const [tab, setTab] = useState<Tab>('home');
  const [pending, setPending] = useState(0);
  const [herd, setHerd] = useState<Awaited<ReturnType<typeof loadHerd>>>([]);
  const [groups, setGroups] = useState<Awaited<ReturnType<typeof loadGroups>>>([]);
  const [medications, setMedications] = useState<Awaited<ReturnType<typeof loadMedications>>>([]);
  // ADR-0019: the production module is on by default; the pull flips it off for the
  // pig pilot. ModuleVisibility answers from the local DB with no network, so this is
  // offline-safe by construction.
  const [productionOn, setProductionOn] = useState(true);
  // 3.5a.9-B: state that lives at the App level so the activity tree can lean on it.
  // selectedAnimalId is set when the operator picks an animal in the picker screen;
  // todayEntries drives "Lo que registré hoy".
  const [selectedAnimalId, setSelectedAnimalId] = useState<string | null>(null);
  const [todayEntries, setTodayEntries] = useState<
    { clientOperationId: string; operationType: string; occurredAt: string; status: 'pending' | 'synced' | 'rejected' | 'cancelled' }[]
  >([]);

  const visibility = useMemo(() => new ModuleVisibility(database), [database]);

  const refresh = useCallback(async () => {
    const [nextHerd, nextGroups, nextMedications, stats, productionVisible, today] = await Promise.all([
      loadHerd(database),
      loadGroups(database),
      loadMedications(database),
      outbox.stats(),
      visibility.canShow('production'),
      outbox.today(),
    ]);

    setHerd(nextHerd);
    setGroups(nextGroups);
    setMedications(nextMedications);
    setPending(stats.pending);
    setProductionOn(productionVisible);
    setTodayEntries(
      today.map((entry) => ({
        clientOperationId: entry.clientOperationId,
        operationType: entry.operationType,
        occurredAt: entry.occurredAt,
        status: entry.status as 'pending' | 'synced' | 'rejected' | 'cancelled',
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
    void engine.syncNow().then(refresh);

    return () => engine.stop();
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
            onActivity={(animalId, _activity) => {
              // All animal activities for now route through EventsScreen. Pre-selection
              // by id will arrive when EventsScreen learns to accept an `initialAnimalId`
              // prop — for now the operator picks the animal once more from the picker,
              // which keeps the existing UI untouched.
              setSelectedAnimalId(animalId);
              setTab('events');
            }}
          />
        ) : null}

        {tab === 'today' ? (
          <TodayScreen
            entries={todayEntries}
            onSelectEntry={() => {
              // 3.5a.8 (corrections) is a separate concern; for now the tap is a no-op
              // so the row renders correctly and the contract with the future screen is
              // already in place.
            }}
          />
        ) : null}

        {/*
          Defense in depth: HomeScreen hides the entry, but if `tab === 'milking'` ever
          ended up set while the module was off — a stale state across a sign-out, a
          deep-link we have not built yet — we still do not render the screen. The data
          path stays open (OutboxService is independent of this branch).
        */}
        {tab === 'milking' && productionOn ? (
          <MilkingScreen
            service={milking}
            candidates={herd}
            recordedBy={auth.currentSession()?.email ?? 'field-app'}
            onRecorded={refresh}
          />
        ) : null}

        {tab === 'events' ? (
          <EventsScreen
            service={events}
            animals={herd}
            groups={groups}
            medications={medications}
            onRecorded={refresh}
          />
        ) : null}

        {tab === 'birth' ? (
          <BirthScreen
            service={births}
            dams={herd.filter((member) => member.sex === 'Female')}
            sires={herd.filter((member) => member.sex === 'Male')}
            onRecorded={refresh}
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
          <BigButton testID="go-home" label="Inicio" tone="neutral" onPress={() => setTab('home')} />
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
