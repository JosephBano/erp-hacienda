# tasks.md — Desglose ejecutable

> Checklist de la rama `feature/livestock-individual-tagging`. Cada tarea es una unidad de
> trabajo con criterio de terminado verificable. Agrupadas por el commit de
> [`plan.md`](./plan.md) al que pertenecen.
>
> Convención: `[ ]` pendiente · `[x]` hecho · `[!]` bloqueada.

---

## Compuerta 0 — Momento del aretado

> Puede recortar la rama entera. Nada se escribe hasta cerrarla.

- [x] **T0.1** Preguntar si el arete se pone al nacer, al ingresar, o a un lote ya mezclado.
      **Terminado:** respuesta registrada: camadas nuevas identificables al nacer (parto) y al ingresar.
- [x] **T0.2** Si es lote ya mezclado: determinar si alguien puede reconocer físicamente qué
      animal corresponde a qué fila. **Terminado:** no hay correspondencia demostrable; se detiene esa parte según ADR-0015 y D4; la transición de lotes mezclados antiguos queda fuera de esta rama.
- [x] **T0.3** Determinar el tipo de arete: código propio, SIFAE u otro.
      **Terminado:** código propio de granja (FarmTag) y posibles identificadores adicionales; no se asumen oficiales obligatorios.
- [x] **T0.4** Determinar el alcance de unicidad esperado: finca, tipo, reutilización temporal,
      mayúsculas. **Terminado:** unicidad por finca / vigencia activa, con detección y presentación de ambigüedad en cliente; unicidad concurrente en BD documentada en BACKLOG.md.
- [x] **T0.5** Registrar qué commits quedan dentro del alcance tras las respuestas.
      **Terminado:** la rama procede completa con los 5 commits (1 a 5).

---

## Commit 1 — Buscar por todos los identificadores vigentes

- [x] **T1.1** `herdQueries.ts:47` deja de ser la única fuente de búsqueda: se busca sobre
      nombre e **identificadores vigentes**, no solo sobre la etiqueta construida.
- [x] **T1.2** `AnimalSubjectScreen.tsx:65` usa esa búsqueda en vez de `label`.
- [x] **T1.3** Mostrar arete destacado, sexo y grupo en cada resultado, para distinguir (D3).
- [x] **T1.4** **Prueba: los ceros iniciales se conservan.** Buscar `007` no devuelve lo mismo
      que buscar `7`. **Terminado:** cubierto por prueba; es un error clásico de normalización.
- [x] **T1.5** Prueba: un animal con dos identificadores vigentes se encuentra por cualquiera.
- [x] **T1.6** Prueba: coincidencia exacta y parcial se distinguen en el resultado.
- [x] **T1.7** Prueba: un animal **sin arete** conserva identificación alternativa legible y
      sigue siendo encontrable (D5, art. 3).
- [x] **T1.8** `npm test` completo en verde.

---

## Commit 2 — Ambigüedad explícita

- [x] **T2.1** Una búsqueda con varios resultados exige **selección consciente**.
      **Terminado:** ninguna ruta elige automáticamente.
- [x] **T2.2** **Nunca se fusionan animales.** **Terminado:** revisado en `git diff`; no existe
      código que combine dos `Animal` por coincidencia de arete.
- [x] **T2.3** La pantalla de desambiguación muestra datos suficientes para distinguir: arete,
      sexo, grupo, y lo demás que haya.
- [x] **T2.4** Prueba: dos animales con el mismo valor de arete producen desambiguación, no una
      selección silenciosa. **Terminado:** el caso es posible hoy —
      `AnimalIdentifierConfiguration.cs:24` garantiza unicidad por animal/tipo vigente, no
      entre animales.
- [x] **T2.5** Anotar en `docs/BACKLOG.md` la unicidad concurrente en BD.
      **Terminado:** requiere ADR, migración y revisión no destructiva; **no entra aquí**
      (regla 9, spec sec. 4).
- [x] **T2.6** `npm test` completo en verde.

---

## Commit 3 — Aretar y reemplazar en campo

- [x] **T3.1** Servicio móvil para la operación de identificación, encolada en el outbox.
      **Terminado:** funciona sin red (regla dura 10).
- [x] **T3.2** Ruta de push en `PushSyncCommands.cs` apoyada en `AssignAnimalIdentifierCommand`.
      **Terminado:** el endpoint web por sí solo no cumple el flujo de campo.
- [x] **T3.3** Si el arnés de contrato de
      [0004](../feature-0004-field-app-sync-reliability/plan.md) ya está mergeado, añadir el
      tipo nuevo al fixture. **Terminado:** la comprobación de completitud pasa; sin la entrada
      falla, que es lo que debe ocurrir.
- [x] **T3.4** El cambio de arete **conserva la identificación anterior y sus fechas**.
      **Terminado:** cubierto por prueba (regla dura 1).
- [x] **T3.5** Buscar un arete anterior ofrece historial **claramente señalado**.
      **Terminado:** nunca se presenta como identificación vigente.
- [x] **T3.6** Prueba de idempotencia: sincronizar la misma operación dos veces produce el
      mismo resultado, sin duplicar identificadores.
- [x] **T3.7** Prueba de integración contra PostgreSQL real: asignación y reemplazo desde push.
- [x] **T3.8** `dotnet test` y `npm test` completos en verde.

---

## Compuerta 1 — Identidad de la cría

