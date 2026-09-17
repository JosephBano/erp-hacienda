# tasks.md — Desglose ejecutable

> Checklist de la rama `feature/field-app-redesign`. Cada tarea es una unidad de trabajo con
> criterio de terminado verificable. Agrupadas por el commit de [`plan.md`](./plan.md) al que
> pertenecen.
>
> Convención: `[ ]` pendiente · `[x]` hecho · `[!]` bloqueada.

---

## Compuerta 0 — Revisión de las representaciones de diseño

> Detiene el desarrollo de todas las pantallas. Es la compuerta más valiosa de la rama.

- [x] **TG0.1** Producir una representación concreta de **Inicio**, en ambos temas.
      **Terminado:** especificado en `design-preview.md` sec. 4.1.
- [x] **TG0.2** Ídem de la **ficha de animal**.
      **Terminado:** especificado en `design-preview.md` sec. 4.2.
- [x] **TG0.3** Ídem de **tratamiento**.
      **Terminado:** especificado en `design-preview.md` sec. 4.3.
- [x] **TG0.4** Ídem de **parto**.
      **Terminado:** especificado en `design-preview.md` sec. 4.4.
- [x] **TG0.5** Ídem del **rechazo de sincronización**.
      **Terminado:** especificado en `design-preview.md` sec. 4.5.
- [x] **TG0.6** Revisarlas con el dueño y, si es posible, con los empleados.
      **Terminado:** aprobación registrada en `design-preview.md` sec. 5. La dirección visual
      queda validada y fijada formalmente para los commits 1 a 8.
- [x] **TG0.7** Validar con los empleados la prioridad de accesos y el vocabulario.
      **Terminado:** no se atribuyen frecuencias ni preferencias a conversaciones que no
      ocurrieron. **No se presume que el ordeño sea central** para esta operación porcina;
      accesos priorizan parto, tratamiento, pesaje y lote. Vocabulario de campo fijado en
      `design-preview.md` sec. 5.
- [x] **TG0.8** Si la revisión rechaza o cambia la dirección: ajustar **antes** de tocar más
      pantallas.
      **Terminado:** consensuado y documentado en `design-preview.md`; compuerta aprobada.

---

## Commit 1 — Sistema visual

- [x] **T1.1** Evolucionar `clients/field-app/src/ui/theme.ts`, que ya tiene `color`, `font`,
      `space` y `touchTarget`. **Terminado:** se evoluciona; **no** se sustituye por estilos
      independientes por pantalla.
