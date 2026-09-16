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

### Recomendación

**D como núcleo.** Es el único candidato que ningún tercero te puede quitar y el único cuyo
avance de tesis y avance de proyecto son el mismo commit.

B (el levantamiento con el cliente 2) deja de ser tesis y vuelve a ser lo que es: trabajo de
producto que harás igual. A queda como ampliación si el cliente permanece. C y E se
solapan con D y pueden ser capítulos suyos.

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
