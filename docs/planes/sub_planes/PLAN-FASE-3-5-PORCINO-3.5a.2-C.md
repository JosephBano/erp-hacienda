# PLAN-FASE-3-5-PORCINO-3.5a.2-C.md — Vacunación como camino separado y UI de campo

> **Sub-plan extraído de `PLAN-FASE-3-5-PORCINO.md` §3.5a.2.**
> Este archivo **es ejecutable de forma independiente** del macro plan y de sus
> pares 3.5a.2-A (catálogos y payload) y 3.5a.2-B (lógica de dosis y series).
> Depende de ambas mergeadas.

- **Rama Git:** `feature/field-app-treatment-ui`
- **ADRs que la respaldan:** ninguno nuevo (la separación tratamiento/vacunación
  es una decisión de UX respaldada por `PLAN-FASE-3-4` §1 y el principio de los
  tres toques).
- **Pares del split:**
  - [`3.5a.2-A`](./PLAN-FASE-3-5-PORCINO-3.5a.2-A.md) (rama `feature/livestock-treatment-catalog`): catálogos y payload — **requerido**.
  - [`3.5a.2-B`](./PLAN-FASE-3-5-PORCINO-3.5a.2-B.md) (rama `feature/livestock-treatment-dose-logic`): lógica de dosis y `TreatmentCourse` — **requerido**.
