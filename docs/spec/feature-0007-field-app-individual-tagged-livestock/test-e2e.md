# test-e2e.md — Verificación manual extremo a extremo

> Escenarios que se ejecutan **sobre dispositivos reales** (o emuladores con base persistente
> y SQLite nativo) contra una instancia del servidor con datos, después de que la suite
> automatizada esté en verde.
>
> Cada escenario dice cómo prepararlo, qué hacer y qué debe pasar. Un escenario que falla se
> reporta con el paso exacto donde falló, no como "no anda".

**Antes de empezar:**

- Servidor corriendo con la rama mergeada localmente, contra PostgreSQL real.
- **Dos dispositivos, A y B.** Varios escenarios verifican conflicto entre ambos offline.
  Anotar modelo, SO y build de cada uno.
- Datos preparados: un animal con **dos identificadores vigentes**; un animal con arete que
  empieza en cero (por ejemplo `007`); un animal **sin ningún identificador**; una hembra con
  preñez activa; un grupo `Headcount` histórico y un grupo `Individual`.
- **Dos animales que compartan valor de arete**, creados a propósito para E2E-4. Es posible:
  la unicidad actual es por animal/tipo vigente, no entre animales.
- El resultado de la Compuerta 0 a la vista: si dijo «lote ya mezclado», E2E-8 **no se ejecuta**.

---

## E2E-1 — Encontrar por cualquier identificador vigente

**Pasos:**
1. Poner A en modo avión.
2. Buscar al animal por su primer identificador vigente.
3. Buscarlo por el segundo.
4. Buscar `007` y luego `7`.
5. Buscar el animal sin ningún identificador.

**Debe pasar:**
- Los pasos 2 y 3 encuentran al **mismo** animal.
- El paso 4 distingue: `007` no devuelve lo mismo que `7`. **Los ceros iniciales se conservan.**
- El paso 5 lo encuentra por su identificación alternativa legible.
- Cada resultado muestra arete destacado, sexo y grupo.
- Todo funciona **sin conexión**.

## E2E-2 — Registrar sobre un animal sin arete

**Pasos:**
1. Con A sin conexión, seleccionar al animal sin identificador.
2. Registrar un pesaje.
3. Asignarle un arete.
4. Sincronizar.

**Debe pasar:**
- El paso 2 **se permite**: la falta temporal de arete no bloquea registrar un hecho real (D5).
- El arete pendiente es visible mientras no lo tenga.
- Tras el paso 3, **todos los registros anteriores se preservan** y quedan asociados al mismo
  animal.

## E2E-3 — Cambiar el arete sin perder la historia

**Pasos:**
1. Anotar el arete vigente de un animal y algún registro suyo.
2. Reemplazar el arete por uno nuevo, sin conexión.
3. Sincronizar.
4. Buscar por el arete **anterior**.
5. Buscar por el nuevo.

**Debe pasar:**
- El paso 4 puede ofrecer historial, **claramente señalado como anterior**. Nunca lo presenta
  como identificación vigente.
- El paso 5 encuentra al animal con normalidad.
- La identificación anterior y sus fechas siguen registradas.
- Ningún registro histórico se perdió ni se reasignó.

## E2E-4 — Ambigüedad: el sistema pregunta, no adivina

**Preparación:** los dos animales que comparten valor de arete.

**Pasos:**
1. Buscar ese valor de arete.
2. Intentar registrar una actividad desde el resultado.

**Debe pasar:**
- Aparecen **ambos** animales, con datos suficientes para distinguirlos.
- El sistema **no elige** por su cuenta.
- **No fusiona** los dos animales bajo ninguna circunstancia.
- No se puede registrar sin haber resuelto conscientemente cuál es.

## E2E-5 — Conflicto de aretado entre dos dispositivos offline

**Pasos:**
1. Poner A y B en modo avión, ambos con el hato sincronizado.
2. En A, asignar el arete `X` al animal 1.
3. En B, asignar el **mismo** arete `X` al animal 2.
4. Sincronizar A y después B.

