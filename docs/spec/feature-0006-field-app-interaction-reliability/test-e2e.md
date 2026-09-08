# test-e2e.md — Verificación manual extremo a extremo

> Escenarios que se ejecutan **sobre un teléfono real**, en compilación equivalente a
> producción, después de que la suite automatizada esté en verde. Las pruebas de pantallas
> usan mocks nativos y renderizado de componentes, no gestos reales ni medición de layout
> (`spec.md` sec. 2): **aprobarlas no cierra esta rama**.
>
> Cada escenario dice cómo prepararlo, qué hacer y qué debe pasar. Un escenario que falla se
> reporta con el paso exacto donde falló, no como "no anda".

**Antes de empezar:**

- Al menos un teléfono usado por los empleados. Anotar modelo, SO y build.
- La matriz de `spec.md` sec. 5: áreas útiles de ~360 × 640 y ~412 × 915 unidades lógicas,
  texto al 100 % y al 150 %, teclado abierto y cerrado.
- Datos de estrés cargados: 200 animales, 20 grupos, nombres de 100 caracteres, un parto de
  20 crías, catálogo de tratamiento con 8 vías y 6 motivos. **Son fixtures de UI, no cifras
  de la finca.**
- **No usar compilación de desarrollo** para medir fluidez.

---

## E2E-1 — Las cuatro pantallas se recorren enteras

**Pasos:** para cada una de `LotSubjectScreen`, `AnimalSubjectScreen`, `TreatScreen` y
`MilkingScreen`, y en cada combinación de la matriz:
1. Abrir la pantalla con los datos de estrés.
2. Desplazarse hasta el final.
3. Pulsar el último control.

**Debe pasar:**
- Se llega al último control en las cuatro pantallas y en las cuatro combinaciones.
- Ningún texto queda recortado de forma irreversible.
- Ningún control queda superpuesto a la barra del sistema.
- Con texto al 150 % el resultado sigue siendo el mismo.

## E2E-2 — No hay scroll atrapado

**Pasos:**
1. En `TreatScreen`, arrastrar verticalmente **sobre el selector de vías**.
2. Arrastrar verticalmente **sobre el formulario**, fuera del selector.
3. Repetir en `MilkingScreen` sobre la lista y sobre el formulario.

**Debe pasar:**
- Cada arrastre mueve lo que el empleado espera mover.
- No hay una zona donde el arrastre «no haga nada» porque dos scrolls compiten.
- Se puede llegar al final de la pantalla desde cualquier punto de partida.

## E2E-3 — La acción final con el teclado abierto

**Pasos:**
1. Abrir un formulario con entrada numérica y tocar el campo para levantar el teclado.
2. Escribir un valor.
3. Sin cerrar el teclado, llegar a la acción principal y pulsarla.
4. Repetir con texto al 150 % y en el área útil más pequeña.

**Debe pasar:**
- La acción principal es visible o alcanzable por desplazamiento normal.
- **Nunca** queda debajo del teclado sin forma de llegar a ella.
- El primer toque intencional sobre la acción hace lo esperado; no se pierde cerrando el teclado.

## E2E-4 — Un aviso de validación no borra nada

**Pasos:**
1. Llenar un formulario con un valor inválido en un campo y valores correctos en el resto.
2. Confirmar.

**Debe pasar:**
- El aviso lleva al campo que falla.
- **Todos los demás valores siguen escritos.**
- Corregir el campo y confirmar registra sin volver a pedir lo ya escrito.

## E2E-5 — Deslizar no registra, doble toque no duplica

> El escenario que sostiene D2. Es el de mayor consecuencia: un duplicado es un dato falso
> en el historial de la finca.

**Pasos:**
1. En una lista de animales, deslizar el dedo sobre las filas y sobre los botones.
2. Abrir un formulario, llenarlo y **pulsar confirmar dos veces seguidas, rápido**.
3. Repetir pulsando tres veces mientras se ve el indicador de guardado.
4. Revisar la pantalla de sincronización y el registro del animal.

**Debe pasar:**
- El paso 1 no registró absolutamente nada.
- Los pasos 2 y 3 produjeron **una sola** operación en el outbox y **un solo** resultado visible.
- El animal tiene un registro, no dos.

## E2E-6 — Retroceder conserva lo escrito

**Pasos:**
1. Iniciar un flujo de varios pasos, por ejemplo el asistente de parto.
2. Llenar el paso 1 y avanzar; llenar el paso 2.
3. Retroceder al paso 1.
4. Avanzar de nuevo al paso 2.
5. Salir del flujo sin confirmar.

**Debe pasar:**
- Los pasos 3 y 4 conservan todo lo escrito: sexo, arete, peso, dosis.
- El paso 5 **advierte** antes de descartar.
- El asistente de parto conserva su secuencia de cuatro pasos: esta rama no lo rediseña.

## E2E-7 — Atrás de Android y regreso de pantalla

**Pasos:**
1. Navegar dos o tres niveles adentro desde una lista larga, tras desplazarse bastante.
2. Volver con el botón de pantalla.
3. Repetir volviendo con el botón atrás de Android.

**Debe pasar:**
- Ambos botones dan el mismo resultado.
- La lista conserva selección y una posición de lectura razonable.
- Ninguna ruta queda inaccesible ni la app se cierra sin querer.

## E2E-8 — Refresco durante la lectura

**Pasos:**
1. Abrir una lista larga y desplazarse hasta la mitad.
2. Provocar una sincronización que traiga cambios.
3. Repetir con un formulario abierto y a medio llenar.

**Debe pasar:**
- La lista no salta al inicio.
- El formulario no se cierra ni pierde lo escrito.
- Si el refresco invalida el sujeto, se trata según
  [0005](../feature-0005-field-app-activity-validation/spec.md).

## E2E-9 — Accesibilidad y reducción de movimiento

**Pasos:**
1. Activar el lector de pantalla y recorrer un formulario completo.
2. Activar reducción de movimiento y repetir un flujo de registro.
3. Poner el texto al 150 % y repetir E2E-1.

**Debe pasar:**
- Selección y confirmación funcionan con lector de pantalla.
- Con reducción de movimiento, el estado se sigue comunicando; ninguna animación es
  necesaria para entender qué pasó.
- El orden de foco es sensato y ningún control queda inalcanzable.

## E2E-10 — La queja original, reproducida

> Solo ejecutable si la Compuerta 0 identificó el fallo.

**Preparación:** el teléfono, la build y el gesto que reportaron los empleados.

**Pasos:**
1. Reproducir el gesto exacto en la build **anterior**. Conservar evidencia.
2. Reproducirlo en la build de esta rama.

**Debe pasar:**
- El fallo se observa en la build anterior y **no** en la nueva.
- Si el fallo era de fluidez y no se reproduce en compilación equivalente a producción, se
  **documenta como no reproducible** y no se declara resuelto (D6).

---

## Cierre de la verificación

Estos escenarios en verde **no cierran la rama por sí solos**. Faltan:

- Los criterios numerados de [`spec.md` sec. 6](./spec.md#6-criterios-de-aceptación), 1 a 7.
- `npm test` y `dotnet test` completos ([`tasks.md`](./tasks.md) TC.1 y TC.2).
- El resultado de la Compuerta 0 y el estado de la queja de fluidez, registrados en el PR.

**Se anota siempre:** modelo, SO y build. Un escenario aprobado sin esa anotación no es
evidencia de campo. **Se conserva evidencia de pantalla del fallo original y de su
reproducción corregida** (criterio 6).
