# ADR-0022 — Rangos de plausibilidad por especie y categoría: configuración, no constante; fail-open, no bloqueo

- **Estado:** Aceptado
- **Fecha:** 2026-08-07
- **Aceptado en rama:** `feature/livestock-plausibility-ranges` (3.5a.6) — pendiente de merge
- **Fase del roadmap:** Fase 3.5 — Adaptación porcina — bloque 3.5a

## Contexto

El cliente reportó que la app acepta 1000 litros de una sola vaca
([`PLAN-FASE-3-5-PORCINO.md` sec.3.5a.6](../spec/PLAN-FASE-3-5-PORCINO.md)). El techo
no puede ser una constante: un lechón al nacer no pesa lo que un cerdo de engorde, ni
una cerda lactante produce lo que una novilla. Y no puede ser un `if (especie == ...)`
por la razón de siempre (Art. 8): una nueva especie o una nueva categoría no pueden
exigir un deploy.

Hoy el problema se resuelve en dos capas que no se hablan:

- **Capa cliente (código existente,
  [`milkingService.ts`](../../clients/field-app/src/services/milkingService.ts)
  líneas 14–25 y 273 ss.):** bloquea entradas imposibles con reglas fijas en código
  (`liters > 0`). La regla existe pero no por especie ni por categoría. Un valor de
  1000 L no es "físicamente imposible" para esa capa, así que pasa.
- **Capa servidor (no existe):** no hay validación por rango. Un `recordMilking`
  con 1000 L pasa si la capa cliente la deja pasar.

El resultado es exactamente lo que el cliente vio: el sistema acepta 1000 L porque la
única defensa contra lo absurdo es una defensa contra lo físicamente imposible, no
contra lo biológicamente inverosímil.

Tres restricciones de diseño que el camino correcto tiene que respetar a la vez:

1. **Configurable, no hardcodeada** (Art. 8): un lechón al nacer, una cerda de
   reemplazo, un novillo de 400 kg — cada combinación tiene techo propio. Y mañana
   otra especie o categoría entra al sistema sin tocar código.
2. **Evaluada localmente en el móvil** (Art. 9): la app no puede pedirle a un
   operario con guantes en el potrero que confirme un valor "porque el servidor lo
   rechazó". Una regla que sólo funciona con señal no es una regla — exactamente el
   argumento que ya se usó para el período de retiro en `milkingService`.
3. **Compatible con la realidad que el operario ve**: si la semilla de rangos no
   existe o está incompleta, el sistema **no puede bloquear el registro** — porque
   un rango olvidado no puede ser más fuerte que un dato verdadero.

La última restricción es la que rompe con el resto del sistema. `Species.IsMilkable`
es fail-closed por default (default `false`) y los permisos son fail-closed por
default: una especie desconocida o un permiso no concedido se rechazan, porque son
reglas de seguridad y el costo de un falso positivo es bajo. **Acá el razonamiento
es inverso**: el costo de un falso positivo (rechazar un dato real del campo) es
mayor que el costo de un falso negativo (aceptar un valor raro que el operario
acaba de tipear). El daño de perder un registro real es peor que el ruido de
aceptar un valor sospechoso.

Ese último punto es la decisión central de este ADR y la que lo separa del patrón
de los demás catálogos: el diseño no es simétrico a `IsMilkable` ni a los
permisos. La simetría se rompe a propósito.

## Decisión

### 1. Una tabla de rangos por especie y categoría, configurable, sin enum en código

```
plausibility_ranges
  species_id       (FK species, NOT NULL)
  category_id      (FK animal_categories, NULL)   -- NULL = aplica a todas las categorías de la especie
  magnitude        (string: 'weight_kg' | 'milk_liters' | 'dose_ml' | …)
                   -- Art. 8: dato, no enum en código. La lista la da el catálogo sembrado.
  plausible_min    (numeric, nullable)            -- por debajo: "¿es correcto?"
  plausible_max    (numeric, nullable)            -- por encima: "¿es correcto?"
  absolute_min     (numeric, nullable)            -- por debajo: rechazo, imposible
  absolute_max     (numeric, nullable)            -- por encima: rechazo, imposible
  is_active        (bool, default true)
  updated_at, updated_by
  UNIQUE (species_id, category_id, magnitude)
```

`category_id` nulable cubre el caso "rango aplica a toda la especie" (por ejemplo,
leche en litros: si la especie es ordeñable, el rango aplica a todas sus
categorías). Cuando una combinación tiene un rango más fino por categoría, gana el
específico.

`magnitude` es **una columna string**, no un enum en C#. Agregar una magnitud nueva
("dosis en ml", "intervalo en días") es un INSERT, no un deploy — el mismo
tratamiento que el resto de los catálogos (Art. 8).

### 2. Tres niveles de respuesta, todos del lado cliente

| Estado del valor                                                     | Respuesta del cliente                                                         |
|----------------------------------------------------------------------|-------------------------------------------------------------------------------|
| Dentro de `[plausible_min, plausible_max]`                           | Pasa sin fricción                                                             |
| Fuera de plausibles pero dentro de absolutos                         | **Confirmación explícita** ("1000 L es mucho más de lo normal. ¿Es correcto?") |
| Fuera de `[absolute_min, absolute_max]`                              | **Bloqueo**: rechazo local, el registro no entra al outbox                    |

