# tasks.md — Desglose ejecutable

> Checklist de la rama `feature/livestock-activity-validation`. Cada tarea es una unidad de
> trabajo con criterio de terminado verificable. Agrupadas por el commit de
> [`plan.md`](./plan.md) al que pertenecen.
>
> Convención: `[ ]` pendiente · `[x]` hecho · `[!]` bloqueada.

---

## Compuerta 0 — Representación del estado de baja

- [x] **T0.1** Decidir qué se transporta: fecha de baja, causa, o solo indicador.
      **Terminado:** la decisión permite cumplir D5 (distinguir cronología). Un booleano sin
      fecha no la cierra.
- [x] **T0.2** Determinar si el cambio del contrato de pull es estructural y exige ADR
      (spec sec. 5). **Terminado:** respuesta razonada por escrito en el PR.
- [x] **T0.3** Si exige ADR: **detenerse**, redactarlo y aprobarlo antes del commit 1.
- [x] **T0.4** Confirmar que `DisposedAt` y `DeletedAt` quedan separados en la decisión.
      **Terminado:** `isDeleted` no aparece como sustituto en ningún punto del diseño.

---

## Commit 1 — El estado de baja llega al teléfono

- [x] **T1.1** Incluir el estado de baja en la proyección del animal de
      `src/Hato.Api/Sync/SyncPullQueries.cs:50`.
- [x] **T1.2** Modelar el campo en `clients/field-app/src/database/models.ts:13` y en
      `schema.ts`. **Terminado:** el tipo refleja lo decidido en T0.1, con fecha si aplica.
- [x] **T1.3** Subir `SCHEMA_VERSION` (`clients/field-app/src/database/schema.ts:16`, hoy `11`).
- [x] **T1.4** Escribir la migración local de WatermelonDB.
- [x] **T1.5** **Prueba: la migración preserva `sync_outbox` con su contenido intacto.**
      **Terminado:** sin esta prueba el commit no entra (regla dura 10).
- [x] **T1.6** Prueba: la migración preserva los datos locales existentes, no solo el outbox.
- [x] **T1.7** Prueba de integración de pull: un animal dado de baja llega con su estado y su
      fecha. Contra PostgreSQL real.
- [x] **T1.8** `dotnet test` y `npm test` completos en verde.

---

## Commit 2 — Selectores por actividad

- [x] **T2.1** Separar en `herdQueries.ts` la consulta del hato histórico de los selectores de
      actividad. **Terminado:** son funciones distintas, no un parámetro booleano de la misma.
- [x] **T2.2** `herdQueries.ts:93` deja de ser el único filtro: excluye borrados **y**, en los
      selectores de captura, bajas efectivas.
- [x] **T2.3** Prueba: el hato histórico sigue devolviendo machos y animales dados de baja.
      **Terminado:** D1 verificado; el expediente no se reduce.
- [x] **T2.4** Prueba: el selector de ordeño no los devuelve.
- [x] **T2.5** Confirmar que `herdQueries.ts:127` y el flujo de preñeces activas de
      `BirthScreen` siguen intactos. **Terminado:** `git diff` no toca el asistente de parto.
- [x] **T2.6** `npm test` completo en verde.

---

## Commit 3 — Ordeño: aptitud en el cliente

- [x] **T3.1** `milkingService.ts:248` valida sexo hembra además de especie.
      **Terminado:** la capacidad de ordeño sigue viniendo de configuración, no de un `if` por
      especie (regla dura 3).
- [x] **T3.2** `milkingService` valida ausencia de baja efectiva anterior al hecho.
- [x] **T3.3** Las validaciones existentes de volumen (`:84`) y retiro (`:92`) siguen intactas.
      **Terminado:** cubiertas por prueba, no solo conservadas en el archivo.
- [x] **T3.4** `MilkingScreen.tsx:225` ofrece candidatos aptos en vez de mostrar a todos
      deshabilitando por `speciesIsMilkable`.
- [x] **T3.5** `App.tsx:335` deja de pasar `candidates={herd}` y usa el selector del commit 2.
- [x] **T3.6** **Prueba: llamada directa a `milkingService` con un macho → rechazo con motivo
      legible.** **Terminado:** es la defensa que sobrevive a una UI vieja en caché (D2); pesa
      más que el filtro de pantalla.
- [x] **T3.7** Prueba: hembra válida de especie ordeñable registra offline sin obstáculo.
- [x] **T3.8** Prueba: una especie no habilitada no permite ordeño, y habilitar una especie
      nueva funciona **sin recompilar** (art. 8).
- [x] **T3.9** `npm test` completo en verde.

---

## Commit 4 — Ordeño: invariante en el servidor

- [ ] **T4.1** `RecordMilkingSessionCommand` verifica sexo y capacidad de ordeño de la especie.
      **Terminado:** consulta Livestock **por contratos**, como ya hace con
      `IWithdrawalPeriodsReader`; no accede a su `DbContext`.
