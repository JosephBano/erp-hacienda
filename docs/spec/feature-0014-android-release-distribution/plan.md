# plan.md — Builds Android y biblioteca privada

> Paquete completo: [tareas](./tasks.md) y [E2E](./test-e2e.md).
> Aplican [políticas](../../POLITICAS-OPERACION.md) y [preflight](../../PREFLIGHT-PRODUCCION.md).

> Plan del [spec](./spec.md), aprobado 2026-09-16, con tareas y E2E.

## Entregas por propósito

1. `docs(delivery): define Android release channels and credentials`.
   Aplicar ADR-0035 aceptado: Delivery, GitHub App, EAS local, firma y almacenamiento. Añadir términos en
   `docs/GLOSSARY.md`, límite modular en ARCHITECTURE.md y custodios en SEGURIDAD.md.
   Inventariar package/firma/versionCode reales; confirmar URLs HTTPS y capacidad de build
   antes de fijar variante prod. No cambiar firma de dispositivos existentes por comodidad.
2. `feat(mobile): separate Android stage and production variants`.
   Crear `clients/field-app/app.config.ts` y adaptar `app.json`, `eas.json` y pipeline de
   versión con configuración de canal allowlist. Preservar identidad prod verificada.
   Verificar configuración resuelta y APK release real: packages distintos, endpoints
   correctos, sin Metro, firma esperada; instalar ambas en Android físico.
3. `feat(delivery): persist build requests and release history`.
   Crear proyectos bajo `src/Modules/Delivery/` siguiendo estructura del repo, con
   integración en `src/Hato.Api/Program.cs` y endpoints en
   `src/Hato.Api/Endpoints/MobileReleasesEndpoints.cs`. Migraciones, permisos People,
   outbox de solicitudes, reserva monotónica de versionCode y auditoría de transiciones.
   Tests futuros `tests/Hato.Delivery.UnitTests` y `tests/Hato.Delivery.IntegrationTests`:
   transiciones, autorización, concurrencia, duplicados y recuperación, PostgreSQL real.
4. `ci(delivery): build signed Android artifacts in hosted runners`.
   Crear `.github/workflows/build-android.yml`, `scripts/android-build-verify.sh` y
   `scripts/android-artifact-manifest.sh`. Secrets de firma por entorno, CI exacto,
   release validation, EAS local y artifacts temporales. Probar APK con herramientas SDK
   `apksigner verify --print-certs` y `apkanalyzer manifest application-id`;
   comparar contra manifiesto y firma aprobados. Tags/ref deben ser compatibles con la
   política de GitHub Environment descrita en feature-0012.
5. `feat(delivery): orchestrate builds and import private artifacts`.
   Worker en Infrastructure de Delivery, credencial GitHub App montada exclusivamente allí,
   consultas de run por ID, reintentos, leases e importación atómica. Pruebas de caída
   tras dispatch, run ajeno, digest inválido, archive path traversal, cuota y expiración.
   Incluir backups de biblioteca en namespace propio mediante feature-0013; medir tamaño
   y ajustar retención propuesta si no cabe en el presupuesto.
6. `feat(web): manage Android builds and authenticated downloads`.
   Crear sección Delivery bajo `clients/admin-web/src/app/`, servicio HTTP y rutas según
   patrón del cliente, sin dependencias UI nuevas salvo ADR. Vista generar ambas, estados
   por canal, historial, publicar/retirar y descarga autorizada. UI no expone tokens,
   no edita workflow y no permite elegir API arbitraria. Probar permisos del backend,
   no solo botones, y descarga/instalación desde Chrome Android en tailnet.
7. `docs(delivery): document stable release and device migration`.
   Crear `docs/APPS-ANDROID.md` como runbook y registrar fila en DOCUMENTACION.md.
   Documentar actualización sin desinstalar, conciliación de outbox, firma perdida,
   compilación fallida, retirada, poda y recuperación de artefacto. Registrar ensayo
   de actualización sobre datos locales pendientes y dos variantes coexistiendo.

## Dependencias y validación de implementación

ADR e inventario → variantes + modelo → pipeline → worker → web → prueba física → stable.
Feature-0012 aporta API prod y acceso privado; feature-0013 aporta copia externa y catálogo
DB recuperable. Stage puede ensayarse antes, pero prod no se publica hasta ambas compuertas.

Cada entrega se verifica primero con pruebas de comportamiento específicas y luego suite
completa del repo antes de PR. Backend `dotnet build -c Release` y `dotnet test -c Release`;
clientes ejecutan scripts actuales de package.json y build release. Congelar evidencias
de package, certificado, hash, SHA, URL y conservación de outbox en dispositivo real.
Pruebas de credenciales usan valores ficticios; no archivar claves en resultados.

Punto de compatibilidad permanente: primera firma/package publicados. Una vez instalados,
su cambio exige migración; no se resuelve borrando SQLite. Retirar una APK del catálogo
no la desinstala ni revoca dispositivos automáticamente.

PR describe compilación por canal, controles de secretos, descarga privada, retención y
verificación física; excluye iOS, Play Store, OTA y automatización arbitraria de workflows.
