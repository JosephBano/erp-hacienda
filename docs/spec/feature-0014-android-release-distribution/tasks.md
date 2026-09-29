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
- [ ] **T2.2** Reservar versionCode monotónico transaccional y validar manifiesto con firma/hash/SHA.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.
- [ ] **T2.3** Crear workflow de runners alojados y secrets por entorno; PR/fork sin firma prod.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.

## Bloque 3 — Solicitud y biblioteca

- [ ] **T3.1** Implementar Delivery, migraciones, permisos, idempotencia y auditoría.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.
- [ ] **T3.2** Implementar worker sin webhook público con GitHub App acotada y validación run/attempt.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.
- [ ] **T3.3** Importar APKs de forma atómica, limitar cuotas, conservar catálogo y copias externas por namespace.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.
- [ ] **T3.4** Aplicar retención con actual/última buena protegidas y publicaciones humanas auditadas.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.

## Bloque 4 — Web y operación

- [ ] **T4.1** Crear sección privada generar ambas, estados parciales, publicar/retirar y descarga autorizada.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.
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
