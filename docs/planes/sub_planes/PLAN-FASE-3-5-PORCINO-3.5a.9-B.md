# PLAN-FASE-3-5-PORCINO-3.5a.9-B.md — Árbol de actividades y navegación de campo

> **Sub-plan extraído de `PLAN-FASE-3-5-PORCINO.md` §3.5a.9.**
> Este archivo **es ejecutable de forma independiente** del macro plan y de su par
> 3.5a.9-A. El macro plan sigue siendo la fuente de verdad para el resto del proyecto:
> toda decisión que aplique a varias ramas vive allá. Acá viven sólo las decisiones y
> el alcance de esta sub-rama.

- **Rama Git:** `feature/field-app-activity-tree`
- **ADR que la respalda:** ninguno directo (vive en el macro plan §2.3 y se apoya en
  las consecuencias del ADR-0019 para visibilidad — implementado en
  [3.5a.9-A](./PLAN-FASE-3-5-PORCINO-3.5a.9-A.md)).
- **Par de split:** [PLAN-FASE-3-5-PORCINO-3.5a.9-A.md](./PLAN-FASE-3-5-PORCINO-3.5a.9-A.md) (visibilidad de módulos por interruptor explícito)
- **Fuente original:** [`PLAN-FASE-3-5-PORCINO.md` §3.5a.9](../PLAN-FASE-3-5-PORCINO.md#35a9--featurefield-app-herd-navigation)
- **Compuerta:** §2.3 y §7-C del macro plan: **el árbol sólo se implementa cuando
  esté dibujado y los toques contados** con el cliente. Sin esa conversación este
  PR no arranca. Es deliberado y es la promesa de los tres toques para 15+
  actividades.

---

## Por qué existe esta sub-rama

`EventsScreen.tsx:113-124` pinta **un `BigButton` por animal**. Con 3 vacas funciona;
con 90 cerdos es un scroll infinito. Y lo peor no es el scroll: el día que la app
agregue las 15+ actividades que el pivote porcino necesita (pesaje muestral,
mortalidad de lote, vacunación de lote, diagnóstico grupal, consumo en sacos,
observación de características, clasificación por peso, corrección, etc.), el
menú de inicio deja de tener una jerarquía razonable y la promesa de los tres
toques se muere sin que nadie tome la decisión de matarla.

La forma de evitarlo no es agregar botones: es **dibujar el árbol primero y contar
los toques sobre papel**. Esta sub-rama implementa el árbol de §2.3 del macro
plan, con el sujeto (animal / lote / parto / lo de hoy) como primer nivel — el
mismo XOR que `animal_events` representa en el backend (ADR-0015).

## Decisiones tomadas en el macro plan que aplican a esta sub-rama

- §2.3 "el árbol de actividades" (macro plan). Es **la fuente** de esta sub-rama.
- §2.3 "Filtrado, no ramificado por especie" (macro plan). El árbol es único; el
  filtrado lo provee la conjunción del ADR-0019 §2.
- §7-C "Frecuencia real de cada actividad" (macro plan). Sin esa respuesta esta
  sub-rama no arranca: el árbol se ordena por frecuencia, no por importancia
  conceptual.

## Decisión de diseño que el macro plan deja abierta y se cierra acá

El árbol del §2.3 se construye **antes** de la pantalla, lo que obliga a fijar dos
cosas en este sub-plan:

1. **Forma del árbol en código.** Una sola constante exportada
   `ACTIVITY_TREE` (TypeScript, `clients/field-app/src/navigation/activityTree.ts`)
   cuya raíz es un nodo con etiqueta "INICIO" y cuatro hijos que coinciden con el
   primer nivel del árbol del macro plan: `UnAnimal`, `UnLote`, `UnParto`,
   `HoyPendientes`. Esto permite que el árbol sea testeable de forma unitaria sin
   renderizar nada y que cambiar la navegación sea un edit a un solo archivo.
2. **Filtrado por capacidades.** Cada nodo expone `requiresCapabilities: Capability[]`
   y la función `filterTree(tree, capabilities)` recorta los hijos no disponibles.
   El conjunto de capacidades del cliente se construye en el boot desde
   `species.milkable`, `groups.headcountEnabled`, `user.permissions`. El
   interruptor de módulo de [3.5a.9-A](./PLAN-FASE-3-5-PORCINO-3.5a.9-A.md)
   entra como `moduleEnabled[key]` y se evalúa antes que las capacidades.

Si la implementación revelara que esta firma no es la correcta, se modifica este
sub-plan antes de mergear el código, no después.

## Tareas

1. **Migrar `ACTIVITY_TREE`** desde el árbol textual del §2.3 a un árbol en código
   con la forma del punto anterior. Las cuatro ramas del primer nivel (sujeto) son
   `UnAnimal`, `UnLote`, `UnParto`, `HoyPendientes`. Las actividades de cada rama
   van en el segundo nivel, ordenadas **por frecuencia declarada por el cliente**,
   no por importancia conceptual.
2. **Selector de animales con búsqueda y filtro por lote.** Sustituye al
   `BigButton` por animal en `EventsScreen` por una pantalla de búsqueda que
   soporta:
   - Búsqueda por identificador (`FarmTag`, `OfficialTag`, nombre).
   - Filtro por lote (`AnimalGroup`).
   - Lista plana de "recientes" (últimos N animales sobre los que este dispositivo
     registró algo).
3. **Selector de lotes** simétrico al de animales, búsqueda por nombre del lote.
4. **Rutas de navegación** (`clients/field-app/src/navigation/`) cableadas al
   árbol y al filtro. El primer nivel del árbol vive en la pantalla principal;
   los hijos son rutas stackeadas. **Sin red, la navegación filtra igual** (Art. 9
   — las capacidades y el flag de módulo vienen del pull).
5. **Rama "Lo que registré hoy"** lista las entradas del `SyncOutbox` del día
   calendario local, con su estado (`pending` / `synced` / `rejected` /
   `cancelled` tras 3.5a.8). Esto generaliza lo que el resumen de leche ya hace.
   Si 3.5a.8 no está mergeado, esta tarea se implementa igual y se conecta al
   existir.
6. **Cada actividad del árbol se resuelve en los toques que se contaron en §2.3**
   del macro plan. Si la implementación excede lo dibujado, se corrige el flujo,
   no se relaja el número. La métrica se valida por una suite de "recuento de
   toques" que dispara un fallo si una ruta suma más toques que el `TapBudget`
   declarado en el árbol.
7. **Pruebas críticas** (detalladas más abajo).

### Tarea derivada: documentación del escaneo QR (cuando llegue el aretado)

Cuando el aretado ocurra (ADR-0015 §"Qué pasa el aretado"), `AnimalIdentifier` ya
lo soporta con tipo `RFID` (ADR-0006). El escaneo QR es entonces un atajo al
selector de animales con un input distinto: **no es trabajo de esta fase**, sólo se
deja documentado en `BACKLOG.md` como evolución natural del selector de animales
implementado en el punto 2.

## Pruebas

Las pruebas mínimas obligatorias:

1. **El árbol filtra sin red.** Sembrar `capabilities.headcount = true` y
   `moduleEnabled.livestock = true`. Quitar la red. Abrir la app. La rama "Un lote"
   aparece. Cambiar `capabilities.headcount = false` (simulando una finca sin
   engorde por conteo). Sin recargar ni re-sync, la rama desaparece.
2. **El árbol respeta el interruptor de módulo.** Módulo `Production` apagado,
   abrir la app: ni la rama "ordeño" ni la ruta directa a `MilkingScreen` están
   disponibles.
3. **El conteo de toques se mantiene dentro del `TapBudget`.** Para cada actividad
   del árbol declarada con `taps: 3` o `taps: 4`, una prueba dispara el flujo
   completo (los handlers reales, no mocks de navegación) y cuenta interacciones.
   Si excede, falla. Esta prueba **es la única defensa contractual** contra el
   escenario "se agregó un botón y nadie midió".
4. **Búsqueda a escala.** Sembrar 200 animales. Verificar que el buscador
   devuelve el correcto, en menos de X ms (umbral a fijar; si no se cumple, abrir
   ticket, no relajar el test).
5. **Recientes.** Registrar un evento sobre el animal X. Verificar que X aparece
   en "Recientes" en el siguiente render. Borrar la base local. Verificar que la
   lista se reconstruye desde el historial del cliente, no desde un cache opaco.

## Lo que NO incluye (queda para otras ramas)

- El interruptor de módulo en sí:
  [3.5a.9-A](./PLAN-FASE-3-5-PORCINO-3.5a.9-A.md).
- Las pantallas de cada actividad (ordeño, pesaje muestral, mortalidad de lote,
  vacunación, diagnóstico grupal, consumo en sacos, etc.). Esas viven en sus
  respectivas ramas del macro plan: 3.5a.6, 3.5a.7, 3.5a.8, 3.5b.4, etc. Esta
  sub-rama **sólo provee el esqueleto** de navegación que las albergará.
- La búsqueda por RFID/QR. Ver tarea 7 anterior.
- Las pantallas de configuración individual (órdenes del árbol que el dueño puede
  ocultar). Eso es producto futuro, no parte de esta sub-rama.

## Cómo probarlo

```bash
git fetch origin
git switch feature/field-app-activity-tree
cd clients/field-app && npm run typecheck && npm test
# El "conteo de toques" corre como parte del suite estándar una vez ACTIVITY_TREE
# esté poblado.
```

Para el flujo manual:

1. Con 90 animales sembrados en el pull, abrir `EventsScreen`. Verificar que ya
   no hay un botón por animal.
2. Buscar por nombre. Tocar una actividad de la rama `UnLote`. Verificar que el
   camino llega en ≤ toques del `TapBudget` declarado.
3. Apagar el módulo `Production` desde el panel. Verificar que la ruta directa a
   `MilkingScreen` desaparece.

## Riesgos específicos de esta sub-rama

| Riesgo | Mitigación |
|---|---|
| El árbol tiene más de 15 actividades y se vuelve inmanejable | El conteo de toques es test, no métrica informal. Si excede, se rediseña el flujo antes de mergear. |
| El árbol se ramifica por especie en vez de filtrarse | El test del punto 1 con `headcount` on/off impide ramificar: si cambiar capacidades cambia la forma del árbol, hay un bug. |
| La rama "Hoy pendientes" no funciona porque 3.5a.8 no mergeó | Documentado en tarea 5: se implementa igual con `pending` / `synced` / `rejected`, y se enchufa `cancelled` cuando llegue. |
| El filtro consulta red | Test del punto 1 corre sin red. Falla quien implemente un fetch en lugar de leer la cache. |