La regla de los tres toques no se rompe: la confirmación del valor improbable vive
**dentro** del mismo toque, no como pantalla separada. La integración concreta con
`VaccinateScreen` y `TreatScreen` (3.5a.2-C) es responsabilidad del sub-plan C;
acá se fija el contrato de comportamiento.

### 3. Fail-open por diseño, no por descuido

**Una especie recién registrada, una categoría nueva, o una magnitud sin fila en
`plausibility_ranges` dejan al sistema sin rango para esa combinación. La
respuesta es: el sistema acepta el valor como si estuviera dentro de plausibles.**

Esto es una decisión deliberada y opuesta al patrón de `Species.IsMilkable`
(fail-closed, default `false`) y de los permisos (fail-closed por default). La
simetría se rompe porque las consecuencias son asimétricas:

- **Fail-closed en `IsMilkable`:** rechazar ordeño de una cerda — costo bajo, se
  arregla con un toggle del panel.
- **Fail-closed en plausibilidad:** rechazar un pesaje real de 350 kg de un toro
  adulto porque nadie sembró el rango para "bovino macho > 24 meses" — costo alto,
  dato perdido.

La formalización del concepto vive en el glosario bajo `Modo fail-open`
([`GLOSSARY.md`](../GLOSSARY.md), sección "Validación configurable (transversal)").
Allí se contrasta con el fail-closed de permisos e `IsMilkable` y se listan los
sitios donde aplica.

**Consecuencia operativa:** la **semilla inicial** (siguiente punto) es parte del
criterio de salida de 3.5a, no un nice-to-have. Sin semilla, la rama no detecta
nada, que es exactamente el modo fail-open. El macro plan sec.3.5a.6 punto 5 ya
lo deja explícito; se vuelve a recordar acá porque es el punto donde el fail-open
se vuelve invisible.

### 4. La semilla existe desde el día 1 de la rama

Rangos iniciales razonables para `weight_kg` (lechón al nacer, cerda gestante,
cerdo de engorde, bovino lechero, bovino de carne, reproductor porcino) y
`milk_liters` (vaca en producción, cabra). Ajustables desde el panel por el
cliente. La lista fina la da el cliente en la sesión sec.7-C del macro plan; la
rama se compromete a cargar una semilla razonable mientras eso llega.

### 5. La UI de confirmación es honesta con la regla

El texto que ve el operario distingue los tres casos:

- Valor dentro de plausibles: nada — pasa.
- Valor fuera de plausibles pero dentro de absolutos: *"350 kg es mucho más de lo
  normal para este animal (esperado: 80–250 kg). ¿Es correcto?"* — y un botón
  "Sí, registrar" que sigue.
- Valor fuera de absolutos: rechazo. *"Este valor está fuera de lo posible para
  este animal. Verifica el dato."* El registro no se envía al outbox.

La confirmación **se persiste**: si el operario confirma, el evento se registra
con `is_plausibility_confirmed = true` (campo del payload) para que el día que se
analicen outliers no haya que reconstruir lo que el operario vio.

### 6. Las dos capas se coordinan, no se contradicen

[`milkingService.ts`](../../clients/field-app/src/services/milkingService.ts)
línea 273 ss. bloquea `liters <= 0` y rechaza litros negativos. Esa defensa sigue
siendo el **piso absoluto** de la capa cliente: un valor negativo es físicamente
imposible y la capa cliente lo rechaza **antes** de que la capa de plausibilidad
entre a evaluarlo. La capa de plausibilidad opera **sobre lo que pasó el filtro
de imposibilidad**. Las dos capas son secuenciales, no redundantes.

El servidor, cuando exista la validación de plausibilidad (futuro), no agrega una
tercera capa: el cliente es la fuente de verdad para la captura y el servidor
confía en `is_plausibility_confirmed` cuando lo ve. Si el servidor encontrara un
valor fuera de rangos sin la bandera, lo registra igual (mismo fail-open) pero lo
marca en la bitácora de inconsistencias para revisión.

### 7. Sync local: el rango es mirror, no fetch

El pull ya entrega catálogos espejo. El patrón vigente es `mortality_causes` —
[`schema.ts`](../../clients/field-app/src/database/schema.ts) tabla homónima,
sembrado por
[`migrations.ts`](../../clients/field-app/src/database/migrations.ts) (línea 142),
y la colección correspondiente en
[`SyncPullQueries.cs`](../../src/Hato.Api/Sync/SyncPullQueries.cs) línea 238
detrás del permiso `LivestockAnimalsRead`. El rango de plausibilidad sigue el
mismo patrón:

- Tabla espejo local `plausibility_ranges` (mismas columnas que el servidor).
- Colección nueva en el pull, detrás de `LivestockAnimalsRead`, junto a
  `mortalityCauses`.
- La capa cliente lee siempre desde la tabla local; **nunca llama al servidor en
  tiempo de captura** (Art. 9).

