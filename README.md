# Proyecto HATO

[![CI](../../actions/workflows/ci.yml/badge.svg)](../../actions/workflows/ci.yml)

> ERP hacienda para una finca en Ecuador. Un desarrollador, años de horizonte, cero prisa,
> máxima disciplina. Los documentos de `docs/` son la memoria del proyecto: para mí en el
> año 3, y para cualquier agente de IA que trabaje en el repo.
>
> *(HATO es nombre de trabajo — "hato" = rebaño/conjunto de ganado. Renómbralo cuando
> encuentres el definitivo; solo actualiza estos docs al hacerlo.)*

**Estado: Fase 0 — Fundaciones (en curso).** Ver [`ROADMAP.md`](docs/ROADMAP.md).

## El proyecto en tres frases

1. **Todo es un evento**: la vida de la finca es una secuencia inmutable de hechos
   fechados; perder datos es el único fallo imperdonable.
2. **Lo específico es dato**: especies, productos, recetas y roles se configuran, no se
   programan — así leche→queso o vacas→equinos no reescriben el core.
3. **Cada fase termina usándose en la finca**: el avance se mide en uso real; lo demás
   es entretenimiento.

## Orden de lectura

| # | Documento | Qué es | Cuándo releerlo |
|---|---|---|---|
| 1 | [`SOUL.md`](docs/SOUL.md) | Por qué existe esto y qué es el éxito | Cuando dude o me canse |
| 2 | [`CONSTITUTION.md`](docs/CONSTITUTION.md) | Las reglas no negociables (20 artículos) | Antes de toda decisión grande |
| 3 | [`AGENTS.md`](AGENTS.md) | Protocolo para agentes de IA (y para mí) | Al iniciar sesión de trabajo con IA |
| 4 | [`GLOSSARY.md`](docs/GLOSSARY.md) | Lenguaje ubicuo ES ↔ EN del dominio | Al modelar cualquier cosa nueva |
| 5 | [`ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Módulos, stack y decisiones de modelado | Al empezar cada módulo |
| 6 | [`DATA-MODEL.md`](docs/DATA-MODEL.md) | Modelo de datos del core (fases 1–2) + diagramas | Al tocar el esquema |
| 7 | [`ROADMAP.md`](docs/ROADMAP.md) | Fases 0–7 con criterios de salida | Cada semana (¿en qué fase estoy?) |
| 8 | [`LEGAL-ECUADOR.md`](docs/LEGAL-ECUADOR.md) | Agrocalidad/SIFAE, ARCSA, SRI, IESS, LOPDP | Antes de cada fase que lo toque |
| 9 | [`BACKUPS.md`](docs/BACKUPS.md) | Estrategia de respaldos y restauración probada | Antes de declarar producción |
| 10 | [`adr/`](docs/adr/) | Decisiones de arquitectura (19 + plantilla) | Antes de contradecir una |
| 11 | [`planes/`](docs/planes/) | Planes de ejecución por fase (ramas, tareas, pruebas) | Al abrir una fase o una rama |
| 12 | [`diagramas/`](docs/diagramas/) | Diagramas ER completos en Mermaid, por núcleo | Al tocar el esquema |

## Stack

.NET 8 (C#) + EF Core + PostgreSQL en el backend — monolito modular, Clean Architecture,
CQRS ligero con MediatR. Angular para el panel web (Fase 1), React Native offline-first
para la app de campo (Fase 3). Detalles y justificación en
[`ARCHITECTURE.md`](docs/ARCHITECTURE.md).

## Estructura del repositorio

```
.
├─ AGENTS.md                  # protocolo para agentes de IA (los lee automáticamente)
├─ Hato.sln
├─ Directory.Build.props      # net8.0, nullable, warnings como errores
├─ docker-compose.yml         # PostgreSQL 16 local
├─ docs/                      # SOUL, CONSTITUTION, ARCHITECTURE, DATA-MODEL…
│  ├─ adr/                    # decisiones de arquitectura: NNNN-titulo-en-kebab.md
│  ├─ planes/                 # planes de ejecución por fase
│  └─ diagramas/              # diagramas ER completos (.mermaid)
├─ src/
│  ├─ Hato.Api/               # composición: DI, auth, endpoints de todos los módulos
│  ├─ Shared/Hato.SharedKernel/
│  └─ Modules/
│     └─ Livestock/{Domain, Application, Infrastructure, Contracts}
├─ tests/                     # <Módulo>.UnitTests · <Módulo>.IntegrationTests
└─ clients/
   ├─ admin-web/              # Angular          (llega en Fase 1)
   └─ field-app/              # React Native     (llega en Fase 3)
```

Los módulos restantes (Breeding, Health, Production, Inventory, Sales, Accounting,
People…) nacen bajo `src/Modules/` con la misma forma, cada uno cuando su fase lo pida.

## Puesta en marcha

Requisitos: .NET SDK 8, Docker.

Ninguna credencial vive en el repositorio, ni siquiera las de desarrollo: la contraseña
de PostgreSQL sale de tu `.env` local y la cadena de conexión, de los *user secrets*
de .NET.

```bash
cp .env.example .env                  # y elige tu contraseña local
docker compose up -d                  # PostgreSQL en localhost:5432

dotnet user-secrets set "ConnectionStrings:HatoDb" \
  "Host=localhost;Port=5432;Database=hato;Username=hato;Password=<la del .env>" \
  --project src/Hato.Api

dotnet tool restore                   # dotnet-ef
dotnet build                          # compila la solución
dotnet test                           # unitarias + integración (Testcontainers)

dotnet ef database update \
  --project src/Modules/Livestock/Hato.Modules.Livestock.Infrastructure \
  --startup-project src/Hato.Api

dotnet run --project src/Hato.Api     # health check en /health
```

Las pruebas de integración levantan su propio PostgreSQL efímero con Testcontainers, así
que necesitan Docker corriendo pero **no** dependen del `docker compose` de desarrollo.

## Cómo se contribuye

GitFlow: `main` (releases etiquetadas) ← `develop` ← `feature/*`. Nadie commitea directo
a `main` ni a `develop` (Art. 13). Commits en
[Conventional Commits](https://www.conventionalcommits.org/) con scope de módulo
(`feat(livestock): add weaning event`). Todo PR lleva pruebas y CI verde (Art. 12); la
plantilla de PR incluye el checklist constitucional.

Antes de abrir un PR, lee [`AGENTS.md`](AGENTS.md) — aplica igual a humanos y a agentes.

## Licencia

[MIT](LICENSE) © 2026 Joseph Baño. El código es libre de reutilizar; los datos de la
finca, obviamente, no viven aquí.
