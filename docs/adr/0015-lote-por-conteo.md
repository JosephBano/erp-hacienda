# ADR-0015 — Lote por conteo: cuando el sistema no sabe cuál animal es cuál

- **Estado:** Propuesto
- **Fecha:** 2026-08-05
- **Fase del roadmap:** Fase 3.5 (Adaptación porcina) — bloque 3.5a

## Contexto

El cliente del piloto maneja **porcinos de engorde sin identificación individual**. Su
operación real, tal como la describió:

1. Las camadas nacen en días consecutivos (lunes de una madre, martes de otra, miércoles
   de otra). Hay 3 cerdas madres, esas sí identificadas con su ID legal.
2. Apenas nacen se **pesan uno por uno** — dato que él usa para decidir futuras madres
   (una hembra de ≥1 kg promete; una de <0.7 kg probablemente no crezca).
3. Durante ~24 días los lechones siguen con su madre. El período se cuenta hasta que **la
   última camada** los cumple, no camada por camada.
4. Cumplidos los 24 días **se mezclan todos y se reclasifican por peso** en corrales
   (pequeños con pequeños, grandes con grandes). Desde ese momento, y hasta la faena,
   **nadie sabe cuál cerdo es cuál**. No hay aretes.

El modelo actual no puede representar el punto 4. Todo el sistema descansa sobre el
supuesto *animal = individuo con UUID* (Art. 3), y en particular:

- `AnimalEvent.Create` exige `animalId != Guid.Empty`
  (`Livestock.Domain/AnimalEvent.cs:58`). **No existe forma de registrar un hecho que le
  ocurrió a un lote.**
- `AnimalGroup.AddMember` / `RemoveMember` operan sobre un `animalId` concreto
  (`Livestock.Domain/AnimalGroup.cs`), así que un movimiento de 12 cabezas exige nombrar
  las 12.

Forzar el modelo actual sobre esta realidad tiene una consecuencia que **no se nota el día
que ocurre**: cuando el operario diga "12 chanchos van al corral de pequeños", el sistema
tendría que elegir 12 filas al azar. La fila resultante afirmaría "hijo de la cerda 2,
ahora en pequeños, pesó 31 kg el 12 de septiembre" sobre un animal que nadie verificó. La
genealogía y la serie de pesos quedarían contaminadas con datos sintéticos
indistinguibles de los reales. En Fase 6 alguien correría "crecimiento por madre" y
obtendría basura sin ninguna señal de que lo es.

El dato que el cliente **sí** necesita atribuir a un individuo (peso al nacer, muerte de
un lechón imputable a su madre) ocurre durante los 24 días en que los lechones **están
físicamente separados por camada**. Ahí la individualidad es real y verificable.

El cliente además declaró su intención de implementar el aretado *si* el sistema demuestra
resultados. El modelo debe permitir esa transición sin migración de datos.

## Decisión

**1. `AnimalGroup` gana un modo de seguimiento.**

```
AnimalGroup.TrackingMode : Individual | Headcount
```

`Individual` es el comportamiento actual y el default: cada miembro es un animal
identificable. `Headcount` declara que el lote sabe **cuántos** hay, no **cuáles**.

**2. Los eventos admiten un grupo como sujeto, en XOR con el animal.**

`AnimalEvent` pasa de `AnimalId` obligatorio a **exactamente uno** de `AnimalId` /
`GroupId`, con CHECK en base de datos. Esto **no es un diseño nuevo**: `DATA-MODEL.md`
§Núcleo 2 ya lo dibuja (`ANIMAL_GROUPS |o--o{ ANIMAL_EVENTS : "grupal"`) y ya dejó tomada
la decisión de implementación — *"empezar simple (evento grupal + expansión en consulta)"*.
Este ADR ejecuta esa decisión sin reabrirla: **el evento grupal no se materializa por
animal**.

**3. Tipos de evento nuevos, todos de sujeto grupal.**

