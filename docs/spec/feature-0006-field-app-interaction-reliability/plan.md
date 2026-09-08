# plan.md — Ejecución de la rama `feature/field-app-interaction-reliability`

> **Qué es este documento.** Cómo se hace el trabajo que [`spec.md`](./spec.md) decidió:
> orden de los commits, qué archivos toca cada uno, qué pruebas exige y cómo se mergea.
> Desglose ejecutable en [`tasks.md`](./tasks.md), verificación en [`test-e2e.md`](./test-e2e.md).
> Las decisiones no se relitigan acá — si algo no cuadra, se corrige el spec primero.

**Objetivo:** que todo control, campo y mensaje de una pantalla sea alcanzable, y que un
gesto no altere el hecho registrado.

**Enfoque:** una rama, **cinco commits secuenciales**, más **una compuerta** previa que
separa lo que se puede corregir hoy de lo que todavía no se ha reproducido. No hay punto de
no retorno: esta rama no toca esquema, contrato de sync ni dominio.

**Spec:** [`spec.md`](./spec.md).

**Independiente.** No espera a 0004, 0005 ni a ningún otro spec de la serie. Es lo primero
que se entrega después del Paso 0 del [índice](../README.md).

---

## Restricciones globales

- **Corregir la estructura, no rediseñar** (D5). Nada de arquitectura de información, paleta
  ni tipografía. Si un cambio no hace falta para alcanzar un control, pertenece a
  [0010](../feature-0010-field-app-redesign/spec.md).
- **Ningún gesto altera el hecho registrado** (D2). Arrastrar no confirma, un doble toque no
  encola dos operaciones, una animación no controla el commit.
- **No se descarta lo escrito** (D3, regla dura 10). Retroceder conserva valores; abandonar
  con cambios advierte antes.
- **Sin dependencias nuevas** (D4, regla dura 2). Ni una librería de animaciones «chiquita».
  Si parece inevitable, se detiene y se propone ADR.
- **Un PR, un propósito** (regla 9). La deuda visual que se vea de paso va a
  `docs/BACKLOG.md` o a 0010, no a esta rama.
- **La queja de fluidez no se cierra sin reproducirla** (D6).
- **Nada se marca completo** sin `npm test` en `clients/field-app` en verde, la suite entera.

---

## Índice