**Debe pasar:**
- Ninguna de las dos operaciones se pierde en silencio.
- El conflicto queda visible y **exige resolución consciente**.
- No se fusionan animales ni se reasigna historia automáticamente.

## E2E-6 — La cría existe antes del primer sync

> El escenario central del commit 4. Solo ejecutable si la Compuerta 1 aprobó el ADR.

**Pasos:**
1. Poner A en modo avión.
2. Registrar un parto de la hembra preñada con **varias crías**, aretando al menos dos.
3. Sin sincronizar, buscar una de las crías.
4. Registrarle un pesaje.
5. **Reiniciar el teléfono por completo.**
6. Recuperar conexión y sincronizar.
7. Consultar la cría en el servidor y en el teléfono.

**Debe pasar:**
- El paso 3 la encuentra: la cría es consultable **antes** de sincronizar, con estado pendiente.
- El paso 4 se permite.
- Tras el paso 6, la cría mantiene **exactamente el mismo UUID** que tenía en el paso 3.
- Su genealogía es correcta: la madre es la del parto.
- El pesaje del paso 4 quedó asociado a esa cría, no a otra ni a ninguna.

## E2E-7 — Nacimiento rechazado, dependientes conservados

**Pasos:**
1. Provocar que el servidor rechace el registro de parto.
2. Con crías ya pesadas offline, sincronizar.

**Debe pasar:**
- El rechazo del nacimiento es visible con su motivo.
- **Los registros dependientes se conservan** y muestran la causa.
- **No** se envían como hechos huérfanos contra animales que no existen en el servidor.

## E2E-8 — Convivencia con los lotes por conteo

> Si la Compuerta 0 determinó «lote ya mezclado», este escenario **no se ejecuta** y la
> transición queda fuera de la rama.

**Pasos:**
1. Abrir un grupo `Headcount` histórico.
2. Abrir un grupo `Individual` nuevo.
3. Registrar alimento en cada uno.
4. Registrar un pesaje individual en el grupo `Individual`.
5. Intentar atribuir a un individuo un hecho antiguo del grupo por conteo.

**Debe pasar:**
- Ambos grupos funcionan; el histórico no se rompió.
- La interfaz distingue con claridad individuo de grupo.
- El paso 3: **alimento queda registrado por grupo** en los dos casos.
- El paso 4 se atribuye al UUID del animal elegido.
- El paso 5 **no es posible**: un hecho conocido solo por cantidad no se adjudica a un individuo.

## E2E-9 — Camada real en campo

> El cierre del criterio 8. Se ejecuta después de que todo lo anterior esté en verde con
> datos ficticios.

**Preparación:** una camada reconocible físicamente, con el encargado presente.

**Pasos:**
1. Aretar la camada en campo con la app, sin conexión.
2. Registrar un pesaje a cada animal.
3. Sincronizar y verificar contra los animales físicos.

**Debe pasar:**
- Cada arete físico corresponde al registro que el sistema muestra.
- Ninguna asociación se hizo por orden, peso o cantidad (D4).
- El encargado puede confirmar la correspondencia mirando los animales.

---

## Cierre de la verificación

Estos escenarios en verde **no cierran la rama por sí solos**. Faltan:

- Los criterios numerados de [`spec.md` sec. 6](./spec.md#6-criterios-de-aceptación), 1 a 8.
- La suite automatizada completa ([`tasks.md`](./tasks.md) TC.1 y TC.2).
- Los resultados de la Compuerta 0 y la Compuerta 1 registrados en el PR.
- La actualización de `WeightSorting` en `GLOSSARY.md` ([`tasks.md`](./tasks.md) TC.5).

**Se anota siempre:** modelo, SO y build de cada dispositivo. **El caso de lote ya mezclado no
se acepta** sin resolver antes la pregunta de identidad física (criterio 8).
