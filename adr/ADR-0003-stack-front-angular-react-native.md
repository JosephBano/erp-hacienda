# ADR-0003 — Angular (web) y React Native (móvil)

- **Estado:** Aceptado
- **Fecha:** (fundación)

## Contexto
El desarrollador domina Angular y React Native. Se evaluó unificar en .NET
(Blazor/MAUI) para tener un solo lenguaje.

## Decisión
Mantener el stack dominado: Angular para el panel administrativo (excelente para ERP:
formularios complejos, estructura opinada) y React Native para la app de campo
(ecosistema offline-first maduro: SQLite/WatermelonDB).

## Alternativas
Blazor + MAUI (descartado: meses de curva de aprendizaje sin beneficio para la finca),
Flutter (descartado: tercer lenguaje).

## Consecuencias
+ Velocidad y confianza desde el día uno; REST sirve idéntico a ambos clientes.
− Dos ecosistemas front que mantener actualizados.
Reversa: solo si un cliente se vuelve inmantenible en su tecnología.
