# ADR-0010 — Configuración EAS Build y direccionamiento del backend desde field-app

- **Estado:** Propuesto
- **Fecha:** 2026-08-05
- **Fase del roadmap:** Fase 3 (App de campo / Sincronización)

## Contexto

`field-app` se construye y distribuye con EAS Build (`app.json` línea 26: `eas.projectId = a09317d2-0eec-42e9-ac50-45262cde28fc`; `package` Android: `com.joemandev.hatofieldapp`). El archivo `eas.json` actual tiene 14 líneas y un único perfil (`preview`, `distribution: internal`, `android.buildType: apk`); no declara `env`, `node`, `image` ni `resourceClass`.

`src/App.tsx:23` lee la URL del backend así:

```ts
const API_BASE_URL = process.env.EXPO_PUBLIC_API_URL ?? 'http://10.0.2.2:5000';
```

Esto es incorrecto en dos dimensiones que el ADR-0008 (Protocolo de Sincronización) y el ADR-0009 (Stack móvil) no alcanzan a fijar por sí solos:

1. **Puerto equivocado**: `5000` no es el puerto del backend. `src/Hato.Api/Properties/launchSettings.json:16` define el perfil `http` con `applicationUrl: http://localhost:5282`. El puerto real es `5282`.
2. **Host por defecto apto solo para el emulador Android** (`10.0.2.2` mapea al loopback del host solo dentro de un emulador). En un dispositivo físico (`it-701a`) la app recibe ese valor y no alcanza al backend.

A esto se suma la mecánica de las variables `EXPO_PUBLIC_*`: se hornean en el bundle JS en **build time**, no en runtime. Eso significa que la URL del backend está congelada para cada perfil EAS y no puede cambiarse desde el dispositivo sin un rebuild.

Las builds de EAS para el perfil `preview` han estado fallando con "lost connection to the worker" (sospecha de OOM durante el paso Gradle); ese síntoma se aborda en el ADR-0012. Aquí solo decidimos la configuración de los perfiles.

Finalmente, la opción de usar la LAN del router rural (DHCP) como canal de sync se descarta por fragilidad operativa: la IP del host cambia cada vez que el router asigna una nueva lease, lo que rompe el `baseUrl` que el bundle lleva horneado.

## Decisión

1. **Tres perfiles EAS** en `eas.json`, cada uno con su `env.EXPO_PUBLIC_API_URL`:
   - `development` → `http://10.0.2.2:5282` (Android emulator; `10.0.2.2` resuelve al loopback del host).
   - `preview` → `http://100.101.240.44:5282` (Tailscale; IP estable asignada al host del backend, `joeman-laptop-1`).
   - `production` → `http://100.101.240.44:5282` (misma IP Tailscale; la firma y el canal de distribución del AAB se configuran fuera del alcance de este ADR).

2. **`cli.version` pinneado** en `eas.json` con la versión exacta. La versión concreta se fija en el PR de implementación contra la documentación de EAS para el SDK actual (Expo SDK 51 en `package.json`). No se documenta aquí un número que pueda quedar obsoleto antes de merge.

3. **`node` pinneado** en `eas.json` con la versión LTS compatible con el SDK actual. La versión concreta se fija en el PR de implementación contra la tabla oficial Node ↔ Expo SDK; se documenta como `node: "<version>"` a nivel raíz de `eas.json`.

4. **`image` y `resourceClass`** quedan en sus valores por defecto. Solo se sube a `image: "large"` y/o `resourceClass: "large"` si, tras aplicar los puntos 1-3 y el ADR-0012, persisten los OOM del worker. No se gasta memoria del worker por adelantado.

5. **Renuncia explícita a LAN con DHCP como canal de sync**. La IP del backend es siempre `100.101.240.44` (Tailscale) en `preview` y `production`. La fragilidad del DHCP rural hace inviable depender de leases cambiantes.

6. **Sin runtime config para `EXPO_PUBLIC_API_URL`**. Esta variable se hornea en el bundle al build time. Un override runtime (p. ej., vía `expo-constants` con un valor remoto) es un anti-patrón para nuestro caso porque:
   - Crea dos clases de fallo indistingibles (bundle mal horneado vs. red caída).
   - Obliga a diseñar un canal de configuración remota adicional antes de tener un caso real que lo justifique.
   - La dirección `100.101.240.44` es estable por construcción (Tailscale); el problema que intentaría resolver un override runtime no existe.

7. **Perfil `development`** debe declarar `android.gradleCommand: ":app:assembleDebug"` y `developmentClient: true` para habilitar dev-client con hot reload durante el trabajo local.

## Alternativas consideradas

- **LAN del router rural (DHCP)**: descartada por fragilidad (la IP cambia con cada lease del router). Confiar en una IP horneada que puede dejar de ser válida al día siguiente es peor que pagar el costo fijo de Tailscale.
- **Variable de entorno en runtime** (lectura vía `expo-constants` + valor remoto): descartada por las razones del punto 6. Si en el futuro la finca tiene más de un host backend, la solución es **otro perfil EAS** (p. ej., `finca-norte`, `finca-sur`), no un override runtime.
- **Build manual con `expo run:android` desde el laptop del operador**: descartada. Confunde QA (cada operador tiene un laptop distinto), no escala a más dispositivos y rompe la inmutabilidad del binario distribuido.
- **Túnel SSH inverso en lugar de Tailscale**: descartada porque Tailscale ya está en uso operativo; añadir SSH añade una pieza móvil más sin beneficio (magic DNS, claves, reconexión) que Tailscale ya da gratis.

## Consecuencias

- **Positivas**:
  + La app que llega al campo apunta por construcción a una dirección que estará disponible (Tailscale siempre activo cuando el operador abre la app, o aparece el mensaje de error esperado).
  + Mover el direccionamiento a `eas.json` deja el código (`App.tsx:23`) con un único `??` de fallback (emulador). No hay IP de producción dentro del bundle dev.
  + Los tres perfiles se prueban al menos una vez antes de declararlos usables. Si `preview` no compila en el primer intento, hay algo que `development` no expone (p. ej., configuración de firma o de gradient) y se detecta antes de tocar al operador.

- **Negativas / costos**:
  − Requiere que el operador confirme que Tailscale está activo en `it-701a` antes de instalar un APK `preview` o `production`. Sin Tailscale, el `baseUrl` `100.101.240.44:5282` no resuelve y la app mostrará error de red en el primer login (no es crash; el flujo offline sigue funcionando).
  − El backend debe arrancar con `--urls http://0.0.0.0:5282` (o equivalente), no con el default que ata a loopback. `dotnet run --project src/Hato.Api --urls http://0.0.0.0:5282` queda documentado en `03-processes/onboarding.md`. Si se olvida, el síntoma es "login falla con error de red" sin más diagnóstico.
  − El perfil `production` produce `.aab` (no `.apk`). La configuración de firma (keystore, `credentials.json`, integración con Google Play Console) queda fuera del alcance de este ADR y se aborda cuando se aproxime la primera release pública.

- **Condición de reversa**: Si Tailscale resulta inviable por costo, agotamiento del rango de IPs o cualquier cambio operativo, se reescribe `env.EXPO_PUBLIC_API_URL` en los tres perfiles con la nueva solución y se actualiza este ADR. Mientras tanto, los perfiles EAS son la única fuente de verdad para el `baseUrl` de producción.
