# ADR-0040 — Reorientación del roadmap por el cambio de cliente piloto

- **Estado:** Propuesto
- **Fecha:** 2026-09-23
- **Fase del roadmap:** transversal — pausa la Fase 3.5 y adelanta la Fase 4
- **Relacionado:** ADR-0024 (desacople del piloto de 3.5a), ADR-0026 (recepción de
  inventario mínima pre-Purchasing), ADR-0033 (producción privada), ADR-0034 (backups
  cifrados), Art. 11, Art. 18

## Contexto

Hechos al 2026-09-23:

- **El cliente del piloto de la Fase 3.5 se desvinculó en septiembre de 2026.** No dio
  motivo. Conserva el software, que se publica con licencia MIT, y no quiso más servicio.
  Su salida ocurrió después de las tres semanas de fallos intermitentes de sincronización
  que motivaron feature-0004 (`docs/ROADMAP.md`, Fase 3, actualización del 2026-09-07). No
  hubo una queja técnica explícita que las vincule.
- **Hay un cliente nuevo**, también porcino. Lo que pide, según conversaciones informales
  que no se han levantado formalmente, cae en tres subsistemas distintos:
  1. **Inventario** de alimento y fármacos, con entradas y salidas.
  2. **Activos y herramientas**, un concepto que el modelo no tiene: no se consumen como un
     insumo, se asignan y se deprecian.
  3. **Gestión de pagos**, con registro de facturas emitidas fuera del sistema. La emisión
     de factura electrónica queda excluida.
- **Los tres pertenecen a la Fase 4 o más allá.** Inventario y pagos son Purchasing y parte
  de Accounting (Fase 4). "Maquinaria/activos" está hoy en la Fase 7+. En cambio, la
  `docs/spec/plan-0003-fase-4/spec.md` describe la Fase 4 como un trabajo "a uno o dos años
  de distancia".
- **Los criterios de salida que quedan abiertos dependen del cliente que se fue.** El de
  3.5a exige una camada real seguida hasta su clasificación por peso a los 24 días, y el de
  3.5b un ciclo de engorde con su conversión alimenticia calculada y una decisión del
  cliente tomada con ella. La Fase 3 exige una validación manual en el teléfono físico de
  la finca. Ninguno se puede cumplir ya donde se planificó.
- **El procedimiento de producción asume que se migrarán los datos del piloto.**
  `docs/PRODUCCION.md` sec. 3 y `docs/PREFLIGHT-PRODUCCION.md` describen un corte de datos
  desde el servidor del piloto, y ese corte es una de las compuertas de apertura
  (`docs/PRODUCCION.md` sec. 7).
- **El ROADMAP no refleja nada de esto.** Sigue presentando la Fase 3.5 como en curso, con
  la 3.5b como lo siguiente.

## Decisión

**Pausar la Fase 3.5 sin borrar nada, adelantar la Fase 4 como siguiente trabajo de
producto y no fijar su alcance hasta que un levantamiento formal con el cliente nuevo
produzca un recorte justificado.**

1. **La Fase 3.5 queda en pausa** desde el 2026-09-23, según la regla anti-estancamiento 4
   (pausar se permite; abandonar en silencio no).
   - No se borra ni se desactiva código, prueba, endpoint, migración ni dato. El dominio
     porcino ya construido (lote por conteo, eventos grupales, tratamientos estructurados,
     rangos de plausibilidad, clasificación por peso) sigue vigente y disponible.
   - La deuda de 3.5a (3.5a.8, la pantalla de causas de muerte de 3.5a.3, 3.5a.4 task 4) y
     todo el bloque 3.5b siguen documentados en `docs/spec/plan-0002-fase-3-5/`, pero no
     bloquean nada ni se trabajan por defecto.
   - Los criterios de salida de 3.5a y 3.5b no se declaran cumplidos ni se reescriben. Si la
     fase se reanuda, se revisan con la finca donde se vayan a cumplir.
2. **La Fase 4 pasa a ser el siguiente trabajo de producto.** Esto adelanta el trabajo sin
   cambiar el orden de las fases en el ROADMAP.
3. **El recorte lo fija el dueño y el levantamiento valida el detalle.** El 2026-09-23 el
   dueño fijó el recorte con el cliente nuevo:
   - **Bloque 1: inventario de alimento y fármacos llevado como kardex**, con entradas con
     y sin factura. Se especifica en `docs/spec/feature-0016-inventario-kardex/` y su modelo
     en [ADR-0041](0041-inventario-kardex-derivado.md).
   - **Bloque 2: ventas informativas y un resumen de lo invertido, lo que hay en bodega, lo
     consumido y lo vendido.** No hay baja del animal, ni cuentas por cobrar, ni
     contabilidad.
   - **Fuera:** activos y herramientas, pagos y cuentas por pagar, y emisión de
     comprobantes.

   El levantamiento de requisitos con el cliente, siguiendo ISO/IEC/IEEE 29148, ya no decide
   el alcance. Sirve para **validar el detalle** de cada bloque: qué ítems, qué documentos,
   qué flujo en bodega.
