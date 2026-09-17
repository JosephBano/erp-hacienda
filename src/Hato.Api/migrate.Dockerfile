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

# Las factories de diseño resuelven ConnectionStrings__HatoDb (o el legado
# CONNECTION_STRING) desde el entorno del contenedor. Así `dotnet ef` no recibe
# credenciales en argv, que sería visible mediante la lista de procesos.
CMD ["sh", "-c", "set -e; for project in src/Modules/*/Hato.Modules.*.Infrastructure/Hato.Modules.*.Infrastructure.csproj; do name=$(basename \"$(dirname \"$project\")\" | sed 's/Hato\\.Modules\\.\\([^.][^.]*\\)\\.Infrastructure/\\1/'); context=\"${name}DbContext\"; echo \"==> applying migrations for $project ($context)\"; dotnet ef database update --project \"$project\" --startup-project src/Hato.Api --context \"$context\"; done; echo '==> all migrations applied'"]
