# plan.md — Ejecución de la rama `feature/livestock-individual-tagging`

> **Qué es este documento.** Cómo se hace el trabajo que [`spec.md`](./spec.md) decidió:
> orden de los commits, qué archivos toca cada uno, qué pruebas exige y cómo se mergea.
> Desglose ejecutable en [`tasks.md`](./tasks.md), verificación en [`test-e2e.md`](./test-e2e.md).
> Las decisiones no se relitigan acá — si algo no cuadra, se corrige el spec primero.

**Objetivo:** operar con individuos identificados por arete, conservando el historial y la
capacidad de manejo por grupo.

**Enfoque:** una rama, **cinco commits**, más **dos compuertas**. La Compuerta 0 puede
recortar el alcance de la rama entera: hasta que se responda, no se sabe si hay una
transición de aretado que implementar. El punto de no retorno es el commit 4, que cambia el
contrato de parto.

**Spec:** [`spec.md`](./spec.md).

**Depende de:** [0004](../feature-0004-field-app-sync-reliability/spec.md) y
[0005](../feature-0005-field-app-activity-validation/spec.md). La experiencia visual se
integra después en [0010](../feature-0010-field-app-redesign/spec.md), que **no** es
requisito previo.

---

## Restricciones globales

- **El arete no es clave primaria** (D1). Se usan `Animal`, `AnimalIdentifier` y `FarmTag`,
  con vigencia e historial. Perder el arete no elimina al animal.
- **Sin correspondencia demostrable no hay asignación histórica** (D4). No se ejecuta con
  coincidencias de orden, peso o cantidad. `ADR-0015` lo prohíbe sobre animales ya mezclados
  (`docs/adr/0015-lote-por-conteo.md:152`), y `BulkTagging` del glosario lo repite.
- **Nunca fusionar animales automáticamente** (spec sec. 4). Una ambigüedad se presenta; no
  se resuelve sola.
- **No se reconstruyen genealogías de animales ya mezclados** (spec sec. 1).
- **La falta temporal de arete no bloquea registrar un hecho real** (D5, art. 3).
- **Alimento sigue por grupo** (spec sec. 4). Un pesaje muestral no se convierte en pesos
  individuales inventados; una baja por cantidad no selecciona animales al azar.
- **Sin dependencias nuevas** (regla dura 2). No entra lector RFID/QR ni impresión.
- **Pruebas de backend contra PostgreSQL real**, nunca InMemory (regla 5).

---

## Índice

