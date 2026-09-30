# plan.md — Integración y alojamiento de monitores

> [Spec](./spec.md) · [Tareas](./tasks.md) · [E2E](./test-e2e.md).

El receptor Healthchecks está desplegado y versionado en
`home-server/docs/spec/hato-monitoring/{spec,plan,tasks,test-e2e}.md`, rama
`feature/hato-monitoring-spec`. Healthchecks, PostgreSQL persistente y Caddy 8450/8451 están
activos; `backup-prod` y `restore-prod` reciben pings de éxito. La entrega de alertas por correo
y ausencia de ping se verificó con checks efímeros (confirmado por el propietario el 2026-09-29);
la matriz de acceso privado (E2E-1), recarga TLS y reinicio de stack (E2E-3), respaldo y restauración
aislada del monitor (E2E-4), y contrato de backups (E2E-2) fueron verificados el 2026-09-30.
El agente Beszel para Oracle VPS está configurado en `ops/production/beszel-agent.compose.yml`
escuchando estrictamente en la IP de tailnet. La medición de 24 horas de recursos (T7) se encuentra
en ejecución mediante timer del sistema, concluyendo el 2026-10-01 02:42 UTC.

1. ADR-0036 aprobado; preflight e investigación de home-server completados.
2. Healthchecks, persistencia, Caddy y secreto de API desplegados y versionados en la rama
   `feature/hato-monitoring-spec` del repositorio `home-server`. Toda configuración de monitor
   se custodia allí sin duplicarla en HATO.
3. Checks de backup/restore, canales de email, ping URLs privados y emisores conectados y
   probados satisfactoriamente.
4. Matriz de acceso, recarga TLS, agente Beszel, respaldo y recuperación en aislamiento
   ejecutados y documentados. Medición continua de recursos de 24 horas iniciada.
5. Evidencia conciliada en ambos repositorios; la compuerta de apertura de producción permanece
   cerrada hasta completar 0012/0013 y sus E2E correspondientes.


Dependencias: ADR → inventario home-server → servicio privado → integración → E2E →
compuerta de producción. ADR-0034 conserva políticas de backup; ADR-0036 sustituye únicamente
su elección de monitor alojado conforme aprobación registrada. No instalar dos receptores por descuido.
