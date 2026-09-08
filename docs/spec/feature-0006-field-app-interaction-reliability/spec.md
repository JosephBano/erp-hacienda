# spec.md — Fiabilidad de desplazamiento, teclado y gestos en la app de campo

> **Estado:** propuesta investigada, **no implementada**. La carpeta tiene los cuatro
> documentos de la convención: `spec.md` (qué se decidió), [`plan.md`](./plan.md) (en qué
> orden y en qué commits), [`tasks.md`](./tasks.md) (el desglose con casillas) y
> [`test-e2e.md`](./test-e2e.md) (la verificación manual). Ningún criterio de producción
> queda cerrado por existir estos documentos.
>
> **Alcance recortado el 2026-09-07.** Este documento contenía además un rediseño integral
> de la aplicación. Eran dos entregables de tamaño incompatible: la queja concreta de los
> empleados —contenido inalcanzable al desplazar— es angosta, está localizada en líneas
> conocidas y puede corregirse sin esperar a nada; el rediseño depende de que aterricen
> 0004, 0005, 0007, 0008 y 0009. Juntos, la queja real quedaba detrás de cinco features.
> El rediseño se mudó a [feature-0010](../feature-0010-field-app-redesign/spec.md). Esta
> carpeta conserva su nombre, `interaction-reliability`, que describe con exactitud lo
> que queda.
>
> **Propósito único:** que todo control, campo y mensaje de una pantalla sea alcanzable,
> y que un gesto no altere el hecho registrado.

- **Rama documental:** `feature/field-app-production-specs`, base `0faf483`.
- **Fecha:** 2026-09-07. **Fase:** 3 / 3.5, estabilización en producción.
- **Reglas:** AGENTS.md 1, 9 y 10; Constitución arts. 3, 9 y 11.
- **Referencias:** [ADR-0005](../../adr/0005-offline-first-movil.md),
  [asistente de parto existente](../feature-0002-field-app-parto-redesign/spec.md).

## 1. Problema y límites de lo conocido

Los empleados reportan problemas con vistas, animaciones e interacción al desplazar la
app. No se conocen todavía teléfono, versión, pantalla ni gesto exacto. La investigación
encuentra estructuras que explican contenido inaccesible, pero **no mide cuadros por
segundo ni demuestra una causa de animaciones entrecortadas en hardware real**.

Esa distinción se conserva a lo largo del documento: hay un defecto de estructura
confirmado por lectura, y hay una queja de fluidez todavía sin reproducir. El primero
se puede corregir ya; el segundo necesita evidencia antes de proponerle una solución.

## 2. Hallazgos por lectura del código

| Evidencia en el repositorio (`0faf483`) | Hallazgo |
|---|---|
| `clients/field-app/src/ui/components.tsx:66` usa `Screen` no desplazable por defecto; `:257` fija `flex: 1`; `clients/field-app/src/ui/theme.ts:44` establece objetivos táctiles de 64. | El contenido largo requiere una decisión explícita de desplazamiento. |
| `clients/field-app/src/screens/LotSubjectScreen.tsx:82` muestra título, resumen y siete botones en `Screen` sin scroll. | Solo los mínimos de los botones ocupan 448 unidades lógicas, antes del resto del contenido. Existe riesgo concreto de desbordamiento. |
| `clients/field-app/src/screens/AnimalSubjectScreen.tsx:77` muestra acciones del animal en otro `Screen` fijo. | Mismo riesgo con títulos largos o texto ampliado. |
| `clients/field-app/src/screens/TreatScreen.tsx:185` y `:242` usan `Screen` fijo y formulario `Card`; todas las vías y motivos se renderizan como botones. | Los selectores tienen scroll, pero el formulario variable no lo tiene. Más catálogo aumenta su altura. |
| `clients/field-app/src/screens/MilkingScreen.tsx:160` y `:225` reservan scroll para la lista; el formulario seleccionado es fijo. | Abrir teclado o mensajes de plausibilidad añade contenido al área no desplazable. |
| Búsqueda `rg -n 'KeyboardAvoidingView\|BackHandler\|Animated\|LayoutAnimation\|AccessibilityInfo' clients/field-app/src` sin coincidencias. | No se encontró manejo explícito de esos mecanismos en `src`; no prueba que el sistema operativo no adapte el teclado ni que falten animaciones internas nativas. |
| `clients/field-app/src/screens/navigation.ts` y `src/App.tsx` dentro de `clients/field-app/`. | La navegación se organiza mediante claves de pestaña y renderizado condicional. Cualquier corrección de regreso y estado del formulario ocurre ahí. |

