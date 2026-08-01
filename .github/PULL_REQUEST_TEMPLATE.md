# Propósito

<!-- Qué resuelve este PR y por qué. Un PR = un propósito (AGENTS.md, regla 9). -->

## Decisiones tomadas

<!-- Entidades, eventos, endpoints o migraciones nuevas. Si hubo una decisión
     estructural, enlaza el ADR correspondiente en docs/adr/. -->

## Cómo probarlo manualmente

<!-- Pasos concretos: endpoint, pantalla, dato de ejemplo. -->

## Qué NO incluye

<!-- Alcance explícitamente dejado fuera, para que el revisor no lo busque. -->

---

## Checklist constitucional

- [ ] Rama `feature/*` desde `develop`; no toqué `main` ni `develop` directamente (Art. 13).
- [ ] Commits en formato Conventional Commits, en inglés, con scope de módulo.
- [ ] Pruebas incluidas: dominio → unitarias; persistencia/API → integración con
      Testcontainers contra PostgreSQL real (Art. 12).
- [ ] Suite completa verde localmente (`dotnet test`).
- [ ] Ningún borrado físico de datos ni edición de eventos históricos (Art. 1).
- [ ] Sin `if`/`switch` por especie, raza o producto en el dominio (Art. 8).
- [ ] Dinero en `decimal`, cantidades con unidad, fechas persistidas en UTC (Art. 10).
- [ ] Cambios de esquema mediante migración EF Core nueva; ninguna migración ya
      mergeada fue editada (Art. 15).
- [ ] Términos nuevos agregados a `docs/GLOSSARY.md` antes de usarlos en código (Art. 20).
- [ ] Dependencias nuevas: ninguna, o justificadas en un ADR aprobado.
