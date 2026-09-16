# 00 — Qué es una tesis, de qué partes consta y qué te van a pedir

> Este documento se **lee**, no se llena. Es el mapa para alguien que nunca ha escrito una
> tesis. Si algo aquí contradice el reglamento de tu instituto, **gana el reglamento**.

---

## 0. Lo primero que tienes que hacer, antes que cualquier otra cosa

**Consigue el reglamento de titulación de tu instituto.** En papel o PDF, el documento
oficial vigente. Todo lo demás de esta carpeta es orientación general; ese reglamento es la
ley. Necesitas saber de él, como mínimo:

- Qué **modalidades de titulación** admite (proyecto de investigación, proyecto técnico,
  examen complexivo, sistematización de experiencias…). No todas exigen lo mismo, y para una
  tecnología superior **muchas veces la modalidad "proyecto técnico" es más adecuada y más
  corta que una tesis de investigación**. Elegir bien la modalidad puede ahorrarte meses.
- La **estructura de capítulos obligatoria** y el formato (márgenes, tipografía, norma de
  citación: APA 7 en la mayoría de institutos ecuatorianos).
- La **extensión** esperada.
- Los **plazos** y el procedimiento: aprobación del tema, designación de tutor, avances,
  informe final, defensa.
- Si exige o no **certificado antiplagio** (Turnitin/Compilatio) y con qué porcentaje máximo.
- Si exige **carta de autorización de la empresa** donde se desarrolla el proyecto.

Sin ese documento estás adivinando. Con él, el 70% de las decisiones de esta carpeta se
resuelven solas.

> Anota en `BITACORA.md` la fecha en que lo conseguiste y qué modalidad elegiste.

---

## 1. Qué es una tesis, sin adornos

Una tesis es **un argumento sostenido con evidencia, escrito de forma que otro pueda
verificarlo o repetirlo**. No es un manual del sistema. No es un informe de lo que hiciste.
No es documentación técnica con carátula.

La diferencia práctica:

| Esto NO es tesis | Esto SÍ es tesis |
|---|---|
| "Desarrollé un sistema con .NET y Angular" | "Un sistema de registro offline-first reduce X en una finca sin conectividad, y aquí está la medición" |
| "El sistema tiene 9 módulos" | "Estos 9 módulos responden a estos requisitos levantados así, y aquí está la trazabilidad" |
| "Funciona bien" | "Evaluado con ISO/IEC 25010 sobre estas características, el resultado fue este" |

**El verbo clave es *demostrar*, no *describir*.** Si tu documento solo describe, no importa
cuán bueno sea el sistema: le falta la tesis.

**La buena noticia para ti:** la parte difícil y cara de una tesis de software es conseguir
un sistema real, desplegado, con usuarios reales. Casi todas las tesis de este nivel
presentan un prototipo que nunca salió del laptop. Tú tienes un sistema que estuvo en
producción en una finca con empleados registrando datos reales. Eso no lo compra nadie con
esfuerzo de última hora. Lo que te falta no es sistema: **te falta medición y argumento**.

---

## 2. Anatomía: los capítulos y qué va en cada uno

Esta es la estructura más común. Tu reglamento puede nombrarlos distinto o fusionarlos.

### Preliminares (no se numeran)
Carátula, declaración de autoría, certificado del tutor, dedicatoria y agradecimientos,
índices (contenido, tablas, figuras), **resumen** y **abstract** (el mismo resumen en
inglés), palabras clave.

> El **resumen** se escribe al final aunque vaya al principio. Son ~250 palabras que
> contienen: problema, objetivo, método, resultado principal y conclusión. Es lo único que
> mucha gente va a leer.

### Capítulo I — El problema
Qué está mal en el mundo real y por qué vale la pena arreglarlo.

- **Planteamiento del problema:** la situación concreta. No "las empresas necesitan
  digitalizarse" (vacío), sino "en la finca X la información de los animales vive en
  cuadernos y en la memoria del mayordomo; cuando un animal enferma nadie puede saber qué
  recibió hace dos años".
- **Formulación del problema:** una **pregunta**. Ejemplo: *¿En qué medida un sistema de
  registro offline-first mejora la completitud y la oportunidad del registro de eventos
  productivos frente al registro manual en papel, en una finca sin conectividad?*
