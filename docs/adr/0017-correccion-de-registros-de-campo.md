# ADR-0017 — Corregir un error de dedo desde el campo sin editar el pasado

- **Estado:** Propuesto
- **Fecha:** 2026-08-05
- **Fase del roadmap:** Fase 3.5 (Adaptación porcina) — bloque 3.5a

## Contexto

El cliente evaluó la app y su devolución fue precisa: *"está pensada para tres toques o
menos, y eso está genial, pero no valida el factor humano — no hay forma de corregir un
error"*. Dio el ejemplo exacto: en la pantalla de partos, si te equivocás y agregás una
hembra de más, **no podés quitarla**; sólo cancelar todo y empezar de nuevo.

Tiene razón, y el alcance del problema es mayor que esa pantalla. La app **no tiene ningún
camino de corrección**, para ningún registro:

- `BirthScreen.tsx:33` sólo sabe agregar crías (`addCalf`); no hay quitar ni editar.
- Los eventos registrados (tratamiento, pesaje, movimiento) no tienen pantalla que los
  liste, y por lo tanto no hay nada que se pueda señalar para corregir. Sólo la leche tiene
  un resumen del día (`milk_yields` + `DailySummary` en `milkingService.ts`).
- Un `recordTreatment` con la dosis mal tecleada se va al outbox y de ahí al servidor, y el
  operario no tiene forma de intervenir en ningún punto.

Lo llamativo es que **el modelo de dominio ya previó esto y nadie lo usó**:
`AnimalEvent.RelatedEventId` existe (`AnimalEvent.cs`), `EventType.Correction` existe
(`EventEnums.cs`), y el Art. 1 define la regla desde la fundación: *"un error se corrige
con un evento de corrección que referencia al original, nunca editando el pasado"*. La
pieza que falta es enteramente del cliente móvil.

Y ahí aparece la complicación real, que es específica de offline-first (Art. 9): **un
registro hecho en el potrero puede estar en dos situaciones muy distintas** cuando el
operario se da cuenta del error, y confundirlas produce o bien corrupción de datos o bien
eventos de corrección que corrigen algo que nunca existió.

## Decisión

**Dos caminos, elegidos por dónde está el registro. Nunca uno solo.**

### Camino A — la operación sigue en el teléfono

Si la entrada del outbox está en `pending` y no ha salido, **el servidor nunca supo de
esto**. No hay hecho que corregir: para el sistema, nunca ocurrió. Se marca la entrada como
cancelada y se acabó.

Emitir aquí un evento de corrección sería peor que inútil: crearía en el servidor un par
"evento equivocado + su corrección" para un hecho que jamás salió del dispositivo,
ensuciando el historial con un error que nadie cometió fuera del teléfono.

**`OutboxStatus` gana un cuarto valor: `cancelled`.** Hoy admite exactamente
`'pending' | 'synced' | 'rejected'` (`outbox.ts:6`), así que este estado **no existe y hay
que agregarlo** — con su migración de esquema local, porque un teléfono en el campo no se
puede reinstalar sin perder la cola (`migrations.ts`).

La entrada **no se borra**. El propio `outbox.ts:31` ya fija la regla de la casa —*"nothing
is ever removed"*— y coincide con el Art. 1. Una cancelación es un hecho auditable: alguien
registró algo y se retractó antes de que saliera.

### Camino B — la operación ya sincronizó

Se encola una operación nueva de tipo `Correction` que referencia al evento original vía
`RelatedEventId`. El original queda intacto (Art. 1), y el historial muestra ambos: lo que
se dijo y lo que se corrigió.

El cliente **conoce el ID del evento en el servidor** porque `sync_outbox.result_ref` ya lo
guarda (`schema.ts`) — la pieza necesaria ya está en su lugar y sin uso.

### La condición de carrera, que es el punto delicado

Entre que el operario toca "corregir" y que el motor de sync empuja el lote pueden pasar
milisegundos. Si se decide el camino leyendo el estado y otro hilo lo cambia, se cancela
localmente algo que ya salió — y queda un registro fantasma en el servidor que nadie sabe
que existe.

**La transición `pending → cancelled` debe ser una escritura condicional atómica dentro de
la misma transacción de WatermelonDB que verifica el estado.** Si al momento de escribir el
estado ya no es `pending`, la cancelación falla y la UI cae al camino B automáticamente,
sin preguntarle nada al usuario. El operario no debe enterarse nunca de que existen dos
mecanismos: él tocó "corregir" y el registro quedó corregido.

### Quién puede corregir y hasta cuándo

