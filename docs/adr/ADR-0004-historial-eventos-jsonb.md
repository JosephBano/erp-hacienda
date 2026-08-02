# ADR-0004 — Historial como eventos inmutables (tabla append-only + JSONB)

- **Estado:** Aceptado
- **Fecha:** (fundación)

## Contexto
El valor central del sistema es el historial completo de cada animal (Art. 1 y 4).
Modelarlo como 15 tablas sueltas fragmenta la línea de tiempo; event sourcing puro
(reconstruir estado solo desde eventos) es sobreingeniería para una persona.

## Decisión
Híbrido pragmático: entidades con estado actual (Animal, Pregnancy, Lactation...) +
tabla `animal_events` append-only con `type`, `occurred_at`, `recorded_by`, `cost` y
`payload JSONB` validado por tipo de evento. Los eventos jamás se editan; correcciones
son eventos que referencian al original. Índices GIN sobre payload donde haga falta.

## Alternativas
Tablas por tipo de evento (fragmenta la cronología), event sourcing completo
(complejidad de proyecciones/replay injustificada).

## Consecuencias
+ Expediente y auditoría gratis; serie temporal lista para analítica/IA; agregar un tipo
de evento nuevo no requiere migración de esquema.
− Disciplina de validación del payload en Application; consultas sobre JSONB requieren
cuidado con índices.
Reversa: eventos de altísimo volumen/consulta pueden ganar tabla propia proyectada,
manteniendo el evento como fuente.
