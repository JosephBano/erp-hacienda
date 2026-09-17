# ADR-0036 — Monitorización autohospedada con Beszel y Healthchecks

- **Estado:** Aceptado.
- **Fecha:** 2026-09-16.
- **Fase:** Transversal.
- **Relación:** sustituye la elección Healthchecks.io alojado de ADR-0034;
  conserva respaldo, retención, custodia y objetivos ya aprobados.

## Contexto

Aprobación explícita del propietario el 2026-09-16: «si apruebo el adr'0036».
La implementación permanece pendiente. Plan de infraestructura documentado en
`home-server/docs/spec/hato-monitoring/`, rama `feature/hato-monitoring-spec`.

El propietario pide software open source sin dependencia de suscripción y ya utiliza
Beszel en home-server. Solicita registrar monitorización como spec separado. Acepta
alojamiento doméstico con batería; esto no garantiza conectividad ni supervisión del hogar.

## Decisión propuesta

Beszel supervisa recursos; Healthchecks autohospedado verifica ejecuciones ausentes.
Home-server posee instalación/TLS/datos/runbook; HATO emite señales de backup/restore.
Sin servicio Healthchecks.io contratado ni exposición pública. Véase
[spec 0015](../spec/feature-0015-self-hosted-monitoring/spec.md).

## Alternativas y consecuencias

Beszel solo no se asume receptor de pings de cron. Un script a medida evita servicio nuevo
pero añade mantenimiento de deduplicación/plazos/alertas. SaaS ofrece independencia del hogar
pero contradice la preferencia actual. Autohospedaje añade consumo y mantenimiento:
medir antes de aceptar despliegue. Caída del hogar deja temporalmente sin avisos; reabrir
si se exige detección de esa caída desde un observador independiente.