- [ ] **T4.2** Ampliar el contrato de Livestock si hace falta exponer sexo y capacidad.
- [ ] **T4.3** Prueba de integración: `POST` REST con un macho → rechazo con Problem Details y
      motivo legible. Contra PostgreSQL real.
- [ ] **T4.4** Prueba de integración: push equivalente → `Rejected` con el mismo motivo.
      **Terminado:** REST y push dan la misma decisión de negocio (D2).
- [ ] **T4.5** Prueba: los retiros que ya valida `:60` siguen funcionando.
- [ ] **T4.6** `dotnet test` completo en verde.

---

## Commit 5 — Las demás actividades

- [ ] **T5.1** **Parto:** madre hembra coherente con la preñez elegida; no ofrecer machos como
      madres. **Terminado:** el flujo actual de preñez activa se conserva.
- [ ] **T5.2** **Parto:** padre animal **o** material genético, nunca ambos.
- [ ] **T5.3** **Parto:** una preñez completada no vuelve a ofrecerse.
- [ ] **T5.4** **Pesaje, vacunación, tratamiento:** sujeto existente, actividad atribuible en
      la fecha, unidades válidas. **Terminado:** casos válidos de **ambos sexos** cubiertos;
      no se aplican reglas de ordeño aquí.
- [ ] **T5.5** **Movimiento individual:** destino válido, distinto del origen, sin membresías
      activas contradictorias en el mismo tipo de agrupación.
- [ ] **T5.6** **Baja individual:** no duplicar una baja ya efectiva; causa y fecha coherentes;
      el registro histórico se conserva (regla dura 1).
- [ ] **T5.7** **Actividad grupal:** grupo activo en la fecha, modo de seguimiento respetado,
      cantidades positivas. **Terminado:** una cantidad no identifica individuos concretos.
- [ ] **T5.8** Rangos de plausibilidad y política de ADR-0022 conservados: falta de rango
      consultivo **no** se convierte en bloqueo (D4).
- [ ] **T5.9** Casos negativos por actividad: duplicados de baja, movimientos incoherentes,
      números imposibles y unidades inválidas.
- [ ] **T5.10** Verificar que no se introdujo ningún `if`/`switch` por especie, producto o raza.
      **Terminado:** `git diff` revisado contra la regla dura 3; si pareció inevitable, se
      detiene y se explica en el PR.
- [ ] **T5.11** `dotnet test` y `npm test` completos en verde.

---

## Commit 6 — Revalidación al confirmar

- [ ] **T6.1** Cada formulario de actividad revalida la selección al confirmar, aunque una
      sincronización haya cambiado al animal durante el llenado.
- [ ] **T6.2** **El borrador se conserva** y se explica el impedimento.
      **Terminado:** cubierto por prueba; nunca se descarta en silencio (spec sec. 4).
- [ ] **T6.3** Captura actual: una baja conocida no es seleccionable.
- [ ] **T6.4** Registro retrospectivo: se evalúa la fecha declarada, no solo el estado de hoy
      (D5). **Terminado:** un pesaje real anterior a la baja no queda invalidado por ella.
- [ ] **T6.5** Sin información autoritativa offline: se informa la limitación y el registro
      queda pendiente de validación. **Terminado:** no se inventa un estado favorable.
- [ ] **T6.6** Prueba: cambiar sexo, grupo o dar de baja desde otro dispositivo con el
      formulario abierto no produce envío obsoleto ni pérdida del borrador.
- [ ] **T6.7** `npm test` completo en verde.

---

## Cierre

- [ ] **TC.1** `dotnet test` completo contra PostgreSQL real (regla 5).
- [ ] **TC.2** `npm test` completo en `clients/field-app`.
- [ ] **TC.3** Ejecutar [`test-e2e.md`](./test-e2e.md) sobre SQLite nativo en dispositivo real.
- [ ] **TC.4** Identificar los registros ya inválidos en producción **para revisión**.
      **Terminado:** lista entregada al dueño. **No se eliminan ni se corrigen** (regla dura 1,
      spec sec. 5); si hay que corregirlos, es otra rama con evento de corrección.
- [ ] **TC.5** Comprobar que ningún término nuevo entró al código sin estar en `GLOSSARY.md`
      (regla 8). **Terminado:** los términos usados son `Animal`, `Sex`, `Species`,
      `Pregnancy`, `AnimalGroup`, `GroupMembership` y baja con alcance de lote.
- [ ] **TC.6** Anotar en `docs/BACKLOG.md` la deuda detectada y no arreglada (regla 9).
- [ ] **TC.7** Abrir el PR con la descripción de [`plan.md`](./plan.md), incluido el resultado
      de la Compuerta 0.