| Evento | Payload | Por qué |
|---|---|---|
| `GroupWeighing` | `{sample_count, avg_kg, min_kg, max_kg}` | Se pesa una muestra, no las 42 cabezas. Un promedio de 10 declarado como promedio de 10 es honesto; 42 pesos inventados no. |
| `GroupMortality` | `{count, cause_id}` | Murieron N. **No se elige un animal.** |
| `GroupTreatment` / `GroupVaccination` | `{head_count, …}` | Se vacuna el lote entero. |
| `GroupDiagnosis` | `{affected_count, condition, notes}` | *"En este lote hay uno enfermo."* |

`EventType.Vaccination` ya existe en `EventEnums.cs` y la app nunca lo emitió — todo entra
hoy como `Treatment` genérico. Pasa a usarse de verdad.

`GroupDiagnosis` merece una nota, porque es el caso que parecía más difícil y resultó el más
fácil. La preocupación inicial era: si hay un cerdo enfermo en un lote anónimo y hay que
sacarlo para tratarlo, ¿cuál de las 42 filas es? Cualquier elección es inventada.

Pero el cliente nunca pidió identificarlo. Su frase textual fue *"se pondría en este lote hay
uno enfermo"*. No quiere saber cuál: quiere marcar que el lote tiene una cabeza con
síntomas. Eso es un diagnóstico de sujeto grupal con `affected_count`, y si se trata, es un
tratamiento de lote sobre 1 cabeza. **Cero conceptos nuevos y cero elección arbitraria.**
Cuando el manejo se describe con las palabras de quien lo hace, el modelo sale más simple
que el que uno imagina defendiéndose de casos hipotéticos.

**4. Las cabezas vivas se derivan, no se editan.**

`LiveHeadCount` = membresías activas − bajas registradas al lote. No hay un contador
mutable que alguien pueda desincronizar de los hechos.

**5. Los lechones siguen naciendo como `Animal` individuales.**

Art. 3 se respeta sin excepción. Cada lechón nace con su UUID, su madre, su sexo y su peso
al nacer. Durante la cohorte de lactancia los eventos son individuales y verdaderos —
incluida la muerte con causa, que es lo que hace calculable la mortalidad predestete por
madre. Al clasificarse por peso, esos mismos animales pasan a un lote `Headcount`: **las
membresías siguen existiendo**, así que el linaje no se pierde; lo que se detiene es el
registro de hechos individuales.

**6. Un animal en lote `Headcount` tiene estado individual indeterminado, y el sistema lo
dice.**

Toda consulta sobre el estado de un animal debe ser consciente del modo de su lote actual.
La respuesta correcta a "¿sigue vivo el animal X?" cuando X está en un lote por conteo es
*"entró al lote; el lote registró 3 bajas sin identificar"*, no un `true` inventado ni un
`false` prudente. Esta es la regla que impide que el compromiso se olvide.

**7. El lote se cierra en bloque, y hasta entonces nadie se cierra individualmente.**

Un lote de engorde se va a faena, y normalmente **por partes**: se venden 20 de 42 y el
resto sale semanas después. Las bajas parciales **no cierran a ningún animal**: bajan el
`LiveHeadCount` del lote, exactamente igual que una mortalidad. Preguntar cuáles 20 se
fueron es la misma pregunta sin respuesta de siempre.

Cuando el lote llega a cero, **la disposición final cierra todas las membresías restantes en
bloque** y marca esos animales como dados de baja con alcance de lote — *"salió como parte
de este lote; su destino individual no se conoce más allá de eso"*.

Sin esta regla el modelo tiene una fuga: las filas de `Animal` nunca se cerrarían, y una
consulta de "cuántos animales vivos tiene la finca" que cuente animales devolvería 42
fantasmas por cada lote que ya se faenó. La consulta consciente del modo (punto 6) resuelve
el estado *de un animal*, pero no los *agregados*; el cierre en cascada sí.

## Alternativas consideradas

- **Mantener todo individual y dejar que el sistema asigne al azar.** Descartada: produce
  datos sintéticos indistinguibles de los reales. Es la única alternativa que **destruye
  información sin avisar**, y el Art. 1 protege el historial precisamente de esto. El costo
  no se paga hoy sino en Fase 6, cuando ya no hay forma de saber qué dato era real.

