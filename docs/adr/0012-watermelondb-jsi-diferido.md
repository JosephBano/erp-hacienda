# ADR-0012 — WatermelonDB JSI diferido para field-app v1

- **Estado:** Propuesto
- **Fecha:** 2026-08-05
- **Fase del roadmap:** Fase 3 (App de campo / Sincronización)

## Contexto

`clients/field-app/src/database/index.ts:20` declara lo siguiente al construir el adaptador:

```ts
const adapter = new SQLiteAdapter({
  schema,
  migrations,
  jsi: true,         // <-- este flag
  onSetUpError: ...
});
```

El flag `jsi: true` está activo, pero su **efecto real** es nulo en la configuración actual del proyecto. Verificación punto por punto:

- No hay `@morrowdigital/watermelondb-expo-plugin` instalado ni en `package.json` ni como dependencia. **Búsqueda confirmada**: 0 ocurrencias del nombre del paquete en el repositorio.
- No hay `expo-build-properties` con `useJSI: true` configurado. 0 ocurrencias en el repositorio.
- No hay registro de `WatermelonDBJSIPackage` en `MainApplication`. 0 ocurrencias, y de hecho el proyecto es Expo dev-client (no hay carpeta `android/` propia; se genera con `expo prebuild`).
- `react-native.config.js` del proyecto (a verificar contra `node_modules/@nozbe/watermelondb/react-native.config.js` cuando se haga `npm install`; el comportamiento documentado del paquete `@nozbe/watermelondb` v0.27 es autoenlazar solo `./native/android` — puente async — y dejar `./native/android-jsi` **sin autoenlazar**). Eso significa que el módulo JSI no entra al build de Android aunque el flag diga `true`.
- Como consecuencia, el `makeDispatcher` de WatermelonDB (asunción basada en el comportamiento documentado de v0.27 a re-verificar contra `node_modules/@nozbe/watermelondb/adapters/sqlite/makeDispatcher/index.native.js` líneas ~124-132 al hacer `npm install`) emite el warning:

  > `"JSI SQLiteAdapter not available… falling back to asynchronous operation..."`

  y todas las llamadas a SQLite pasan por el puente async, igual que si `jsi` fuera `false`.

En resumen: **`jsi: true` es cosmético** en este proyecto. El código está mintiendo sobre su comportamiento.

A esto se suma el síntoma de las builds EAS Build (perfil `preview`), que mueren con "lost connection to the worker" (sospecha de OOM durante compilación NDK). Si en el futuro alguien activara el plugin JSI oficial, el build nativo de `sqlite3.c` + `simdjson.cpp` para los 4 ABIs estándar de Android agotaría la RAM del worker de EAS (los workers de EAS rondan los 4-6 GB; el ADR-0010 deja `resourceClass` en default hasta ver comportamiento real).

## Decisión

1. **`jsi: false`** en `clients/field-app/src/database/index.ts:20`. Cambio de una línea. Alinear el código con la realidad del build, que **ya corre async** desde el primer commit.

2. **No instalar** `@morrowdigital/watermelondb-expo-plugin` ni configurar `WatermelonDBJSIPackage` ni `expo-build-properties` con `useJSI` para esta versión. No se introduce código que active un comportamiento nativo sin haber medido antes su necesidad.

3. **JSI se reevaluará** cuando **una** de estas condiciones se cumpla (lo que ocurra primero):
   - **(a)** Hay **≥ 4 empleados concurrentes** en la finca usando la app diariamente, y la latencia del pull inicial o de las consultas reactivas en la home screen se reporta como molesta (umbral concreto, a definir en ese momento con datos del piloto).
   - **(b)** En hardware Android de gama baja (<6 GB de RAM), la latencia del pull inicial supera un umbral concreto. Ese umbral se fija en el momento de la reevaluación, no aquí, porque hoy no hay datos.

4. **Publicación de WatermelonDB ≥ 0.28 con plugin JSI oficial soportado por Expo SDK actual**: también es motivo suficiente para reevaluar. No se hace depender la decisión de un número de versión no existente; se reabra el ADR cuando la condición se cumpla.

## Alternativas consideradas

- **Activar JSI ya, pagando el costo de build nativo**: descartada por combinación de tres factores: (i) cero evidencia de necesidad hoy; (ii) OOM activo en builds EAS, que JSI empeoraría; (iii) el ADR-0011 ya documenta la política de no pre-instalar lo que no se usa.
- **Migrar a `expo-sqlite` directo** y abandonar WatermelonDB: descartada. WatermelonDB ya está aprobado por el ADR-0009, su modelo reactivo (`@nozbe/with-observables`) está integrado en `App.tsx` y en todas las pantallas; reemplazarlo es reescritura completa del módulo `database/`, no optimización.
- **Dejar `jsi: true` "por si el plugin se activa en otra rama"**: descartada porque el flag miente sobre el comportamiento actual, y los revisores futuros lo leerán y diseñarán en torno a una garantía que no existe. El costo de una línea que dice la verdad es cero.
- **Construir un wrapper JSI propio**: descartada. Reescribir la mitad de un adaptador SQLite es exactamente el tipo de optimización prematura prohibida por el Art. 9 del AGENTS.

## Consecuencias

- **Positivas**:
  + Builds EAS no se exponen al riesgo de OOM por compilación NDK innecesaria de `sqlite3.c` + `simdjson.cpp` para 4 ABIs. Esto **conecta directamente con el ADR-0010** (pin de `cli.version` y `node`, `resourceClass` en default hasta ver comportamiento real).
  + El código deja de mentir: un revisor que abra `database/index.ts` entiende inmediatamente que la app corre async. Esto reduce la sorpresa cuando alguien lea los logs y vea el warning de fallback en producción.
  + Diferir JSI hasta tener datos de uso real (≥4 empleados concurrentes **o** evidencia de latencia molesta en gama baja) es coherente con el Art. 9 del AGENTS ("no optimices prematuramente").

- **Negativas / costos**:
  − Si la latencia del pull inicial resulta molesta en gama baja, pagamos rework nativo (instalar plugin, configurar MainApplication, pelearse con NDK + memoria del worker de EAS). **Mitigación**: medir el pull inicial en un piloto real (dispositivo gama baja del operador, no emulador del laptop) antes de declarar la decisión estable. Si la medición sale mal, se reabre este ADR sin más costo.
  − Cada futura reevaluación de JSI abre un PR no trivial (configuración de plugin + posible cambio de `resourceClass` EAS). Esto es el precio esperado de haber diferido; está documentado, no es sorpresa.

- **Condición de reversa**: Cuando una de las dos condiciones (a) o (b) se cumpla, **o** cuando WatermelonDB publique una versión ≥ 0.28 con plugin JSI oficial soportado por Expo SDK actual, se reabre este ADR con datos concretos del piloto y se decide activar JSI (probablemente incluyendo subir `resourceClass` en `eas.json` para los workers, y/o separar el perfil EAS de la JSI-enabled build del de la JSI-disabled build).
