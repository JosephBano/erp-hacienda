#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# Reconstruye el espacio de trabajo privado de la tesis desde las plantillas.
#
# Las plantillas (docs/tesis/plantillas/) se versionan y no contienen ningún dato
# real. El trabajo llenado vive en docs/tesis/privado/, que .gitignore excluye por
# completo. Si esa carpeta se pierde, este script devuelve el esqueleto; el
# contenido llenado es responsabilidad de tu propio respaldo (no del respaldo del
# proyecto, que solo cubre la base de datos de la finca — ver docs/BACKUPS.md).
#
# Nunca sobrescribe: un archivo que ya existe en privado/ se salta y se informa.
# ==============================================================================

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PLANTILLAS_DIR="${REPO_ROOT}/docs/tesis/plantillas"
PRIVADO_DIR="${REPO_ROOT}/docs/tesis/privado"

if [[ ! -d "${PLANTILLAS_DIR}" ]]; then
  echo "ERROR: no existe ${PLANTILLAS_DIR}" >&2
  exit 1
fi

mkdir -p "${PRIVADO_DIR}"

creados=0
saltados=0

for plantilla in "${PLANTILLAS_DIR}"/*.plantilla.md; do
  [[ -e "${plantilla}" ]] || continue
  base="$(basename "${plantilla}")"
  destino="${PRIVADO_DIR}/${base/.plantilla.md/.md}"

  if [[ -e "${destino}" ]]; then
    echo "  saltado (ya existe): ${destino#"${REPO_ROOT}/"}"
    saltados=$((saltados + 1))
  else
    cp "${plantilla}" "${destino}"
    echo "  creado:              ${destino#"${REPO_ROOT}/"}"
    creados=$((creados + 1))
  fi
done

echo
echo "Listo: ${creados} creado(s), ${saltados} saltado(s)."
echo "Tu tesis vive en docs/tesis/privado/ y git no la ve. Respáldala aparte."
