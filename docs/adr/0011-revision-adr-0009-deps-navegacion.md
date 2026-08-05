# ADR-0011 — Revisión del ADR-0009: reducir la lista aprobada de dependencias a lo que el código realmente usa

- **Estado:** Propuesto
- **Fecha:** 2026-08-05
- **Fase del roadmap:** Fase 3 (App de campo / Sincronización)

## Contexto

El ADR-0009 aprobó formalmente la siguiente sublista de dependencias para `clients/field-app`, agrupadas bajo "Navegación & UI":

> `@react-navigation/native`, `@react-navigation/stack`, `@react-navigation/bottom-tabs`, `react-native-screens`, `react-native-safe-area-context`, `react-native-gesture-handler`, `react-native-reanimated`, `@expo/vector-icons`

Al hacer una auditoría de uso real, se buscó cada uno de estos paquetes en `clients/field-app/src/**/*.{ts,tsx,js}` con `grep`:

- **`@react-navigation/native`**: 0 imports.
- **`@react-navigation/stack`**: 0 imports.
- **`react-navigation/bottom-tabs`**: 0 imports (no aparece siquiera en `package.json`).
- **`react-native-screens`**: 0 imports.
- **`react-native-safe-area-context`**: 0 imports.
- **`react-native-gesture-handler`**: 0 imports.
- **`react-native-reanimated`**: 0 imports.
- **`@expo/vector-icons`**: 0 imports en `src/**` (sí aparece como dependencia transitiva vía `expo`, pero no se importa directamente).

La auditoría es consistente con la decisión vigente y documentada en `clients/field-app/src/App.tsx:28-34`:

> *"Navigation is a plain piece of state rather than a router: the app has five destinations, all one tap from the home screen, and every extra layer between a gloved thumb and a record is a layer that can go wrong at 5 AM."*

El código define `type Tab = 'home' | 'milking' | 'events' | 'birth' | 'editAnimal' | 'sync'` y navega con `useState<Tab>('home')` (líneas 26 y 59). Las pruebas + el conjunto de pantallas validadas manualmente en PRs previos usan este patrón sin router.

El ADR-0009 está vigente para el resto de su contenido: WatermelonDB, `expo-secure-store`, `expo-crypto`, `expo-dev-client`, `expo-status-bar`, `@nozbe/with-observables`. **No** se reabre el stack, solo la sublista de navegación.

## Decisión

1. **Eliminar del `package.json` de `clients/field-app`** las siguientes dependencias, hoy presentes y hoy sin uso:
   - `@react-navigation/native` (`^6.1.18`)
   - `@react-navigation/stack` (`^6.4.1`)
   - `react-native-screens` (`3.31.1`)
   - `react-native-safe-area-context` (`4.10.5`)
   - `react-native-gesture-handler` (`~2.16.1`)

2. **Dejar sin tocar** el resto de la lista aprobada por el ADR-0009:
   - Core móvil (`react`, `react-native`, `expo`, `expo-dev-client`, `expo-status-bar`, `expo-crypto`, `expo-secure-store`).
   - Persistencia local (`@nozbe/watermelondb`, `@nozbe/with-observables`).
   - NetInfo (`@react-native-community/netinfo`) usado por el `SyncEngine`.
   - Testing & Tooling (`typescript`, `jest`, `@testing-library/react-native`, `@types/react`).

3. **Marcar el ADR-0009 como "superseded en esta sección específica"**: el ADR-0009 sigue vigente para el resto de su contenido; la sublista "Navegación & UI" queda reemplazada por "no hay librería de navegación en v1; véase `App.tsx` para el patrón vigente".

4. **No pre-instalar** librerías de navegación para Fase 4 (Adjuntos) o Fase 5 (Transformación). Si en una fase futura surge navegación drill-down que `useState<Tab>` no pueda manejar sin proliferación inmanejable de tipos (p. ej., una pila de pantallas de detalle de animal con sub-pantallas de historia clínica), se reabre con un **ADR nuevo** que justifique la necesidad concreta. No se anticipa por instinto.

## Alternativas consideradas

- **Mantener los paquetes "por si acaso"**: descartada. El ADR-0005 (offline-first) y `App.tsx:28-34` ya documentan la política vigente; tener instalados cinco paquetes que contradicen esa política es deuda que paga el siguiente lector. Además, cada build EAS paga el costo de enlazarlos aunque nadie los importe (ver Consecuencias).
- **Reemplazar `useState<Tab>` por un router minimalista casero**: descartada. El estado actual funciona y tiene cinco valores; cualquier abstracción casera es el primer paso hacia una reimplementación defectuosa de `react-navigation` dos fases después.
- **Migrar directamente a `@react-navigation/native` ahora para "preparar" Fase 4**: descartada por Art. 9 del AGENTS ("No optimices prematuramente"). No sabemos qué forma tendrá la navegación de Fase 4; instalar `@react-navigation/native` y luego descubrir que necesitábamos otra cosa es peor que instalar lo correcto cuando llegue el caso real.
- **`expo-router` en lugar de `@react-navigation/*`**: ni se evalúa aquí. Si en una fase futura se necesitara un router, se compararía `expo-router` vs. `@react-navigation/*` con un ADR dedicado, no como decisión derivada de este.

## Consecuencias

- **Positivas**:
  + `npm install` inicial más rápido (cinco paquetes menos: ~segundos en local; minutos en CI limpio).
  + Builds EAS ya no enlazan los `.aar` / frameworks nativos asociados (`react-native-screens`, `react-native-gesture-handler`). Esto **conecta directamente con el OOM observado en builds EAS** (trabajadores con 4-6 GB de RAM) que el ADR-0012 también aborda: menos código nativo compilado = menos presión sobre el worker.
  + La "política de navegación de field-app" queda con una **única decisión escrita** entre el comentario de `App.tsx:28-34` y este ADR. Dos decisiones contradictorias (la sublista aprobada del ADR-0009 y el `useState<Tab>` vigente) generaban duda a cualquier revisor futuro.
  + `package.json` pasa a reflejar exactamente lo que el código usa. Lectura más rápida, menos superficie para `npm audit`.

- **Negativas / costos**:
  − Si `jest` o algún test indirecto carga los paquetes vía `transformIgnorePatterns` (raro pero posible), el test suite podría romperse tras el `uninstall`. **Mitigación**: correr `npm test` después de eliminar los paquetes. Si rompe, decidir caso por caso (la solución probable es eliminar el `import` indirecto, no revertir el ADR).
  − El ADR-0009 queda con una sección obsoleta marcada. **Mitigación**: al aceptar este ADR, editar ADR-0009 reemplazando la sublista "Navegación & UI" por una nota que apunte aquí y al comentario de `App.tsx`.

- **Condición de reversa**: Si en una fase futura (p. ej., Fase 4 con adjuntos fotográficos por animal, o Fase 5 con transformación / reporting) surge una necesidad concreta de navegación drill-down que `useState<Tab>` no pueda expresar sin proliferación inmanejable de tipos, se reabre con un ADR específico que justifique **qué librería**, **para qué pantallas**, y **por qué ahora**. Ese ADR nuevo deroga la política "sin router" solo en el alcance mínimo necesario.
