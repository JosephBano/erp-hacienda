#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# Crea o reconstruye el espacio de trabajo privado de un trabajo académico.
#
#   ./scripts/tesis-init.sh <nombre-del-trabajo>
#   ./scripts/tesis-init.sh devops-guardrails
#
# Las plantillas (docs/tesis/plantillas/) se versionan, se comparten entre todos
# los trabajos y no contienen ningún dato real. Cada trabajo llenado vive en
# docs/tesis/privado/<nombre>/, y .gitignore excluye docs/tesis/privado/ por
# completo. Si esa carpeta se pierde, este script devuelve el esqueleto; el
# contenido llenado es responsabilidad de tu propio respaldo (no del respaldo del
# proyecto, que solo cubre la base de datos de la finca — ver docs/BACKUPS.md).
#
# Admite varios trabajos a propósito: el tema puede cambiar y no hay razón para
# que el andamiaje lo impida. Escribir dos a la vez sí es mala idea, pero eso lo
# decide quien escribe, no este script.
#
# Nunca sobrescribe: un archivo que ya existe se salta y se informa.
# ==============================================================================

if [[ $# -ne 1 ]]; then
  echo "uso: $0 <nombre-del-trabajo>" >&2
  echo "     el nombre va en kebab-case, p. ej.: devops-guardrails" >&2
  exit 2
fi

TRABAJO="$1"
if [[ ! "${TRABAJO}" =~ ^[a-z0-9]+(-[a-z0-9]+)*$ ]]; then
  echo "ERROR: '${TRABAJO}' no es kebab-case (minúsculas, dígitos y guiones)." >&2
  exit 2
fi

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PLANTILLAS_DIR="${REPO_ROOT}/docs/tesis/plantillas"
DESTINO_DIR="${REPO_ROOT}/docs/tesis/privado/${TRABAJO}"

if [[ ! -d "${PLANTILLAS_DIR}" ]]; then
  echo "ERROR: no existe ${PLANTILLAS_DIR}" >&2
  exit 1
fi

mkdir -p "${DESTINO_DIR}"

creados=0
saltados=0

for plantilla in "${PLANTILLAS_DIR}"/*.plantilla.md; do
  [[ -e "${plantilla}" ]] || continue
  base="$(basename "${plantilla}")"
  destino="${DESTINO_DIR}/${base/.plantilla.md/.md}"

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
echo "Tu trabajo vive en docs/tesis/privado/${TRABAJO}/ y git no lo ve. Respáldalo aparte."
echo "La guía de lectura no se copia: está en docs/tesis/plantillas/00-QUE-ES-UNA-TESIS.md"