- **El registrador**: el mismo día calendario de la fecha de registro. Cubre el caso real
  —darse cuenta en la jornada— sin abrir la puerta a reescrituras tardías de un mes atrás.
- **El administrador, desde el panel**: sin límite de tiempo. Siempre queda como evento de
  corrección con autor y fecha, nunca como edición.
- La corrección **hereda el permiso del registro que corrige**: quien no puede registrar un
  tratamiento tampoco puede corregir uno.

### Que sea alcanzable

Una pantalla **"lo que registré hoy"** que liste los registros del día con su estado de
sincronización y permita corregir cada uno. Sin ella la decisión es teórica: hoy no existe
ninguna superficie donde un evento ya registrado sea señalable. Es la generalización de lo
que el resumen diario de leche ya hace bien.

Y en el parto, específicamente: poder **quitar y editar una cría** antes de confirmar, más
una pantalla de resumen previa. Ese es el error que el cliente reportó, y ocurre **antes**
de que exista registro alguno — no necesita ninguno de los dos caminos, sólo que la
pantalla deje deshacer.

## Alternativas consideradas

- **Permitir editar el evento en el servidor.** Descartada: viola el Art. 1, que no es
  negociable sin ADR que lo enmiende conscientemente. Además destruiría la propiedad que
  hace barato el sync: `animal_events` es append-only, y de ahí sale que los conflictos
  sean mínimos por diseño (ADR-0008).

- **Sólo el camino B (siempre evento de corrección).** Descartada: crea en el servidor
  pares "error + corrección" de hechos que nunca salieron del teléfono. Contamina el
  historial con ruido puramente local y hace que el expediente de un animal sea más difícil
  de leer justamente por haber sido cuidadoso.

- **Sólo el camino A (borrar de la cola, y si ya salió, aguantarse).** Descartada: deja sin
  resolver el caso más común en el campo real, que es darse cuenta horas después, cuando el
  teléfono ya encontró señal.

- **Ventana de gracia: retener las operaciones N minutos antes de empujarlas.** Descartada,
  pese a ser elegante: contradice el Art. 9. La app debe empujar apenas hay señal, porque en
  esta finca la señal es el recurso escaso — retener a propósito es apostar a que habrá otra
  ventana de conectividad más tarde.

- **Borrar la entrada cancelada en vez de marcarla.** Descartada por el Art. 1 y por la
  regla que el propio `outbox.ts` ya se había impuesto. Una retractación es información.

## Consecuencias

- **Positivas**:
  + El operario tiene un solo gesto ("corregir") y el sistema elige el mecanismo. La
    promesa de los tres toques se mantiene: la complejidad la absorbe el software, que es
    de quien era el trabajo.
  + Se activan tres piezas que llevaban dos fases construidas y sin uso:
    `AnimalEvent.RelatedEventId`, `EventType.Correction` y `sync_outbox.result_ref`.
  + El Art. 1 deja de ser una regla que sólo el backend respeta porque el móvil no ofrecía
    manera de violarla, y pasa a estar implementado de punta a punta.
  + La pantalla "lo que registré hoy" tiene valor propio más allá de la corrección: es la
    primera superficie donde un empleado puede verificar su propia jornada.

- **Negativas / costos**:
  − Un cuarto estado en el outbox obliga a revisar cada consulta que hoy asume tres. En
    particular `stats()` y la bandeja de problemas (`outbox.ts:141-147`) cuentan por estado
    y quedarían mudas sobre las cancelaciones. **Mitigación**: el tipo es una unión de
    literales en TypeScript, así que agregar el valor rompe el build en cada `switch` no
    exhaustivo — falla ruidosamente al compilar, no en silencio en el potrero.
  − La escritura condicional atómica es la clase de código que parece correcto y falla una
    vez cada mil veces. **Mitigación**: es obligatoria una prueba que fuerce la carrera
    (cancelar mientras un push está en vuelo) y verifique que no queda registro fantasma;
    va en la suite `Hato.Sync.IntegrationTests`, que ya existe justo para esto y ya
    encontró un bug real del cliente con los escenarios de corte a mitad de lote.
  − La ventana de "mismo día calendario" será insuficiente alguna vez —el error que se
    descubre al día siguiente—, y esos casos escalan al administrador. Se acepta: la
    alternativa es una ventana laxa que convierte la corrección en edición encubierta.

- **Condición de reversa**: si durante el piloto el camino A resulta ser prácticamente todo
  el uso (el operario siempre se da cuenta antes de que haya señal), se puede simplificar la
  UI para que el camino B viva sólo en el panel de administración. La señal a medir es
  concreta: **proporción de correcciones por camino A frente a camino B** durante el primer
  mes.
