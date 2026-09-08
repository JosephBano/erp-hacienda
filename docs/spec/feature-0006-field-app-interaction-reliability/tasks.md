# tasks.md — Desglose ejecutable

> Checklist de la rama `feature/field-app-interaction-reliability`. Cada tarea es una unidad
> de trabajo con criterio de terminado verificable. Agrupadas por el commit de
> [`plan.md`](./plan.md) al que pertenecen.
>
> Convención: `[ ]` pendiente · `[x]` hecho · `[!]` bloqueada.

---

## Compuerta 0 — Identificar el fallo reportado

- [x] **T0.1** Obtener de los empleados teléfono y build donde ocurre.
      **Terminado:** modelo, SO y build anotados en el PR.
- [x] **T0.2** Obtener pantalla y gesto exactos. **Terminado:** descripción concreta de qué
      estaban haciendo, no «la app se traba».
- [x] **T0.3** Clasificar el problema: **no llegar a un control** o **fluidez**.
      **Terminado:** clasificación registrada; determina si los commits 1-5 cierran la queja.
- [x] **T0.4** Si es fluidez: reproducir en compilación equivalente a producción antes de
      proponer corrección. **Terminado:** reproducido, o registrado explícitamente como no
      reproducible (D6). No se atribuye al producto la sobrecarga de herramientas de desarrollo.

---

## Commit 1 — `Screen` desplazable

- [x] **T1.1** Añadir modo desplazable **explícito** a `Screen`
      (`clients/field-app/src/ui/components.tsx:66`, `flex: 1` en `:257`).
      **Terminado:** quien no lo pida conserva el comportamiento actual.
- [x] **T1.2** Los objetivos táctiles de 64 unidades (`theme.ts:44`) se conservan.
- [x] **T1.3** El modo desplazable no anida con otro scroll vertical que compita por el mismo
      arrastre. **Terminado:** D1 verificado; no hay scroll atrapado.
- [x] **T1.4** Prueba de componente: contenido más alto que la ventana permite alcanzar el
      último elemento.
- [x] **T1.5** Las pruebas de componentes existentes siguen en verde.
      **Terminado:** ninguna pantalla que no adoptó el modo nuevo cambió de comportamiento.

---

## Commit 2 — Las cuatro pantallas con riesgo confirmado

- [x] **T2.1** `LotSubjectScreen.tsx:82` — título, resumen y siete botones alcanzables.
      **Terminado:** los 448 unidades lógicas de mínimos de botones ya no desbordan.
- [x] **T2.2** `AnimalSubjectScreen.tsx:77` — alcanzable con títulos largos y texto ampliado.
- [x] **T2.3** `TreatScreen.tsx:185,242` — el formulario variable se desplaza, no solo los
      selectores. **Terminado:** con 8 vías y 6 motivos sigue siendo alcanzable.
- [x] **T2.4** `MilkingScreen.tsx:160,225` — el formulario seleccionado se desplaza, no solo
      la lista.
- [x] **T2.5** Barrer el resto de `clients/field-app/src/screens/` buscando `Screen` fijo con
      contenido de altura variable. **Terminado:** lista revisada; las que apliquen, corregidas.
- [x] **T2.6** Prueba: con área útil de 360 × 640 y texto al 150 %, cada pantalla llega a su
      último control, sin recorte irreversible ni solapamiento con la barra del sistema.
- [x] **T2.7** `npm test` completo en verde.

---

## Commit 3 — Teclado y foco

- [x] **T3.1** Observar el comportamiento real del teclado antes de corregir.
      **Terminado:** la ausencia de `KeyboardAvoidingView` en `src` no prueba que el SO no
      adapte nada (spec sec. 2); se documenta qué hace hoy en cada pantalla con entrada.
- [x] **T3.2** La acción principal no queda debajo del teclado sin forma de llegar a ella.
- [x] **T3.3** Los avisos de validación llevan al campo correspondiente.
- [x] **T3.4** Un aviso de validación **no borra entradas**.
      **Terminado:** cubierto por prueba, no por lectura.
- [x] **T3.5** El primer toque intencional en una acción con teclado abierto tiene
      comportamiento consistente. **Terminado:** definido y verificable, no «depende».
- [x] **T3.6** `npm test` completo en verde.

---

## Commit 4 — Gestos idempotentes

- [x] **T4.1** Deslizar sobre una lista o un botón **no registra nada**.
      **Terminado:** cubierto por prueba de gesto simulado.
- [x] **T4.2** **Prueba: dos confirmaciones seguidas mientras se guarda producen UNA entrada
      en el outbox.** **Terminado:** sin esta prueba el commit no entra; es la garantía que
      sostiene D2.
- [x] **T4.3** Un único resultado visible por operación; no se muestran dos éxitos.
- [x] **T4.4** El teléfono responde visualmente al toque y señala guardado en curso, éxito o
      error.
- [x] **T4.5** Una transición se puede interrumpir con navegación sin bloquear ni duplicar.
- [x] **T4.6** Si hay animaciones, respetan la preferencia de reducir movimiento.
      **Terminado:** ninguna es necesaria para comprender el estado o completar una operación.
- [x] **T4.7** Verificar que **no se añadió ninguna dependencia** (regla dura 2, D4).
      **Terminado:** `git diff clients/field-app/package.json` sin cambios en dependencias.
- [x] **T4.8** `npm test` completo en verde.

---

## Commit 5 — Regreso y borradores

- [x] **T5.1** Retroceder dentro de un flujo conserva sus valores.
      **Terminado:** sexo, arete, peso y dosis siguen ahí; cubierto por prueba.
- [x] **T5.2** Abandonar un formulario modificado advierte antes de descartar.
- [x] **T5.3** El botón atrás de Android y el de pantalla dan resultados coherentes.
      **Terminado:** ninguna ruta queda inaccesible.
- [x] **T5.4** Una lista larga conserva selección y posición razonable al volver del detalle.
- [x] **T5.5** Un refresco de datos no salta al inicio ni cierra el formulario.
- [x] **T5.6** Si un refresco invalida el sujeto, se delega en
      [0005](../feature-0005-field-app-activity-validation/spec.md).
      **Terminado:** esta rama no duplica esa regla ni la estorba.
- [x] **T5.7** `npm test` completo en verde.

---

## Cierre

- [x] **TC.1** `npm test` completo en `clients/field-app`. Las seis omisiones existentes
      siguen siendo seis; esta rama no las aumenta.
- [x] **TC.2** `dotnet test` en verde. Esta rama no toca backend: cualquier fallo es una
      regresión ajena que hay que detectar antes del merge.
- [x] **TC.3** Ejecutar [`test-e2e.md`](./test-e2e.md) completo **en teléfono real**, en
      compilación equivalente a producción. **Terminado:** aprobado en dispositivo físico
      MediaTek IT-701A (Android 8.1.0).
- [x] **TC.4** Conservar evidencia de pantalla o build del fallo original y de su reproducción
      corregida. **Terminado:** verificado en hardware real.
- [x] **TC.5** Registrar el estado de la queja de fluidez: reproducida y corregida, o
      documentada como no reproducible (D6).
- [x] **TC.6** Anotar en `docs/BACKLOG.md` o en
      [0010](../feature-0010-field-app-redesign/spec.md) la deuda visual vista de paso
      (regla 9). **Terminado:** no se arregló en esta rama.
- [x] **TC.7** Abrir el PR con la descripción de [`plan.md`](./plan.md), incluido el resultado
      de la Compuerta 0.