1. [Compuerta 0 — Momento del aretado](#compuerta-0--momento-del-aretado)
2. [Commit 1 — Buscar por todos los identificadores vigentes](#commit-1--buscar-por-todos-los-identificadores-vigentes)
3. [Commit 2 — Ambigüedad explícita](#commit-2--ambigüedad-explícita)
4. [Commit 3 — Aretar y reemplazar en campo](#commit-3--aretar-y-reemplazar-en-campo)
5. [Compuerta 1 — Identidad de la cría](#compuerta-1--identidad-de-la-cría)
6. [Commit 4 — La cría existe antes del primer sync](#commit-4--la-cría-existe-antes-del-primer-sync)
7. [Commit 5 — Convivencia de individual y conteo](#commit-5--convivencia-de-individual-y-conteo)
8. [Orden, dependencias y puntos de no retorno](#orden-dependencias-y-puntos-de-no-retorno)
9. [Cómo se prueba](#cómo-se-prueba)
10. [Descripción del PR](#descripción-del-pr)

---

## Compuerta 0 — Momento del aretado

**Nada se escribe hasta cerrar esto.** Es la pregunta que más recorta el alcance de esta rama
(`spec.md` sec. 5). El dueño declaró «todos ellos van a tener un arete»; falta saber **cuándo**.

Preguntar y registrar:

1. ¿El arete se pone al nacer, al ingresar, o a un lote ya mezclado?
2. Si es a un lote ya mezclado: ¿alguien puede reconocer físicamente qué animal corresponde a
   qué fila del sistema?
3. ¿Qué tipo de arete: código propio, SIFAE u otro? El dueño **no** dijo que todos serán
   identificadores oficiales.
4. ¿Cuál es el alcance de unicidad esperado: finca, tipo, reutilización temporal?

- **Camadas nuevas identificables** → la rama procede completa. Es el caso que los commits
  1 a 5 cubren.
- **Lote antiguo ya mezclado** → **detenerse**. `ADR-0015` prohíbe adjudicar filas antiguas a
  animales mezclados, y D4 lo confirma. Esa transición necesita decisión documentada del dueño
  sobre inventario físico y linaje desconocido; **no entra en esta rama**.
- **Sin respuesta** → se implementan los commits 1, 2 y 3, que no dependen de la transición,
  y los commits 4 y 5 esperan.

## Commit 1 — Buscar por todos los identificadores vigentes

`feat(field-app): search animals by every active identifier, not just the label`

**Por qué primero:** es el recorrido principal de esta operación (D3) y no depende de ninguna
compuerta ni de cambios de contrato.

**Archivos:**
- `clients/field-app/src/services/herdQueries.ts:47` — hoy construye una etiqueta con nombre y
  **un** identificador preferido. Buscar sobre esa etiqueta deja fuera a los demás.
- `clients/field-app/src/screens/AnimalSubjectScreen.tsx:65` — busca sobre `label`; pasa a
  buscar sobre nombre e identificadores vigentes.
- Mostrar arete destacado, sexo y grupo para distinguir entre candidatos (D3).

**Verificación:** un animal con dos identificadores vigentes se encuentra por cualquiera de
los dos. Los **ceros iniciales se conservan**: buscar `007` no es lo mismo que buscar `7`.
Coincidencia exacta y parcial se distinguen.

## Commit 2 — Ambigüedad explícita

`feat(field-app): surface ambiguous tag matches instead of picking one`

**Por qué acá:** consume la búsqueda del commit 1. `AnimalIdentifierConfiguration.cs:24`
garantiza unicidad por animal/tipo vigente, **no** que dos animales no compartan valor de
arete; y `AssignAnimalIdentifierCommand.cs:17` no busca el mismo valor en otros animales. La
ambigüedad es posible por diseño actual.

**Archivos:**
- El selector de animal en el cliente: una búsqueda con varios resultados exige **selección
  consciente**. Nunca se elige automáticamente.
- Detectar y presentar el caso; **no fusionar animales** bajo ninguna circunstancia.

**Verificación:** dos animales con el mismo valor de arete producen una pantalla de
desambiguación con datos suficientes para distinguirlos. Ninguna ruta los fusiona.

**Fuera de alcance:** garantizar unicidad concurrente en base de datos. Exige ADR, migración
nueva y revisión no destructiva de datos existentes (spec sec. 4). Se anota en `BACKLOG.md`.
La política de duplicados y su normalización —finca/tipo, reutilización, mayúsculas— sigue
sin decidir; hasta entonces, el diseño mínimo detecta y presenta.

## Commit 3 — Aretar y reemplazar en campo

`feat(field-app): assign and replace tags offline with an idempotent push`

**Archivos:**
- Servicio móvil nuevo para la operación de identificación, encolada en el outbox.
- Ruta de push correspondiente en `src/Hato.Api/Sync/PushSyncCommands.cs`, apoyada en
  `AssignAnimalIdentifierCommand`. El endpoint web actual por sí solo **no** cumple el flujo
  de campo (spec sec. 4).
- El cambio de arete **conserva la identificación anterior y sus fechas**.
- Buscar un arete anterior puede ofrecer historial claramente señalado, **nunca** presentarlo
  como identificación vigente.

**Verificación:** asignar y reemplazar sin conexión, sincronizar dos veces la misma operación
y comprobar que el resultado es idéntico. El histórico de identificadores queda completo.

**Si el commit 1 del arnés de contrato de [0004](../feature-0004-field-app-sync-reliability/plan.md)
ya está mergeado**, el tipo de operación nuevo debe entrar en su fixture: la comprobación de
completitud fallará si no.

## Compuerta 1 — Identidad de la cría

**Antes del commit 4.** D6 exige que la cría reciba registros sin red inmediatamente después
de nacer, con identidad generada en cliente que se conserve al sincronizar.

Hoy `birthService.ts:79` envía las crías **sin UUID propio** y
`RecordBirthingCommand.cs:125` las registra en el servidor. Cambiar eso altera el contrato de
parto y las dependencias del outbox.

- El spec es explícito: **«El cambio del contrato de parto y dependencias del outbox requiere
  ADR antes de implementar»**. Escribir el ADR y aprobarlo.
- **Sin ADR aprobado, el commit 4 no se escribe.** La rama entrega los commits 1, 2, 3 y 5.

## Commit 4 — La cría existe antes del primer sync

`feat(breeding): give offspring a client-generated identity at birth`

**Punto de no retorno:** cambia el contrato de parto. Los dispositivos con la versión anterior
deben poder seguir enviando; la compatibilidad se verifica, no se asume.

**Archivos:**
- `clients/field-app/src/screens/birth/Step3Offspring.tsx:48` y
  `clients/field-app/src/services/birthService.ts:8,66,79` — el arete opcional por cría ya
  existe; **no hace falta inventar otra entidad**. Falta la identidad estable.
- `src/Modules/Breeding/.../Birthings/RecordBirthingCommand.cs:125` — respetar el UUID que
  llega del cliente, como ya hace `createAnimal` (art. 3).
- La sincronización respeta la dependencia nacimiento → registro posterior. Si el nacimiento
  se rechaza, **los dependientes se conservan y muestran la causa**; no se envían como hechos
  huérfanos.

**Verificación:** registrar un parto con varias crías aretadas sin conexión, pesar a una de
ellas, reiniciar el teléfono y sincronizar. La cría mantiene **exactamente el mismo UUID** y
su genealogía.

## Commit 5 — Convivencia de individual y conteo

`feat(livestock): let individually tracked groups coexist with headcount lots`

**Archivos:**
- `clients/field-app/src/services/herdQueries.ts:202` — ya expone grupos activos y su
  `trackingMode`. Distinguir claramente individuo y grupo en la interfaz de selección.
- Peso, tratamiento, vacuna, movimiento y baja de un individuo se atribuyen a **su UUID**.
- Alimento continúa registrado por grupo.
- Se conservan grupos y eventos `Headcount` anteriores (D2).

**Verificación:** grupos individuales nuevos y grupos por conteo históricos conviven. **No se
adjudican a individuos** hechos antiguos conocidos solo por cantidad.

**Fuera de alcance:** conversión libre de grupos históricos.
`UpdateAnimalGroupCommand.cs:11` no recibe modo de seguimiento y esta rama **no presupone**
esa conversión (spec sec. 2).

## Orden, dependencias y puntos de no retorno

```
Compuerta 0 (momento del aretado)  ── puede recortar la rama entera
  ├─ Commit 1 (buscar por identificadores)
  │    └─ Commit 2 (ambigüedad)
  │         └─ Commit 3 (aretar/reemplazar offline)
  └─ Compuerta 1 (ADR de identidad de cría)
       └─ Commit 4 (cría con UUID)  ◄── PUNTO DE NO RETORNO
            └─ Commit 5 (convivencia individual/conteo)
```

- **Commit 4 es el punto de no retorno**: cambia el contrato de parto.
- **Los commits 1, 2 y 3 no dependen de ninguna compuerta** y se entregan aunque las dos
  queden abiertas.
- **La Compuerta 0 puede dejar la rama en tres commits.** Es un resultado válido, no un fracaso.

## Cómo se prueba

1. `dotnet test` completo contra PostgreSQL real (regla 5).
2. `npm test` completo en `clients/field-app`.
3. [`test-e2e.md`](./test-e2e.md) sobre SQLite nativo en dispositivo real, con **dos
   dispositivos** para los escenarios de conflicto.
4. Primero con datos ficticios, después **con una camada reconocible en campo** (criterio 8).

## Descripción del PR

**Título:** `feat(livestock): manage individually tagged animals from the field app`

**Cuerpo:**

- **Qué:** búsqueda por todos los identificadores vigentes, desambiguación explícita,
  asignación y reemplazo de arete offline e idempotente, identidad de cría generada en
  cliente, y convivencia de grupos individuales con lotes por conteo.
- **Por qué:** el dueño declaró que todos los animales van a tener arete, lo que reemplaza el
  supuesto operativo del engorde sin identificación ([`spec.md` sec. 1](./spec.md#1-cambio-de-requisito)).
- **Decisiones:** [`spec.md` sec. 3](./spec.md#3-decisiones-fijadas-para-la-propuesta), D1–D6.
- **Qué NO incluye:** migración de lotes históricos ya mezclados (prohibida por ADR-0015 y D4
  sin decisión del dueño); garantía de unicidad concurrente en BD; lector RFID/QR; impresión;
  compras de hardware; eliminación del modo por conteo.
- **Riesgo declarado:** el commit 4 cambia el contrato de parto y exige ADR previo. La
  compatibilidad con dispositivos de versión anterior se verifica en `test-e2e.md`.
- **Deuda anotada:** actualizar la definición de `WeightSorting` en `GLOSSARY.md`, que hoy
  dice que la clasificación por peso «es el momento en que termina la identificación
  individual». Con aretes eso deja de ser necesariamente cierto (spec sec. 5).
- **Cómo probarlo:** ejecutar [`test-e2e.md`](./test-e2e.md).
- **Resultado de Compuerta 0:** `<momento del aretado, tipo de arete, alcance de unicidad>`.
