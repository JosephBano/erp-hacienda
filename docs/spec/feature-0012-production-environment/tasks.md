# tasks.md — Checklist de implementación

> Pendiente de ejecución. [Spec](./spec.md) · [Plan](./plan.md) · [E2E](./test-e2e.md).
> Políticas comunes: [POLITICAS-OPERACION](../../POLITICAS-OPERACION.md).
> Ninguna casilla implica autorización para borrar datos reales ni instalación ya realizada.

## Bloque 1 — Preflight y decisiones

- [ ] **T1.1** Aprobar ADR de producción y revisar condiciones OCI/Tailscale; registrar titular y recuperación privadamente.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.
- [ ] **T1.2** Inventariar VPS, hostname, fuente del piloto y dispositivos; comprobar control de cuenta y acceso de recuperación.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.
- [ ] **T1.3** Verificar CI de develop y abrir rama de implementación; conservar cambios ajenos.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.

## Bloque 2 — Acceso y usuario

- [ ] **T2.1** Instalar Tailscale desde repositorio oficial verificado y unir VPS con identidad del propietario; registrar hostname privado.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.
- [ ] **T2.2** Aplicar grants: empleado HTTPS, administrador SSH/HTTPS, CI solo despliegue. Verificar una segunda sesión antes de cerrar SSH público.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.
- [ ] **T2.3** Crear hato-deploy, claves dedicadas y helpers root-owned según spec; validar sshd y sudoers.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.
- [ ] **T2.4** Implementar comprobación independiente de run/attempt/SHA/approval/artifact; rechazar evidencia incompleta y replay.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.

## Bloque 3 — Stack y secretos

- [ ] **T3.1** Crear compose de producción y bindings privados; probar IPv4/IPv6 y ausencia de exposición DB.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.
- [ ] **T3.2** Separar roles runtime/migrador/backup; montar secretos protegidos sin argv ni logs.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.
- [ ] **T3.3** Configurar admin inicial de un solo uso, JWT propio, rotación de logs y mantenimiento.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.
- [ ] **T3.4** Construir imágenes ARM64 por digest en runner y documentar custodia GHCR.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.

## Bloque 4 — Release y apertura

- [ ] **T4.1** Implementar workflow de main con release validada, approval, lock, backup y smoke del SHA.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.
- [ ] **T4.2** Completar restauración externa de feature-0013 y medir RPO/RTO.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.
- [ ] **T4.3** Ensayar corte de datos con origen preservado y outboxes conciliados; aplicar compatibilidad de POLITICAS-OPERACION.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.
- [ ] **T4.4** Ejecutar test-e2e completo y suite del repositorio; registrar evidencia antes de PR y apertura.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.

## Cierre

- [ ] **SRC.1** Identificar API → host → DB → volumen reales y acta de origen mediante
  PREFLIGHT-PRODUCCION.md. **Terminado:** responsable confirma UUID/eventos conocidos y
  outboxes pendientes; restore de ensayo verificado antes de autorizar corte.

- [ ] **TLS.1** Crear Caddyfile production y timer semanal de tailscale cert con recarga
  por socket Unix protegido. **Terminado:** E2E-8 pasa y clave no accesible por deploy.
- [ ] **TLS.2** Añadir alerta de certificado a menos de 21 días y recuperación ante fallo.
  **Terminado:** alerta recibida con fixture y certificado anterior conservado tras fallo.

- [ ] **TC.1** Todos los escenarios E2E pasan con fecha, SHA y evidencia redactada.
- [ ] **TC.2** Suite completa del repositorio verde contra PostgreSQL real antes de push.
- [ ] **TC.3** Runbooks, permisos y referencias actualizados; ADR aprobado antes de código.
- [ ] **TC.4** PR con propósito, decisiones, prueba manual y exclusiones. No marcar fase cerrada por despliegue técnico.
