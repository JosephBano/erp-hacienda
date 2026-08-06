# ADR-0018 — Características observables del animal: un solo mecanismo para los juicios

- **Estado:** Propuesto
- **Fecha:** 2026-08-06
- **Fase del roadmap:** Fase 3.5 (Adaptación porcina) — bloque 3.5b, **pero es transversal a todas las especies**

## Contexto

El cliente pidió un sistema de calificación de madres: qué tan buena madre es una cerda,
según comportamientos que la gente del campo observa —si cuida a las crías, si las muerde,
si no las pisa—. Al diseñarlo se modeló como `MaternalBehaviorAssessment`, una entidad con
columnas específicas de conducta materna porcina.

**Eso es un `if (especie == 'cerdo')` disfrazado de tabla.** El Art. 8 lo prohíbe
explícitamente: *"si un requerimiento nuevo pide un `if (especie == 'cerdo')` en el core, la
respuesta es rediseñar el dato, no escribir el `if`"*. El diseño lo cumplió en los catálogos
duros —vías de administración, causas de muerte— y lo violó justo donde el dato es más
blando.

La observación que corrige el rumbo vino del dueño del producto: esto no es "calificación de
madres", es **características observables del animal**, y la calificación de madres es un
caso de uso. Los ejemplos que dio son de otras especies y otros propósitos:

- Equinos: *"este es muy manso"*, *"este patea"*.
- Bovinos: *"esta tiende a escaparse del corral, es muy traviesa"*.
- Porcinos: los comportamientos maternos originales.

Hay además un hecho del dominio que conviene aceptar en vez de combatir: **estos conceptos
son ambiguos y subjetivos por naturaleza**. No son mediciones mal hechas que se puedan
afinar hasta volverlas precisas. Son juicios de personas con experiencia, y su valor está
justamente ahí. El modelo tiene que tratarlos como lo que son.

El riesgo de generalizar es conocido: un catálogo de atributos libres es a un paso de un EAV
genérico, el patrón donde toda la información termina escondida en pares clave-valor sin
tipo ni invariantes. Un EAV está justificado cuando el conjunto de atributos es realmente
abierto, configurable por el usuario y disperso —que es este caso, y el Art. 8 lo exige—
pero **necesita un límite duro**, o en dos años alguien guarda "peso" como característica y
la conversión alimenticia se rompe en silencio.

## Decisión

### 1. Dos entidades: la definición y la observación

```
AnimalTrait       { nombre, kind, tipo_de_valor, configuración_del_tipo,
                    especie?, visible_como_advertencia, activa, versión }
TraitObservation  { animal, trait (versión), valor, observado_en,
                    observado_por, contexto?, notas? }
```

`kind` ∈ { `Conductual`, `Morfológica`, `Manejo` }. La evaluación de futura madre es
"capturar el conjunto de morfológicas en una sesión"; el índice materno es "leer un conjunto
de conductuales con pesos". Un solo mecanismo, dos usos.

`especie` nula significa que aplica a todas.

### 2. Las características se observan, no se asignan

**Una característica nunca es una columna editable sobre `Animal`.** Es una serie de
observaciones fechadas y firmadas, y "esta yegua es mansa" es un **resumen derivado** de esa
serie, nunca un valor almacenado.

Tres razones, y la primera es la que decide:

1. **Conserva la tendencia.** La conducta materna se evalúa *por parto*, no globalmente:
   una etiqueta fija nadie la revisa, una serie muestra si la cerda mejoró o empeoró. Al
   generalizar, esa propiedad se gana para todas las especies en vez de perderse.
2. **Un cambio es visible.** Una yegua que se vuelve nerviosa después de una lastimadura es
   un hecho interesante. Con una columna editable alguien la sobrescribe y el cambio
   desaparece; con observaciones, el cambio *es* el dato.
3. Es coherente con el Art. 4, que ya rige todo lo demás: si algo cambió, quedó como hecho
   fechado.

`contexto` es una referencia opcional al hecho durante el cual se observó —un parto, una
jornada de manejo—, y es lo que permite decir "en este parto aplastó" sin perder que fue
*en ese* parto.

### 3. El criterio que decide dónde va cada dato

Una sola pregunta, aplicable por cualquiera sin consultar a nadie:

> **¿Dos personas competentes, con el animal delante, obtendrían el mismo número?**
>
> - **Sí** → es una medición → **esquema y eventos**
> - **No** → es un juicio → **característica**

