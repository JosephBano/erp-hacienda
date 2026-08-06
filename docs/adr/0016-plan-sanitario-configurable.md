# ADR-0016 — Un solo motor de cronograma para vacunas, procedimientos y decisiones de manejo

- **Estado:** Propuesto
- **Fecha:** 2026-08-05
- **Fase del roadmap:** Fase 3.5 (Adaptación porcina) — **diseñado en 3.5a, implementado en 3.5b**

## Contexto

El cliente pidió tres cosas que a primera vista son tres funcionalidades distintas:

1. Un **cronograma de vacunación por lote**, configurable, para los porcinos de engorde,
   con alertas en el panel de administración.
2. Un cronograma **distinto para las madres**, porque no reciben lo mismo que el engorde.
3. Un "cronograma de eventos generalizado **según el género** del animal" para cosas que no
   son vacunas: la castración de los machos, la preselección de posibles buenas madres.

Son el mismo problema. Los tres dicen: *"a los N días de tal hecho, a los animales que
cumplen tal condición, hay que hacerles tal cosa; avísame si no se hizo"*. Lo único que
cambia entre ellos es el ancla, el desfase y el filtro de a quién aplica — es decir,
**datos**.

El Art. 8 es explícito sobre cómo resolver esto: *"si un requerimiento nuevo pide un
`if (especie == 'cerdo')` en el core, la respuesta es rediseñar el dato, no escribir el
`if`"*. Construir tres motores, o uno con ramas por tipo, sería exactamente el error que
ese artículo prohíbe.

El motor de alertas ya existe y funciona. `Tasks.Application/Alerts/GenerateAlertsCommand.cs`
tiene cuatro generadores (parto próximo, retiro activo, palpación pendiente, lote por
vencer) construidos sobre un patrón uniforme: leer por contrato público de otro módulo
(Art. 6), verificar `AlertAlreadyActiveAsync(código, entidad)` para no duplicar, crear la
alerta. Un quinto generador cabe sin tocar los cuatro existentes.

**Por qué este ADR se escribe ahora aunque se implemente en 3.5b:** el evento de
tratamiento de 3.5a debe llevar desde el primer día un `healthPlanItemId?` en su payload.
Si el piloto corre un mes registrando tratamientos sin ese campo, esos datos **no se pueden
enlazar retroactivamente** al cronograma — nadie podrá decir después si aquella vacuna del
14 de agosto fue la del calendario o una aplicación por enfermedad. El campo cuesta nada
hoy y es irrecuperable mañana.

## Decisión

**1. Un `HealthPlan` con ítems, y nada más.**

```
HealthPlan       { nombre, especie, activo }
HealthPlanItem   { plan, nombre, tipo_de_evento, ancla, desfase_días,
                   ventana_días, ítem_inventario?, vía?, dosis?, dosis_unidad?,
                   repeticiones?, aplica_a_categoría?, aplica_a_sexo? }
```

**2. El ancla es un dato, y ahí está toda la generalidad.**

`PlanAnchor` ∈ { `Nacimiento`, `InicioDeLote`, `Parto`, `Destete` }. Es lo único que hace
falta para que los tres pedidos del cliente quepan en un modelo:

| Pedido del cliente | Ancla | Desfase | Filtro |
|---|---|---|---|
| Vacuna del calendario de engorde | `InicioDeLote` | +21 d | categoría engorde |
| Vacuna de las madres | `Parto` | −14 d | categoría reproductora |
| Castración de machos | `Nacimiento` | +7 d | **sexo = macho** |
| Preselección de futuras madres | `Nacimiento` | +150 d | **sexo = hembra** |

La castración y la preselección **no son "otro cronograma"**: son ítems con un filtro por
sexo. Esa es la observación que colapsa tres motores en uno.

**3. Un plan se asigna a un lote o a un individuo.**

El engorde recibe el plan por lote (`HealthPlanAssignment` → `AnimalGroup`), las 3 cerdas
madres lo reciben individualmente (→ `Animal`). El mismo ítem se resuelve contra la fecha
del ancla correspondiente en cada caso.

**4. Vencimiento y cumplimiento.**

Un ítem está **pendiente** entre `fecha_teórica − ventana` y `fecha_teórica + ventana`, y
**vencido** después. Se marca cumplido cuando existe un evento (`Vaccination`, `Treatment`,
o el que declare el ítem) que lo referencia por `healthPlanItemId`. No hay un botón de
"marcar como hecho" separado del registro del hecho: **el cumplimiento es el evento**
(Art. 4).

