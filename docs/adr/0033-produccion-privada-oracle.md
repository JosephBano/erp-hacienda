# ADR-0033 — Producción privada Oracle y despliegue restringido

- **Estado:** Aceptado.
- **Fecha:** 2026-09-16.
- **Fase:** Transversal.

## Contexto

**Aprobación registrada el 2026-09-16:** después de identificar expresamente ADR-0033
y ADR-0035 como pendientes, el propietario respondió «apruebo». Se acepta este diseño
de producción, incluyendo GHCR, helpers y privilegios. La aprobación anterior de backups
permanece registrada en [ADR-0034](./0034-backups-cifrados-drive.md).
La aprobación es documental: `hato-deploy`, configuración e implementación siguen pendientes.
Diseño: [spec 0012](../spec/feature-0012-production-environment/spec.md) y
[plan](../spec/feature-0012-production-environment/plan.md).

Existe staging doméstico y VPS Oracle comprobada por SSH. El propietario pide producción
privada con teléfonos incorporados al tailnet. home-server ya opera Caddy con TLS Tailscale.

## Decisión

Oracle aloja producción; staging permanece doméstico conforme ADR-0031. Acceso HTTPS por
Caddy y certificado del nombre ts.net de Oracle, sin Funnel. Identidades de empleados
individuales. Releases aprobadas de main, imágenes ARM64 por digest en GHCR construidas
en runners alojados. Usuario hato-deploy restringido a helper root-owned; validación
independiente de run/approval y sin socket Docker. Ver spec feature-0012 y políticas.

La premisa de ADR-0031 de que los teléfonos no estarán en tailnet se reemplaza para
producción; no se modifica el destino ni la política histórica de staging.

## Alternativas

Serve simplifica renovación pero agrega un patrón operativo diferente al conocido.
Producción doméstica mantiene riesgos de enlace y fallo compartido. Docker group al
usuario CI concede autoridad de root y no satisface el aislamiento pretendido.

## Consecuencias

Requiere gestionar renovación TLS, planes Tailscale y cuenta OCI recuperable. Un único
host no ofrece HA. Reabrir si acceso privado no resulta operable o se necesita continuidad
mayor. No se acepta disponibilidad garantizada del free tier.
