# 02 — Marco teórico

> Se llena en `docs/tesis/privado/02-MARCO-TEORICO.md`.
> Regla que gobierna este capítulo: **nada entra aquí si no se usa después.** Un concepto
> explicado en el Capítulo II y nunca mencionado en el IV o el V es relleno, y se nota.

---

## 1. Antecedentes (estado del arte)

Trabajos previos parecidos al tuyo. Para cada uno: qué hizo, qué logró y **qué hueco dejó**.
Ese hueco es lo que justifica que tu trabajo exista, y es lo que casi nadie escribe.

| Fuente | Año | Qué hizo | Qué midió | Hueco que deja |
|---|---|---|---|---|
| | | | | |

**Dónde buscar** (ver `06-FUENTES` para el método):
- Tesis de institutos y universidades ecuatorianas sobre sistemas agropecuarios — te dan el
  formato esperado además del contenido.
- Literatura de *precision livestock farming* y su crítica: casi toda asume sensores, IoT y
  conectividad. Tu contexto (un teléfono, sin señal, un empleado con guantes) es justamente
  donde esa literatura no llega. **Ese es tu hueco, y es real.**
- Sistemas de información en contextos rurales / ICT4D: sostenibilidad y abandono.
- Trabajos sobre sincronización offline-first y consistencia eventual.

## 2. Bases teóricas

Solo los conceptos que vas a usar. Guía de contenido según el tema elegido en `01-TEMA`:

- **Sistemas de información y ERP**: qué es un ERP, por qué modular.
- **Arquitectura de software**: monolito modular, Clean Architecture, CQRS.
  → Fuente interna: `docs/ARCHITECTURE.md`, ADR-0001, ADR-0002.
- **Offline-first y sincronización**: consistencia eventual, idempotencia, resolución de
  conflictos (last-write-wins), colas de salida (outbox).
  → Fuente interna: ADR-0005, ADR-0008, `docs/spec/feature-0004-*`.
- **Modelado de eventos inmutables**: event sourcing ligero, por qué el dato no se edita.
  → Fuente interna: ADR-0004, ADR-0017.
- **Ingeniería de requisitos**: elicitación, especificación, trazabilidad.
- **Calidad de producto software**: el modelo de ISO/IEC 25010.
- **Dominio pecuario**: hato, lote, categoría, evento sanitario, período de retiro,
  conversión alimenticia. → Fuente interna: `docs/GLOSSARY.md`.
- **Solo para el tema E** — control de inventario (existencias, lotes, kardex, discrepancia
  de conteo), costos por centro, y **formalización de la información en la pequeña
  explotación agropecuaria**: por qué el registro informal persiste y qué se pierde con él.
  Aquí es donde vive el hueco del tema: la literatura de sistemas de gestión agropecuaria
  suele partir de una explotación que **ya lleva registros**, no de una que arranca de cero.

> **Cómo usar el repositorio como fuente sin hacer trampa.** Los ADR y los documentos de
> `docs/` son *fuentes primarias de tu propio proyecto*: sirven para el Capítulo IV
> (desarrollo) y como evidencia en el V (resultados), **no** como respaldo teórico. El
> respaldo teórico tiene que venir de literatura externa citable. Si en el marco teórico la
> única referencia es tu propio repositorio, no hay marco teórico.

## 3. Marco normativo

### 3.1 Normas técnicas

**La norma central depende del tema elegido en `01-TEMA`**, porque el objeto de medición
cambia. No arrastres la tabla entera: quédate con las filas de tu tema.

| Norma | Para qué la usas | Tema | Dónde aparece después |
|---|---|---|---|
| **ISO/IEC 25010** — calidad de **producto** | Evaluar el sistema como artefacto | D (central), E (secundaria) | Cap. III (instrumento), Cap. V |
| **ISO/IEC 25012** — calidad de **datos** | Evaluar exactitud, completitud, consistencia, credibilidad y actualidad de la información | **E (central)** | Cap. III (instrumento), Cap. V |
| **ISO/IEC 25040** — **proceso** de evaluación | Dar estructura por etapas al antes/después | E | Cap. III (método) |
| **ISO/IEC/IEEE 29148** — ingeniería de requisitos | Estructura del levantamiento y de la especificación | D, E | Cap. III (método), Cap. IV |
| _(opcional)_ ISO/IEC/IEEE 42010 — descripción de arquitectura | Justifica el uso de ADRs | D | Cap. IV |
| _(no es ISO)_ **DORA** — métricas de entrega | Frecuencia, lead time, tasa de fallo, restauración | **D (central)** | Cap. III, Cap. V |

> **25010 y 25012 no son intercambiables y confundirlas se nota.** La primera mide si el
> *software* es bueno; la segunda, si el *dato* que contiene lo es. Un sistema impecable
> lleno de existencias que no cuadran puntúa alto en 25010 y bajo en 25012 — y en una finca
> que venía de anotar en un cuaderno, lo que importa es la segunda.

> ⚠️ **Verifica la revisión vigente de cada norma antes de citarla** (las ISO se revisan y el
> año forma parte de la cita). Anota aquí la revisión que usaste y la fecha en que lo
> comprobaste. Citar un año equivocado en la carátula es un error caro y evitable.
>
> - ISO/IEC 25010, revisión usada: ______ · verificado el: ______
> - ISO/IEC 25012, revisión usada: ______ · verificado el: ______
> - ISO/IEC 25040, revisión usada: ______ · verificado el: ______
> - ISO/IEC/IEEE 29148, revisión usada: ______ · verificado el: ______

**Sobre ISO/IEC 25010 en concreto** (tema D, o la parte de producto del tema E). Define
características de calidad (adecuación funcional, eficiencia de desempeño, compatibilidad,
usabilidad, fiabilidad, seguridad, mantenibilidad, portabilidad) con sus subcaracterísticas.
**No las uses todas.** Elige las
3–5 que tu sistema realmente pone en juego y justifica la selección; evaluar las ocho a
fondo es una tesis entera por sí sola. Candidatas naturales aquí: **fiabilidad** (el sync no
puede perder datos), **adecuación funcional**, **usabilidad** (el empleado a las 5 AM) y
**mantenibilidad** (un desarrollador solo, horizonte de años).

### 3.2 Marco legal ecuatoriano

→ Fuente interna: `docs/LEGAL-ECUADOR.md`, que ya recoge Agrocalidad/SIFAE, ARCSA, SRI,
IESS y **LOPDP**.

La **LOPDP** no es decorativa en tu caso: vas a tratar datos de empleados de una finca
(quién registró qué, y a qué hora). Eso es tratamiento de datos personales y sostiene el
consentimiento informado de `05-TRAMITES`. Menciónalo en el marco legal y vuélvelo a citar
en la metodología.

## 4. Definición de términos
_(Glosario de los términos de dominio que el tribunal probablemente no conoce. Puedes partir
de `docs/GLOSSARY.md`, pero reescríbelo en prosa: una tabla ES↔EN de nombres de código no es
un glosario académico.)_