El sub-plan 3.5a.2-C materializa la parte de C.1 (sync local) que hace
alcanzable esta tabla para `VaccinateScreen` y `TreatScreen`.

## Alternativas consideradas

- **Constantes hardcodeadas por especie en código.** Descartada por Art. 8 y
  porque convierte "qué es razonable para un bovino lechero" en un problema de
  programación, no de configuración. Una nueva especie o una nueva categoría
  requeriría un deploy.

- **Fail-closed con default seguro** (rechazar si no hay rango). Descartada con
  argumento explícito: el costo de perder un dato real es mayor que el costo de
  aceptar un valor raro. La simetría con `IsMilkable` y permisos es tentadora
  pero falsa — esas son reglas de seguridad (costo de falso positivo bajo); la
  plausibilidad es una regla de captura (costo de falso positivo alto: un dato
  del campo perdido para siempre).

- **Validación sólo en el servidor.** Descartada por Art. 9. Un control que
  sólo funciona con señal no es un control — exactamente el argumento que ya se
  usó para el retiro en `milkingService`. Aplica igual acá.

- **Sin catálogo: reglas fijas en `milkingService.ts` por especie.** Es lo que
  existe hoy. No detecta "1000 L" porque no conoce el rango. Es la versión que
  el cliente señaló como insuficiente.

- **Reusar `Species.IsMilkable` con campos adicionales `MaxLiters`/`MaxKg`.**
  Descartada: acoplaría dos decisiones que hoy son independientes. Una especie
  ordeñable podría no tener techo de litros (la configurabilidad tiene que vivir
  aparte). Además, una magnitud puede no tener nada que ver con leche (peso,
  dosis, futura distancia recorrida).

- **Confirmación por vibración o toast sin persistir la confirmación.**
  Descartada: deja el dato sin huella de que el operario lo confirmó. Cuando
  tres meses después alguien revise outliers en el panel, no hay forma de
  distinguir el valor confirmado del tecleado por error. Persistir la
  confirmación es barato y resuelve ese problema.

## Consecuencias

### Positivas

- El Art. 8 se cumple en magnitudes físicas (peso, litros, futuro: dosis):
  agregar una magnitud nueva es un INSERT, no un deploy. Lo mismo que el resto
  de los catálogos.
- El Art. 9 se cumple: la captura funciona sin red, porque el rango es mirror
  local. La garantía es del mismo tipo que `withdrawal_periods` y
  `mortality_causes`.
- El sistema nunca rechaza un dato del campo por falta de configuración: el
  operario nunca queda bloqueado por un INSERT que alguien olvidó hacer en el
  panel. La disponibilidad del registro es la prioridad, porque un registro
  perdido no se recupera.
- La confirmación se persiste, así que la analítica futura (outliers,
  comparación contra el plan sanitario) puede distinguir "valor confirmado por
  el operario" de "valor tecleado y aceptado en silencio".
- La capa cliente de imposibilidad (`liters > 0` y similares) sigue funcionando
  como red de seguridad, sin solaparse con la plausibilidad.

### Negativas / costos

- La simetría con `IsMilkable` y permisos se rompe a propósito. Quien lee el
  sistema por primera vez va a extrañar que "lo desconocido" no se rechace. La
  razón está en este ADR y se referencia desde el comentario en el código
  (`schema.ts`, `milkingService.ts`); conviene repetirla en ambos archivos al
  implementar.
- La semilla inicial es requisito del criterio de salida, no opcional. Sin
  semilla, la rama no detecta nada y pasa inadvertida en silencio. ADR-0020 ya
  tiene este punto como pendiente explícito (sec.2: "3.5a.6 (plausibility
  ranges) — … Se difiere el pesaje muestral hasta que 3.5a.6 exista").
- Tres niveles de respuesta (pasa / confirma / bloquea) requieren una UI que
  sepa cuándo mostrar confirmación. La integración con `VaccinateScreen` y
  `TreatScreen` (3.5a.2-C.2) tiene que coordinarse con la captura de la
  confirmación.
- El servidor confiará en `is_plausibility_confirmed` del cliente. Esto
  significa que un cliente comprometido podría aceptar valores fuera de rango
  sin mostrar la confirmación. Es la misma forma de confianza que ya se le da al
  resto de la captura offline; la amenaza es equivalente y la mitigación
  (auditoría periódica en el panel) es la misma.

### Condición de reversa

Si tras un ciclo de engorde real la distribución de valores confirmados como
"improbables" es concentrada —siempre los mismos rangos y las mismas especies—,
la confirmación se vuelve ruido y se evalúa endurecer el `plausible_max` por
defecto (estrechar la semilla). La señal a medir es concreta: **proporción de
eventos con `is_plausibility_confirmed = true` por especie y magnitud**. Si pasa
del 5% estable, la semilla es demasiado laxa; si pasa del 0% durante un
trimestre, la confirmación nunca aparece y la UI probablemente no la muestra.

Si, al revés, el operario reporta fricciones reales con el bloqueo del absoluto
("300 kg es posible, no me dejó"), se reabre el ADR con el dato concreto y se
evalúa ampliar `absolute_max` por defecto. La semilla es un punto de partida, no
una reja.