4. **La Fase 3 sigue abierta con el mismo criterio de salida.** La validación manual
   pendiente (`test-e2e.md` de feature-0004 en SQLite nativo) se hará en un teléfono de la
   finca que use el sistema de verdad, que a la fecha solo puede ser la del cliente nuevo.
5. **Producción arranca sin datos.** Los datos del piloto anterior no se migran. El corte de
   datos de `docs/PRODUCCION.md` sec. 3 deja de ser una compuerta de apertura. Las demás
   compuertas (restauración probada de backups y distribución de Android) siguen en pie.
   Los datos del piloto anterior **no se borran**: se quedan donde están hasta decidir
   explícitamente qué hacer con ellos (ver Consecuencias).

## Alternativas consideradas

**Seguir con la 3.5b como estaba planificada.** Su criterio de salida exige una decisión de
manejo del cliente tomada con la conversión alimenticia calculada por el sistema. Sin ese
cliente no hay quién la tome, y la 3.5b se volvería una fase que no puede cerrarse (Art. 11).
El cliente nuevo también es porcino, pero no ha pedido nada de la 3.5b. Construirla primero
sería construir para una finca que no la pidió.

**Cerrar la 3.5a como está y sacar la 3.5b del roadmap.** Declarar cerrada una fase con el
criterio de salida sin cumplir es justo lo que se revirtió en la Fase 3 el 2026-08-03. Sacar
la 3.5b descarta un análisis que puede volver a tener dueño. Pausar cuesta lo mismo y no
cierra ninguna puerta.

**Comprometer los tres subsistemas que pidió el cliente nuevo.** Comprometerse con
inventario, activos y pagos a la vez repite el riesgo de construir mucho antes del primer
uso real, que la regla anti-estancamiento 1 obliga a corregir recortando. Por eso el recorte
deja un bloque en marcha, uno después y el resto fuera.

**Esperar al levantamiento formal para fijar el recorte.** Era lo que proponía la primera
redacción de este ADR. Se descartó porque el dueño y el cliente ya acordaron el recorte, y
esperar solo retrasaba el primer uso real. El levantamiento sigue siendo necesario, pero
para el detalle.

**Migrar de todos modos los datos del piloto anterior a producción.** Serían datos de una
operación que ya no usa el sistema, mezclados con los del cliente nuevo en la misma base.
Tampoco hay un cliente que confirme que el origen es el real, que es uno de los pasos de
validación del corte.

## Consecuencias

**Lo bueno.** El roadmap vuelve a decir la verdad sobre qué es lo siguiente. El dominio
porcino construido no se pierde y le sirve al cliente nuevo. El levantamiento formal obliga
a recortar antes de construir. Producción se abre con una compuerta menos, la que menos
dependía del equipo.

**Lo malo.** La deuda de 3.5a se queda sin fecha. El plan de la Fase 4 existe solo como
spec, sin ADRs de modelo, y ahora pasa a ser urgente. `docs/spec/plan-0003-fase-4/spec.md`
sec. 3 lista las preguntas abiertas que habrá que responder.

**Lo que hay que vigilar.**

- **Activos y herramientas es un concepto nuevo.** Si el recorte lo incluye, necesita
  entrada en `GLOSSARY.md` y un ADR de modelo antes del código (Art. 14), y no puede nacer
  como un `if` por tipo de bien (Art. 8).
- **Registro de pagos sin emisión de factura es coherente con el Art. 18.** Pero la línea
  "Comprobantes vía proveedor autorizado SRI" de la Fase 4 en el ROADMAP queda sin
  confirmar: nadie la pidió. El levantamiento dirá si se queda en la Fase 4 o se difiere.
- **ADR-0026 declaró que la recepción de inventario mínima quedaría deprecada al llegar
  Purchasing.** El bloque 1 incluye entradas de inventario, así que esa deprecación deja de
  ser teórica. La resuelve el ADR-0041.
- **Los datos del piloto anterior siguen en custodia de este proyecto.** Qué hacer con
  ellos (conservarlos, entregarlos o eliminarlos) es una decisión aparte, con implicaciones
  de la LOPDP. Se anota en `docs/BACKLOG.md` y no se resuelve aquí.
- **`docs/PREFLIGHT-PRODUCCION.md` describe el inventario del piloto anterior.** Deja de ser
  un paso previo a la apertura, pero no se borra: sus prohibiciones sobre el teléfono del
  empleado siguen siendo la referencia si algún día hay que cortar datos de una finca real.

**Condición de reversa.** Se reabre esta decisión si el levantamiento muestra que lo que el
cliente nuevo necesita primero es análisis de engorde (3.5b) y no inventario. También si el cliente anterior vuelve o aparece otra finca que cumpla los criterios
de salida de la 3.5 tal como están escritos.
