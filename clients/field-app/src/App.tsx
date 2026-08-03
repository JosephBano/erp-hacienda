import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { SafeAreaView, StatusBar, StyleSheet, View } from 'react-native';

import { createDatabase } from './database';
import { AnimalEditService } from './services/animalEditService';
import { AuthService } from './services/authService';
import { BirthService } from './services/birthService';
import { EventService } from './services/eventService';
import { HttpSyncApi } from './services/syncApi';
import { MilkingService } from './services/milkingService';
import { Outbox } from './services/outbox';
import { SyncEngine } from './services/syncEngine';
import { loadGroups, loadHerd, loadMedications } from './services/herdQueries';
import { AnimalEditScreen } from './screens/AnimalEditScreen';
import { BirthScreen } from './screens/BirthScreen';
import { EventsScreen } from './screens/EventsScreen';
import { LoginScreen } from './screens/LoginScreen';
import { MilkingScreen } from './screens/MilkingScreen';
import { SyncStatusScreen } from './screens/SyncStatusScreen';
import { BigButton, Body, Screen, Title } from './ui/components';
import { theme } from './ui/theme';

const API_BASE_URL = process.env.EXPO_PUBLIC_API_URL ?? 'http://10.0.2.2:5000';
const DEVICE_ID = 'field-device';

type Tab = 'home' | 'milking' | 'events' | 'birth' | 'editAnimal' | 'sync';

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

  const refresh = useCallback(async () => {
    const [nextHerd, nextGroups, nextMedications, stats] = await Promise.all([
      loadHerd(database),
      loadGroups(database),
      loadMedications(database),
      outbox.stats(),
    ]);

    setHerd(nextHerd);
    setGroups(nextGroups);
    setMedications(nextMedications);
    setPending(stats.pending);
  }, [database, outbox]);

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
        <StatusBar barStyle="light-content" />
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
      <StatusBar barStyle="light-content" />

      <View style={styles.content}>
        {tab === 'home' ? (
          <Screen testID="home-screen">
            <Title>{`Hola, ${auth.currentSession()?.fullName ?? ''}`}</Title>
            <Body testID="home-pending">{`${pending} registro(s) sin enviar`}</Body>
            <BigButton testID="go-milking" label="Ordeño" onPress={() => setTab('milking')} />
            <BigButton testID="go-events" label="Eventos" tone="neutral" onPress={() => setTab('events')} />
            <BigButton testID="go-birth" label="Parto" tone="neutral" onPress={() => setTab('birth')} />
            <BigButton testID="go-edit-animal" label="Editar animal" tone="neutral" onPress={() => setTab('editAnimal')} />
            <BigButton testID="go-sync" label="Sincronización" tone="neutral" onPress={() => setTab('sync')} />
          </Screen>
        ) : null}

        {tab === 'milking' ? (
          <MilkingScreen service={milking} candidates={herd} onRecorded={refresh} />
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

        {tab === 'sync' ? <SyncStatusScreen engine={engine} outbox={outbox} /> : null}
      </View>

      {tab !== 'home' ? (
        <View style={styles.footer}>
          <BigButton testID="go-home" label="Inicio" tone="neutral" onPress={() => setTab('home')} />
        </View>
      ) : null}
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  root: {
    flex: 1,
    backgroundColor: theme.color.background,
  },
  content: {
    flex: 1,
  },
  footer: {
    padding: theme.space.md,
  },
});