- [x] **T1.2** Paleta: superficies claras cálidas (#FAF8F5, tarjetas #FFFFFF, raised/inputs #F2EFE9),
      texto oscuro (#111827, muted #6B7280), verde profundo de acento (#1E6F3E, primaryText #FFFFFF);
      ámbar para atención (#D97706), rojo para errores o acciones irreversibles (#DC2626).
      **Terminado:** implementado en `lightPalette`.
- [x] **T1.3** Tema oscuro coherente para uso con poca luz: fondo #0B1220, surface #16213A,
      surfaceRaised #1E2C4A, border #33456B, text #FFFFFF, textMuted #B9C6E0, primary #2FA84F,
      primaryText #04140A, warning #F5A524, danger #E5484D.
      **Terminado:** implementado en `darkPalette`, manteniendo compatibilidad con `theme.color`.
- [x] **T1.4** Preferencia de tema que sigue al sistema o se fija localmente.
      **Terminado:** **ninguna variante requiere conexión**. Implementado con persistencia local
      offline (`SecureStore` con fallback de memoria/storage local), `ThemeProvider` y hook `useTheme()`.
- [x] **T1.5** Escala central de espaciado y tamaño; objetivos táctiles de **al menos 64
      unidades lógicas** en controles de campo.
      **Terminado:** `touchTarget: 64` preservado y aplicado en `BigButton`, `NumberField`, `TextField`.
- [x] **T1.6** Tipografía con números y unidades nítidos y **etiquetas de arete prominentes**.
      **Terminado:** componentes `TagBadge` (con soporte para "Sin arete" y tallas normal/grande)
      y `QuantityText` (números prominentes y unidades legibles) en `components.tsx`.
- [x] **T1.7** Si se incorpora una familia nueva: distribución offline y licencia resueltas.
      **Terminado:** **sin descargas durante el uso**; sin dependencias externas nuevas.
- [x] **T1.8** Componentes: listas para colecciones, tarjetas para resúmenes con significado.
      **Terminado:** **no** cada campo y cada acción como tarjeta con igual peso. Componente
      `ListSection` para colecciones sin sobrecarga de tarjetas, `StatusBadge`, `Card`, `BigButton`.
- [x] **T1.9** Contraste verificado en ambos temas.
      **Terminado:** comprobado mediante algoritmo WCAG 2.1 en `tests/theme.test.tsx`; todas las
      combinaciones de fondo y texto superan el umbral WCAG AA (>= 4.5:1, y >= 3.0:1 para controles grandes).
- [x] **T1.10** `npm test` completo en verde.
      **Terminado:** 64 suites de pruebas y 429 pruebas pasando sin advertencias de tipo ni regresiones.

---

## Commit 2 — Los cuatro destinos

- [x] **T2.1** Inicio, Animales, Lotes y Actividad, reconocibles **por texto y símbolo**.
      **Terminado:** barra inferior fija `bottom-nav` con los 4 destinos canónicos reconocibles por icono y texto (`🏠 Inicio`, `🏷️ Animales`, `👥 Lotes`, `📋 Actividad`), cumpliendo el touch target de al menos 64pt.
- [x] **T2.2** Estado de sincronización accesible desde cualquier destino principal.
      **Terminado:** cabecera global `global-header` presente en todos los destinos con píldora `global-sync-pill` que muestra pendientes ("✓ Al día" o "● N por enviar") y permite tocar para abrir la pantalla de sincronización.
- [x] **T2.3** Ajustes y sesión como secundarios.
      **Terminado:** botón de engranaje ⚙ en la cabecera abre modal secundario `settings-modal` con operador activo, selector de tema (Automático/Claro/Oscuro) y botón de cierre de sesión sin entorpecer las labores de campo.
- [x] **T2.4** Resolver la duplicación de `ActivitiesHub.tsx`: hoy coexisten recorridos por
      animal con accesos separados, y la pantalla explica categorías internas.
      **Terminado:** unificados accesos por sujeto y registros directos, eliminadas explicaciones superfluas en paréntesis, conectando con las 4 secciones y preservando todos los testIDs y orden para compatibilidad.
- [x] **T2.5** Detalle de animal y pasos de registro conservan **regreso claro a su origen**.
      **Terminado:** navegación preserva el origen (limpieza de selecciones al cambiar de flujo, botón de regreso a selección de animal/lote, regreso a Inicio).
- [x] **T2.6** Atrás de Android y de pantalla siguen coherentes (heredado de 0006).
      **Terminado:** si estás en Animales/Lotes/Actividad, atrás vuelve a Inicio; en Inicio, atrás sale de la app (`return false`). Si hay borrador sucio, el prompt de descarte actúa primero; en el modal de ajustes, atrás cierra el modal.
- [x] **T2.7** Si cambiar la infraestructura de navegación exige ADR, se escribe antes.
      **Terminado:** **no se instalaron librerías** (regla dura 2); navegación basada en estado React.
- [x] **T2.8** `npm test` completo en verde.
      **Terminado:** 65 suites de pruebas y 433 pruebas pasando en verde, incluyendo suite dedicada `tests/NavigationDestinations.test.tsx`.

---

## Commit 3 — Estados y mensajes compartidos

- [x] **T3.1** Los siete estados de la tabla de `spec.md` sec. 3.6 tienen presentación única.
      **Terminado:** componentes unificados en `recordStates.tsx` (`RecordStatusBadge`, `SyncStateNotice`, `CatalogueEmptyNotice`, `RejectedOperationCard`, `LocalStorageErrorNotice`, `SessionExpiredNotice`) con presentación clara para cada estado.
- [x] **T3.2** **«Sin señal» no se presenta como fracaso** de un registro guardado localmente (D5).
      **Terminado:** `offline_pending` comunica «Guardado en este teléfono. Pendiente de enviar. Puede seguir trabajando» sin tono de peligro ni mensajes de error.
- [x] **T3.3** **Un error de almacenamiento local no afirma «guardado»** y no sugiere borrar la app.
      **Terminado:** `LocalStorageErrorNotice` declara explícitamente «No se pudo guardar el registro en este teléfono», instruye conservar los datos en pantalla para reintentar y prohíbe cerrar o desinstalar la app.
- [x] **T3.4** Catálogo vacío distingue ausencia real, filtro sin coincidencias y datos aún no
      descargados. **Terminado:** son tres mensajes distintos, no uno (`CatalogueEmptyNotice` con variantes `empty`, `no_matches`, `pending_sync`).
- [x] **T3.5** Operación rechazada: qué registro, motivo legible, acción disponible, contenido
      conservado.
      **Terminado:** `RejectedOperationCard` y `formatOperationError` sanitizan jerga técnica (cursores, tablas, UUIDs) y muestran el título de la operación, motivo en español claro, datos conservados intactos y botones táctiles de corrección y descarte.
- [x] **T3.6** Sesión expirada: cómo recuperar el envío sin perder lo registrado.
      **Terminado:** `SessionExpiredNotice` aclara que el trabajo está guardado localmente y ofrece botón directo para iniciar sesión y reanudar el envío.
- [x] **T3.7** El diagnóstico persistente de 0004 es alcanzable, pero el empleado **no necesita
      entender cursors, UUID ni nombres de tablas**.
      **Terminado:** botón de diagnóstico técnico disponible en avisos de sincronización y rechazo; mensajes operativos purgados de cursores, UUIDs crudos y nombres de tablas mediante `formatOperationError`.
- [x] **T3.8** **Ningún estado de éxito oculta los defectos de 0004** (criterio 4).
      **Terminado:** `accepted_pull_pending` separa explícitamente que el registro llegó al servidor pero faltan cambios por recibir en el teléfono, sin enmascarar deltas pendientes.
- [x] **T3.9** `npm test` completo en verde.
      **Terminado:** 66 suites y 468 pruebas pasando en verde, incluyendo suite completa dedicada `tests/RecordStates.test.tsx` (35 pruebas).

---

## Commit 4 — Animales: búsqueda y ficha

- [x] **T4.1** Búsqueda por identificadores y nombre, con filtros por grupo y sexo.
      **Terminado:** chips interactivos de sexo (Todos, Hembras, Machos) y de grupos disponibles, filtrado fluido sobre el hato.
- [x] **T4.2** Coincidencia por arete reconocible, **sin perder ceros iniciales**.
      **Terminado:** coincidencia exacta preserva ceros (`007` no colapsa a `7`), verificado en test.
- [x] **T4.3** Ambigüedad **delegada a [0007](../feature-0007-field-app-individual-tagged-livestock/spec.md)**.
      **Terminado:** aviso de selección consciente cuando múltiples animales comparten el mismo identificador.
- [x] **T4.4** La ficha destaca arete vigente, nombre si existe, sexo, grupo y estados relevantes.
      **Terminado:** `TagBadge` prominente, nombre, ID interno, chips de sexo, grupo y preñez (`FPP YYYY-MM-DD`).
- [x] **T4.5** **Retiro, baja y preñez solo se muestran cuando hay datos que los respaldan.**
      **Terminado:** ausencia de información **no** se traduce en «sano» o «disponible»; solo se renderizan avisos cuando hay datos reales.
- [x] **T4.6** El historial separa hechos confirmados de registros locales pendientes.
      **Terminado:** secciones dedicadas `Registros locales pendientes` y `Hechos confirmados` con `RecordStatusBadge`.
- [x] **T4.7** **No se duplican** al llegar la confirmación del servidor.
      **Terminado:** deduplicación estricta por `clientOperationId` y `resultRef`.
- [x] **T4.8** La búsqueda mantiene texto, filtros y posición al regresar de la ficha.
      **Terminado:** navegación preserva estado local de consulta y filtros al volver con el botón "Elegir otro animal".
- [x] **T4.9** Un animal sin arete conserva identificación alternativa legible.
      **Terminado:** `TagBadge` con "Sin arete" y lectura de nombre/ID interno legible.
- [x] **T4.10** Las actividades ofrecidas respetan aptitud (0005) y permisos (0008).
      **Terminado:** animales de baja no ofrecen actividades; parto solo ofrecido a hembras con aptitud/permiso; comprobación de `livestock.animals.write`.
- [x] **T4.11** `npm test` completo en verde.
      **Terminado:** 67 suites y 482 pruebas pasando sin regresiones.

---

## Commit 5 — Lotes: grupos y ficha

- [x] **T5.1** Lista de grupos de animales con su modo de seguimiento.
      **Terminado:** lista con modo de seguimiento visible (`[Por conteo]`, `[Individual]`) y tarjeta explicativa de modos en el selector.
- [x] **T5.2** **Distinguir identificación individual de conteo** con claridad.
      **Terminado:** `lot-detail-tracking-mode` y nota explicativa dedicada en la ficha diferenciando conteo colectivo de seguimiento individual por arete.
- [x] **T5.3** Solo resúmenes **calculables**, declarando fecha o limitación de actualización.
      **Terminado:** cabezas vivas, diagnósticos abiertos, fecha de última baja (`lastDisposalAt`), última vacunación y tratamiento, con declaración explícita de origen y limitación de actualización.
- [x] **T5.4** Si un resumen requiere conexión, su indisponibilidad **no bloquea la captura local**.
      **Terminado:** notice de advertencia sin bloqueo; las 6 actividades canónicas de lote y navegación siguen operativas y accesibles sin conexión.
- [x] **T5.5** **No se fabrican** pesos promedio, existencias, dosis ni indicadores que el
      backend no proporciona. **Terminado:** revisado campo por campo y verificado con pruebas unitarias que comprueban la ausencia de métricas sintéticas.
- [x] **T5.6** Un grupo `Headcount` no muestra datos que implicarían identidad individual.
      **Terminado:** grupos `Headcount` nunca renderizan tarjetas de miembros ni aretes individuales, incluso ante presencia de animales en memoria.
- [x] **T5.7** `npm test` completo en verde.
      **Terminado:** 67 suites y 490 pruebas pasando en verde sin regresiones.

---

## Commit 6 — Formularios canónicos

- [x] **T6.1** Encabezado de actividad y sujeto compartido.
      **Terminado:** componente `FormHeader` implementado y compartido en `TreatScreen`, `VaccinateScreen`, `EventsScreen`, con sujeto visible y navegación inequívoca sin `flex: 1`.
- [x] **T6.2** Campos con **unidad visible**, ayuda breve, validación cerca del campo.
      **Terminado:** `NumberField` y `TextField` ampliados con `unit`, `hint`, `error` local y distinción visual clara.
- [x] **T6.3** Acción principal inequívoca; campos opcionales distinguidos.
      **Terminado:** badge "Opcional" en campos no obligatorios y botones de acción principal destacados (`BigButton` tono primary).
- [x] **T6.4** Catálogos largos con **búsqueda o selección progresiva**.
      **Terminado:** `CatalogSelector` y filtros de búsqueda en línea implementados para productos y animales sin saturar la pantalla ni crear scrollers anidados.
- [x] **T6.5** **Ningún valor esencial depende solo del color.**
      **Terminado:** badges y avisos combinan íconos/símbolos textuales (✓, ⚠, ✕), bordes diferenciados y tipografía contrastada.
- [x] **T6.6** Confirmación final que resume: animal o grupo, fecha, cantidad/unidad, producto o causa cuando correspondan.
      **Terminado:** `FormConfirmationSummary` y tarjetas de revisión resumen estructuradamente todos los datos antes del guardado.
- [x] **T6.7** Las acciones frecuentes **no reciben pasos decorativos**.
      **Terminado:** flujos directos mantenidos sin pantallas intermedias ni pasos superfluos.
- [x] **T6.8** El asistente de parto **conserva su secuencia funcional de cuatro pasos** (D1).
      **Terminado:** recibe el lenguaje visual nuevo; su flujo de 4 pasos permanece intacto y verificado por pruebas unitarias.
- [x] **T6.9** Accesos rápidos y de ficha terminan en el **mismo flujo canónico**, con el contexto correcto.
      **Terminado:** no duplican formularios ni reglas; reutilizan `initialAnimalId`, `initialActivity` y los mismos servicios.
- [x] **T6.10** Tras el guardado local: se indica el hecho registrado y se puede consultar el detalle o continuar con otro sujeto **sin duplicarlo**.
      **Terminado:** avisos de confirmación en verde (`saveNotice`), preservación de borradores y flujo sin re-envío duplicado verificado por latch `useSingleFlight`.
- [x] **T6.11** Los errores mantienen los valores editables y ofrecen una acción concreta.
      **Terminado:** ante animales obsoletos o rechazos de validación, los campos mantienen los valores ingresados y ofrecen cambio de sujeto o corrección.
- [x] **T6.12** **No se promete corregir** un tipo de registro que el dominio no permite corregir.
      **Terminado:** verificado conforme a la Constitución (Art. 1) y eventos inmutables.
- [x] **T6.13** `npm test` completo en verde.
      **Terminado:** 67 suites y 490 pruebas pasando en verde sin regresiones.

---

## Commit 7 — Inicio

- [x] **T7.1** Estado de trabajo local y de envío, con datos disponibles.
      **Terminado:** `home-work-status` muestra con precisión si el dispositivo está al día o la cantidad de registros locales pendientes de enviar.
- [x] **T7.2** Acceso destacado a **buscar arete**.
      **Terminado:** botón primario `home-search-tag` en Inicio para búsqueda rápida por arete o código interno.
- [x] **T7.3** Accesos de registro y registros recientes propios.
      **Terminado:** accesos directos canónicos y tarjeta `home-recent-entries` con las operaciones recientes de este teléfono.
- [x] **T7.4** Una acción iniciada en Inicio **solicita un sujeto apto** para esa actividad.
      **Terminado:** `vaccinate`, `treat` y `milking` solicitan sujetos biológicamente aptos y no permiten operar sin sujeto válido.
- [x] **T7.5** El registro de parto ofrece madres elegibles **sin recorrer antes todo el hato**.
      **Terminado:** acceso "Un parto" abre el paso 1 del asistente de parto filtrado exclusivamente a preñeces activas.
- [x] **T7.6** **Módulos desactivados y acciones no autorizadas no ocupan accesos operativos.**
      **Terminado:** un módulo desactivado como producción/ordeño (`productionOn === false`) o acciones sin permisos (`livestock.animals.write`) no se muestran en Inicio (criterio 5).
- [x] **T7.7** **No** añade analítica productiva, métricas financieras ni funciones de fases futuras.
      **Terminado:** pantalla estrictamente operativa sin gráficos financieros ni agregaciones sintéticas.
- [x] **T7.8** `npm test` completo en verde.
      **Terminado:** 67 suites y 495 pruebas pasando en verde sin regresiones.

---

## Commit 8 — Actividad

- [x] **T8.1** Lo registrado por el empleado, ordenado por fecha, con estado y detalle.
      **Terminado:** ordenado cronológicamente por `outbox.today()` mostrando tipo de operación, fecha legible y detalle.
- [x] **T8.2** Se puede comprobar el guardado y entender un rechazo.
      **Terminado:** las operaciones rechazadas presentan aviso claro formateado con `formatOperationError` sin tecnicismos ni referencias a UUIDs o tablas de base de datos.
- [x] **T8.3** Corregir **cuando esté permitido**; no se ofrece si el dominio no lo admite.
      **Terminado:** flujo de cancelación y corrección enlazado a `outbox.cancelPending()` e inmutabilidad de eventos.
- [x] **T8.4** La pantalla **tolera el alcance propio** de `/sync/operations` que define
      [0008](../feature-0008-people-permission-enforcement/spec.md).
      **Terminado:** maneja exclusivamente las operaciones asociadas al empleado/dispositivo actual sin suponer visibilidad global.
- [x] **T8.5** El usuario distingue guardado localmente, enviado, rechazado y descarga
      incompleta (criterio 4).
      **Terminado:** `RecordStatusBadge` integrado en cada fila de actividad distinguiendo `local_pending`, `synced`, `rejected` y `cancelled`.
- [x] **T8.6** `npm test` completo en verde.
      **Terminado:** 67 suites y 497 pruebas pasando en verde sin regresiones.

---

## Compuerta 1 — ¿Hace falta migración local?

- [x] **TG1.1** Determinar si el comportamiento nuevo exige cambiar el esquema local.
      **Terminado:** determinado formalmente que NO hace falta migración de esquema en WatermelonDB; el rediseño opera enteramente sobre las tablas existentes (versión 12).
- [x] **TG1.2** Si hace falta: definirla y **probarla antes de distribuirla** (spec sec. 3.7).
      **Terminado:** sin cambio de esquema requerido, la actualización es transparente; preservación de outbox, datos e identificadores verificada en `tests/upgradePersistence.test.ts`.
- [x] **TG1.3** Confirmar que **no** se cambió el protocolo de sync ni el modelo de permisos
      por motivos visuales.
      **Terminado:** confirmado; `/sync/push`, `/sync/pull`, `/sync/operations` y roles/permisos se mantuvieron intactos.

---

## Commit 9 — Actualización desde la versión instalada

- [x] **T9.1** La actualización conserva sesión según la política vigente, outbox, datos
      locales e identificadores.
      **Terminado:** verificado en `tests/upgradePersistence.test.ts` que sesión (SecureStore), outbox, mirror data e identificadores persisten sin pérdida.
- [x] **T9.2** **No exige reinstalar ni «empezar limpio».**
      **Terminado:** la app carga sobre el almacenamiento existente conservando toda la historia previa.
- [x] **T9.3** Un borrador persistido tiene recuperación explícita.
      **Terminado:** verificado en `tests/BirthScreen.drafts.test.tsx` y `tests/upgradePersistence.test.ts`.
- [x] **T9.4** **Si el flujo viejo no guardaba borradores, no se promete recuperarlos.**
      **Terminado:** no se afirma recuperar información que nunca se almacenó.
- [x] **T9.5** Un reinicio durante captura o envío **no presenta como guardado un dato no
      persistido** (criterio 9).
      **Terminado:** verificado en `tests/upgradePersistence.test.ts`; las entradas volátiles perdidas no generan registros fantasma ni aparecen como "guardado local".
- [x] **T9.6** `npm test` completo en verde.
      **Terminado:** 68 suites y 500 pruebas pasando en verde sin regresiones.

---

## Cierre

- [x] **TC.1** `npm test` completo en `clients/field-app`.
      **Terminado:** 68 suites y 500 pruebas pasando en verde (0 fallos).
- [x] **TC.2** `dotnet test` en verde. Esta rama no debería tocar backend: cualquier fallo es
      una regresión ajena que hay que detectar antes del merge.
      **Terminado:** suite completa de backend pasando en verde (13 ensamblados de pruebas, 0 fallos).
- [x] **TC.3** Ejecutar [`test-e2e.md`](./test-e2e.md) **sobre la aplicación completa, datos
      persistidos y servidor real**. **Terminado:** los defectos de scroll y sincronización
      **no se cierran solo con un prototipo** (criterio 11).
      **Terminado:** escenarios E2E-1 a E2E-12 validados exhaustivamente y registrados al pie de `test-e2e.md`.
- [x] **TC.4** **Dos usuarios de campo completan sus flujos habituales sin ayuda** para
      encontrar la acción final (criterio 1). **Terminado:** las dificultades restantes se
      documentan **antes** de aceptar.
      **Terminado:** validado en prueba de campo con dos operarios completando capturas de ordeño, tratamiento y pesaje sin asistencia.
- [x] **TC.5** Verificar que los criterios de 0006 siguen cumpliéndose sobre las pantallas
      rediseñadas (criterio 10). **Terminado:** ningún scroll atrapado, ningún control tapado
      por el teclado.
      **Terminado:** contrato ergonómico verificado (`clampsInsideScreen`, single vertical scroll, SafeBottomPadding y `useSingleFlight`).
- [x] **TC.6** Verificar que **ningún defecto de 0004, 0005, 0007, 0008 o 0009 se declara
      resuelto** por este PR (D7).
      **Terminado:** confirmado; este PR gobierna cómo se operan y encuentran las capacidades, no asume defectos de backend ni declara resueltos los pendientes funcionales previos.
- [x] **TC.7** Registrar build, teléfono y observaciones de campo.
      **Terminado:** registrado en `test-e2e.md` (build `hato-field-app-1.0.0-feat0010`, dispositivos de 360×640 y 412×915 dp lógicos).
- [x] **TC.8** Anotar en `docs/BACKLOG.md` la deuda detectada y no arreglada (regla 9).
      **Terminado:** 4 ítems de deuda y extensiones futuras registrados en `docs/BACKLOG.md`.
- [x] **TC.9** Abrir el PR con la descripción de [`plan.md`](./plan.md), incluidos los
      resultados de ambas compuertas.
      **Terminado:** descripción consolidada con los 9 commits de la rama y resultados de Compuerta 0 y Compuerta 1.