1. [Compuerta 0 — Identificar el fallo reportado](#compuerta-0--identificar-el-fallo-reportado)
2. [Commit 1 — `Screen` desplazable](#commit-1--screen-desplazable)
3. [Commit 2 — Las cuatro pantallas con riesgo confirmado](#commit-2--las-cuatro-pantallas-con-riesgo-confirmado)
4. [Commit 3 — Teclado y foco](#commit-3--teclado-y-foco)
5. [Commit 4 — Gestos idempotentes](#commit-4--gestos-idempotentes)
6. [Commit 5 — Regreso y borradores](#commit-5--regreso-y-borradores)
7. [Orden, dependencias y puntos de no retorno](#orden-dependencias-y-puntos-de-no-retorno)
8. [Cómo se prueba](#cómo-se-prueba)
9. [Descripción del PR](#descripción-del-pr)

---

## Compuerta 0 — Identificar el fallo reportado

**Antes de escribir código.** El spec distingue dos cosas: un defecto de estructura
confirmado por lectura, y una queja de fluidez sin reproducir.

Recoger de los empleados y registrar en el PR:

1. Teléfono y build donde ocurre.
2. Pantalla y gesto exactos: ¿qué estaban haciendo cuando «no se movía» o «se trababa»?
3. Si el problema es **no llegar** a un control o es **fluidez** de la animación.

- **Es no llegar a un control** → los commits 1 a 5 lo cubren; procede tal cual.
- **Es fluidez** → los commits 1 a 5 se hacen igual, porque el defecto de estructura es real,
  pero **no cierran la queja**. Reproducir en compilación equivalente a producción antes de
  proponer una corrección. No se atribuye al producto la sobrecarga de herramientas de
  desarrollo.
- **No se puede identificar** → se documenta explícitamente como no reproducible (D6). No se
  declara resuelto por haber arreglado el scroll.

## Commit 1 — `Screen` desplazable

`feat(field-app): give Screen an explicit scrollable mode`

**Por qué primero:** las cuatro pantallas del commit 2 necesitan el mecanismo. Hacerlo pantalla
por pantalla produciría cuatro soluciones distintas al mismo problema.

**Archivos:**
- `clients/field-app/src/ui/components.tsx:66` — `Screen` hoy no es desplazable por defecto y
  `:257` fija `flex: 1`. Añadir un modo desplazable **explícito**, sin cambiar el
  comportamiento de quien no lo pida.
- Conservar los objetivos táctiles de 64 unidades de `clients/field-app/src/ui/theme.ts:44`.

**Verificación:** una pantalla de prueba con contenido más alto que la ventana permite
alcanzar el último elemento. Las pantallas que no adoptaron el modo nuevo no cambian de
comportamiento — se comprueba con las pruebas de componentes existentes en verde.

**Un solo gesto vertical.** El modo desplazable no debe anidarse con otro scroll vertical que
compita por el mismo arrastre (D1).

## Commit 2 — Las cuatro pantallas con riesgo confirmado

`fix(field-app): make long screens reachable by scrolling`

**Archivos:**
- `clients/field-app/src/screens/LotSubjectScreen.tsx:82` — título, resumen y siete botones en
  `Screen` sin scroll. Solo los mínimos de los botones ocupan 448 unidades lógicas.
- `clients/field-app/src/screens/AnimalSubjectScreen.tsx:77` — mismo riesgo con títulos largos
  o texto ampliado.
- `clients/field-app/src/screens/TreatScreen.tsx:185,242` — los selectores ya tienen scroll;
  el formulario variable no. Más catálogo aumenta su altura.
- `clients/field-app/src/screens/MilkingScreen.tsx:160,225` — la lista tiene scroll; el
  formulario seleccionado es fijo.
- Barrer el resto de `clients/field-app/src/screens/` en busca de `Screen` fijo con contenido
  de altura variable. El alcance del spec incluye «cualquier otra».

**Verificación:** con área útil de 360 × 640 y texto al 150 %, cada una de las cuatro permite
llegar a su último control. Sin recorte irreversible ni controles superpuestos a la barra del
sistema.

## Commit 3 — Teclado y foco

`fix(field-app): keep the primary action reachable with the keyboard open`

**Por qué acá:** depende del commit 2. Un formulario que ya no desborda puede volver a
desbordar cuando aparece el teclado.

**Archivos:**
- Las pantallas con entrada de texto. `rg -n 'KeyboardAvoidingView' clients/field-app/src` no
  encuentra hoy manejo explícito; eso **no prueba** que el sistema operativo no adapte nada
  (spec sec. 2), así que primero se observa el comportamiento real y después se corrige lo
  que falle.
- La acción principal no puede quedar debajo del teclado sin forma de llegar a ella.
- Los avisos de validación llevan al campo correspondiente y **no borran entradas**.

**Verificación:** con teclado abierto, en ambas áreas útiles de la matriz, la acción final se
alcanza. Un aviso de validación deja el valor escrito intacto.

## Commit 4 — Gestos idempotentes

`fix(field-app): make a drag or a double tap unable to record anything`

**Archivos:**
- Los componentes de acción de `clients/field-app/src/ui/components.tsx` y las pantallas que
  confirman registros.
- Deslizar sobre una lista o un botón **no registra nada**.
- Confirmar repetidamente mientras se guarda produce **una sola** operación local y un único
  resultado visible.
- El teléfono responde visualmente al toque y señala guardado en curso, éxito o error.

**Verificación:** prueba que dispara dos confirmaciones seguidas y afirma que el outbox
recibió **una** entrada. Es la garantía que sostiene D2; sin ella el commit no entra.

## Commit 5 — Regreso y borradores

`fix(field-app): preserve form values when navigating back`

**Archivos:**
- `clients/field-app/src/screens/navigation.ts` y `clients/field-app/src/App.tsx` — la
  navegación se organiza por claves de pestaña y renderizado condicional; el regreso y el
  estado del formulario se resuelven ahí.
- El botón atrás de Android y el de pantalla tienen resultados coherentes y no dejan rutas
  inaccesibles.
- Retroceder dentro de un flujo conserva sus valores; abandonarlo con cambios advierte antes
  de descartarlos.
- Una lista larga conserva selección y posición razonable al volver del detalle. Un refresco
  de datos no salta al inicio ni cierra el formulario.

**Verificación:** volver de un paso conserva sexo, arete, peso y dosis ya introducidos. Si un
refresco invalida el sujeto, se trata según [0005](../feature-0005-field-app-activity-validation/spec.md)
— esta rama no duplica esa regla, solo no la estorba.

## Orden, dependencias y puntos de no retorno

```
Compuerta 0 (teléfono, build, gesto)
  └─ Commit 1 (Screen desplazable)
       └─ Commit 2 (cuatro pantallas)
            └─ Commit 3 (teclado)
  Commit 4 (gestos)      ── independiente del 1-3
  Commit 5 (regreso)     ── independiente del 1-3
```

- **No hay punto de no retorno.** Esta rama no toca esquema, contrato de sync ni dominio;
  cualquier commit admite `git revert` limpio.
- **Commits 4 y 5 son independientes** y pueden adelantarse si la Compuerta 0 se alarga.
- **El commit 3 no puede adelantarse al 2**: corregir el teclado sobre una pantalla que ya
  desborda sin teclado mide el problema equivocado.

## Cómo se prueba

1. `npm test` completo en `clients/field-app`. Las seis omisiones existentes
   (`MilkingScreen.test.tsx`, `AnimalEditScreen.test.tsx`) siguen documentadas; esta rama no
   las cierra ni las aumenta.
2. [`test-e2e.md`](./test-e2e.md) sobre **teléfono real**, en compilación equivalente a
   producción. Las pruebas de pantallas usan mocks nativos y renderizado de componentes, no
   gestos reales ni medición de layout (spec sec. 2): **aprobarlas no cierra esta rama**.
3. `dotnet test` completo. Esta rama no toca backend: cualquier fallo es una regresión ajena
   que hay que detectar antes del merge.

## Descripción del PR

**Título:** `fix(field-app): make every control reachable and every gesture harmless`

**Cuerpo:**

- **Qué:** añade modo desplazable explícito a `Screen`, lo aplica a las pantallas con riesgo
  confirmado, mantiene la acción principal alcanzable con el teclado abierto, impide que un
  arrastre o un doble toque registren algo, y conserva los valores al retroceder.
- **Por qué:** los empleados reportaron problemas al desplazar la app. La investigación
  localizó estructuras que explican contenido inaccesible
  ([`spec.md` sec. 2](./spec.md#2-hallazgos-por-lectura-del-código)).
- **Decisiones:** [`spec.md` sec. 3](./spec.md#3-decisiones-fijadas-para-la-propuesta), D1–D6.
- **Qué NO incluye:** el rediseño integral, que vive en
  [0010](../feature-0010-field-app-redesign/spec.md); arquitectura de información, paleta,
  tipografía; librerías de animación; cambios en la infraestructura de navegación.
- **Riesgo declarado:** bajo. Sin cambios de esquema, contrato ni dominio.
- **Estado de la queja de fluidez:** `<reproducida y corregida | documentada como no
  reproducible>`. No se declara resuelta por haber arreglado el scroll (D6).
- **Cómo probarlo:** ejecutar [`test-e2e.md`](./test-e2e.md).
- **Resultado de Compuerta 0:** `<teléfono, build y gesto reportados>`.