Peso: dos personas con la misma balanza sacan 35.4 kg. Medición.
"Es mansa": dos personas discrepan honestamente. Juicio.

El criterio resuelve además el caso límite que quedaba turbio, el **número de tetas**: dos
personas cuentan los mismos pezones pero **discrepan sobre cuáles son funcionales** —un
pezón invertido cuenta para uno y no para el otro—. Es un juicio. Va como característica
morfológica.

### 4. El guardarraíl es estructural, no reglamentario

Una regla escrita en un documento es una sugerencia. El límite se impone **quitándole al
sistema la capacidad de expresar lo prohibido**.

Una característica sólo puede tomar cuatro formas de valor:

| Tipo | Configuración | Ejemplo |
|---|---|---|
| `Booleano` | — | ¿patea? sí/no |
| `EscalaOrdinal` | conjunto **cerrado** de niveles etiquetados | manso / normal / nervioso · 1–5 con etiqueta por nivel |
| `ConteoAcotado` | mínimo y máximo declarados | tetas funcionales, 0–20 |
| `TextoLibre` | — | "abre el pestillo con el hocico" |

Y de ahí sale el límite duro: **no existe el decimal libre y no existe el campo de unidad.**

Con eso, `peso = 35.4 kg` es **inexpresable**. No está prohibido: no hay dónde ponerlo. El
que lo intente descubre que no puede declarar la unidad ni guardar el decimal, y esa
fricción **es informativa** — le está diciendo que ese dato va al esquema. La regla deja de
depender de que alguien la lea.

El corsé se verificó contra los casos reales y no aprieta: condición corporal es 1–5 con
medios puntos (nueve niveles de un conjunto cerrado), temperamento es ordinal, tetas es
conteo acotado. **Si aparece un juicio que necesita un decimal sin unidad, es señal de que
no era un juicio.**

Refuerzos secundarios, en la misma dirección:

- **El camino correcto tiene que ser más barato.** Registrar un pesaje son dos toques;
  crear una característica exige definirla antes en el panel. Nadie mete el peso como
  característica ni queriendo. La gente toma el camino barato: hay que hacer que el barato
  sea el correcto.
- **Un test fija la forma del catálogo**, no sus nombres —el cliente agrega los que quiera,
  eso lo exige el Art. 8— sino la invariante: ninguna característica tiene unidad, ninguna
  acepta decimal libre. Si alguien agrega esa capacidad "porque hacía falta", la suite se
  pone roja y hay una conversación en el PR en vez de un descubrimiento en Fase 6.

### 5. Absorbe `SelectionCriterion` y elimina `MaternalBehaviorAssessment`

`SelectionCriterion` —diseñado para evaluar futuras madres, con tipos conteo / escala 1–5 /
booleano— **es este mismo mecanismo**: definición configurable, valor tipado, registrado
contra un animal con fecha y observador. La única diferencia era cuándo se captura y qué
decisión alimenta, y eso es el `kind`.

**Dos subsistemas planificados se vuelven uno solo y más chico.**

### 6. El índice materno se apoya en eventos, no en opiniones, siempre que pueda

Aplicando el criterio del punto 3 al aplastamiento de crías, que se había planificado como
ítem de calificación conductual: dos personas **sí** coinciden en cuántos lechones
aparecieron muertos aplastados. Es una **medición** → evento de mortalidad con causa, que ya
existe en el plan.

Y "esta cerda es torpe con las crías" ni siquiera hace falta como característica: **se
deriva** de contar los eventos de mortalidad por aplastamiento de esa madre. Objetivo, sin
sesgo de observador.

Resultado: la parte dura del índice sale de hechos contables (mortalidad por causa,
destetados, peso de camada, intervalo destete–celo) y **sólo lo genuinamente subjetivo**
—¿deja mamar?, ¿es agresiva al manejo?— pasa por características. **Menos subjetividad, no
más.**

### 7. Las características visibles son advertencias de campo

`visible_como_advertencia` hace que la característica aparezca en la ficha del animal en el
móvil, antes de que alguien lo toque. *"PATEA"* al empleado nuevo, *"se escapa del corral"*
al que arma el grupo, *"no deja mamar"* al que revisa la camada.

Esto convierte al sistema en algo que ya no sólo registra: **transfiere el conocimiento del
empleado con veinte años al que llegó el lunes.** Para una finca donde ese conocimiento vive
hoy en la cabeza de una persona, probablemente valga más que el índice de madres.

### 8. La observación lleva quién la hizo