Las pruebas de pantallas usan mocks nativos y renderizado de componentes, no gestos
reales ni medición del layout. La ejecución y las seis omisiones existentes se registran
en [0004](../feature-0004-field-app-sync-reliability/spec.md#21-verificación-ejecutada-y-sus-límites).

## 3. Decisiones fijadas para la propuesta

- **D1 — Cada pantalla tiene un recorrido de desplazamiento comprensible.** Formulario,
  avisos y acción final son alcanzables con teclado abierto o cerrado; evitar scrolls
  verticales anidados que compitan por el mismo gesto.
- **D2 — Los gestos no alteran el hecho registrado.** Arrastrar no confirma, un doble
  toque no encola dos operaciones y una animación no controla el commit de datos.
- **D3 — Respetar captura offline y lo ya escrito.** Retroceder dentro de un flujo
  conserva sus valores. Abandonarlo con cambios advierte antes de descartarlos.
- **D4 — No añadir una librería de animaciones por anticipación.** Se ajusta con
  componentes disponibles; una dependencia nueva requiere necesidad medida y ADR.
- **D5 — Corregir la estructura, no rediseñar.** Esta entrega no cambia arquitectura de
  información, paleta ni tipografía. Los cambios visuales que no hagan falta para
  alcanzar un control pertenecen a [0010](../feature-0010-field-app-redesign/spec.md).
- **D6 — La queja de fluidez no se cierra sin reproducirla.** Mientras no haya teléfono,
  build y gesto identificados, no se declara resuelta por haber arreglado el scroll.

## 4. Alcance y comportamiento esperado

Incluye las pantallas con riesgo confirmado —`LotSubjectScreen`, `AnimalSubjectScreen`,
`TreatScreen`, `MilkingScreen`— y cualquier otra que use `Screen` fijo con contenido de
altura variable. Se revisan desplazamiento, teclado, foco, regreso y pulsaciones. No se
revisan aquí jerarquía visual, colores ni vocabulario.

La acción principal debe ser visible o alcanzable por desplazamiento normal. No puede
quedar debajo del teclado sin forma de llegar a ella. Los avisos de validación llevan
al campo correspondiente y no borran entradas. El primer toque intencional en una
acción con teclado abierto debe tener un comportamiento consistente y verificable.

Una lista larga conserva selección y posición razonable al volver del detalle. Un
refresco de datos no salta al inicio ni cierra el formulario. Si invalida el sujeto,
aplica [0005](../feature-0005-field-app-activity-validation/spec.md). El botón atrás de
Android y el de pantalla tienen resultados coherentes y no dejan rutas inaccesibles.

El teléfono responde visualmente al toque y señala guardado local en curso, éxito o
error. Una transición se puede interrumpir con navegación sin bloquear ni duplicar.
Si existen animaciones, respetan la preferencia de reducir movimiento; ninguna es
necesaria para comprender el estado o completar una operación.

Antes de proponer una corrección de fluidez hay que identificar teléfono, build y gesto,
y reproducir el problema en compilación equivalente a producción. No se atribuye al
producto la sobrecarga de herramientas de desarrollo.

## 5. Matriz de aceptación propuesta

Condiciones de prueba propuestas, no mediciones de los teléfonos actuales: Android con
área útil aproximada de 360 × 640 y 412 × 915 unidades lógicas, texto al 100 % y 150 %,
teclado abierto/cerrado. Incluir al menos un teléfono real usado por los empleados y
anotar modelo, SO y build.

Usar 200 animales, 20 grupos, nombres de 100 caracteres, un parto de 20 crías y un
catálogo de tratamiento con 8 vías y 6 motivos. Son fixtures de estrés de UI, no cifras
afirmadas sobre la finca.

## 6. Criterios de aceptación

1. En la matriz anterior todos los campos, mensajes y botones se alcanzan, sin recorte
   irreversible ni controles superpuestos al teclado o a la barra del sistema.
2. No hay scroll vertical atrapado. La selección y confirmación funcionan con texto
   ampliado, lector de pantalla y reducción de movimiento cuando hay animaciones.
3. Deslizar sobre una lista o botón no registra nada; confirmar repetidamente mientras
   se guarda produce una sola operación local y un único resultado visible.
4. Volver de un paso conserva sexo, arete, peso, dosis y demás valores ya introducidos.
   Abandonar un formulario modificado tiene salida explícita y comprensible.
5. Refresco automático no cambia arbitrariamente la selección, posición de lectura ni
   texto escrito. Un sujeto invalidado se trata según 0005.
6. Se verifica en teléfono real la secuencia toque, arrastre, teclado, atrás y retorno
   para cada pantalla afectada. Se conserva evidencia de pantalla/build del fallo y su
   reproducción corregida; aprobar tests de componentes por sí solo no cierra este spec.
7. La queja de fluidez se cierra solo con teléfono, build y gesto identificados y el
   problema reproducido y corregido, o se documenta explícitamente como no reproducible.

## 7. Dependencias y límites

- El rediseño integral es [0010](../feature-0010-field-app-redesign/spec.md), que hereda
  estos criterios: rediseñar sobre pantallas que atrapan el scroll reproduciría el defecto
  con otra apariencia. Este spec **no** depende de 0010 y se entrega antes.
- Falta identificar los teléfonos, builds y gestos problemáticos. Se conserva la
  distinción entre hallazgo de código y fallo reproducido en hardware.
- Este spec no autoriza instalar librerías, sustituir React Native ni cambiar la
  infraestructura de navegación. Un cambio de esa clase requeriría ADR y pertenece a 0010.
- Los contratos de historial inmutable, identidad interna, captura offline y visibilidad
  de rechazos deben conservarse.
