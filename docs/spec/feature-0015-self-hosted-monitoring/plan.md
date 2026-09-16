# plan.md — Integración y alojamiento de monitores

> [Spec](./spec.md) · [Tareas](./tasks.md) · [E2E](./test-e2e.md).

Investigación y paquete de infraestructura completados en
`home-server/docs/spec/hato-monitoring/{spec,plan,tasks,test-e2e}.md`, rama
`feature/hato-monitoring-spec`. Ese plan concreta stack, DB, archivos, puertos reservados
8450/8451 y reparación de recarga Caddy. No se han verificado puertos vivos ni desplegado.

1. `docs(ops): define self-hosted monitoring ownership`: aplicar ADR-0036 aceptado, inventariar
   recursos/puertos/versiones de home-server y leer sus instrucciones. Enlazar ramas/PRs.
2. En home-server, definir stack Healthchecks con DB persistente, proceso de envío de
   alertas, secretos, Caddy, backup y límites. Elegir rutas según estructura existente;
   no inventar comandos operativos antes de ese inventario. Probar aislamiento y reinicio.
3. En home-server, configurar check diario y mensual, SMTP y Beszel para VPS con grants
   mínimos. Medir recursos y registrar evidencia privada de entrega al destinatario.
4. En HATO, conectar `scripts/backup-status.sh` de feature-0013 con endpoint privado,
   sin cambiar reglas de validez ni retención. Configuración por entorno; URLs fuera de git.
5. Ensayar fallos, silencio, reinicio y restauración; actualizar runbooks en ambos repos.
   Abrir producción solo tras evidencia completa. Ejecutar checks correspondientes y
   suite exigida del repo antes de PR; no alterar servidores por aprobar documentación.

Dependencias: ADR → inventario home-server → servicio privado → integración → E2E →
compuerta de producción. ADR-0034 conserva políticas de backup; ADR-0036 sustituye únicamente
su elección de monitor alojado conforme aprobación registrada. No instalar dos receptores por descuido.
