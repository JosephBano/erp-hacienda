# PLAN-FASE-3-5-PORCINO-3.5a.2-A.md — Catálogos, payload y forward-compat del tratamiento

> **Sub-plan extraído de `PLAN-FASE-3-5-PORCINO.md` §3.5a.2.**
> Este archivo **es ejecutable de forma independiente** del macro plan y de sus
> pares 3.5a.2-B (lógica de dosis y series) y 3.5a.2-C (UI de campo). El macro plan
> sigue siendo la fuente de verdad para el resto del proyecto: toda decisión que
> aplique a varias ramas vive allá. Acá viven sólo las decisiones y el alcance de
> esta sub-rama.

- **Rama Git:** `feature/livestock-treatment-catalog`
- **ADR que la respalda:** ninguno nuevo (los catálogos aplican Art. 8; el campo
  `health_plan_item_id` nullable sigue al ADR-0016 que se mergea más tarde).
- **Pares del split:**
  - [`3.5a.2-B`](./PLAN-FASE-3-5-PORCINO-3.5a.2-B.md) (rama `feature/livestock-treatment-dose-logic`): las tres formas de dosis, calculada vs. administrada, opcionalidad, observación libre y `TreatmentCourse`.
  - [`3.5a.2-C`](./PLAN-FASE-3-5-PORCINO-3.5a.2-C.md) (rama `feature/field-app-treatment-ui`): vacunación como camino separado y pantalla de campo con vía y motivo en la misma pasada.
