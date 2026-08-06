# ADR-0014 — Adoptar `expo-system-ui` para que `userInterfaceStyle: "dark"` llegue al chrome nativo

- **Estado:** Propuesto
- **Fecha:** 2026-08-06
- **Fase del roadmap:** Fase 3 (App de campo / Sincronización)

## Contexto

`clients/field-app/app.json:9` declara `"userInterfaceStyle": "dark"`. Esa intención se aplica **solo** al árbol React Native — textos, superficies, bordes — vía la `Appearance` API y los defaults de los temas. **No se aplica al chrome nativo de Android** (status bar, navigation bar) sin ayuda.

Evidencia del fallo en producción (rama `feature/field-app-ui-forms-fix`, logs de `adb logcat -d --pid=$PID`):

```
ReactNativeJS: Running "main"
ReactNativeJS: [API] GET http://100.101.240.44:5282/api/v1/sync/pull?batchSize=100 → HTTP 200
...
unknown:ReactNative: StatusBarModule: Ignored status bar change, current activity is edge-to-edge.
unknown:ReactNative: StatusBarModule: Ignored status bar change, current activity is edge-to-edge.
unknown:ReactNative: StatusBarModule: Ignored status bar change, current activity is edge-to-edge.
unknown:ReactNative: StatusBarModule: Ignored status bar change, current activity is edge-to-edge.
```

Las llamadas a `StatusBar.setBarStyle("light-content")` y `StatusBar.setBackgroundColor(...)` que hace el field-app (ver `clients/field-app/src/App.tsx:130-132`) son **ignoradas** en Android 16+ edge-to-edge. La consecuencia visible en las capturas del piloto: la status bar del sistema operativo queda con el color/contraste del **tema del sistema** (claro u oscuro según el dispositivo), no del tema de la app. Un teléfono configurado en modo claro del sistema operativo muestra una barra blanca sobre el fondo oscuro de la app, que rompe la "una sola mano lee todo esto al sol" del Art. 12.

La forma soportada oficialmente por Expo para que `userInterfaceStyle` se propague al chrome nativo es `expo-system-ui`. Con `softwareKeyboardLayoutMode="resize"` y la integración con el plugin, el plugin escribe `windowLightStatusBar` y `windowLightNavigationBar` en `android/values/themes.xml` y los mantiene sincronizados con el `Appearance` actual.

## Decisión

1. **Instalar** `expo-system-ui` y declararlo en `app.json`:
   ```json
   {
     "expo": {
       "plugins": ["expo-system-ui"]
     }
   }
   ```

   El plugin no necesita configuración adicional — lee `userInterfaceStyle` del propio `app.json` y la propaga al nativo.

2. **Eliminar** las llamadas explícitas a `StatusBar.setBarStyle("light-content")` y `StatusBar.setBackgroundColor(...)` de `App.tsx`. Pasan a ser responsabilidad del plugin. Si en algún momento queremos **forzar** un estilo distinto al del sistema, lo hacemos en un solo lugar (el plugin), no en cada `<Screen>`.

3. **Mantener** el `<StatusBar translucent={false} ... />` mientras siga siendo necesario como workaround de edge-to-edge en Android 16+. Una vez que la app pase a React Native 0.86+ (donde `StatusBar` lo hace correctamente), se evalúa.

4. **NO** añadir esta dependencia sin un PR separado aprobado. **Este ADR es la propuesta**, no la implementación. La regla 2 del AGENTS.md exige "Propón, espera aprobación humana."

## Alternativas consideradas

- **Dejar el `StatusBar` actual y aceptar que el chrome nativo no refleja el tema**: descartada. El Art. 12 dice "Contraste empujado bien pasado los mínimos usuales; los grises medios desaparecen al sol" — un status bar blanco sobre fondo oscuro rompe ese principio. No es solo cosmético.
- **Forzar theme oscuro a nivel SO vía un `themes.xml` custom** que pone `windowLightStatusBar="false"` hardcoded: descartada. No respeta el toggle del usuario en el sistema, lo cual viola el principio de least surprise. `expo-system-ui` lee `userInterfaceStyle` y lo respeta.
- **Cambiar a React Native 0.86** que arregla `StatusBar` upstream: descartada para esta entrega. RN 0.86 trae su propio OOM en builds EAS (per ADR-0010) y no queremos reabrir eso.

## Consecuencias

- **Positivas**:
  + El status bar del sistema refleja `userInterfaceStyle` automáticamente. Si el sistema está en modo claro, las superficies del app son oscuras (controlado por el tema RN), pero el chrome nativo queda en claro también — todo coherente.
  + La navigation bar del sistema (la barra inferior con back/home/recent en Android) también respeta el tema. Hoy se ve blanca cuando debería ser oscura en algunos dispositivos.
  + El plugin es ~30 KB, sin runtime en el APK, sin impacto en el bundle JS.

- **Negativas / costos**:
  − Hay que agregar `expo-system-ui` como dependencia (cumple Art. 2: este ADR es la propuesta).
  − En Android 16+ edge-to-edge, la status bar sigue dibujándose **encima** del contenido. Esto es por diseño del SO y `expo-system-ui` no lo arregla — solo aplica el color/contraste correctos. La solución completa requiere `react-native-safe-area-context` para inset-padding en `<SafeAreaView edges={['top']}>`, lo que sería un ADR-0015 separado.
  − Si alguien desinstala `expo-system-ui` en el futuro, los flags de Manifest vuelven a defaults de Expo y el problema vuelve. El reviewer debe verificar que `app.json` siga declarando el plugin.

- **Condición de reversa**: Si `expo-system-ui` rompe algo o se discontinúa, se restaura el `<StatusBar>` manual y se reabre el ADR. El riesgo es bajo: el plugin es maduro (Expo SDK 56 lo soporta oficialmente) y la integración es de una línea.