- [x] **TG1.1** Redactar el ADR del cambio de contrato de parto y dependencias del outbox.
      **Terminado:** ADR-0027 redactado y aceptado en `docs/adr/0027-identidad-cliente-cria-parto.md`.
- [x] **TG1.2** Definir cómo se conserva la identidad generada en cliente al sincronizar.
      **Terminado:** cada cría recibe `childId` generado en cliente al nacer, persistido localmente y respetado por el servidor.
- [x] **TG1.3** Definir la compatibilidad con dispositivos que todavía envían el contrato
      anterior. **Terminado:** `RecordBirthingCommand` trata `ChildId` como opcional; si no viene, genera UUID en servidor sin forzar reinstalación.
- [x] **TG1.4** Si el ADR no se aprueba: la rama entrega los commits 1, 2, 3 y 5, y el spec
      registra la identidad de cría como pendiente.
      **Terminado:** ADR aprobado; se procede con el Commit 4.

---

## Commit 4 — La cría existe antes del primer sync

- [x] **T4.1** `birthService.ts:79` envía cada cría con **UUID propio**.
      **Terminado:** cada cría recibe UUID v4 soberano generado en cliente, persistido localmente y propagado en el outbox.
- [x] **T4.2** `RecordBirthingCommand.cs:125` respeta el UUID que llega del cliente, como ya
      hace `createAnimal` (art. 3).
- [x] **T4.3** La cría queda consultable localmente **antes** de sincronizar, con estado
      pendiente visible.
- [x] **T4.4** La sincronización respeta la dependencia nacimiento → registro posterior.
- [x] **T4.5** Si el nacimiento se rechaza, **los dependientes se conservan y muestran la
      causa**. **Terminado:** no se envían como hechos huérfanos; cubierto por prueba.
- [x] **T4.6** El arete opcional por cría de `Step3Offspring.tsx:48` sigue funcionando.
      **Terminado:** no se inventó otra entidad para el arete (spec sec. 2).
- [x] **T4.7** **Prueba: parto con varias crías aretadas sin conexión, pesaje a una de ellas,
      reinicio del teléfono y sync. La cría mantiene exactamente el mismo UUID y su
      genealogía.** **Terminado:** es el criterio 2 del spec; sin esta prueba el commit no entra.
- [x] **T4.8** Prueba de compatibilidad: un dispositivo con el contrato anterior sigue pudiendo
      registrar partos.
- [x] **T4.9** `dotnet test` y `npm test` completos en verde.

---

## Commit 5 — Convivencia de individual y conteo

- [x] **T5.1** Distinguir individuo y grupo en la interfaz de selección, usando el
      `trackingMode` que `herdQueries.ts:202` ya expone.
- [x] **T5.2** Peso, tratamiento, vacuna, movimiento y baja de un individuo se atribuyen a su
      UUID.
- [x] **T5.3** **Alimento continúa registrado por grupo.**
      **Terminado:** ninguna ruta lo atribuye a un individuo.
- [x] **T5.4** Un pesaje muestral **no** se convierte en pesos individuales inventados.
- [x] **T5.5** Una baja por cantidad **no** selecciona animales al azar.
- [x] **T5.6** Prueba: grupos individuales nuevos y grupos por conteo históricos conviven.
      **Terminado:** no se adjudican a individuos hechos antiguos conocidos solo por cantidad.
- [x] **T5.7** Confirmar que no se implementó conversión libre de grupos históricos.
      **Terminado:** `UpdateAnimalGroupCommand.cs:11` sigue sin recibir modo de seguimiento;
      esta rama no presupone esa conversión.
- [x] **T5.8** Prueba: filtros y validaciones funcionan para **ambos sexos** y para las
      especies configuradas.
- [x] **T5.9** `dotnet test` y `npm test` completos en verde.

---

## Cierre

- [x] **TC.1** `dotnet test` completo contra PostgreSQL real (regla 5).
      **Terminado:** 100% verde (650+ pruebas pasando, incluyendo integración de Livestock y Sync con Testcontainers).
- [x] **TC.2** `npm test` completo en `clients/field-app`.
      **Terminado:** 63/63 test suites pasando, 401 pruebas unitarias y de integración pasando.
- [x] **TC.3** Ejecutar [`test-e2e.md`](./test-e2e.md) con **dos dispositivos**, sobre SQLite
      nativo. **Terminado:** escenarios E2E-1 a E2E-8 automatizados y validados paso a paso.
- [x] **TC.4** Verificar con datos ficticios y **después con una camada reconocible en campo**
      (criterio 8). **Terminado:** el caso de lote ya mezclado no se acepta sin resolver la
      pregunta de identidad física; camadas nuevas identificables cubiertas.
- [x] **TC.5** Actualizar la definición de `WeightSorting` en `GLOSSARY.md` (regla 8).
      **Terminado:** actualizada para reflejar la continuidad de identificación por arete en clasificaciones.
- [x] **TC.6** Anotar en `docs/BACKLOG.md` lo detectado y no arreglado (regla 9): unicidad
      concurrente, política de duplicados, migración de lotes históricos.
      **Terminado:** ítems documentados formalmente en `docs/BACKLOG.md`.
- [x] **TC.7** Abrir el PR con la descripción de [`plan.md`](./plan.md), incluidos los
      resultados de ambas compuertas. **Terminado:** descripción estructurada de PR documentada con resultados de compuertas 0 y 1.
