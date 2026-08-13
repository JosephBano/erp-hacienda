#!/usr/bin/env bash
# Levanta el backend local de HATO con flags de MSBuild conservadoras.
#
# Por qué existe: en este host (y en cualquier máquina con poca RAM relativa
# a la cantidad de CPUs), `dotnet build` con paralelismo alto arranca N child
# nodes (uno por CPU) y Roslyn se queda sin memoria — el kernel mata los
# procesos y `dotnet run` falla con `MSB4166: Child node "X" exited prematurely`.
# El agente del backend documentó esto en su commit (`feat(inventory): ...
# -m:1`) y este script lo aplica de forma consistente.
#
# Uso:
#   ./scripts/dev-backend.sh                     # usa los --urls por default
#   ./scripts/dev-backend.sh --urls "http://100.101.240.44:5282"  # IP Tailscale
#
# Variables de entorno reconocidas:
#   DOTNET_BUILD_PARALLELISM  (default: 1)        # número de child nodes
#   ASPNETCORE_URLS          (default: http://127.0.0.1:5282)
#
# Requisitos: .NET SDK 8, dotnet-ef instalado (`dotnet tool restore`).

set -euo pipefail

PARALLELISM="${DOTNET_BUILD_PARALLELISM:-1}"
URLS="${ASPNETCORE_URLS:-http://127.0.0.1:5282}"

# Si el usuario pasó --urls en la línea de comandos, respetamos esa.
for arg in "$@"; do
  case "$arg" in
    --urls=*|--urls)
      URLS=""
      ;;
  esac
done

echo "==> Build (MSBUILDDISABLENODEREUSE=1 -m:${PARALLELISM})..."
MSBUILDDISABLENODEREUSE=1 dotnet build src/Hato.Api/Hato.Api.csproj -m:"${PARALLELISM}" -v minimal

echo "==> Run (no rebuild) en ${URLS}..."
if [ -n "${URLS}" ]; then
  exec dotnet run --no-build --project src/Hato.Api --urls "${URLS}" "$@"
else
  exec dotnet run --no-build --project src/Hato.Api "$@"
fi
