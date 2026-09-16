# syntax=docker/dockerfile:1.7

# Build context is the repository root (compose sets `context: .`).
# One-shot: aplica las migraciones EF Core de cada módulo contra el Postgres
# del compose y termina. La api y la web dependen de este servicio
# (`condition: service_completed_successfully`), así que nunca arrancan
# antes de que el esquema esté listo.

FROM mcr.microsoft.com/dotnet/sdk:8.0
WORKDIR /src

COPY .config ./.config
COPY . .

RUN dotnet tool restore

# BLOQUEADOR conocido (feature-0012-production-environment, Entrega 3): el
# `--connection "$CONNECTION_STRING"` de abajo deja la contraseña visible en
# `ps aux` de cualquier usuario del host. No se puede quitar todavía: `dotnet
# ef database update` usa, para cada módulo, el IDesignTimeDbContextFactory
# correspondiente (p. ej.
# src/Modules/Inventory/Hato.Modules.Inventory.Infrastructure/Persistence/InventoryDbContextFactory.cs)
# cuando no se pasa `--connection`, y esas factories solo leen
# `dotnet user-secrets` (ConfigurationBuilder().AddUserSecrets(...)), no
# variables de entorno — no llaman `.AddEnvironmentVariables()`. Sin
# `--connection`, caerían a "Host=localhost;Database=hato_migrations_design_time_only"
# y las migraciones fallarían contra el `postgres` del compose. Arreglarlo de
# raíz requiere tocar las 6 factories de los módulos (fuera del alcance de
# este archivo); queda documentado como bloqueador aislado en el reporte de
# esta entrega, no bloquea el resto de compose.production.yml.
CMD ["sh", "-c", "set -e; export CONNECTION_STRING=\"${CONNECTION_STRING:-Host=postgres;Port=5432;Database=hato;Username=hato;Password=$POSTGRES_PASSWORD}\"; for project in src/Modules/*/Hato.Modules.*.Infrastructure/Hato.Modules.*.Infrastructure.csproj; do name=$(basename \"$(dirname \"$project\")\" | sed 's/Hato\\.Modules\\.\\([^.][^.]*\\)\\.Infrastructure/\\1/'); context=\"${name}DbContext\"; echo \"==> applying migrations for $project ($context)\"; dotnet ef database update --project \"$project\" --startup-project src/Hato.Api --context \"$context\" --connection \"$CONNECTION_STRING\"; done; echo '==> all migrations applied'"]
