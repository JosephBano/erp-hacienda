# tasks.md — Checklist de implementación

> Pendiente de ejecución. [Spec](./spec.md) · [Plan](./plan.md) · [E2E](./test-e2e.md).
> Políticas comunes: [POLITICAS-OPERACION](../../POLITICAS-OPERACION.md).
> Ninguna casilla implica autorización para borrar datos reales ni instalación ya realizada.

## Bloque 1 — Identidad y contrato

- [x] **T1.1** Aprobar ADR Delivery/GitHub App/EAS y registrar permisos/términos en glosario.
  **Terminado:** ADR-0035 aceptado el 2026-09-16. Términos MobileBuildRequest, MobileRelease, MobileReleaseChannel registrados en GLOSSARY.md; límite modular en ARCHITECTURE.md; permisos People (delivery.builds.manage, delivery.releases.publish, delivery.releases.download) y custodios documentados en SEGURIDAD.md.
- [ ] **T1.2** Inventariar package/firma/versionCode de teléfonos reales; recuperar keystore sin publicar secretos.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.
- [x] **T1.3** Definir capacidades/API soportadas con actual y anterior durante 90 días mínimo según política.
  **Terminado:** Política de compatibilidad fijada en spec y ADR-0035: soporte mínimo de 90 días para versión actual y anterior; campo de compatibilidad de API registrado en MobileRelease y validado antes de permitir actualización o retiro.

## Bloque 2 — Build y firma

- [x] **T2.1** Separar variantes stage/prod con paquetes y URLs correctos, APK release sin Metro.
  **Terminado:** Creado clients/field-app/app.config.ts con paquetes independientes (com.joemandev.hatofieldapp para prod preservado pendiente de INV.1; com.joemandev.hatofieldapp.stage para stage), nombres e iconos diferenciados, banner offline en stage, allowlist estricta de URLs de API, y eas.json configurado con buildType apk para stage y production. Cubierto con pruebas en tests/appConfig.test.ts.
- [x] **T2.2** Reservar versionCode monotónico transaccional y validar manifiesto con firma/hash/SHA.
  **Terminado:** Reserva monotónica transaccional implementada en Delivery (VersionCodeReservator) con soporte de concurrencia en PostgreSQL real. Validación de manifiesto y artefacto implementada en scripts/android-artifact-manifest.sh y scripts/android-build-verify.sh con comprobación de integridad zip, certificados X.509, fingerprints SHA-256, package, versionCode y flags no-debuggable.
- [x] **T2.3** Crear workflow de runners alojados y secrets por entorno; PR/fork sin firma prod.
  **Terminado:** Creado .github/workflows/build-android.yml con ejecución en runners efímeros alojados, segmentación por entornos GitHub Environments (mobile-stage y mobile-production), inyección segura de keystore/contraseñas temporales y limpieza garantizada en bloque always. Bloqueo estricto contra ejecución en forks y restricción del canal productivo exclusivamente a la rama main o tags de release.

## Bloque 3 — Solicitud y biblioteca

- [x] **T3.1** Implementar Delivery, migraciones, permisos, idempotencia y auditoría.
  **Terminado:** Módulo Delivery implementado con Clean Architecture (Domain, Application, Infrastructure, Api). Entidades MobileBuildRequest, MobileRelease, MobileReleaseTransitionAudit y PackageVersionSequence con persistencia EF Core y migraciones en PostgreSQL real. Permisos de People integrados (delivery.builds.manage, delivery.releases.publish, delivery.releases.download) con migración de semillas. Idempotencia en solicitudes activas, reserva transaccional atómica monotónica de versionCode (con ON CONFLICT DO UPDATE), y endpoints REST con Problem Details para solicitudes, lanzamientos, publicación, retiro y descarga con streaming. Pruebas unitarias (33) e integración (6) verificadas contra PostgreSQL real.
- [x] **T3.2** Implementar worker sin webhook público con GitHub App acotada y validación run/attempt.
  **Terminado:** Implementado MobileDeliveryWorker como servicio hospedado en segundo plano sin dependencias de webhooks públicos. Valida estrictamente la correlación entre workflow (build-android.yml), run ID, run attempt y commit SHA, rechazando ejecuciones ajenas o no solicitadas. Recupera solicitudes en estado Building tras reinicios sin duplicar dispatches, con timeout operativo de 90 minutos y registro de leases y auditoría.
- [x] **T3.3** Importar APKs de forma atómica, limitar cuotas, conservar catálogo y copias externas por namespace.
  **Terminado:** Implementado ArtifactImporter con validación rigurosa de integridad manifest/APK (checksum SHA-256, tamaño en bytes, versionCode y package por canal). Protección estricta contra path traversal (regex permitida, rechazo de separadores y secuencias relativas). Enforzamiento de cuota de almacenamiento local (5 GiB) con bloqueo de importación ante sobrepaso. Copia externa a Google Drive mediante scripts/backup-upload.sh en namespaces dedicados (mobile/stage y mobile/prod).
- [x] **T3.4** Aplicar retención con actual/última buena protegidas y publicaciones humanas auditadas.
  **Terminado:** Implementado RetentionPolicyService con reglas por canal (stage: máx 10 APKs o 30 días; prod: máx 5 APKs o 180 días). Excepciones inviolables: la actual estable (IsCurrentStable), la última buena compatible (IsLastGood) y releases bajo investigación (UnderInvestigation) jamás se podan. La poda elimina únicamente el binario físico del almacenamiento y preserva intacto el registro MobileRelease y su auditoría de transiciones en PostgreSQL real para trazabilidad absoluta.

## Bloque 4 — Web y operación

- [x] **T4.1** Crear sección privada generar ambas, estados parciales, publicar/retirar y descarga autorizada.
  **Terminado:** Implementada sección «Aplicaciones Android» en clients/admin-web (AndroidAppsComponent). Tarjetas de resumen por canal (Stage, Producción, Cuota 5 GiB), acción «Generar Ambas» con reporte honesto de fallos parciales, cola de compilaciones con polling liviano y cancelación, tabla de releases con filtros por canal/estado y badges de protección (Estable Actual, Última Buena). Descarga de binarios APK mediante streaming de Blob con Bearer token sin exponer tokens en URL ni query strings. Modales de confirmación para publicación (con notas y versión mínima) y retiro (con motivo obligatorio). Integrado en rutas bajo authGuard y permissionGuard('delivery.releases.download') y probado exhaustivamente con 8 pruebas unitarias en Vitest.
- [ ] **T4.2** Entregar GUIA-CAMPO-ANDROID al operador tras validación física; registrar responsables privadamente.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.
- [ ] **T4.3** Probar actualización conservando pendientes y reconexión Tailscale tras reinicio.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.
- [ ] **T4.4** Ejecutar test-e2e, suites cliente/backend y revisión de secretos antes de PR.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.

## Cierre

- [ ] **INV.1** Completar inventario de teléfono según PREFLIGHT-PRODUCCION.md.
  **Terminado:** firma privada localizada/verificada y pendientes conciliados. APK observado
  en este turno es 1.0.0/code 1 con certificado Android Debug; no autoriza reutilizarlo
  como firma estable ni desinstalar para reemplazarlo.

- [ ] **TC.1** Todos los escenarios E2E pasan con fecha, SHA y evidencia redactada.
- [ ] **TC.2** Suite completa del repositorio verde contra PostgreSQL real antes de push.
- [ ] **TC.3** Runbooks, permisos y referencias actualizados; ADR aprobado antes de código.
- [ ] **TC.4** PR con propósito, decisiones, prueba manual y exclusiones. No marcar fase cerrada por despliegue técnico.
