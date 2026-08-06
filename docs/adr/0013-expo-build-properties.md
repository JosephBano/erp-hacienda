# ADR-0013 — Adoptar `expo-build-properties` para que la configuración Android sobreviva `prebuild`

- **Estado:** Propuesto
- **Fecha:** 2026-08-06
- **Fase del roadmap:** Fase 3 (App de campo / Sincronización)

## Contexto

`clients/field-app/app.json` no declara ningún plugin de `expo-build-properties`. La consecuencia práctica es que toda la configuración específica de Android vive en **archivos manuales** dentro de `clients/field-app/android/`, que es un directorio regenerado por `npx expo prebuild`:

| Archivo | Qué hace | Quién lo lee |
|---|---|---|
| `android/app/src/main/AndroidManifest.xml` | `android:networkSecurityConfig="@xml/network_security_config"` | Prebuild lo regenera — sobrevive solo si nadie corre `prebuild --clean` |
| `android/app/src/main/res/xml/network_security_config.xml` | Permite cleartext **solo** para `100.101.240.44` (IP Tailscale del backend) | Idem |
| `android/gradle.properties` | Reduce Xmx a 1536m, desactiva parallel, fija ABIs en `armeabi-v7a,arm64-v8a` | Idem |

El estado actual funciona en piloto (la rama `feature/field-app-ui-forms-fix` contiene los tres archivos y la app levanta + sincroniza), pero **no sobrevive** un `prebuild --clean`. Cualquiera que clone el repo, corra `npm install` y luego `expo prebuild --clean` pierde el network_security_config y la app cae con "Cleartext HTTP traffic to 100.101.240.44 not permitted".

Además, los tres archivos manuales no están versionados como **configuración**: están en `.gitignore` (prebuild los borra) y aparecen como ruido en `git status` cada vez que `app.json` cambia. Cualquier intento de mover el cleartext a `expo-build-properties` con `usesCleartextTraffic: true` da la **granularidad equivocada** — es un boolean all-or-nothing, no permite "cleartext para Tailscale IP, HTTPS para el resto".

## Decisión

1. **Instalar** `expo-build-properties` (≤ 1 línea en `package.json`, ya está soportado por Expo SDK 56 vía `expo install`). Mantener el árbol `android/` bajo control de **dos** fuentes declarativas:
   - `app.json` → vía el plugin `expo-build-properties`, lo que se pueda expresar declarativamente.
   - El archivo `network_security_config.xml` → seguir como **manual**, fuera de `app.json`, porque la granularidad per-IP no la soporta el plugin.

   El plugin se configura para **no pisar** el XML existente: solo fija `usesCleartextTraffic` a `false` (default seguro) y deja que el XML aplique la excepción por dominio. Prebuild va a regenerar `android/`, va a escribir `android:networkSecurityConfig` desde el XML (que sigue ahí), y va a aplicar el boolean desde `app.json`.

   ```json
   {
     "expo": {
       "plugins": [
         [
           "expo-build-properties",
           {
             "android": {
               "usesCleartextTraffic": false,
               "compileSdkVersion": 36,
               "targetSdkVersion": 36,
               "minSdkVersion": 24
             }
           }
         ]
       ]
     }
   }
   ```

2. **Conservar** `clients/field-app/android/app/src/main/res/xml/network_security_config.xml` como archivo versionado **fuera** de `.gitignore`. El XML declara `cleartextTrafficPermitted="false"` por default y `cleartextTrafficPermitted="true"` solo para `<domain>100.101.240.44</domain>`. El Manifest referenciará este XML vía `android:networkSecurityConfig`. Si en algún momento el backend pasa a público, este XML se simplifica (se elimina la excepción) y se commitea un PR aparte.

3. **Conservar** `clients/field-app/android/app/src/main/AndroidManifest.xml` con `android:networkSecurityConfig="@xml/network_security_config"` como override del plugin. El plugin puede dejar un `android:usesCleartextTraffic="false"` redundante — el XML gana por su especificidad (lee el sistema Android por `<application>` antes que por el boolean). No se rompe nada.

4. **Migrar** `gradle.properties` (Xmx 1536m, parallel false, ABIs) a un archivo versionado fuera de `android/`:
   - Opción A: un plugin config-plugin custom que escribe el `gradle.properties` durante prebuild.
   - Opción B: dejar `gradle.properties` versionado **dentro de** `android/` con un README que diga "no correr prebuild --clean sin re-aplicar estos valores".

   Decisión provisional: **Opción B** para esta entrega. El `gradle.properties` se commitea con los valores ya tuned, y se documenta en el comentario del archivo. La Opción A queda para un PR siguiente si `prebuild` se vuelve a usar durante el desarrollo.

5. **NO** añadir esta dependencia sin un PR separado aprobado. **Este ADR es la propuesta**, no la implementación. La regla 2 del AGENTS.md exige "Propón, espera aprobación humana."

## Alternativas consideradas

- **Dejar todo manual, documentar el flujo de prebuild**: descartada. Cada nueva clone va a romper la primera vez que alguien corra prebuild, y la memoria institucional sobre qué archivos hay que re-crear se pierde rápido. Es exactamente el tipo de optimización prematura-inversa que pide el Art. 9.
- **Mover TODO el cleartext a `usesCleartextTraffic: true`** y borrar el XML: descartada. La Constitución Art. 12 dice "alto contraste, guantes, sol" — el principio es que los defaults son seguros. Permitir cleartext global es regresión de seguridad por un problema de tooling. La granularidad per-IP es lo correcto.
- **Construir un config-plugin custom** que escribe el `network_security_config.xml` durante prebuild: descartada para esta entrega. Sería lo más limpio a largo plazo, pero escribir y mantener el plugin excede el alcance de este PR. Se anota como follow-up.
- **Inlining del `<network-security-config>` en el Manifest vía `android:networkSecurityConfig` con un blob XML**: descartada. Manifest no soporta XML inline para eso; necesita un archivo separado.

## Consecuencias

- **Positivas**:
  + El boolean `usesCleartextTraffic` queda declarativo en `app.json`. Cualquiera que abra el archivo entiende la intención sin tener que leer un Manifest XML generado.
  + Las versiones `compileSdk`/`targetSdk`/`minSdk` se centralizan en `app.json` en lugar de quedar dispersas entre `gradle.properties`, `build.gradle` y el Manifest regenerado.
  + El plugin `expo-build-properties` es ~50 KB de código, sin runtime, sin UI. La huella en el APK es nula.
  + El XML granular sobrevive prebuild porque no es un archivo regenerado por el plugin (es un recurso estático en `res/xml/`).

- **Negativas / costos**:
  − Hay que agregar `expo-build-properties` como dependencia (cumple Art. 2: este ADR es la propuesta, pendiente de aprobación).
  − El `gradle.properties` queda versionado dentro de `android/`, que se regenera. El comentario en el archivo debe decir claramente que **no correr `prebuild --clean` sin re-aplicar los valores de Xmx/parallel/ABIs**. Si esto se vuelve un problema recurrente, el follow-up es el config-plugin custom.
  − Si alguien en el futuro quita `expo-build-properties` de `app.json`, la app va a seguir compilando pero con defaults distintos (`compileSdk` = el de Expo, etc.). El reviewer debe estar atento a este acoplamiento.

- **Condición de reversa**: Si `expo-build-properties` deja de mantenerse o rompe el build, se revierte a archivos manuales puros (el XML sigue versionado, el Manifest se vuelve a patchear) y se reabre el ADR.