- **No crear animales individuales nunca; que el parto registre sólo `{nacidos: 11,
  hembras: 6, pesos: [...]}` como datos de la camada.** Descartada, aunque es la opción más
  simple y también honesta. Dos costos concretos: (i) se pierde la muerte de un lechón como
  *hecho atribuible a una madre*, que es justamente el análisis que el cliente pidió;
  (ii) el día del aretado no hay entidad a la cual pegarle el arete — habría que crear los
  individuos desde cero, perdiendo el linaje de los animales ya nacidos.

- **Un `Lot` como agregado nuevo, separado de `AnimalGroup`.** Descartada por duplicación:
  `AnimalGroup` ya tiene membresías temporales con historial (`GroupMembership` con
  `JoinedAt`/`LeftAt`), que es exactamente lo que se necesita para el prorrateo por
  animal-día. Un agregado paralelo obligaría a mantener dos modelos de pertenencia y a
  decidir en cada consulta cuál mirar.

- **Una bandera por especie (`Species.IsIndividuallyTracked`) en vez de por grupo.**
  Descartada: la misma finca tiene las 3 cerdas madres identificadas y los lechones de
  engorde anónimos, **en la misma especie**. La capacidad es del lote, no de la especie.

## Qué pasa el día del aretado

El cliente declaró su intención de comprar una máquina de aretes y etiquetas si el sistema
demuestra resultados, y de volver ahí al plan original: historial por cerdo, no por lote.
Conviene dejar escrito qué ocurre con lo construido, porque la respuesta no es obvia y
porque **la transición tiene una trampa que es exactamente el error que este ADR existe para
evitar**.

### La trampa: no se aretan filas viejas ya mezcladas

Si un lote lleva 60 días en modo `Headcount` y aparece la máquina de aretes, la tentación es
recorrer las 42 filas anónimas y pegarle un arete a cada una. **Eso está prohibido.** Sería
elegir arbitrariamente qué fila corresponde a qué cerdo físico — el mismo dato sintético que
todo el diseño evita, resucitado en el momento de la transición y con aspecto de progreso.

Las tres salidas, en orden de preferencia:

- **Aretar al nacer, de las camadas nuevas en adelante.** Es la buena y no requiere nada
  especial: el lechón recibe su `AnimalIdentifier` sobre la **misma fila** que ya se le crea
  hoy al nacer, y esa fila lo acompaña toda la vida. El período `Headcount` sencillamente no
  ocurre para esa camada. **Cero migración, cero pérdida, cero mentira.**
- **Dejar que los lotes viejos terminen sin aretar.** Se van a faena como están. Su historial
  queda tal como se registró: honesto y de granularidad de lote.
- **Si de todos modos hay que aretar un lote ya mezclado**, no se reutilizan las filas: se
  cierra el lote (disolución, punto 7) y se crean individuos nuevos cuyo linaje es *"de la
  cohorte X, madre no determinable"*. Se pierde el vínculo al peso al nacer — que para ese
  cerdo concreto **nunca fue recuperable**, sólo parecía estarlo.

La conclusión práctica: **el aretado no es un cambio de código, es un cambio de práctica en
el corral.** Aretar más temprano mueve la frontera de identidad hacia el nacimiento hasta
hacerla desaparecer. El modelo ya lo soporta hoy.

### Qué pasa con el código escrito ahora

**Se conserva íntegro.** No es andamiaje: es capacidad. Cuatro razones concretas:

1. **Los datos históricos existen para siempre** (Art. 1). Los eventos de sujeto grupal de
   los primeros lotes no se pueden borrar, así que el código que sabe **leerlos** no puede
   eliminarse nunca. Borrarlo dejaría ilegible el historial de la etapa que financió el
   aretado.
2. **La transición nunca es total ni instantánea.** Va a haber meses de convivencia entre
   lotes viejos anónimos y camadas nuevas aretadas. El sistema tiene que manejar ambos a la
   vez, que es justamente lo que `tracking_mode` por lote —y no por especie ni global—
   permite sin ningún caso especial.