- **Objetivos:**
  - **General:** uno solo, alineado con la pregunta. Empieza con un verbo en infinitivo.
  - **Específicos:** 3 a 5, cada uno un paso verificable hacia el general. Regla práctica:
    **cada objetivo específico debe producir algo que se pueda mostrar** (un documento de
    requisitos, un módulo, una medición, un análisis). Si un objetivo no produce nada
    mostrable, no es un objetivo, es un deseo.
  - Verbos que sirven: *identificar, diseñar, desarrollar, implementar, evaluar, comparar,
    determinar*. Verbos que no sirven porque no son verificables: *conocer, entender,
    profundizar, concientizar*.
- **Justificación:** por qué importa, a quién beneficia, y por qué *ahora*.
- **Alcance y limitaciones:** **este apartado te salva la defensa.** Aquí escribes
  explícitamente qué NO hace tu trabajo. "No se implementa facturación electrónica";
  "el estudio se limita a una finca, por lo que los resultados no son generalizables".
  Una limitación declarada es honestidad. La misma limitación descubierta por el tribunal
  es un hallazgo en tu contra.

### Capítulo II — Marco teórico
Sobre qué conocimiento previo te apoyas. Tiene tres capas:

- **Antecedentes:** trabajos parecidos al tuyo (tesis, papers, sistemas comerciales). Qué
  hicieron, qué lograron, **y qué hueco dejaron que tú llenas**. Esta última parte es la que
  casi nadie escribe y la que justifica que tu trabajo exista.
- **Bases teóricas:** los conceptos que necesitas para que te entiendan — arquitectura de
  software, offline-first, sincronización y consistencia eventual, ingeniería de requisitos,
  gestión de hatos. Solo lo que vas a usar después. Un marco teórico que explica cosas que
  el resto de la tesis nunca menciona es relleno, y se nota.
- **Marco normativo/legal:** las normas ISO que usas y la legislación aplicable. Aquí tienes
  ventaja: `docs/LEGAL-ECUADOR.md` ya recoge Agrocalidad/SIFAE, ARCSA, SRI, IESS y LOPDP.

> **Regla dura:** todo lo que no sea tuyo va citado. Todo. Un párrafo sin cita que suena a
> experto es, para el tribunal y para el antiplagio, un problema.

### Capítulo III — Marco metodológico
Cómo lo investigaste. Es el capítulo que más gente hace mal y el que decide si tu resultado
vale algo.

- **Enfoque:** cuantitativo (mides números), cualitativo (interpretas observaciones y
  entrevistas) o **mixto** (ambos — lo más probable en tu caso).
- **Tipo/alcance:** exploratorio, descriptivo, correlacional o explicativo. Un
  antes/después en una sola finca suele ser **descriptivo con componente cuasi-experimental**.
- **Población y muestra:** a quién/qué observas. En tu caso probablemente no es "personas"
  sino **registros**, y los usuarios son pocos: dilo tal cual, no infles.
- **Técnicas e instrumentos:** entrevista, observación, encuesta, análisis documental,
  extracción de datos del sistema. Cada instrumento debe existir como **anexo**: el
  cuestionario real, la guía de entrevista real.
- **Variables e indicadores:** qué mides exactamente y con qué unidad. "Usabilidad" no es un
  indicador; "tiempo medio para registrar un evento, en segundos" sí lo es.
- **Metodología de desarrollo:** qué proceso de software seguiste (aquí describes tu
  realidad: fases con criterio de salida por uso real, ADRs, TDD, PR+CI).

### Capítulo IV — Desarrollo / propuesta
Lo que construiste. Requisitos (funcionales y no funcionales), diseño (arquitectura,
modelo de datos, diagramas), implementación, pruebas.

> Tu repositorio ya tiene casi todo esto escrito: `ARCHITECTURE.md`, `DATA-MODEL.md`, los
> diagramas Mermaid, los ADR. **Este capítulo es cosecha, no producción.** Pero se reescribe
> en prosa académica y con las referencias puestas: pegar documentación técnica tal cual es
> el error más común y el más visible.

### Capítulo V — Resultados y discusión
Qué salió. Tablas, gráficos, comparación antes/después, resultados de la evaluación.

