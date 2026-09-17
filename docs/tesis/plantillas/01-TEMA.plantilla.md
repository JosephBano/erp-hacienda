# 01 — Tema, problema y objetivos

> Se llena en `docs/tesis/privado/01-TEMA.md`. Elegir el tema es tuyo; aquí están los
> candidatos derivados del estado real del proyecto y lo que cada uno exige a cambio.
> **Estado:** ☐ candidatos en evaluación ☐ tema elegido ☐ tema aprobado por el instituto

---

## Parte A — Elegir el tema

Regla para decidir: **no elijas por cuál suena mejor, elige por cuál puedes terminar.** Lo
que mata una tesis es depender de datos que quizá nunca llegues a tener. La columna
"Riesgo" es la que importa.

### Candidato A — Sistema offline-first con evaluación de línea base

> *Implementación y evaluación de un sistema de gestión ganadera offline-first en una
> finca sin conectividad: comparación del registro manual frente al registro digital.*

- **Pregunta:** ¿en qué medida cambia la completitud y la oportunidad del registro de eventos
  productivos al pasar del cuaderno al sistema, en una finca sin conectividad?
- **Qué exige:** tomar la **línea base en la finca del cliente 2 antes de instalar nada**, y
  luego un período de uso real medido. Consentimiento firmado.
- **A favor:** es el tema más natural, más vistoso y el que mejor aprovecha lo que ya
  construiste. El antes/después da tablas y gráficos claros.
- **Riesgo: ALTO.** Depende por completo de que el cliente 2 se concrete, deje instalar y
  siga usando el sistema meses. El cliente 1 se desvinculó sin dar motivo; esa dependencia ya
  te falló una vez.

### Candidato B — Ingeniería de requisitos para inventario y pagos

> *Levantamiento y especificación de requisitos bajo ISO/IEC/IEEE 29148 para un módulo de
> inventario y gestión de pagos en una explotación pecuaria, y definición de su alcance
> mínimo viable.*

- **Pregunta:** ¿qué requisitos de inventario, activos y pagos tiene realmente una
  explotación pecuaria pequeña, y cuál es el subconjunto mínimo que aporta valor de uso?
- **Qué exige:** entrevistas y observación con el cliente 2 **en las primeras semanas**, más
  la especificación formal y la trazabilidad requisito → diseño → implementación parcial.
- **A favor:** es trabajo que **tienes que hacer de todos modos** antes de escribir una línea
  de ese módulo. Norma reconocida, entregable claro, y no exige meses de uso.
- **Riesgo: BAJO-MEDIO.** Solo necesita el contacto inicial con el cliente, no su permanencia.

### Candidato C — Evaluación de calidad y proceso, sin depender de cliente

> *Evaluación de la calidad de un sistema de información agropecuario bajo ISO/IEC 25010 y
> análisis del proceso de desarrollo asistido por agentes de inteligencia artificial.*

- **Pregunta:** ¿qué nivel de calidad alcanza el sistema bajo ISO/IEC 25010 y qué clases de
  defecto produjo el desarrollo asistido por agentes, según la evidencia del repositorio?
- **Qué exige:** solo lo que ya existe — historial de git, 27 ADR, retrospectivas, el reporte
  de fallos de sincronización tras tres semanas de producción, la fase cerrada en falso.
- **A favor:** **cero dependencia de terceros.** Se puede terminar aunque no aparezca ningún
  cliente. Tema muy vigente.
- **Riesgo: BAJO.** El riesgo aquí es otro: sin usuarios, la evaluación de usabilidad de la
  25010 queda coja y hay que declararlo como limitación.

### Candidato D — Guardarraíles de entrega para desarrollo asistido por agentes ✅

> *Guardarraíles de integración y despliegue continuo para el desarrollo asistido por agentes
> de inteligencia artificial: diseño, implementación y evaluación sobre un sistema de
> información agropecuario en desarrollo.*

- **Pregunta:** ¿qué guardarraíles de integración y despliegue continuo evitan que la
  aceleración del desarrollo asistido por agentes de IA se pague en defectos que llegan a
  producción?