3. **`Headcount` no es una capacidad porcina.** Sirve para pollos (que no se aretan nunca),
   para lotes comprados a terceros que llegan sin identificación, para animales cuyo arete se
   cayó y esperan reposición, y para cualquier especie que la finca agregue después. Es
   configuración por lote, que es lo que el Art. 8 pide.
4. **El aretado masivo se apoya en este modelo**, no lo reemplaza: el flujo "tomar un lote,
   imprimir N etiquetas, asignar identificadores" parte de un lote por conteo y usa sus
   membresías.

Lo que sí cambia con el tiempo es el **uso**, no el código: las pantallas de registro por
lote (pesaje muestral, mortalidad de lote) dejan de ser el camino principal y pasan a ser el
camino de los casos sin identificar. Si con los años ningún lote nuevo naciera en modo
`Headcount`, lo único razonable sería esconder el modo detrás de una opción avanzada al
crear un lote — **sin tocar el modelo, las consultas ni los datos**.

## Consecuencias

- **Positivas**:
  + El sistema nunca afirma saber algo que no sabe. "Entraron 90 y murieron 3, no sé cuáles
    3" es un dato correcto y completo para las decisiones que el cliente toma hoy.
  + El dato que sí importa individualmente (peso al nacer, mortalidad predestete por madre)
    queda capturado con precisión real, porque se captura cuando la separación física
    existe.
  + **La transición al aretado no requiere migración de datos ni reescritura**: aretando al
    nacer, el identificador se adosa a la **misma fila** que ya se crea hoy, y el período
    `Headcount` deja de existir para esa camada. `AnimalIdentifier` con vigencia temporal
    (ADR-0006) ya está diseñado para el arete que llega tarde. Ver "Qué pasa el día del
    aretado" arriba, incluida la trampa de aretar filas viejas ya mezcladas.
  + Ejecuta un diseño que ya estaba escrito en `DATA-MODEL.md` desde la Fase 1 y llevaba
    dos fases sin implementarse. El evento grupal también resuelve "vacunar todo el lote",
    que era una carencia independiente del pivote porcino.
  + Nada de esto es específico de cerdos. Un lote de pollos, o de terneros sin arete,
    usa el mismo mecanismo (Art. 8).

- **Negativas / costos**:
  − `AnimalEvent` deja de tener un sujeto único, y todo consumidor del historial debe
    manejar los dos casos. **Mitigación**: el XOR se impone con CHECK en BD además del
    dominio, así que un consumidor que asuma `AnimalId` no nulo falla ruidosamente al
    escribir, no en silencio al leer.
  − Las consultas de estado individual se vuelven condicionales al modo del lote. Es
    complejidad genuina, y es el precio de no mentir. **Mitigación**: se concentra en una
    sola función de lectura consciente del modo, no repartida por los llamadores.
  − Durante la ventana `Headcount` no hay serie de peso por animal, así que la analítica
    individual de engorde no existe hasta el aretado. Esto **no es una pérdida** causada
    por la decisión: es la realidad de la finca, que el modelo ahora refleja en vez de
    disimular.

  − La disolución en cascada del punto 7 marca de baja animales cuyo destino individual
    nadie verificó. Es información degradada a propósito, y hay que decirlo en la ficha del
    animal ("baja con alcance de lote") en vez de mostrarlo como una venta común.

- **Condición de reversa**: este ADR **no se revierte con el aretado**. El modo `Headcount`
  queda como capacidad permanente por las cuatro razones de "Qué pasa el día del aretado", y
  la primera es suficiente por sí sola: los eventos grupales ya registrados son historia
  inmutable (Art. 1) y el código que los lee no puede eliminarse mientras existan — es decir,
  nunca. Lo único que puede cambiar con los años es la **visibilidad** del modo al crear un
  lote, si dejara de usarse para lotes nuevos. Sin tocar el modelo, las consultas ni los
  datos.