- **Resultados** = los números, sin opinión.
- **Discusión** = qué significan, cómo se comparan con los antecedentes del Capítulo II, y
  qué los amenaza.
- Un resultado negativo o parcial **es un resultado**. Escribirlo con honestidad te fortalece
  en la defensa; esconderlo y que el tribunal lo note, te hunde.

### Capítulo VI — Conclusiones y recomendaciones
- **Conclusiones:** una por cada objetivo específico, respondiendo si se cumplió y con qué
  evidencia. Ni una conclusión sobre algo que no mediste.
- **Recomendaciones:** trabajo futuro, tanto para la finca como para quien continúe el
  sistema.

### Cierre
Bibliografía (APA 7, todo lo citado y nada más) y anexos (instrumentos, cartas, manuales,
código relevante, evidencias).

---

## 3. Vocabulario académico, traducido

| Te dicen | Significa |
|---|---|
| **Objeto de estudio** | Sobre qué recae tu investigación (el proceso de registro de la finca), no el sistema |
| **Variable independiente** | Lo que tú introduces o cambias (el sistema) |
| **Variable dependiente** | Lo que esperas que cambie por eso (tiempo de registro, completitud, errores) |
| **Operacionalizar una variable** | Convertir un concepto vago en algo medible con unidad y fórmula |
| **Instrumento** | El cuestionario/guía/ficha concreta con la que recoges el dato |
| **Validación del instrumento** | Que alguien con criterio (tu tutor, un experto) revise y firme que el cuestionario mide lo que dice medir. **Suele pedirse; pregunta temprano** |
| **Triangulación** | Confirmar un hallazgo con dos o más fuentes distintas |
| **Línea base** | La medición de cómo estaban las cosas **antes** de tu intervención. Si no la tomas antes, no existe después |
| **Estado del arte** | Qué se ha hecho ya sobre tu tema |
| **Marco muestral** | De dónde sale tu muestra |
| **Consentimiento informado** | Documento firmado donde la persona acepta participar sabiendo para qué se usan sus datos |
| **Anexo** | Material de respaldo al final, referenciado desde el texto |

---

## 4. Los seis errores que hunden tesis de software

1. **No tomar la línea base.** Se instala el sistema y después se quiere comparar contra el
   "antes" que ya nadie puede medir. **Es irreversible.** Ver `04-PLAN-DE-DATOS`.
2. **Objetivos que no se pueden verificar.** "Mejorar la gestión" no se demuestra. "Reducir
   el tiempo medio de registro de un evento" sí.
3. **Marco teórico de relleno.** Diez páginas sobre la historia de las bases de datos que no
   se usan en ningún otro capítulo.
4. **Capítulo de resultados sin resultados.** Capturas de pantalla del sistema en lugar de
   mediciones. Una captura no es un resultado.
5. **Pegar documentación técnica en prosa académica.** Se nota a un párrafo de distancia.
6. **Dejar los trámites para el final.** La carta de autorización, la validación del
   instrumento y el antiplagio tienen tiempos que no dependen de ti. Ver `05-TRAMITES`.

---

## 5. Lo que en tu caso hay que cuidar especialmente

- **El cliente 1 se desvinculó y se llevó la continuidad.** Cualquier tema que dependa de
  datos longitudinales de esa finca está muerto. Lo que sobrevive es lo que vive en este
  repositorio: historial de git, 27 ADR, retrospectivas de fase, el reporte de fallos de
  sincronización de producción.
- **El cliente 2 todavía no empieza.** Eso es una ventaja rarísima: puedes tomar la línea
  base *antes* de instalar nada. Se hace una sola vez y no hay segunda oportunidad.
- **Datos de terceros.** La finca y sus empleados son personas reales bajo la LOPDP.
  Consentimiento informado, anonimización, y nada identificable en este repositorio público.

---

## 6. Qué hacer esta semana

1. Conseguir el reglamento de titulación y ver qué modalidades admite.
2. Averiguar cómo se asigna tutor y cuándo hay que presentar el tema.
3. Correr `./scripts/tesis-init.sh` y empezar a llenar `01-TEMA.md` con los candidatos.
4. Abrir `BITACORA.md` y anotar hoy.

Lo demás espera. Estas cuatro cosas desbloquean todo el resto.
