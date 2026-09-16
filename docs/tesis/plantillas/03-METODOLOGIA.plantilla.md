# 03 — Metodología

> Se llena en `docs/tesis/privado/03-METODOLOGIA.md`.
> Este es el capítulo que decide si tus resultados valen algo. Un resultado obtenido con un
> método que no se sostiene no es un resultado: es una opinión con tabla.

---

## 1. Enfoque y tipo de investigación

- **Enfoque:** ☐ cuantitativo ☐ cualitativo ☐ mixto
  _(Lo más probable aquí es **mixto**: números del sistema + entrevistas y observación.)_
- **Alcance:** ☐ exploratorio ☐ descriptivo ☐ correlacional ☐ explicativo
- **Diseño:** ☐ no experimental ☐ **cuasi-experimental antes/después** ☐ estudio de caso
- **Justificación de la elección:** _(dos párrafos. Por qué este diseño responde tu pregunta
  y no otro.)_

> **Nota honesta que conviene escribir tú mismo antes de que la escriba el tribunal:** con
> una sola finca y pocos usuarios esto es un **estudio de caso**, no un experimento
> generalizable. Declararlo no te debilita — te ahorra la pregunta incómoda y demuestra
> criterio metodológico.

## 2. Población y muestra

- **Unidad de análisis:** ☐ los registros/eventos ☐ las personas ☐ ambos
  _(Piénsalo bien: en un estudio de completitud del registro, la unidad son los **eventos
  registrados**, no las personas. Eso cambia por completo el tamaño de tu "muestra".)_
- **Población:** ______
- **Muestra y criterio de selección:** ______
- **Participantes humanos:** número, rol, y el consentimiento que firman (→ `05-TRAMITES`).

## 3. Variables e indicadores (operacionalización)

La tabla más importante del capítulo. **"Usabilidad" no es un indicador; "tiempo medio de
registro de un evento, en segundos" sí lo es.**

| Variable | Definición operacional | Indicador | Unidad | Fuente del dato | Instrumento |
|---|---|---|---|---|---|
| Completitud del registro | eventos registrados ÷ eventos ocurridos | % | Sistema + conteo de referencia | Consulta SQL / ficha |
| Oportunidad del registro | tiempo entre el hecho y su registro | horas | Sistema (`fecha_evento` vs `created_at`) | Consulta SQL |
| Esfuerzo de registro | tiempo para registrar un evento | segundos | Observación cronometrada | Ficha de observación |
| Errores de captura | correcciones ÷ registros | % | Sistema (eventos de corrección) | Consulta SQL |
| Calidad percibida | características ISO/IEC 25010 seleccionadas | escala Likert 1–5 | Usuario | Cuestionario |
| _(añade / quita)_ | | | | | |

> Toda fila cuyo dato salga del sistema debe tener su **consulta SQL escrita y guardada**
> antes de empezar a medir, no improvisada al final. Va como anexo.

## 4. Técnicas e instrumentos

| Técnica | Instrumento (anexo) | A quién / sobre qué | Cuándo |
|---|---|---|---|
| Entrevista semiestructurada | Guía de entrevista | Dueño de la finca | Levantamiento inicial |
| Observación directa | Ficha de observación | Registro de eventos en campo | Línea base y post |
| Análisis documental | Ficha de análisis | Cuadernos y registros en papel existentes | Línea base |
| Encuesta | Cuestionario ISO/IEC 25010 | Usuarios del sistema | Al cierre del período de uso |
| Extracción de datos | Consultas SQL versionadas | Base de datos del sistema | Continuo |

**Cada instrumento tiene que existir como documento real y completo** antes de usarse, y va
como anexo de la tesis. Un instrumento redactado después de recoger el dato no es un
instrumento.

**Validación de instrumentos:** pregunta a tu tutor si tu instituto exige validación por
expertos (juicio de expertos con ficha firmada). Es común, y tiene tiempos que no controlas.
→ `05-TRAMITES`.

## 5. Ingeniería de requisitos — ISO/IEC/IEEE 29148

Usa la norma como **estructura del levantamiento con el cliente 2**, y quedas cubierto en
dos frentes: la tesis obtiene su capítulo de requisitos y el proyecto obtiene el insumo para
decidir el alcance del módulo de inventario y pagos.

Pasos y salidas:

1. **Elicitación** — entrevista y observación en sitio. Salida: notas y transcripción.
2. **Análisis** — separar lo que se dijo de lo que se necesita; detectar conflictos.
3. **Especificación** — requisitos numerados, cada uno *verificable*, *sin ambigüedad* y
   *atómico*. Funcionales (RF-nn) y no funcionales (RNF-nn).
4. **Validación** — el cliente confirma que lo especificado es lo que quiso decir. Por
   escrito.
5. **Priorización y alcance** — MoSCoW u otro criterio, para llegar a la **rebanada mínima
   que se usa de verdad**. Este paso es el que protege el proyecto: "inventario y pagos
   completo" para un desarrollador solo son años.
6. **Trazabilidad** — matriz requisito → diseño → implementación → prueba.

> **Cuidado de alcance, y es serio.** Lo que el cliente describió abarca insumos
> consumibles, activos y herramientas (que no se consumen: se asignan, se deprecian, se les
> hace mantenimiento) y gestión de pagos. Son tres subsistemas distintos, no uno. El
> resultado del levantamiento debe ser un **recorte justificado**, y ese recorte es en sí
> mismo un hallazgo defendible de la tesis.

## 6. Metodología de desarrollo

Describe el proceso real del proyecto, que ya está documentado y es más riguroso que el de
la mayoría de tesis:

- Fases con **criterio de salida por uso real en la finca** (Art. 11) → `docs/ROADMAP.md`.
- Decisiones registradas en **ADR** inmutables → `docs/adr/`.
- **TDD** y suite de pruebas con PostgreSQL real → `docs/PROTOCOLO-DE-TRABAJO.md`.
- Rama → PR → CI → merge, sin excepciones (Art. 13).
- Reglas no negociables en `docs/CONSTITUTION.md`.

**Menciona también el desarrollo asistido por agentes de IA.** Es parte honesta de tu
proceso, está documentado en `AGENTS.md` y en las retrospectivas, y ocultarlo sería peor que
declararlo. Si elegiste el Candidato C de `01-TEMA`, esto deja de ser una nota y pasa a ser
objeto de estudio.

## 7. Procedimiento

Cronología de la ejecución, paso a paso y con fechas previstas. Debe cuadrar con
`04-PLAN-DE-DATOS` y con el cronograma de `05-TRAMITES`.

## 8. Consideraciones éticas

- Consentimiento informado de cada participante, firmado y archivado fuera del repositorio.
- Anonimización: la finca y las personas aparecen como "Finca A", "Usuario 1".
- Tratamiento de datos personales conforme a la **LOPDP**.
- **Declaración de conflicto de interés:** eres el desarrollador del sistema que evalúas.
  Decláralo y explica qué hiciste para mitigar el sesgo (medidas objetivas tomadas del
  sistema en vez de solo percepción, instrumentos validados por un tercero, datos crudos
  disponibles para revisión).
