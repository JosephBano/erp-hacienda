import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  BackHandler,
  Platform,
  Pressable,
  SafeAreaView,
  StatusBar,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';

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
  loadActiveHerd,
  loadMedications,
  loadMilkingCandidates,
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
import {
  CANONICAL_DESTINATIONS,
  resolveCanonicalTab,
  type CanonicalTab,
  type TabKey,
} from './screens/navigation';
import { BigButton, Body, Card, Notice, Screen, Title } from './ui/components';
import { DraftGuardProvider, useDraftGate } from './ui/draftGuard';
import { ThemeProvider, useTheme, theme } from './ui/theme';

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
function AppShell() {
  const { theme: activeTheme, setThemeMode, themeMode, isDark } = useTheme();
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
  const animalGroupsApi = useMemo(
    () => new HttpAnimalGroupsApi({ baseUrl: API_BASE_URL, getToken: () => auth.token() }),
    [auth],
  );

  const [ready, setReady] = useState(false);
  const [authenticated, setAuthenticated] = useState(false);
  const [hasCachedSession, setHasCachedSession] = useState(false);
  const [tab, setTab] = useState<Tab>('home');
  const [pending, setPending] = useState(0);
  const [showSettingsModal, setShowSettingsModal] = useState(false);
  const [herd, setHerd] = useState<Awaited<ReturnType<typeof loadHerd>>>([]);
  const [activeHerd, setActiveHerd] = useState<Awaited<ReturnType<typeof loadActiveHerd>>>([]);
  const [milkingCandidates, setMilkingCandidates] = useState<Awaited<ReturnType<typeof loadMilkingCandidates>>>([]);
  const [groups, setGroups] = useState<Awaited<ReturnType<typeof loadGroups>>>([]);
  const [treatmentProducts, setTreatmentProducts] = useState<Awaited<ReturnType<typeof loadTreatmentProducts>>>([]);
  const [medications, setMedications] = useState<Awaited<ReturnType<typeof loadMedications>>>([]);
  const [mortalityCauses, setMortalityCauses] = useState<Awaited<ReturnType<typeof loadMortalityCauses>>>([]);
  const [feedItems, setFeedItems] = useState<Awaited<ReturnType<typeof loadFeedItems>>>([]);
  const [pregnantDams, setPregnantDams] = useState<PregnantDam[]>([]);
  const [productionOn, setProductionOn] = useState(true);
  const [selectedAnimalId, setSelectedAnimalId] = useState<string | null>(null);
  const [eventsInitialAnimalId, setEventsInitialAnimalId] = useState<string | undefined>(undefined);
  const [eventsInitialActivity, setEventsInitialActivity] = useState<'weight' | 'move' | 'disposal' | undefined>(undefined);
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
  const gate = useDraftGate();

  const goHome = useCallback(() => {
    setTab('home');
    setSelectedAnimalId(null);
    setEventsInitialAnimalId(undefined);
    setEventsInitialActivity(undefined);
    setSelectedGroupId(null);
    setLotEventsGroupId(undefined);
    setLotEventsActivity(undefined);
  }, []);

  const navigateToCanonical = useCallback(
    (destKey: CanonicalTab) => {
      gate.request(() => {
        if (destKey === 'home') {
          goHome();
        } else if (destKey === 'animals') {
          setTab('animals');
          setSelectedAnimalId(null);
          setEventsInitialAnimalId(undefined);
          setEventsInitialActivity(undefined);
        } else if (destKey === 'lots') {
          setTab('lots');
          setSelectedGroupId(null);
          setLotEventsGroupId(undefined);
          setLotEventsActivity(undefined);
        } else if (destKey === 'activity') {
          setTab('activity');
        }
      });
    },
    [gate, goHome],
  );

  const refresh = useCallback(async () => {
    const [
      nextHerd,
      nextActiveHerd,
      nextMilkingCandidates,
      nextGroups,
      nextTreatmentProducts,
      nextMedications,
      nextMortalityCauses,
      nextFeedItems,
      nextPregnantDams,
      stats,
      productionVisible,
      today,
    ] = await Promise.all([
      loadHerd(database),
      loadActiveHerd(database),
      loadMilkingCandidates(database),
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
    setActiveHerd(nextActiveHerd);
    setMilkingCandidates(nextMilkingCandidates);
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

    engine.start();

    const unsubscribe = engine.subscribe?.((result) => {
      if (result.pulled > 0 || result.pushed > 0 || result.rejected > 0) {
        void refresh();
      }
    });

    void engine.syncNow().then(refresh);

    return () => {
      unsubscribe?.();
      engine.stop();
    };
  }, [authenticated, engine, refresh]);

  /**
   * Android's hardware back coherence (T2.6):
   * If the secondary settings modal is open, back dismisses it.
   * If a draft is dirty, the discard prompt asks first.
   * If on Animales, Lotes, Actividad, or secondary screens, back returns to Inicio.
   * Only on Inicio does back return false to let the OS close the app.
   */
  useEffect(() => {
    const onBack = () => {
      if (showSettingsModal) {
        setShowSettingsModal(false);
        return true;
      }
      if (gate.isAsking) {
        gate.keep();
        return true;
      }
      if (tab === 'home') return false;
      gate.request(goHome);
      return true;
    };

    const subscription = BackHandler.addEventListener('hardwareBackPress', onBack);
    return () => subscription.remove();
  }, [gate, goHome, showSettingsModal, tab]);

  if (!ready) {
    return (
      <SafeAreaView testID="app-root" style={[styles.root, { backgroundColor: activeTheme.color.background }]}>
        <StatusBar barStyle={isDark ? 'light-content' : 'dark-content'} backgroundColor={activeTheme.color.background} translucent={false} />
        <Screen>
          <Title>HATO</Title>
          <Body muted>Abriendo la base local…</Body>
        </Screen>
      </SafeAreaView>
    );
  }

  if (!authenticated) {
    return (
      <SafeAreaView testID="app-root" style={[styles.root, { backgroundColor: activeTheme.color.background }]}>
        <StatusBar barStyle={isDark ? 'light-content' : 'dark-content'} backgroundColor={activeTheme.color.background} translucent={false} />
        <LoginScreen
          auth={auth}
          hasCachedSession={hasCachedSession}
          onAuthenticated={() => setAuthenticated(true)}
        />
      </SafeAreaView>
    );
  }

  const activeCanonicalTab = resolveCanonicalTab(tab);

  return (
    <SafeAreaView testID="app-root" style={[styles.root, { backgroundColor: activeTheme.color.background }]}>
      <StatusBar barStyle={isDark ? 'light-content' : 'dark-content'} backgroundColor={activeTheme.color.background} translucent={false} />

      {/* Global Header — always accessible sync state & secondary settings */}
      <View
        testID="global-header"
        style={[
          styles.header,
          {
            backgroundColor: activeTheme.color.surface,
            borderBottomColor: activeTheme.color.border,
          },
        ]}
      >
        <View style={styles.headerBrand}>
          <Text style={[styles.headerTitle, { color: activeTheme.color.text }]}>HATO</Text>
          <Text style={[styles.headerSubtitle, { color: activeTheme.color.textMuted }]}>Móvil</Text>
        </View>
        <View style={styles.headerActions}>
          <TouchableOpacity
            testID="global-sync-pill"
            accessibilityRole="button"
            accessibilityLabel={`Sincronización: ${pending === 0 ? 'Al día' : `${pending} por enviar`}`}
            style={[
              styles.syncPill,
              {
                backgroundColor: pending > 0 ? activeTheme.color.warning : activeTheme.color.surfaceRaised,
                borderColor: pending > 0 ? activeTheme.color.warning : activeTheme.color.primary,
              },
            ]}
            onPress={() => {
              gate.request(() => {
                setTab('sync');
              });
            }}
          >
            <Text
              testID="global-sync-pill-text"
              style={[
                styles.syncPillText,
                {
                  color: pending > 0 ? activeTheme.color.warningText : activeTheme.color.primary,
                },
              ]}
            >
              {pending === 0 ? '✓ Al día' : `● ${pending} por enviar`}
            </Text>
          </TouchableOpacity>

          <TouchableOpacity
            testID="header-settings-button"
            accessibilityRole="button"
            accessibilityLabel="Ajustes y sesión"
            style={[
              styles.settingsButton,
              {
                backgroundColor: activeTheme.color.surfaceRaised,
                borderColor: activeTheme.color.border,
              },
            ]}
            onPress={() => setShowSettingsModal(true)}
          >
            <Text style={[styles.settingsButtonText, { color: activeTheme.color.text }]}>⚙</Text>
          </TouchableOpacity>
        </View>
      </View>

      <DraftGuardProvider onDirtyChange={gate.markDirty}>
        <View style={styles.content}>
          {tab === 'home' ? (
            <ActivitiesHub
              pending={pending}
              productionOn={productionOn}
              permissions={auth.currentSession()?.permissions}
              recentEntries={todayEntries.slice(0, 5)}
              onSelect={(route) => {
                setTab(route as Tab);
                if (route !== 'animal-subject' && route !== 'animals') {
                  setSelectedAnimalId(null);
                }
                if (route !== 'events') {
                  setEventsInitialAnimalId(undefined);
                  setEventsInitialActivity(undefined);
                }
                if (route !== 'lot-subject' && route !== 'lots') {
                  setSelectedGroupId(null);
                  setLotEventsGroupId(undefined);
                  setLotEventsActivity(undefined);
                }
              }}
            />
          ) : null}

          {tab === 'animal-subject' || tab === 'animals' ? (
            <AnimalSubjectScreen
              animals={herd.map((member) => {
                const pregnant = pregnantDams.find((p) => p.animalId === member.animalId);
                return {
                  animalId: member.animalId,
                  label: member.label,
                  sex: member.sex,
                  groupName: member.groupName,
                  tag: member.tag,
                  activeIdentifiers: member.activeIdentifiers,
                  historicalIdentifiers: member.historicalIdentifiers,
                  name: member.name,
                  hasPendingTag: member.hasPendingTag,
                  disposedAt: member.disposedAt,
                  isWithheld: member.isWithheld,
                  withheldUntil: member.withheldUntil,
                  isPregnant: Boolean(pregnant),
                  expectedBirthDate: pregnant?.expectedBirthDate,
                };
              })}
              recentIds={[]}
              selectedAnimalId={selectedAnimalId ?? undefined}
              onSelectAnimal={(animalId) => setSelectedAnimalId(animalId)}
              onClearSelection={() => setSelectedAnimalId(null)}
              onActivity={(animalId, activity) => {
                setSelectedAnimalId(animalId);
                if (activity === 'treatment') {
                  setTab('treat');
                  return;
                }
                if (activity === 'birth') {
                  setTab('birth');
                  return;
                }
                setEventsInitialAnimalId(animalId);
                setEventsInitialActivity(activity as any);
                setTab('events');
              }}
              outbox={outbox}
              database={database}
              groups={groups}
              permissions={auth.currentSession()?.permissions}
            />
          ) : null}

          {tab === 'lot-subject' || tab === 'lots' ? (
            <LotSubjectScreen
              lots={groups.map((group) => ({
                groupId: group.groupId,
                label: group.label,
                trackingMode: group.trackingMode,
                speciesId: group.speciesId,
              }))}
              selectedGroupId={selectedGroupId ?? undefined}
              onSelectLot={(groupId) => setSelectedGroupId(groupId)}
              onClearSelection={() => setSelectedGroupId(null)}
              onActivity={(groupId, activity) => {
                setSelectedGroupId(groupId);
                setLotEventsGroupId(groupId);
                setLotEventsActivity(activity);
                setTab('lot-events');
              }}
              animalGroupsApi={animalGroupsApi}
              animals={activeHerd}
              permissions={auth.currentSession()?.permissions}
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
              onBack={() => setTab('lots')}
            />
          ) : null}

          {tab === 'today' || tab === 'activity' ? (
            <TodayScreen
              entries={todayEntries}
              outbox={outbox}
              events={events}
              onChanged={() => void refresh()}
            />
          ) : null}

          {tab === 'milking' && productionOn ? (
            <MilkingScreen
              service={milking}
              database={database}
              candidates={milkingCandidates}
              recordedBy={auth.currentSession()?.email ?? 'field-app'}
              onRecorded={refresh}
            />
          ) : null}

          {tab === 'events' ? (
            <EventsScreen
              service={events}
              database={database}
              animals={activeHerd}
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
              animals={activeHerd}
              products={treatmentProducts}
              onRecorded={refresh}
              onCancel={() => setTab('home')}
            />
          ) : null}

          {tab === 'treat' ? (
            <TreatScreen
              service={events}
              database={database}
              animals={activeHerd}
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
      </DraftGuardProvider>

      {/* Discard prompt when dirty; return to Inicio button when on other screens */}
      {gate.isAsking ? (
        <View testID="app-discard-prompt" style={styles.footer}>
          <Card>
            <Notice tone="warning" text="Hay datos escritos sin registrar en esta pantalla." />
            <BigButton
              testID="keep-editing"
              label="Seguir aquí"
              onPress={gate.keep}
            />
            <BigButton
              testID="discard-draft"
              label="Salir y descartar"
              tone="danger"
              onPress={gate.discard}
            />
          </Card>
        </View>
      ) : tab !== 'home' ? (
        <View testID="app-footer" style={styles.footer}>
          <BigButton
            testID="go-home"
            label="Inicio"
            tone="neutral"
            onPress={() => gate.request(goHome)}
          />
        </View>
      ) : null}

      {/* Fixed bottom navigation bar with 4 canonical destinations */}
      {!gate.isAsking && (
        <View
          testID="bottom-nav"
          style={[
            styles.bottomNav,
            {
              backgroundColor: activeTheme.color.surface,
              borderTopColor: activeTheme.color.border,
            },
          ]}
        >
          {CANONICAL_DESTINATIONS.map((dest) => {
            const isSelected = activeCanonicalTab === dest.key;
            return (
              <TouchableOpacity
                key={dest.key}
                testID={dest.testID}
                accessibilityRole="tab"
                accessibilityLabel={dest.label}
                accessibilityState={{ selected: isSelected }}
                style={[
                  styles.navTab,
                  isSelected && { backgroundColor: activeTheme.color.surfaceRaised },
                ]}
                onPress={() => navigateToCanonical(dest.key)}
              >
                <Text style={styles.navSymbol}>{dest.symbol}</Text>
                <Text
                  style={[
                    styles.navLabel,
                    {
                      color: isSelected ? activeTheme.color.primary : activeTheme.color.textMuted,
                      fontWeight: isSelected ? '700' : '500',
                    },
                  ]}
                >
                  {dest.label}
                </Text>
              </TouchableOpacity>
            );
          })}
        </View>
      )}

      {/* Secondary settings and session modal */}
      {showSettingsModal && (
        <View testID="settings-modal" style={styles.settingsOverlay}>
          <Pressable
            testID="settings-backdrop"
            style={styles.settingsBackdrop}
            onPress={() => setShowSettingsModal(false)}
          />
          <View
            style={[
              styles.settingsCard,
              {
                backgroundColor: activeTheme.color.surface,
                borderColor: activeTheme.color.border,
              },
            ]}
          >
            <Title>Ajustes y sesión</Title>

            <View testID="settings-user-info" style={styles.settingsSection}>
              <Body muted>Operador activo</Body>
              <Body>{auth.currentSession()?.fullName ?? auth.currentSession()?.email ?? 'Operador de campo'}</Body>
              {auth.currentSession()?.email ? (
                <Body muted>{auth.currentSession()?.email}</Body>
              ) : null}
            </View>

            <View style={styles.settingsSection}>
              <Body muted>Tema de pantalla</Body>
              <View style={styles.themeButtonsRow}>
                <TouchableOpacity
                  testID="theme-mode-system"
                  accessibilityRole="button"
                  style={[
                    styles.themeOptionButton,
                    {
                      borderColor: activeTheme.color.border,
                      backgroundColor: themeMode === 'system' ? activeTheme.color.primary : activeTheme.color.surfaceRaised,
                    },
                  ]}
                  onPress={() => void setThemeMode('system')}
                >
                  <Text
                    style={[
                      styles.themeOptionText,
                      { color: themeMode === 'system' ? activeTheme.color.primaryText : activeTheme.color.text },
                    ]}
                  >
                    Automático
                  </Text>
                </TouchableOpacity>

                <TouchableOpacity
                  testID="theme-mode-light"
                  accessibilityRole="button"
                  style={[
                    styles.themeOptionButton,
                    {
                      borderColor: activeTheme.color.border,
                      backgroundColor: themeMode === 'light' ? activeTheme.color.primary : activeTheme.color.surfaceRaised,
                    },
                  ]}
                  onPress={() => void setThemeMode('light')}
                >
                  <Text
                    style={[
                      styles.themeOptionText,
                      { color: themeMode === 'light' ? activeTheme.color.primaryText : activeTheme.color.text },
                    ]}
                  >
                    Claro
                  </Text>
                </TouchableOpacity>

                <TouchableOpacity
                  testID="theme-mode-dark"
                  accessibilityRole="button"
                  style={[
                    styles.themeOptionButton,
                    {
                      borderColor: activeTheme.color.border,
                      backgroundColor: themeMode === 'dark' ? activeTheme.color.primary : activeTheme.color.surfaceRaised,
                    },
                  ]}
                  onPress={() => void setThemeMode('dark')}
                >
                  <Text
                    style={[
                      styles.themeOptionText,
                      { color: themeMode === 'dark' ? activeTheme.color.primaryText : activeTheme.color.text },
                    ]}
                  >
                    Oscuro
                  </Text>
                </TouchableOpacity>
              </View>
            </View>

            <BigButton
              testID="settings-sign-out"
              label="Cerrar sesión"
              tone="danger"
              onPress={async () => {
                setShowSettingsModal(false);
                await auth.logout();
                setAuthenticated(false);
                setHasCachedSession(false);
                setTab('home');
              }}
            />

            <BigButton
              testID="settings-close-button"
              label="Cerrar"
              tone="neutral"
              onPress={() => setShowSettingsModal(false)}
            />
          </View>
        </View>
      )}
    </SafeAreaView>
  );
}

export default function App() {
  return (
    <ThemeProvider>
      <AppShell />
    </ThemeProvider>
  );
}

const ANDROID_NAV_BAR_PADDING = 48;

const styles = StyleSheet.create({
  /**
   * Every branch of this component sits in `root`, and that is where the system
   * navigation bar has to be dodged. On Android (back / home / recent, drawn over the
   * bottom edge of the window on edge-to-edge devices) `SafeAreaView` from react-native
   * does nothing at all — it only applies insets on iOS — so the app reserves the strip
   * itself with `ANDROID_NAV_BAR_PADDING`, and pays nothing on iOS, where there is no
   * system bar to dodge.
   *
   * It used to live on the footer instead, which hid the defect the operators reported:
   * the home screen renders no footer, so the hub's last button ("Sincronización") sat
   * under the system buttons on a short screen. Reserving the strip at the root covers
   * every screen, footer or not.
   */
  root: {
    flex: 1,
    backgroundColor: theme.color.background,
    paddingBottom: Platform.select({
      ios: 0,
      android: ANDROID_NAV_BAR_PADDING,
      default: 0,
    }),
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: theme.space.md,
    paddingVertical: theme.space.sm,
    borderBottomWidth: 1,
    minHeight: 56,
  },
  headerBrand: {
    flexDirection: 'row',
    alignItems: 'baseline',
    gap: theme.space.xs,
  },
  headerTitle: {
    fontSize: theme.font.title - 4,
    fontWeight: '800',
    letterSpacing: 0.5,
  },
  headerSubtitle: {
    fontSize: theme.font.micro,
    fontWeight: '600',
  },
  headerActions: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: theme.space.sm,
  },
  syncPill: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: theme.space.sm + 2,
    paddingVertical: theme.space.xs + 2,
    borderRadius: theme.radius.lg,
    borderWidth: 1,
    minHeight: 36,
  },
  syncPillText: {
    fontSize: theme.font.micro,
    fontWeight: '700',
  },
  settingsButton: {
    width: 44,
    height: 44,
    borderRadius: theme.radius.md,
    borderWidth: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  settingsButtonText: {
    fontSize: 20,
  },
  content: {
    flex: 1,
  },
  /** Holds the "Inicio" / "Volver" button. The system bar is already cleared by `root`. */
  footer: {
    paddingHorizontal: theme.space.md,
    paddingTop: theme.space.md,
    paddingBottom: theme.space.md,
  },
  bottomNav: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-around',
    borderTopWidth: 1,
    minHeight: theme.touchTarget,
    paddingHorizontal: theme.space.xs,
  },
  navTab: {
    flex: 1,
    minHeight: theme.touchTarget,
    justifyContent: 'center',
    alignItems: 'center',
    paddingVertical: theme.space.xs,
    borderRadius: theme.radius.md,
    marginHorizontal: 2,
  },
  navSymbol: {
    fontSize: 20,
    marginBottom: 2,
  },
  navLabel: {
    fontSize: theme.font.micro,
  },
  settingsOverlay: {
    ...StyleSheet.absoluteFill,
    zIndex: 999,
    justifyContent: 'center',
    alignItems: 'center',
    padding: theme.space.md,
  },
  settingsBackdrop: {
    ...StyleSheet.absoluteFill,
    backgroundColor: 'rgba(0, 0, 0, 0.65)',
  },
  settingsCard: {
    width: '100%',
    maxWidth: 400,
    borderRadius: theme.radius.lg,
    borderWidth: 1,
    padding: theme.space.lg,
    gap: theme.space.md,
    elevation: 8,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.3,
    shadowRadius: 8,
  },
  settingsSection: {
    gap: theme.space.xs,
    paddingVertical: theme.space.xs,
  },
  themeButtonsRow: {
    flexDirection: 'row',
    gap: theme.space.xs,
    marginTop: theme.space.xs,
  },
  themeOptionButton: {
    flex: 1,
    minHeight: 44,
    borderRadius: theme.radius.md,
    borderWidth: 1,
    justifyContent: 'center',
    alignItems: 'center',
    paddingHorizontal: theme.space.xs,
  },
  themeOptionText: {
    fontSize: theme.font.micro,
    fontWeight: '700',
  },
});
