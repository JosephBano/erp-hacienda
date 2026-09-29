# APPS-ANDROID.md — Compilación, firma, distribución privada y operación Android

> **Audiencia:** Administrador del sistema y custodio de infraestructura.  
> **Normativa y base:** [ADR-0035](adr/0035-distribucion-android-privada.md), [ADR-0034](adr/0034-firma-ci-entornos-github.md), [SEGURIDAD.md](SEGURIDAD.md), [PREFLIGHT-PRODUCCION.md](PREFLIGHT-PRODUCCION.md).  
> **Para el operador de campo:** Consultar [GUIA-CAMPO-ANDROID.md](GUIA-CAMPO-ANDROID.md).

---

## 1. Arquitectura de distribución privada

HATO ERP distribuye sus aplicaciones Android de campo fuera de Google Play Store para garantizar soberanía, compatibilidad offline-first estricta y aislamiento en la red privada Tailscale.

### 1.1 Variantes y canales

| Parámetro | Canal Stage | Canal Producción |
|---|---|---|
| **Application ID / Package** | `com.joemandev.hatofieldapp.stage` | `com.joemandev.hatofieldapp` |
| **Nombre de app** | HATO Campo (Stage) | HATO Campo |
| **Ícono** | Distintivo con cinta «STAGE» | Ícono institucional HATO |
| **URL API permitida** | `http://10.0.2.2:5000/api`, `http://localhost:5000/api`, `https://*.ts.net/api` | `https://*.ts.net/api` (Tailscale) |
| **Keystore y firma** | Certificado Stage (`mobile-stage`) | Certificado Prod (`mobile-production`) |
| **Política de retención** | Máx 10 APKs o 30 días | Máx 5 APKs o 180 días |
| **Protección de poda** | No aplica | `IsCurrentStable`, `IsLastGood`, `UnderInvestigation` |

---

## 2. Inventario inicial y primera instalación

### 2.1 Verificación de firma previa en dispositivo físico (Preflight INV.1)

Si el teléfono en campo posee una versión previa instalada durante fases de desarrollo (ej. `1.0.0/code 1` firmada con el certificado debug por defecto de Android SDK):
1. **Comprobar Outbox local:** Abrir HATO Campo → Bandeja de sincronización (`SyncTray`). Verificar que no existan mutaciones pendientes de envío al servidor.
2. **Forzar sincronización:** Conectar el teléfono a internet vía Tailscale y confirmar que todos los registros de campo hayan ingresado en el backend y recibido confirmación.
3. **Comprobar huella del APK actual:**
   ```bash
   adb shell pm list packages -f | grep hatofieldapp
   adb pull /data/app/.../base.apk /tmp/installed-app.apk
   apksigner verify --print-certs /tmp/installed-app.apk
   ```
4. **Corte y reemplazo asistido:** Android prohíbe terminantemente actualizar un paquete existente si la firma del nuevo APK no coincide con la del APK instalado (`INSTALL_FAILED_UPDATE_INCOMPATIBLE`). Si el teléfono tiene la firma debug, la migración a la firma productiva formal requiere:
   - Confirmar vaciado total de la outbox local.
   - Desinstalar el APK debug: `adb uninstall com.joemandev.hatofieldapp`.
   - Instalar el APK productivo firmado: `adb install -r hato-1.1.0-prod-bX-SHA.apk`.
   - Iniciar sesión y realizar la descarga de catálogos y re-hidratación inicial.

---

## 3. Pipeline de compilación y firma (GitHub Actions)

### 3.1 Orquestación y secretos

El flujo `.github/workflows/build-android.yml` se ejecuta exclusivamente en runners efímeros alojados por GitHub y utiliza secretos segmentados por **GitHub Environments**:

- Entorno `mobile-stage`:
  - `ANDROID_KEYSTORE_BASE64`: Archivo `hato-mobile-stage.keystore` codificado en base64.
  - `ANDROID_KEYSTORE_PASSWORD`, `ANDROID_KEY_ALIAS`, `ANDROID_KEY_PASSWORD`.