- **Qué exige:** solo el repositorio. El trabajo de ingeniería está especificado en
  `docs/spec/feature-0011-devops-delivery-pipeline/spec.md`.
- **A favor — y es el argumento decisivo:** es **trabajo que se va a hacer de todos modos**,
  y la línea base ya existe y está fechada. Una fase cerrada en falso, tres defectos de
  pérdida silenciosa de datos que una suite verde no atrapó, siete defectos más hallados tras
  tres semanas de uso real, 97 PRs y 325 commits con fecha. Ninguna tesis consigue un "antes"
  así; este se pagó por el camino doloroso.
- **Instrumento de medición:** métricas **DORA** (frecuencia de integración, lead time de
  cambio, tasa de fallo del cambio, tiempo de restauración), complementadas con ISO/IEC 25010
  en fiabilidad y mantenibilidad.
- **Riesgo: MUY BAJO.** Cero dependencia de terceros.
- **Límite honesto:** dos de las cuatro métricas DORA solo son reconstruibles clasificando el
  historial a mano, y eso se declara como limitación (`spec.md` sec. 9).

### Candidato E — Formalización del control de inventario y finanzas ✅

> *Implementación de un sistema de información para el control de inventario y finanzas en
> una explotación pecuaria: evaluación del cambio en la calidad de la información de gestión
> frente al registro informal.*

- **Pregunta:** ¿en qué medida cambia la calidad de la información de gestión —completitud,
  exactitud, consistencia y actualidad— al sustituir el registro informal (papel, memoria o
  nada) por un sistema de información, en una explotación pecuaria pequeña?
- **Qué exige:** el levantamiento y la línea base con el cliente 2 **antes de instalar nada**,
  y un período de uso real medido.
- **A favor:** es un tema genérico, replicable y que un tribunal reconoce de inmediato. Y
  coincide con lo que el cliente 2 pidió, así que el avance de producto y el de tesis son el
  mismo trabajo.
- **Riesgo: MEDIO.** Depende de que el cliente permanezca. Ver "El período" abajo.

#### Lo que este tema NO puede demostrar, y hay que decirlo desde el título

Con **una sola finca, sin grupo de control**, no se puede atribuir al sistema una mejora de
rentabilidad: el margen depende del precio del cerdo, del clima y de la sanidad mucho más que
del software. Prometerlo en el título es la forma más rápida de perder la defensa.

Por eso el objeto de medición es la **calidad de la información**, no el rendimiento
económico:

| Indicador | ¿Atribuible al sistema? | Papel en la tesis |
|---|---|---|
| Completitud del registro (hechos anotados ÷ ocurridos) | **Sí** | Principal |
| Discrepancia entre conteo físico y existencias registradas | **Sí** | Principal |
| Tiempo en producir una cifra de gestión | **Sí** | Principal |
| % de costos con respaldo documental | **Sí** | Principal |
| Trazabilidad de una salida de inventario hasta su compra | **Sí** | Principal |
| Margen, utilidad, rentabilidad | **No** | Secundario, **sin afirmar causalidad** |

Los económicos se reportan porque interesan al dueño y porque el sistema los hace visibles
por primera vez, pero se presentan como *lo que ahora se puede saber*, no como *lo que el
sistema mejoró*.

#### La norma: aquí no es la 25010

El objeto de medición es el dato, no el producto:

- **ISO/IEC 25012** — modelo de **calidad de datos**: exactitud, completitud, consistencia,
  credibilidad, actualidad. Es el instrumento central: da las dimensiones ya nombradas sobre
  las que se construyen los indicadores de la tabla anterior.
- **ISO/IEC 25040** — **proceso de evaluación**, por etapas. Da estructura metodológica al
  antes/después y evita que la evaluación sea "miré y me pareció mejor".
- **ISO/IEC 25010** queda como secundaria, solo para la parte de producto.

> ⚠️ Verifica la revisión vigente de cada una antes de citarla, y anótala en `02-MARCO-TEORICO`.

#### La línea base puede no existir, y eso es el resultado del Capítulo I

Si la información vive en la cabeza del dueño, no hay registros que medir. Entonces la línea
base **se construye con instrumento**: entrevista estructurada, conteo físico de existencias,
y reconstrucción desde facturas y comprobantes.