**5. Un quinto generador de alertas, copiando el patrón existente.**

`HEALTH_PLAN_ITEM_DUE`, en `GenerateAlertsCommand`, leyendo por un contrato público nuevo
(`IHealthPlanDueItemsReader`) igual que los cuatro generadores actuales leen los suyos.
Cero SQL cruzado entre esquemas (Art. 6): un rename de columna en Livestock debe romper el
build de Tasks, no fallar en silencio en producción.

**6. Todo es catálogo.**

Agregar una vacuna al calendario, cambiar un desfase de 21 a 24 días o crear un plan para
una especie nueva es un INSERT desde el panel. Nunca un deploy.

## Alternativas consideradas

- **Un cronograma por tipo (vacunación / procedimientos / manejo reproductivo).**
  Descartada: triplica el motor de vencimiento, el generador de alertas y la UI de
  configuración, a cambio de cero capacidad adicional. Además obliga a decidir en qué
  cajón va cada ítem nuevo, y "preselección de futuras madres" no tiene un cajón obvio.

- **Fechas absolutas por lote en vez de ancla + desfase.** Descartada: obligaría a
  recalcular y recargar el calendario a mano cada vez que nace una camada. El ancla existe
  precisamente porque la fecha real la pone el hecho, no el planificador.

- **Reglas como código (una clase por regla del calendario).** Descartada por Art. 8. Es
  además la opción que garantiza que el cliente dependa del desarrollador para cambiar un
  intervalo de 21 a 24 días — exactamente lo contrario de "software a medida".

- **Usar el módulo `Tasks` como dueño del cronograma.** Descartada: `Tasks` es el receptor
  de alertas y no conoce a nadie por dentro. El plan sanitario es conocimiento de manejo
  ganadero y vive en `Livestock`; `Tasks` sólo lo lee por contrato, como ya lee gestaciones
  y retiros.

- **Implementarlo en 3.5a junto con la captura.** Descartada por el escalonamiento acordado:
  el cronograma se puede llevar en papel unas semanas —que es como se lleva hoy— mientras el
  campo empieza a cargar datos. Lo que **no** se puede posponer es el `healthPlanItemId` en
  el payload del tratamiento, y por eso este ADR se escribe ahora.

## Consecuencias

- **Positivas**:
  + Los tres pedidos del cliente se satisfacen con un solo modelo, una sola UI de
    configuración y un solo generador de alertas.
  + El cliente puede cambiar su propio calendario sin pedir un deploy, que es la promesa
    central del Art. 8 y la razón por la que esto es software a medida y no un producto
    genérico con cerdos encima.
  + El campo `healthPlanItemId` capturado desde 3.5a convierte el mes de datos del piloto
    en datos enlazables, no en un hueco.
  + Distinguir "vacuna programada" de "tratamiento porque se enfermó" —el `TreatmentReason`
    del bloque 3.5a— sólo tiene sentido si existe un cronograma contra el cual "programada"
    signifique algo. Los dos diseños se sostienen mutuamente.

- **Negativas / costos**:
  − Un plan mal configurado genera alertas incorrectas en masa, y una avalancha de alertas
    falsas entrena al usuario a ignorarlas — lo que arruina también las alertas buenas.
    **Mitigación**: `AlertAlreadyActiveAsync` ya impide el duplicado por entidad, y la
    ventana de cumplimiento se define por ítem para que un plan holgado no dispare a diario.
  − Cuatro anclas pueden quedarse cortas (p. ej. "a los N días del primer celo"). Agregar
    una quinta es una migración de catálogo, no de esquema, pero **sí** exige tocar código
    en el resolutor de fechas. Se acepta: cuatro cubren todo lo levantado, y el YAGNI del
    Art. 17 aplica.
  − El cumplimiento atado al evento significa que un ítem hecho pero no registrado aparece
    como vencido. Es deliberado (Art. 4: *si no quedó registrado, para el sistema no
    ocurrió*), pero generará fricción real las primeras semanas del piloto.

- **Condición de reversa**: si tras un ciclo completo de engorde el cliente no usa el
  cronograma configurable —lo llena una vez y no lo vuelve a tocar, o lo ignora y sigue con
  el calendario en papel—, se reevalúa reducirlo a un calendario fijo por especie mucho más
  simple. La señal a observar durante el piloto es concreta: **cuántos ítems del plan se
  cumplen dentro de su ventana** frente a cuántos vencen sin evento.