- Entorno `mobile-production`:
  - `ANDROID_KEYSTORE_BASE64`: Archivo `hato-mobile-prod.keystore` codificado en base64.
  - `ANDROID_KEYSTORE_PASSWORD`, `ANDROID_KEY_ALIAS`, `ANDROID_KEY_PASSWORD`.
  - Requiere protección de rama `main` o tags `v*`. Bloqueado para forks y PRs externos.

### 3.2 Pasos del workflow

1. **Checkout y validación de commit.**
2. **Configuración de Node.js, Java 17 y Android SDK build-tools.**
3. **Inyección temporal del keystore:**
   ```bash
   echo "$ANDROID_KEYSTORE_BASE64" | base64 -d > /tmp/release.keystore
   ```
4. **Construcción del APK release:** `npx eas-cli build --platform android --local --profile <stage|production>`.
5. **Verificación y generación de manifiesto:**
   - Ejecución de `scripts/android-build-verify.sh`: comprueba que el APK esté firmado con v2/v3, que no sea debuggable (`android:debuggable="false"`), y que el package name y versionCode correspondan exactamente con la solicitud.
   - Ejecución de `scripts/android-artifact-manifest.sh`: calcula hash SHA-256, tamaño en bytes, fingerprint X.509 y genera `manifest.json`.
6. **Limpieza garantizada:**
   El paso de limpieza corre en bloque `always()` de GitHub Actions, garantizando la eliminación de `/tmp/release.keystore` incluso ante fallos o cancelaciones.
7. **Publicación de artefactos:** Los archivos `hato-*.apk` y `manifest.json` se publican como build artifacts de GitHub para su importación por el backend.

---

## 4. Importación, catálogo y respaldo externo

### 4.1 Worker de importación (`MobileDeliveryWorker`)

1. El worker monitorea las solicitudes de compilación en estado `Dispatched` o `Building`.
2. Al completarse el workflow en GitHub Actions, descarga los artefactos correlacionando `gitHubRunId` y commit SHA.
3. Valida estrictamente el nombre del archivo contra la expresión regular:
   `^hato-[0-9]+\.[0-9]+\.[0-9]+-(stage|prod)-b[0-9]+-[a-f0-9]{7,40}\.apk$`
   Rechazando cualquier intento de path traversal o nombres foráneos.
4. Comprueba que el hash SHA-256 del binario coincida con el reportado en `manifest.json`.
5. Enforza la cuota local de **5.0 GiB**. Si la cuota se sobrepasa, suspende la importación con alarma en bitácora.
6. Registra la nueva `MobileRelease` en la base de datos PostgreSQL con estado `Available`.
7. Ejecuta la copia secundaria hacia Google Drive mediante `scripts/backup-upload.sh` en los namespaces dedicados:
   - `mobile/stage/`
   - `mobile/prod/`

---

## 5. Protocolo de actualización en campo

Para actualizar un dispositivo de campo existente sin pérdida de información:

1. **Verificación previa:** En el dispositivo, ingresar a la pantalla de sincronización y corroborar que no existan operaciones pendientes.
2. **Descarga autorizada:** Desde el navegador del dispositivo, ingresar a `https://<servidor-finca>/android-apps` con credenciales que tengan el permiso `delivery.releases.download`.
3. **Descarga mediante Blob:** El botón «Descargar APK» solicita el archivo por streaming autenticado con Bearer token. No expone secretos ni tokens en la URL.
4. **Instalación sobre la existente (In-Place Update):**
   - Abrir el archivo APK descargado y pulsar **«Actualizar»**.
   - **NUNCA desinstalar la versión previa**, a fin de conservar intacta la base de datos SQLite local (`hato_offline.db`), las credenciales del operador y las claves criptográficas almacenadas en Android Keystore.
