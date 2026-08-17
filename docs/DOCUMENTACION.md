# DOCUMENTACION.md — El sistema de documentación del proyecto

> Este documento responde **¿dónde va lo que voy a escribir?** Define qué documento existe,
> qué pregunta responde cada uno, y qué obliga a actualizarlo. Es la fuente de verdad de
> `docs/` — si vas a crear un documento nuevo y no aparece aquí, primero preguntate si de
> verdad responde una pregunta que ningún otro responde (D1 de
> `docs/spec/feature-0003-reestructura-documentacion/spec.md`).

## 1. La regla que gobierna todo

**Un documento existe si responde una pregunta que ningún otro responde.** Si dos documentos
responden la misma pregunta, uno sobra — es la falla concreta que produjo, en su momento, dos
`docs/BACKLOG.md` con contenido distinto y sin que ninguno mencionara al otro.

Antes de crear un archivo nuevo en `docs/`: buscá la pregunta en la tabla de la sec. 2. Si ya
está respondida, edita ese documento. Si no está, y la pregunta es real y duradera (no la
duda de una tarea puntual), agregá una fila a la tabla en el mismo commit que crea el
documento.

## 2. Taxonomía

| Documento | Pregunta que responde | Cambia cuando |
|---|---|---|
| `README.md` | ¿Cómo levanto y corro esto? | Cambia el arranque, el stack o las pruebas. |
| `SOUL.md` | ¿Por qué existe este proyecto? | Casi nunca. |
| `CONSTITUTION.md` | ¿Qué reglas no se rompen? | Con un ADR que la enmiende. |
| `AGENTS.md` | ¿Cómo trabaja un agente aquí? | Cambia una convención. |
| `PROTOCOLO-DE-TRABAJO.md` | ¿Cómo se lleva una rama de la idea al merge? | Cambia el ciclo o la exigencia de pruebas. |
| `DOCUMENTACION.md` | ¿Dónde va lo que voy a escribir? | Nace un tipo de documento nuevo. |
| `ROADMAP.md` | ¿En qué fase estamos y qué la cierra? | Se abre o cierra una fase. |
| `ARCHITECTURE.md` | ¿Cómo está partido el sistema? | Nace un módulo o cambia un límite. |
| `DATA-MODEL.md` | ¿Qué datos existen y por qué así? | Entra una migración estructural. |
| `SEGURIDAD.md` | ¿Cómo se protege y qué está expuesto? | Cambia auth, permisos o se halla un hueco. |
| `GLOSSARY.md` | ¿Cómo se llama esto en español y en código? | Aparece un término de dominio nuevo. |
| `LEGAL-ECUADOR.md` | ¿Qué exige la ley ecuatoriana? | Cambia la norma. |
| `BACKUPS.md` | ¿Cómo se respalda y se restaura? | Cambia la estrategia. |
| `docs/BACKLOG.md` | ¿Qué sabemos que falta y decidimos no hacer ahora? | Continuamente. |
| `adr/NNNN-*.md` | ¿Por qué se decidió esto y qué se descartó? | Nunca: un ADR se reemplaza, no se edita. |
| `spec/<x>/spec.md` | ¿Qué se construye y qué queda fijado? | Antes de implementar. |
| `spec/<x>/plan.md` | ¿En qué orden y en qué commits? | Al replanificar. |
| `spec/<x>/tasks.md` | ¿Qué falta exactamente? | Continuamente, durante la ejecución. |
| `spec/<x>/test-e2e.md` | ¿Cómo compruebo a mano que funciona? | Cambia el flujo de usuario. |
| `diagramas/*.mermaid` | ¿Cómo se ve esto? | Entra una migración o cambia un flujo. |

Son 20 entradas y cubren, sin resto, todo `.md` de la raíz del repositorio y de la raíz de
`docs/` (`AGENTS.md`, `README.md`; `ARCHITECTURE.md`, `docs/BACKLOG.md`, `BACKUPS.md`,
`CONSTITUTION.md`, `DATA-MODEL.md`, `GLOSSARY.md`, `LEGAL-ECUADOR.md`, `ROADMAP.md`,
`SOUL.md`), más las tres filas de patrón que cubren carpetas (`adr/`, `spec/<x>/`,
`diagramas/`). Verificable con `ls *.md` y `ls docs/*.md`: todo lo que devuelven aparece
arriba.

`PROTOCOLO-DE-TRABAJO.md` y `SEGURIDAD.md` están en la tabla porque esta misma rama los creó,
en los commits `f2a714c` y `614cebd` respectivamente. No fueron aspiracionales: eran
referencias hacia adelante dentro de un mismo trabajo, no una promesa sin fecha, y ya se
cumplieron.

## 3. Disparadores de actualización

La causa raíz de que la documentación dejara de ser navegable no era falta de contenido: era
que nada obligaba a tocarla cuando el código cambiaba. Estos cinco disparadores se agregan a
la checklist de autorrevisión de `PROTOCOLO-DE-TRABAJO.md` y son la parte operativa de este
sistema — sin ellos, la tabla de la sec. 2 es solo un índice que también envejece mal.

- **Entra una migración** → se revisa el DER del núcleo afectado y `DATA-MODEL.md`.
- **Nace un endpoint** → se revisa `SEGURIDAD.md` (qué permiso exige).
- **Nace un término de dominio** → `GLOSSARY.md` (ya es la regla del Art. 20 de
  `CONSTITUTION.md`).
- **Se cierra una fase** → retrospectiva en `ROADMAP.md`, `tasks.md` de la fase cerrado, y
  revisión de `docs/BACKLOG.md` para promover o descartar.
- **Se decide algo estructural** → ADR, antes de implementar.

## 4. Archivado

Un plan de una fase cerrada **no se borra**: su carpeta permanece con el `tasks.md` completo
como registro de lo que costó — es el motivo por el que, por ejemplo, `docs/spec/plan-0001-fase-3/`
sigue existiendo después de que la fase cierra. Lo que se archiva se marca en el encabezado
del documento, no se mueve de lugar ni se vacía.

## 5. Ver también

- `docs/plantillas/` — la forma de cada tipo de documento de plan (`spec.md`, `plan.md`,
  `tasks.md`, `test-e2e.md`, un ADR, un diagrama).
- `docs/spec/feature-0003-reestructura-documentacion/spec.md` sec. 5 — el diseño completo de esta
  taxonomía, con los hallazgos que la motivaron.