Son juicios subjetivos: dos empleados califican distinto a la misma cerda, y si uno es
sistemáticamente más duro el ranking queda torcido sin que nadie lo note. **No se resuelve
ahora**, pero si el observador no queda registrado el sesgo es indetectable para siempre.
Con el dato guardado, el día que el ranking dé raro se puede mirar.

### 9. Una definición usada no se edita: se versiona

Si una escala de 1–5 pasa a 1–10 después de haber registrado observaciones, todo lo anterior
se vuelve ininterpretable en silencio: un 3 viejo y un 3 nuevo no significan lo mismo.

Una `AnimalTrait` con observaciones **se versiona o se reemplaza, nunca se edita en sitio**,
y cada observación apunta a la versión con la que se capturó. Es el Art. 1 aplicado al
catálogo.

## Alternativas consideradas

- **Dejar `MaternalBehaviorAssessment` como estaba.** Descartada por Art. 8: es una tabla
  que sólo significa algo para cerdas, y la finca ya tiene bovinos y podría tener equinos.
  Además obligaría a construir una tabla nueva por cada especie que quiera registrar
  conducta.

- **Columnas de características directamente sobre `Animal`.** Descartada: pierde la
  tendencia (punto 2), obliga a una migración por cada característica nueva —lo contrario
  del Art. 8— y hace invisible el momento en que un animal cambió.

- **Un EAV sin restricción de tipos.** Descartada: es la versión de esta decisión que
  fracasa. Sin el punto 4 el mecanismo se convierte en el vertedero donde termina la
  información que debía estar tipada, y el fracaso es silencioso y tardío.

- **Mantener `SelectionCriterion` separado de las características conductuales.**
  Descartada: son el mismo mecanismo con distinto momento de captura. Sostener dos catálogos,
  dos CRUD y dos pantallas para la misma forma de dato es duplicación pura.

- **Escala numérica libre en vez de conjunto cerrado etiquetado.** Descartada por dos
  motivos: rompe el guardarraíl (un decimal libre admite cualquier medición), y un "4" sin
  etiqueta no significa lo mismo para dos observadores, que es justamente el problema que
  este ADR intenta acotar.

## Consecuencias

- **Positivas**:
  + El Art. 8 se cumple donde más costaba: la parte blanda del dominio pasa a ser
    configuración. Agregar "se escapa del corral" para bovinos o "patea" para equinos es un
    INSERT, sin deploy y sin tocar el core.
  + **Dos subsistemas planificados se vuelven uno**, así que el alcance de 3.5b baja en vez
    de subir.
  + La ambigüedad de los conceptos deja de ser un problema a resolver de antemano: el
    cliente calibra qué características importan y cuánto pesan **usándolo**, que es la
    única forma en que esto se puede calibrar bien.
  + El índice materno queda **más objetivo** que en el diseño anterior, porque el criterio
    del punto 3 empuja hacia eventos contables todo lo que se puede contar.
  + Las advertencias de campo (punto 7) son valor real fuera de la reproducción y fuera de
    los porcinos, por muy poco código.

- **Negativas / costos**:
  − Es un EAV, con todo lo que eso implica: consultas más incómodas que sobre columnas
    tipadas, y ningún constraint de base de datos sobre el *significado* de un valor.
    **Mitigación**: el punto 4 acota los tipos posibles a cuatro, y el punto 9 impide que
    una definición mute bajo los datos ya capturados.
  − Un catálogo configurable se llena de características que nadie usa. **Mitigación**: la
    bandera `activa` y, en la revisión de cierre de fase, mirar cuántas tienen observaciones
    recientes.
  − El sesgo entre observadores existe y este ADR **no lo corrige**, sólo lo hace
    detectable. Es una limitación aceptada conscientemente: corregirlo exigiría calibración
    entre observadores, que no tiene sentido en una finca con dos o tres personas.
  − El versionado de definiciones (punto 9) es complejidad real en el CRUD del panel, y es
    la parte donde es más fácil equivocarse. **Mitigación**: prueba obligatoria de que una
    observación histórica conserva su interpretación tras cambiar la definición.

- **Condición de reversa**: si al cerrar la Fase 3.5 el catálogo tiene características
  definidas pero **casi ninguna observación registrada**, significa que el mecanismo es
  correcto en el papel y molesto en el corral. En ese caso se reduce a lo único que demostró
  valor —probablemente las advertencias visibles del punto 7— y el índice materno pasa a
  calcularse sólo con KPIs derivados de eventos. La señal a medir es concreta:
  **observaciones registradas por animal y por mes durante el piloto**.
