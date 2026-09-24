# ADR-0042 — Submódulos: interruptores jerárquicos sobre el ADR-0019

- **Estado:** Propuesto
- **Fecha:** 2026-09-23
- **Fase del roadmap:** transversal. Lo estrena el bloque 1 de la Fase 4 adelantada (ADR-0040)
- **Relacionado:** ADR-0019 (visibilidad de módulos, que este ADR **amplía** sin cambiar sus
  reglas), ADR-0041 (inventario, el primer módulo con submódulos), Art. 8, Art. 9
- **Especificación:** `docs/spec/feature-0017-modulos-y-submodulos/spec.md`

## Contexto

El ADR-0019 introdujo un interruptor por módulo completo (`FarmModule`: `key`, `enabled`,
`disabled_reason`). Se siembran seis: `production`, `livestock`, `inventory`, `breeding`,
`tasks` y `people`. Hechos al 2026-09-23:

- **El cliente del bloque 1 pidió poder activar y desactivar lo que no usa**, con una
  sección propia en el panel, y que el teléfono lo refleje al sincronizar.
- **El módulo completo es demasiado grueso** para el inventario nuevo. Una finca que no
  fabrica alimento no tiene por qué ver "Preparar alimento", aunque use el resto del
  inventario.
- **El teléfono respeta los interruptores** (`clients/field-app/src/services/moduleVisibility.ts`).
  **El panel web no:** solo los muestra en una pestaña de Catálogos
  (`catalogs.component.ts:125`), y su menú muestra los módulos apagados igual que los
  encendidos.

## Decisión

**`FarmModule.Key` admite claves jerárquicas separadas por punto. Un nodo es visible solo si
él y todos sus ancestros están encendidos.**

1. **Mismo mecanismo, sin otro paralelo.** Un submódulo es una fila más de `FarmModule`,
   con clave `padre.hijo` (`inventory.transformations`). La normalización de la clave
   (minúsculas, sin espacios) no cambia.
2. **La visibilidad es la conjunción de toda la cadena.** Apagar `inventory` oculta todos
   sus hijos sin tocar sus filas. Al volver a encenderlo, cada hijo recupera su propio
   estado.
3. **Todas las reglas del ADR-0019 siguen vigentes para cada nivel.** En particular la 4:
   apagar oculta la entrada, pero **los endpoints siguen aceptando lo que ya estaba
   registrado**. Una operación hecha offline antes de que el teléfono supiera del apagado
   sube y se acepta.
4. **El panel web también respeta los interruptores.** Su menú y sus rutas se filtran con
   la misma regla que el teléfono. Los interruptores tienen una **sección propia,
   "Módulos"**, con los submódulos anidados bajo su padre.
5. **Crear submódulos es una decisión de cada módulo, no de este ADR.** Este ADR no divide
   ningún módulo existente. El inventario crea los suyos en su spec (ADR-0041, feature-0016).
   Dividir `breeding` u otros queda para cuando un cliente lo necesite
   (`docs/BACKLOG.md`).
6. **Un submódulo no puede apagar su propia pantalla de administración.** La sección
   "Módulos" depende del permiso de administrador, no de ningún interruptor (ADR-0019,
   regla 6).

## Alternativas consideradas

**Seguir con módulos completos.** No cambia nada, pero "Preparar alimento" queda visible
para quien no lo usa, y la primera división futura se diseña desde cero. Descartada.

**Una tabla de funciones (`feature flags`) separada de `FarmModule`.** Sería más general,
pero habría dos mecanismos de visibilidad que el teléfono y el panel tendrían que combinar.
El ADR-0019 ya descartó un mecanismo paralelo por esa misma razón. Descartada.

**Relación padre-hijo con una columna `parent_id`.** Es más explícita, pero exige una
migración de esquema y un árbol que mantener, cuando la clave con punto ya codifica la
jerarquía y el cliente la evalúa sin consultas extra. Descartada. Si algún día hace falta
ordenar o describir el árbol, se agrega sin romper las claves.

**Dividir ya todos los módulos existentes.** El cliente del bloque 1 tiene `breeding`
apagado completo y no lo necesita. Hacerlo ahora es tocar módulos ajenos al bloque
(regla 9 de `AGENTS.md`). Descartada.

## Consecuencias

**Lo bueno.** El cliente oculta lo que no usa, sin desarrollador y en todos los canales. El
panel deja de mostrar módulos apagados. El mecanismo queda listo para dividir cualquier
módulo cuando haga falta.

**Lo malo.** La visibilidad depende ahora de una cadena, no de una fila. Un error en la
evaluación de ancestros ocultaría o mostraría pantallas equivocadas en los dos clientes.

**Lo que hay que vigilar.**
- La regla de la cadena se implementa **dos veces**: en el teléfono (TypeScript) y en el
  panel (TypeScript, Angular). Ambas llevan las mismas pruebas de tabla.
- Un teléfono con una versión anterior de la app **ignora los submódulos**: solo evalúa
  claves de módulo completo. Seguirá mostrando su pantalla de consumo de alimento por lote
  aunque `inventory.usages` esté apagado, hasta que se actualice. Los endpoints lo aceptan
  igual (decisión 3), así que no se pierde nada, pero hay que decirlo en la nota de la
  versión.

**Condición de reversa.** Se reabre si hace falta más de dos niveles de jerarquía con
reglas propias, o una visibilidad que no sea la conjunción de la cadena.
