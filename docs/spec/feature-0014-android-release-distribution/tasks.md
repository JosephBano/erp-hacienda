# tasks.md — Checklist de implementación

> Pendiente de ejecución. [Spec](./spec.md) · [Plan](./plan.md) · [E2E](./test-e2e.md).
> Políticas comunes: [POLITICAS-OPERACION](../../POLITICAS-OPERACION.md).
> Ninguna casilla implica autorización para borrar datos reales ni instalación ya realizada.

## Bloque 1 — Identidad y contrato

- [ ] **T1.1** Aprobar ADR Delivery/GitHub App/EAS y registrar permisos/términos en glosario.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.
- [ ] **T1.2** Inventariar package/firma/versionCode de teléfonos reales; recuperar keystore sin publicar secretos.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.
- [ ] **T1.3** Definir capacidades/API soportadas con actual y anterior durante 90 días mínimo según política.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.

## Bloque 2 — Build y firma

- [ ] **T2.1** Separar variantes stage/prod con paquetes y URLs correctos, APK release sin Metro.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.
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
