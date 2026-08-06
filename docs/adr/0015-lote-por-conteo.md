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

`EventType.Vaccination` ya existe en `EventEnums.cs` y la app nunca lo emitió — todo entra
hoy como `Treatment` genérico. Pasa a usarse de verdad.

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

## Consecuencias

- **Positivas**:
  + El sistema nunca afirma saber algo que no sabe. "Entraron 90 y murieron 3, no sé cuáles
    3" es un dato correcto y completo para las decisiones que el cliente toma hoy.
  + El dato que sí importa individualmente (peso al nacer, mortalidad predestete por madre)
    queda capturado con precisión real, porque se captura cuando la separación física
    existe.
  + **La transición al aretado es un cambio de bandera**, sin migración de datos: el lote
    pasa a `Individual`, los eventos vuelven a tener sujeto animal, y los animales que ya
    existen reciben su `AnimalIdentifier` — que ADR-0006 ya modela con vigencia temporal,
    justo para el caso de "arete que llega tarde".
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

- **Condición de reversa**: cuando el aretado esté implementado y todos los lotes operen en
  modo `Individual`, este ADR no se revierte — el modo `Headcount` queda como capacidad
  disponible para lotes futuros que nazcan sin identificar. Se reabriría sólo si se
  descubriera que la finca **nunca** vuelve a tener animales anónimos, en cuyo caso el modo
  se marcaría obsoleto sin borrar los datos históricos que lo usaron (Art. 1).
