# tasks.md — Checklist de implementación

> Pendiente de ejecución. [Spec](./spec.md) · [Plan](./plan.md) · [E2E](./test-e2e.md).
> Políticas comunes: [POLITICAS-OPERACION](../../POLITICAS-OPERACION.md).
> Ninguna casilla implica autorización para borrar datos reales ni instalación ya realizada.

## Bloque 1 — Políticas y cuentas

- [x] **A1** Registrar aprobación del propietario a la política de respaldo, retención y
  custodia. **Evidencia:** ADR-0034 aceptado el 2026-09-16. Las tareas operativas siguientes
  permanecen pendientes; esta casilla no acredita cuentas, scripts ni backups funcionando.

- [ ] **T1.1** Aprobar ADR rclone/Drive/Healthchecks y retención de POLITICAS-OPERACION.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.
- [ ] **T1.2** Configurar carpeta exclusiva y OAuth de la cuenta de backups; probar renovación desatendida y recuperar crypt desde copia offline.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.
- [ ] **T1.3** Configurar checks externos y email; guardar ping URLs como secretos.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.

## Bloque 2 — Dump y restore

- [ ] **T2.1** Corregir scripts a formato custom, checksums obligatorios y archivos parciales protegidos.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.
- [ ] **T2.2** Crear hato-backup sin Docker; instalar helper de dump root-owned sin argumentos arbitrarios y rol lector.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.
- [ ] **T2.3** Crear manifiesto consistente y guardas de destino para restore aislado; ejecutar contra PostgreSQL real.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.

## Bloque 3 — Upload y poda

- [ ] **T3.1** Implementar copy cifrado, verificación remota y marcador complete; probar reintentos.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.
- [ ] **T3.2** Implementar retención por IDs propios, dry-run y protección última copia; sin sync/purge global.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.
- [ ] **T3.3** Medir tamaño, cuota y papelera; probar insuficiencia de espacio y token revocado.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.

## Bloque 4 — Automatización y recuperación

- [ ] **T4.1** Instalar timers persistentes y lock; enlazar /fail y éxito solo después de copia comprobada.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.
- [ ] **T4.2** Programar restore mensual aislado y alerta al vencer 35 días; dump diario vence a 26 horas.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.
- [ ] **T4.3** Documentar restauración de módulos, permisos y artefactos asociados; medir RPO/RTO.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.
- [ ] **T4.4** Ejecutar test-e2e y suite completa antes de PR; no adjuntar dumps al CI público.
  **Terminado:** evidencia reproducible de la acción y resultado esperado del spec archivada
  en el PR o inventario privado; los escenarios E2E relacionados pasan sin secretos en logs.

## Cierre

- [ ] **ONB.1** Acompañar alta/selección de cuenta de backups y carpeta backups-hato-erp.
  **Terminado:** folder ID/propiedad verificados en inventario privado; sin correo en git.
- [ ] **ONB.2** Acompañar OAuth, crypt y recuperación offline según PREFLIGHT-PRODUCCION.
  **Terminado:** fixture cifrada subida, descargada y comprobada desde configuración recuperada.
- [ ] **ONB.2A** Crear cliente OAuth propio de rclone bajo la cuenta de backups y validar
  consentimiento desatendido. **Terminado:** client ID compartido no se usa, secretos están
  fuera de git y el refresh token sigue operativo tras la ventana de prueba definida.
- [ ] **ONB.3** Verificar cuota real, renovación OAuth y email de alertas autohospedadas.
  **Terminado:** resultados privados redactados y E2E-2/3/6/8 en verde.
- [ ] **NAME.1** Implementar namespaces/nombres del spec y retención pre-release.
  **Terminado:** prueba de IDs únicos, UTC, parciales, monthly y cuota conjunta con monitor/APK.

- [ ] **CAP.1** Implementar límite compartido Drive de 300 GB decimales y alertas
  a 210/255 GB. **Terminado:** E2E-8 verifica concurrencia, cuota global y copia protegida.

- [ ] **TC.1** Todos los escenarios E2E pasan con fecha, SHA y evidencia redactada.
- [ ] **TC.2** Suite completa del repositorio verde contra PostgreSQL real antes de push.
- [ ] **TC.3** Runbooks, permisos y referencias actualizados; ADR aprobado antes de código.
- [ ] **TC.4** PR con propósito, decisiones, prueba manual y exclusiones. No marcar fase cerrada por despliegue técnico.
