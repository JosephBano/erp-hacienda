# ADR-0005 — Móvil offline-first con sincronización

- **Estado:** Aceptado
- **Fecha:** (fundación)

## Contexto
Los registros críticos (ordeño 5 AM, tratamientos, partos) ocurren en potreros sin señal.
Si la app exige conexión, los empleados vuelven al cuaderno y el proyecto muere en la
práctica (riesgo #2 identificado).

## Decisión
React Native con base local (WatermelonDB/SQLite). UUIDs generados en cliente.
Sincronización pull/push contra endpoints `/sync` idempotentes con `updated_at` y
borrados lógicos. Los eventos son append-only ⇒ los conflictos reales son mínimos;
para entidades editables: last-write-wins + bitácora de conflictos revisable en el panel.

## Alternativas
App online con caché (descartado: registrar es la función principal y debe funcionar
siempre), PWA (descartado: peor historia offline y de hardware en Android de gama media).

## Consecuencias
+ Adopción real en campo; resiliencia total a la conectividad rural.
− Es la pieza técnica más difícil del proyecto: merece su propia fase (Fase 3) y pruebas
de sincronización dedicadas (incluyendo reintentos y duplicados).
