# ADR-0002 — REST versionado en lugar de GraphQL

- **Estado:** Aceptado
- **Fecha:** (fundación)

## Contexto
GraphQL aporta cuando hay múltiples clientes con necesidades de datos divergentes y
equipos de front independientes. Aquí hay dos clientes (Angular admin, RN móvil) hechos
por la misma persona, y el móvil además habla mayormente con endpoints de sincronización.

## Decisión
API REST versionada (/api/v1), Problem Details para errores, DTOs por caso de uso.
Los handlers CQRS internos quedan agnósticos del protocolo.

## Alternativas
GraphQL/HotChocolate (descartado por ahora: complejidad extra en autorización, N+1,
caching y versionado sin beneficio presente). gRPC (innecesario para estos clientes).

## Consecuencias
+ Menos superficie que mantener; herramientas y caching triviales.
Reversa: si aparecen clientes con necesidades de datos realmente divergentes, GraphQL
puede añadirse como capa sobre los mismos handlers, sin reescritura.