- **Fuente original:** [`PLAN-FASE-3-5-PORCINO.md` §3.5a.2](../PLAN-FASE-3-5-PORCINO.md#35a2--featurelivestock-treatment-detail--estructural)

---

## Por qué existe esta sub-rama

Hoy `dose` es **texto libre** en el `eventService.ts`, lo que incumple el Art. 10
("toda cantidad física lleva su unidad explícita") y vuelve imposible distinguir
una vacuna de calendario de un tratamiento por enfermedad — que es el punto 1 del
cliente del piloto.

Resolver ese problema tiene tres frentes de naturaleza muy distinta y por eso
se partió la rama original 3.5a.2:

1. **Datos:** un catálogo configurable para las vías de administración (Art. 8),
   un `TreatmentReason` para distinguir "tocaba por cronograma" de "curé algo" de
   "preventivo", y un payload estructurado que reemplace el texto libre.
2. **Lógica:** cómo se expresa la dosis en el nuevo modelo (tres formas posibles)
   y cómo se serializa un tratamiento de varios días como una sola unidad — todo
   ese trabajo es [3.5a.2-B](./PLAN-FASE-3-5-PORCINO-3.5a.2-B.md).
3. **UI de campo:** cómo se ve todo eso en la app, sin romper la regla de los tres
   toques — eso es [3.5a.2-C](./PLAN-FASE-3-5-PORCINO-3.5a.2-C.md).

Esta sub-rama resuelve **sólo el frente de datos**, que es el que más cuesta
recuperar tarde y el que menos depende del resto.

## Lo que resuelve el macro plan y aplica acá

- **El choque con la Constitución es aparente.** Asumir que estructurar la dosis
  significa hacerle elegir la unidad al operario confunde dos cosas: la **unidad
  no es del operario, es del producto**. El ítem de inventario ya declara su
  unidad; la app pide un número y nada más. El trabajo de la unidad va en la
  sub-rama [3.5a.2-B](./PLAN-FASE-3-5-PORCINO-3.5a.2-B.md) (donde el operario
  decide qué dosis escribir); acá se garantiza que el dato tiene **dónde** y
  **cómo** recibirlas.
- **Forward-compat con el cronograma.** La rama **no espera** a 3.5b.1 para
  declarar `health_plan_item_id`. Si el piloto corre un mes sin ese campo, los
  tratamientos que se registren **no se pueden enlazar retroactivamente** con su
  ítem de plan cuando el cronograma exista. Cuesta una línea hoy; es irrecuperable
  mañana. ADR-0016 §"Condición de reversa" lo deja escrito.

## Tareas

1. **Catálogo `administration_routes`** (Art. 8 — configurable en BD, no `enum`
   en código):
   ```
   { id, key, label_es, default_unit_id, is_active }
   ```
   Semilla inicial: *oral en agua*, *oral en alimento*, *intramuscular (IM)*,
   *subcutánea (SC)*, *tópica*, *intranasal*, *intrauterina*. Ampliable desde el
   panel `admin-web`.
2. **`TreatmentReason` ∈ {`Scheduled`, `Curative`, `Preventive`}.** Tabla
   configurable en BD (mismo motivo que el catálogo de vías — no se codifica en
   el dominio). Semilla con los tres valores.
3. **Payload de `TreatmentEvent`:**
   ```
   {
     animal_id_or_group_id: ...,         // XOR con grupo (ADR-0015)
     route_id:          uuid,            // FK a administration_routes
     reason:            enum-string,     // uno de TreatmentReason
     batch_id:          uuid?,           // lote de inventario consumido (nullable)
     health_plan_item_id: uuid?,         // forward-compat con 3.5b.1
     applied_by_user_id: uuid,           // quien aplicó (vet / técnico)
     recorded_by_user_id: uuid,          // quien tipeó (operario) — ambos pueden ser el mismo
     occurred_at:       timestamp,
     ...
   }
   ```
   El dominio exige **`applied_by` ≠ `recorded_by` por nulabilidad**, no por
   desigualdad semántica: las dos FKs conviven, el sistema **no** asume que son
   la misma persona. Aplicar esto ahora evita que después se codifique "el que
   registra es el que aplica" como un `default` oculto.
4. **`Disposal.Cause` y `GroupMortality.Cause`** ya los introduce [3.5a.3](./../PLAN-FASE-3-5-PORCINO.md#35a3--featurelivestock-mortality-causes)
   (catálogo de causas de muerte). Esta sub-rama **referencia** el catálogo pero
   no lo crea — `mortality_causes` es responsabilidad de 3.5a.3.
5. **Migración EF Core:** tablas nuevas `administration_routes` y
   `treatment_reasons`; columnas nuevas en `animal_events` (los campos del
   payload). El campo `dose` libre persiste por ahora para no romper registros
   pre-existentes (Art. 1); 3.5a.2-B lo depreca. `RecordedBy` cambia de
   `string` a FK a `users`, conservando un `recorded_by_label` para no perder
   historia en datos legacy (mismo patrón que PLAN-FASE-3-4 §3.A "Bitácora").
6. **Catálogo en `admin-web`:** pantalla CRUD para `administration_routes` y
   `treatment_reasons` (alta, baja lógica, etiqueta visible). Permiso:
   `livestock.treatments.configure`.
7. **Sync:** ambos catálogos entran a una colección del pull, **read-only** para
   el cliente. Ningún cambio en el esquema local del móvil más allá del read.
8. **Pruebas críticas** (detalladas más abajo).
9. **`health_plan_item_id` se crea como columna nullable** (no FK todavía —
   3.5b.1 traerá la tabla). La presencia del campo en la fila es lo que importa
   hoy, no la integridad referencial, que se valida cuando exista el plan.

## Pruebas

Las pruebas mínimas obligatorias:

1. **Catálogos configurables.** Crear una nueva vía y un nuevo motivo por el
   panel; verificar que aparecen en la lista de opciones del siguiente
   tratamiento registrado. Borrar (lógicamente) una vía que ya estaba en uso: el
   registro histórico sigue mostrándola.
2. **`applied_by_user_id` ≠ `recorded_by_user_id`.** Un registro válido puede
   tener aplicadas y registradas por la misma persona (campo del formulario "yo
   mismo" marcado). El sistema **no exige** que sean distintas; sólo garantiza
   que las dos rutas existan como dato. Un registro con `applied_by_user_id =
   NULL` se rechaza.
3. **`health_plan_item_id` nullable.** Insertar tratamiento sin
   `health_plan_item_id` es válido hoy. Cuando se mergee 3.5b.1 (cronograma),
   el pull que existe debe ser **idempotente**: registros creados antes de la
   tabla siguen con `NULL` y los nuevos pueden ya referenciar. No hay
   migración de datos.
4. **Catálogos llegan al móvil.** Sembrar una vía nueva en el servidor. En el
   cliente, hacer pull; verificar que la lista del formulario de tratamiento la
   incluye.
5. **Borrado lógico.** Borrar una vía que figura en un evento histórico: el
   evento la muestra igual con su etiqueta original; el pull la sigue
   entregando porque `is_active` es flag, no `DELETE`.

Adicional recomendado:

- Permiso `livestock.treatments.configure` exige el flag en Angular y en la API.
- Auditoría: `who`/`when` sobre cambios en catálogos.

## Lo que NO incluye (queda para 3.5a.2-B y 3.5a.2-C)

- **Las tres formas de dosis** (absoluta, por peso, por cabeza) y la lógica de
  cálculo → [3.5a.2-B](./PLAN-FASE-3-5-PORCINO-3.5a.2-B.md).
- **Dosis calculada vs. administrada** y `TreatmentCourse` → también
  [3.5a.2-B](./PLAN-FASE-3-5-PORCINO-3.5a.2-B.md).
- **Pantalla de campo** con vía y motivo en una sola pasada, y la separación
  entre tratamiento y vacunación en la app →
  [3.5a.2-C](./PLAN-FASE-3-5-PORCINO-3.5a.2-C.md).
- El catálogo `mortality_causes` no es de esta sub-rama — vive en
  [3.5a.3](../PLAN-FASE-3-5-PORCINO.md#35a3--featurelivestock-mortality-causes).

## Cómo probarlo

```bash
git fetch origin
git switch feature/livestock-treatment-catalog
dotnet test --configuration Release
cd clients/admin-web && npm run build --configuration production
```

Para la parte manual:

```bash
psql -d hato -c "SELECT * FROM administration_routes ORDER BY id;"
psql -d hato -c "SELECT * FROM treatment_reasons;"
# Crear una nueva vía desde el panel; re-pull desde el móvil; verificar.
```

## Riesgos específicos de esta sub-rama

| Riesgo | Mitigación |
|---|---|
| `TreatmentReason` se codifica como `enum` en lugar de tabla | Test de arquitectura: un `enum` en `Livestock.Domain` que matchee los valores falla la compilación. |
| `health_plan_item_id` se crea como FK antes de tiempo | Esta sub-rama crea la columna nullable sin FK; el constraint llega con 3.5b.1. |
| Pérdida del dato `recorded_by` viejo (texto) al pasarlo a FK | `recorded_by_label` persiste paralelo (Plan Fase 3.4 §3.A "Bitácora"). |
| Catálogo inflado en el bundle del móvil | Sólo las opciones activas (filter por `is_active`). Si pasa el umbral, se pasa a un endpoint bajo demanda — pero hoy no hace falta. |