- **Fuente original:** [`PLAN-FASE-3-5-PORCINO.md` §3.5a.2](../PLAN-FASE-3-5-PORCINO.md#35a2--featurelivestock-treatment-detail--estructural)

---

## Por qué existe esta sub-rama

La interfaz de "registrar tratamiento" carga con todo a la vez: producto, vía,
motivo, dosis, lote de inventario, observaciones, período de retiro. Esa pantalla
intenta responder a **dos intenciones distintas** que el cliente separa a la
hora de operar:

- "**Voy a aplicar el cronograma de vacunas de hoy**" — vía casi siempre una
  sola, motivo siempre `Scheduled`, dosis casi siempre `PerHead`, lote
  pre-seleccionado por la lista de pendientes.
- "**Tengo un animal enfermo, voy a tratarlo**" — vía y motivo variables,
  dosis casi siempre `Absolute` o `PerWeight`, lote posiblemente nuevo, notas
  casi siempre obligatorias.

Mezclar las dos en una sola pantalla era exactamente el tipo de fricción que la
regla de los tres toques combate: el caso normal (vacunación de cronograma,
diario) debería sumar tres toques, y debería tener **otra pantalla** que la del
caso raro (tratamiento curativo). Forzar ambas en la misma UI agrega pasos al
caso común para cubrir el infrecuente.

Esta sub-rama separa las dos intenciones en dos rutas, mantiene los toques del
camino "vacunación" en tres, y deja al "tratamiento" con la profundidad que
justifica la menor frecuencia.

## Decisiones tomadas en el macro plan y que aplican a esta sub-rama

- **Tres toques para lo normal, cuatro para lo raro** — la regla del macro
  plan §2.3 se aplica literal en esta sub-rama: el camino "vacunar" debe
  resolverse en tres toques, el "tratar un animal enfermo" en cuatro.
- **Pantalla de campo: vía y motivo en la misma pasada, sin sumar toques al
  caso normal.** Si la pantalla de tratamiento pide vía y motivo en una pasada
  separada, suma un toque de más. La forma de evitarlo es presentarlos como un
  único campo combinado o como campos opcionales que se auto-rellenan cuando
  aplica. Esta decisión se fija acá porque depende de la UI concreta, no del
  modelo.
- **Sincronización sin red (Art. 9).** Las pantallas deben funcionar en modo
  avión — los catálogos vienen del pull y la cola del outbox ya existe.

## Tareas

1. **Pantalla `VaccinateScreen` en `field-app`.** Camino principal del
   registro de vacunación:
   - Lote o sujeto (animal / lote) en el primer toque, vía búsqueda o recientes
     (lo provee [3.5a.9-B](./PLAN-FASE-3-5-PORCINO-3.5a.9-B.md), que ya debió
     haber mergeado para llegar acá; si no, este PR no arranca).
   - Selección del producto desde el inventario en el segundo toque.
   - Motivo `Scheduled` prefijado, lote de inventario pegado al producto,
     `DoseKind = PerHead` por defecto, vía y cantidad se completan con la
     indicación del producto. **Tercer toque: confirmar.**
   - Total: **tres toques** para el caso común.
2. **Pantalla `TreatScreen` en `field-app`.** Camino "curar / prevenir", más
   profundo:
   - Sujeto (animal individual, **no** grupo en esta pantalla inicial), luego
     producto, luego **un único formulario** con vía + motivo + dosis + lote.
     Los defaults razonables (motivo `Curative`, dosis `Absolute`) se sugieren
     pero son editables.
   - Notas **siempre visibles** y obligatorias sólo cuando `AdministeredDose`
     sea `NULL` (lo decide 3.5a.2-B; la UI sólo refleja la regla).
   - Total: **cuatro toques** para el caso raro (sujeto, producto, formulario,
     confirmar).
3. **Camino "Lo que registré hoy":** las dos pantallas empujan sus
   operaciones al `SyncOutbox` con tipo distinto (`vaccination` vs `treatment`)
   para que la lista del día pueda filtrarlas. Este sub-cambio se conecta con
   [3.5a.8](../PLAN-FASE-3-5-PORCINO.md#35a8--featurefield-app-correcciones-adr-0017)
   cuando exista (correcciones; es opcional acá).
4. **Validación local de plausibilidad (Art. 9).** Las pantallas consultan los
   `plausibility_min/max` y `absolute_min/max` sincronizados del catálogo (
   [`3.5a.6`](../PLAN-FASE-3-5-PORCINO.md#35a6--featurelivestock-plausibility-ranges)),
   para no pedir "1000 L" cuando el rango diga que es absurdo. Si
   `3.5a.6` aún no mergeó, la UI muestra un placeholder "rango no
   configurado" y deja pasar el valor — fail-open según el diseño de
   `3.5a.6`.
5. **`TreatmentCourse` se refleja en la UI como una sola fila de la lista
   del día**, no como N eventos sueltos (decisión de
   [3.5a.2-B](./PLAN-FASE-3-5-PORCINO-3.5a.2-B.md#tareas)); la pantalla hoy no
   lo distingue y eso está bien como primer paso, pero se documenta la
   dirección: en una iteración posterior la lista del día debe agrupar
   aplicaciones del mismo `TreatmentCourse`.
6. **Caminos de salida y "cancelar" explícitos** — si la pantalla se cierra a
   mitad, la cola queda limpia, no queda un estado sucio en
   `VaccinateScreen`/`TreatScreen`. Mismo patrón que
   [`3.5a.0`](../PLAN-FASE-3-5-PORCINO.md#35a0--featurefield-app-input-guards--empezar-por-acá)
   fijó para `BirthScreen`.
7. **Pruebas críticas** (detalladas más abajo).

### Tarea derivada: conector con la rama de búsqueda QR / RFID

Cuando el aretado llegue (ADR-0015), un input de búsqueda por RFID acelerará
los dos flujos al primer toque. Esta sub-rama deja la pantalla **lista para**
recibir un input inicial distinto — un `picker` que pueda ser `search` o
`scan`. No se implementa acá.

## Pruebas

Las pruebas mínimas obligatorias:

1. **Tres toques para vacunación individual.** Sembrar 1 animal con su arete.
   Recorrer `VaccinateScreen` con el formulario completo: el conteo de
   toques observables en el árbol de navegación **debe ser exactamente 3**.
   Si la implementación excede, la prueba falla y la pantalla se corrige antes
   de mergear. Esta es la misma defensa del
   [3.5a.9-B](./PLAN-FASE-3-5-PORCINO-3.5a.9-B.md), pero específica de esta
   pantalla.
2. **Cuatro toques para tratamiento curativo.** Recorrer `TreatScreen`
   completo con todos los campos llenos. Conteo exacto = 4.
3. **Sin red total.** Modo avión, recorrer las dos pantallas hasta el
   outbox. La operación queda pendiente con su tipo (`vaccination` /
   `treatment`). Verificar que la cola se reconstruye tras reiniciar la app.
4. **Plausibilidad.** Sin rangos configurados, valores absurdos pasan
   (fail-open del `3.5a.6`). Con rangos configurados, valores
   improbables piden confirmación y valores imposibles se rechazan.
5. **Validación local de admin route inválida.** Intentar enviar un evento
   con `route_id` que no exista o esté `is_active = false` — la app debe
   detectar el problema en el outbox antes de mandar (cliente problema
   visible) y el servidor lo rechaza igual si llegara.
6. **Cancelar no deja estado sucio.** Iniciar una vacunación, cerrarla
   después de seleccionar producto. Reabrir: la pantalla debe arrancar en
   estado limpio (sin sujeto, sin producto preseleccionado).

Adicional recomendado:

- Recorrido por voz / TalkBack: la separación debe leerse correctamente sin
  pista visual.
- Modo oscuro y alto contraste de las dos pantallas.

## Lo que NO incluye (queda para otras ramas)

- Las pantallas de **corrección** sobre tratamientos ya registrados — es
  [`3.5a.8`](../PLAN-FASE-3-5-PORCINO.md#35a8--featurefield-app-correcciones-adr-0017).
- La **ficha del lote** con el último tratamiento aplicado — eso es
  [`3.5a.7`](../PLAN-FASE-3-5-PORCINO.md#35a7--featurefield-app-lot-registration).
- El escaneo **QR/RFID**. La estructura de la pantalla queda lista, pero la
  integración va cuando llegue el aretado.
- **El árbol de actividades completo** que ubica estas pantallas en su
  lugar → [`3.5a.9-B`](./PLAN-FASE-3-5-PORCINO-3.5a.9-B.md).

## Cómo probarlo

```bash
git fetch origin
git switch feature/field-app-treatment-ui
cd clients/field-app && npm run typecheck && npm test
```

Para el flujo manual con la app levantada:

1. Sembrar un animal con arete en el pull.
2. Abrir la app, ir a **Un animal → Vacunar**. Confirmar tres toques para el
   caso común.
3. Ir a **Un animal → Tratar**. Confirmar cuatro toques para el caso raro.
4. Poner modo avión, repetir ambos flujos. Verificar que la cola del outbox
   los persiste con tipo `vaccination` y `treatment` respectivamente.

## Riesgos específicos de esta sub-rama

| Riesgo | Mitigación |
|---|---|
| "Vacunar" termina exigiendo más de tres toques | Test #1 obligatorio. Si se excede, se rediseña la pantalla. |
| "Tratar" oculta campos del formulario | El modo `Absolute` con notas se prueba explícitamente; un bug acá se descubre en la primera inspección. |
| El outbox marca ambas operaciones como el mismo tipo | Forzar tipos distintos en `outbox.ts` (test específico). |
| Cancelar deja el sujeto/producto pre-seleccionado | Test #6 obligatorio. |
| La UI ignora `is_active = false` del catálogo local | Sincronización respeta `is_active`; test #5 captura la ruta. |
