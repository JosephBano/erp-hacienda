# ADR-0001 — Monolito modular, no microservicios

- **Estado:** Aceptado
- **Fecha:** (fundación)

## Contexto
Un solo desarrollador, proyecto de años, dominio amplio (12+ módulos). Los microservicios
exigen orquestación, observabilidad distribuida, redes, deploys múltiples y debugging
distribuido: costo operativo inasumible para una persona.

## Decisión
Monolito modular en .NET: módulos con Clean Architecture propia, comunicados solo por
contratos públicos y eventos de dominio (MediatR). Un deploy. Base de datos única con
esquemas separados por módulo si se desea aislamiento adicional.

## Alternativas
Microservicios (descartado: costo operativo), monolito clásico en capas sin fronteras de
módulo (descartado: se degrada a barro con los años).

## Consecuencias
+ Simplicidad operativa, refactors baratos, transacciones simples.
− Exige disciplina en fronteras (Art. 6); la tentación de atajos existe.
Reversa: si un módulo demuestra necesidad real de escalar o desplegarse aparte, se
extrae — posible justamente porque las fronteras existieron desde el día uno.
