# ADR-0035 — Dos canales Android firmados y catálogo privado

- **Estado:** Aceptado.
- **Fecha:** 2026-09-16.
- **Fase:** Transversal / móvil.

## Contexto

**Aprobación registrada el 2026-09-16:** el propietario respondió «apruebo» tras identificar
ADR-0033 y ADR-0035 como decisiones pendientes. Se acepta este diseño completo, incluido
Delivery, GitHub App y pipeline de firma. La retención y copia externa ya estaban aprobadas
en [ADR-0034](./0034-backups-cifrados-drive.md). No se acredita implementación ni se infiere
la identidad/firma de las aplicaciones instaladas: ese inventario sigue pendiente.
Diseño: [spec 0014](../spec/feature-0014-android-release-distribution/spec.md) y
[plan](../spec/feature-0014-android-release-distribution/plan.md).

Preview y production comparten URL; existe un solo package y production genera AAB.
El propietario solicita dos APKs descargables desde web y promoción a estable.

## Decisión

Stage desde develop, prod desde release main aprobada; stable exige validación humana.
Packages/firma persistentes por canal, preservar identidad prod ya instalada. Delivery
como módulo operativo, solicitudes persistidas y worker con GitHub App limitada al repo;
GitHub Actions alojado y EAS local construyen APK release. No webhook público ni PAT en
navegador. Catálogo autenticado y copia cifrada privada de binarios retenidos.
VersionCode monotónico por package; actualización conserva SQLite/outbox. Compatibilidad
actual/anterior por al menos 90 días y retiro condicionado a conciliación. Ver feature-0014.

## Alternativas

Mismo package pisa staging/producción. Release pública filtra distribución; ejecutar builds
en VPS compite con DB. EAS remoto es alternativa si ensayo local no cumple recursos, con
revisión de costos y custodia antes de cambiar. La estabilidad no se deduce del nombre main.

## Consecuencias

Nuevo módulo, migraciones y custodia de firmas; GitHub App Actions write tiene autoridad
amplia en el repo aunque wrapper limite workflow. Pérdida de firma compromete actualización.
Reabrir al adoptar Play Store, iOS o cambiar límites de capacidad/costo.
