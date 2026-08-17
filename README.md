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
| 11 | [`spec/`](docs/spec/) | Especificaciones y planes de ejecución (ramas, tareas, pruebas) | Al abrir una fase o una rama |
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
│  ├─ spec/                   # especificaciones y planes de ejecución (features y fases)
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

Ninguna credencial vive en el repositorio, ni siquiera las de desarrollo: el `.env` local
es la **única fuente** del puerto y la contraseña del PostgreSQL. La cadena de conexión
sale de los *user secrets* de .NET (si corres el backend fuera de Docker) o de las
variables de entorno del servicio `api` (si lo corres dentro).

```bash
cp .env.example .env                  # completa POSTGRES_PASSWORD y, si hace falta, POSTGRES_PORT
docker compose up -d                  # PostgreSQL en localhost:${POSTGRES_PORT:-5432}
```

`POSTGRES_PORT` define el **puerto en tu máquina** mapeado al 5432 interno del contenedor.
Cámbialo solo si 5432 ya está ocupado en tu equipo (en este repo es habitual por
otros proyectos). El backend, dentro de la red de compose, habla con `postgres:5432`,
así que mover el puerto del host no toca ninguna línea del código del backend.

### Opción A — backend local, DB en contenedor

```bash
set -a; . ./.env; set +a              # trae POSTGRES_PASSWORD y POSTGRES_PORT del .env

dotnet user-secrets set "ConnectionStrings:HatoDb" \
  "Host=localhost;Port=${POSTGRES_PORT};Database=hato;Username=hato;Password=${POSTGRES_PASSWORD}" \
  --project src/Hato.Api

dotnet tool restore                   # dotnet-ef
dotnet build                          # compila la solución
dotnet test                           # unitarias + integración (Testcontainers)

dotnet ef database update \
  --project src/Modules/Livestock/Hato.Modules.Livestock.Infrastructure \
  --startup-project src/Hato.Api

dotnet run --project src/Hato.Api     # health check en http://localhost:5xxx/health
```

> **Si `dotnet run` falla con `MSB4166: Child node "X" exited prematurely`:** el
> build paralelo se quedó sin RAM. En máquinas con muchas CPUs y poca memoria
> libre, MSBuild arranca un child node por CPU y Roslyn los mata. Solución:
> [`scripts/dev-backend.sh`](scripts/dev-backend.sh) — un wrapper que aplica
> `MSBUILDDISABLENODEREUSE=1 -m:2` antes del `dotnet run`, separando build
> de ejecución para no repetir el costo en cada arranque:
>
> ```bash
> ./scripts/dev-backend.sh --urls "http://127.0.0.1:5282;http://100.101.240.44:5282"
> ```
>
> Variables reconocidas: `DOTNET_BUILD_PARALLELISM` (default `1`), `ASPNETCORE_URLS`.

### Opción B — backend y DB en contenedores (todo el stack)

```bash
docker compose up -d --build          # api + postgres, api en http://localhost:8080/health
bash scripts/smoke-api-container.sh   # verificación end-to-end del contenedor (sube, golpea /health, baja)
```

`ASPNETCORE_ENVIRONMENT=Development` y la cadena de conexión del `api` se fijan en
`docker-compose.yml`; los valores vienen del mismo `.env`. Para producción real (TLS,
JWT signing key, CORS restrictivo) ese archivo se sustituye por una variante — fuera
del alcance de este PR.

### Las pruebas de integración y su PostgreSQL

Corren contra un PostgreSQL de verdad, nunca InMemory (Art. 12, `AGENTS.md` regla 5): la
mitad de lo que verifican —el `CHECK` del ADR-0015, la validación de `jsonb`, el índice
único que hace idempotente el push, la CTE recursiva del pedigrí— no existe en un proveedor
falso. De dónde sale ese PostgreSQL sí es configurable:

- **Por defecto**: Testcontainers levanta un contenedor efímero por *fixture*. No hace falta
  configurar nada, pero necesita un demonio Docker capaz de crear redes bridge.
- **Con `HATO_TEST_POSTGRES`**: se usa un servidor que ya está corriendo. Cada *fixture*
  sigue creando su propia base y borrándola al terminar, así que el aislamiento es el mismo.

La segunda vía existe porque hay máquinas donde Testcontainers no arranca (Docker rootless,
sandboxes, algunas configuraciones de WSL) y el síntoma es siempre el mismo:
`failed to create endpoint testcontainers-ryuk-… operation not supported`. Cuando nadie del
equipo puede correr la suite localmente, cada arreglo se empuja a ciegas y el CI termina de
depurador — eso es lo que esta salida evita.

La credencial sale de tu `.env`, igual que todo lo demás: acá tampoco hay ninguna escrita
en el repositorio. El usuario `hato` del `docker compose` ya puede crear bases, que es lo
único que la variable necesita.

```bash
docker compose up -d                  # el mismo PostgreSQL de desarrollo

set -a; . ./.env; set +a              # trae POSTGRES_PASSWORD del .env
export HATO_TEST_POSTGRES="Host=127.0.0.1;Port=5432;Database=postgres;Username=hato;Password=$POSTGRES_PASSWORD"

dotnet test                           # ahora sin Testcontainers
```

El CI usa exactamente ese camino con un `services: postgres` (ver `.github/workflows/ci.yml`).
Si la variable no está definida, todo vuelve a Testcontainers sin tocar una línea de código.

Cada *fixture* borra su base al terminar. Si una corrida se interrumpe a la brava quedan
bases huérfanas; se barren así:

```bash
docker exec hato-postgres psql -U hato -d postgres -tAc \
  "SELECT datname FROM pg_database WHERE datname LIKE 'hato_test_%';" \
  | xargs -r -I{} docker exec hato-postgres psql -U hato -d postgres \
      -c 'DROP DATABASE IF EXISTS "{}" WITH (FORCE);'
```

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