**El grado de informalidad se vuelve una variable medible**, no un obstáculo. Que salga
catastrófica —existencias sin cuadrar, costos sin respaldo, cifras imposibles de producir— no
es un fracaso del trabajo: es su planteamiento del problema, con números en vez de
adjetivos.

#### "A medida" como hallazgo, no como limitación

Que el sistema sea específico para esta finca es una debilidad académica **salvo que se
convierta en pregunta**. El Art. 8 de la Constitución del proyecto dice *«lo específico es
dato, no código»*, así que la pregunta es:

> ¿Qué proporción de los requisitos del cliente se resolvió **configurando** el sistema
> existente, y cuánta exigió **código nuevo**?

Es medible con el historial de git, es original, y convierte lo hecho a medida en un
resultado sobre extensibilidad en vez de en una excusa.

#### El período

**El diseño cierra a los 3–4 meses**, con el año como ampliación opcional si el cliente
permanece. Un año de observación es una dependencia que el cliente 1 ya rompió sin avisar; el
trabajo tiene que poder terminarse antes de que eso pueda volver a pasar. Ver
`04-PLAN-DE-DATOS` sec. 5, "Plan B".

### Recomendación

**Dos trabajos, y se eligieron a propósito por su riesgo, no por su atractivo:**

- **D — Guardarraíles de entrega.** Riesgo muy bajo: no depende de nadie. Es el que garantiza
  que hay título.
- **E — Formalización del control de inventario y finanzas.** Riesgo medio: depende del
  cliente 2. Es el que tiene alcance y utilidad para terceros.

No compiten: D se apoya en el repositorio y E en la finca, y sus instrumentos son distintos
(DORA e ISO/IEC 25010 contra ISO/IEC 25012 y 25040). Si el cliente 2 se desvincula como el 1,
**D sigue en pie y hay tesis igual** — que es exactamente la razón de tener dos.

**Pero se escriben uno a la vez.** Dos tesis en paralelo, para una persona, es la forma más
fiable de no terminar ninguna (`docs/tesis/README.md` sec. 2).

B (el levantamiento con el cliente 2) deja de ser tesis y pasa a ser el Capítulo IV de E. A y
C se solapan con D y E y pueden ser capítulos suyos.

**Decisión tomada:** _(escribe aquí el tema elegido y la fecha)_

---

## Parte B — Llenar una vez elegido el tema

### B.1 Título
_(Preciso, sin adjetivos de marketing. Debe contener: qué se hace, sobre qué y dónde.)_

### B.2 Planteamiento del problema
_(La situación concreta y real. Hechos, no generalidades. Puedes apoyarte en `docs/SOUL.md`,
que ya describe el problema mejor que la mayoría de planteamientos: la historia de la finca
vive en cuadernos y en la memoria del mayordomo.)_

### B.3 Formulación del problema
_(Una pregunta, con signos de interrogación.)_

### B.4 Objetivo general
_(Uno. Verbo en infinitivo. Alineado con la pregunta de B.3.)_

### B.5 Objetivos específicos
_(3 a 5. Cada uno debe producir algo mostrable. Marca junto a cada uno cuál es ese
entregable — si no puedes nombrarlo, el objetivo está mal escrito.)_

1. … → entregable:
2. … → entregable:
3. … → entregable:

### B.6 Justificación
_(Por qué importa, a quién beneficia, por qué ahora.)_

### B.7 Alcance
_(Qué SÍ cubre: módulos, período, finca, usuarios.)_

### B.8 Limitaciones — el apartado que te salva la defensa
_(Qué NO cubre y qué amenaza la validez. Candidatos que ya conoces:)_

- Estudio en una sola finca: los resultados no son generalizables.
- No se implementa emisión de comprobantes electrónicos (SRI).
- El investigador es a la vez el desarrollador del sistema: hay sesgo, y se declara.
- Número muy pequeño de usuarios: no admite tratamiento estadístico inferencial.
- _(añade las que aparezcan)_

### B.9 Hipótesis _(solo si tu modalidad la exige)_
_(Muchas modalidades de proyecto técnico no piden hipótesis. Confirma en el reglamento antes
de inventar una.)_
