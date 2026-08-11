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

CMD ["sh", "-c", "set -e; export CONNECTION_STRING=\"Host=postgres;Port=5432;Database=hato;Username=hato;Password=$POSTGRES_PASSWORD\"; for project in src/Modules/*/Hato.Modules.*.Infrastructure/Hato.Modules.*.Infrastructure.csproj; do name=$(basename \"$(dirname \"$project\")\" | sed 's/Hato\\.Modules\\.\\([^.][^.]*\\)\\.Infrastructure/\\1/'); context=\"${name}DbContext\"; echo \"==> applying migrations for $project ($context)\"; dotnet ef database update --project \"$project\" --startup-project src/Hato.Api --context \"$context\" --connection \"$CONNECTION_STRING\"; done; echo '==> all migrations applied'"]