5. **Verificación post-actualización:** Abrir HATO Campo, confirmar el número de versión en el pie de página y ejecutar una sincronización de prueba.

---

## 6. Contingencia, reversión y rollback

### 6.1 Detección de regresión o fallo crítico

Si una versión publicada presenta anomalías en campo (ej. fallos en pesajes, error de cálculo o regresión de datos):

1. **Retiro inmediato:**
   - El administrador ingresa a `/android-apps`.
   - En la fila de la versión afectada, hace clic en **«Retirar»**.
   - Ingresa obligatoriamente el motivo del retiro (ej. *«Regresión crítica en cálculo de dosis de antibiótico»*).
   - El sistema cambia el estado a `Withdrawn`, revoca la condición de `IsCurrentStable` y promueve automáticamente a la última versión conocida buena (`IsLastGood`).
2. **Aislamiento de la versión:**
   - La API bloquea de inmediato la descarga de cualquier versión en estado `Withdrawn`.
   - Si la versión requiere auditoría técnica forense, se marca como `UnderInvestigation`, lo cual suspende automáticamente su eliminación física por la política de retención.
3. **Restablecimiento en campo:**
   - Para actualizar hacia la versión corregida o volver a la versión *Last Good*, se genera una nueva compilación con un `versionCode` monotónicamente superior que incorpore la corrección o el rollback del código.
   - Android prohíbe el downgrade de `versionCode` mediante el instalador estándar. Por tanto, ante un rollback se compila el código estable previo incrementando el `versionCode`.

---

## 7. Custodia, respaldo y rotación de claves

### 7.1 Custodia de Keystores

1. Las claves de firma productivas (`hato-mobile-prod.keystore`) son activos críticos del negocio.
2. **Prohibición absoluta:** Nunca deben guardarse en el repositorio Git, en ramas públicas ni en imágenes Docker.
3. Las copias maestras residen en dos medios físicos cifrados fuera de línea bajo custodia del propietario de la finca y el líder técnico.

### 7.2 Rotación de firmas (v3 Signing Lineage)

Si una clave de firma privada se ve comprometida o debe renovarse por ciclo de vida:
1. Se genera un nuevo par de claves RSA 4096 / EC.
2. Se utiliza la herramienta oficial `apksigner rotate` para crear una prueba de linaje criptográfico:
   ```bash
   apksigner rotate \
     --old-keystore hato-mobile-prod-old.keystore \
     --old-key-alias hatoprod \
     --new-keystore hato-mobile-prod-new.keystore \
     --new-key-alias hatoprod2026 \
     --lineage /tmp/hato-signing-lineage.bin
   ```
3. El pipeline de GitHub Actions aplicará el archivo `--lineage` con `--rotation-min-sdk-version 28`, permitiendo que los dispositivos con la clave anterior se actualicen sin requerir desinstalación ni perder sus datos locales.

---

## 8. Protocolo ante extravío o robo de dispositivo

En caso de pérdida, sustracción o decomiso de un teléfono de campo:

1. **Corte de red en Tailscale:**
   - Acceder inmediatamente a la consola administrativa de Tailscale (`login.tailscale.com`).
   - Ubicar el dispositivo de campo comprometido y seleccionar **«Disable»** o **«Remove node»**. Esto corta de inmediato cualquier acceso a la red de la finca y sus servicios internos.
2. **Revocación de credenciales en HATO ERP:**
   - Desde el panel web, acceder a **Administración → Roles y Permisos / Usuarios**.
   - Desactivar la cuenta del operador asignado al dispositivo o forzar cambio de contraseña.
   - Invalidar todas las sesiones activas en el módulo People.
3. **Rechazo en sincronización:**
   - Cualquier petición HTTP que intente enviar el dispositivo rechazará las credenciales con HTTP 401/403.
4. **Registro de auditoría:**
   - Documentar el incidente en la bitácora administrativa de la finca indicando marca, modelo, última IP conocida y acciones de mitigación adoptadas.
