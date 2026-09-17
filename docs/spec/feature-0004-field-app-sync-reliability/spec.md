# spec.md — Sincronización móvil fiable y estado visible consistente

> **Estado:** propuesta investigada, **no implementada**. La carpeta tiene los cuatro
> documentos de la convención: `spec.md` (qué se decidió), [`plan.md`](./plan.md) (en qué
> orden y en qué commits), [`tasks.md`](./tasks.md) (el desglose con casillas) y
> [`test-e2e.md`](./test-e2e.md) (la verificación manual). Ningún criterio de producción
> queda cerrado por existir estos documentos.
>
> Esta carpeta reúne un único propósito: que enviar, recibir y mostrar datos produzcan
> un estado consistente y comprobable sin perder registros de campo.

- **Rama documental:** `feature/field-app-production-specs`, desde `develop` en `0faf483`.
- **Fecha de investigación:** 2026-09-07.
- **Fase:** estabilización de Fase 3 y captura de Fase 3.5.
- **Referencias:** [ADR-0005](../../adr/0005-offline-first-movil.md),
  [ADR-0008](../../adr/0008-protocolo-sincronizacion.md),
  [ADR-0017](../../adr/0017-correccion-de-registros-de-campo.md).
- **Reglas:** AGENTS.md 1, 5, 6, 7 y 10; Constitución arts. 1, 3, 9, 11 y 14.

## 1. Problema y evidencia de producción

El dueño informa tres semanas de datos en producción, fallos intermitentes al
sincronizar y cambios o eliminaciones del servidor que no se reflejan correctamente
al regresar al teléfono. No hay todavía identificación de APK/build, versión del
backend, entidad afectada, hora ni método de eliminación. Es un reporte de uso real,
no una reproducción técnica ni evidencia de pérdida definitiva de datos.

El ROADMAP conserva un estado anterior al reporte. Las tres semanas acreditan uso,
pero no prueban el criterio «sin pérdida ni duplicación» de Fase 3.

## 1.1 Candidato principal de la causa reportada: el ordeño individual

Entre los diez hallazgos de la sección siguiente, **S8 no es un riesgo de contrato: es un
defecto vivo y determinista**, y es la explicación más económica del reporte de fallos al
sincronizar. Se separa aquí porque tratarlo con el mismo peso que los demás retrasaría la
única comprobación barata que puede cerrar el diagnóstico.

La cadena, confirmada por lectura en `0faf483`:

1. `clients/field-app/src/services/milkingService.ts:82` declara `isPlausibilityConfirmed`
   como parámetro con valor por defecto, y `:98` lo incluye **siempre** en el payload
   encolado. No es un campo opcional del JSON: viaja en todos los envíos.
2. `src/Modules/Production/Hato.Modules.Production.Application/Milking/RecordMilkingSessionCommand.cs:13`
   declara `Date, Shift, RecordedBy, TotalLiters, GroupId, Notes, IndividualYields`. No
   declara ese campo.
3. `src/Hato.Api/Sync/PushSyncCommands.cs:74` fija `UnmappedMemberHandling.Disallow` y
   `:377` deserializa con esas opciones, de modo que el miembro desconocido produce
   `JsonException` → `DomainException` → resultado **`Rejected`**.

El contraste que lo confirma como caso aislado: las demás rutas de push no exponen el
campo a nivel raíz. `clients/field-app/src/services/eventService.ts:244` lo anida dentro
de `payloadJson`, que viaja como cadena, así que pesaje, tratamiento y eventos de grupo
pasan intactos; y `CreateTreatmentCourseCommand.cs:36` sí lo declara como miembro. El
ordeño individual es **la única ruta con un campo desconocido en la raíz del payload**.

Consecuencia esperada: todo ordeño individual registrado en el teléfono se rechaza en el
servidor, siempre, no de forma intermitente. Falta reproducirlo contra API real; es la
comprobación más barata de esta investigación y debería ejecutarse antes que cualquier
otra corrección de este spec.

**S8 y S9 se agravan mutuamente y no deben leerse por separado.** S8 produce rechazos
sistemáticos; S9 describe que un rechazo previo puede presentarse luego como sincronizado
si el teléfono reintenta esa operación. El servidor responde `Duplicate` acompañado de
`claim.Existing.ErrorDetails` (`PushSyncCommands.cs:116`), pero
`clients/field-app/src/services/syncEngine.ts:171` trata `Duplicate` como envío correcto
y descarta `errorDetails`. Un ordeño rechazado puede así terminar mostrado como enviado.

**Causa de clase, no de caso.** No existe `SyncPushMilkingTests.cs` en
`tests/Hato.Sync.IntegrationTests/`, pese a que sí hay pruebas de push para parto,
tratamiento, consumo, conflictos y protocolo. El servidor acepta diez tipos de operación
(`PushSyncCommands.cs:208-322`) y ninguna prueba verifica que el payload que **realmente
produce el móvil** encaje con el comando que lo recibe. Corregir solo el ordeño dejaría
intacto el hueco que permitió el defecto; ver D6 y D7.

## 2. Investigación previa

Las rutas de código siguientes son relativas a la raíz del repositorio y sus líneas
corresponden a `0faf483`. «Confirmado por lectura» no equivale a incidente reproducido
en producción. No se accedió a bases, credenciales o teléfonos de producción.

| ID | Evidencia | Conclusión y límite |
|---|---|---|
| S1 | `clients/field-app/src/App.tsx:130` carga el hato en estado React; `:178` refresca tras el sync inicial. `clients/field-app/src/services/syncEngine.ts:73` ejecuta sync al cambiar la conectividad sin notificar al contenedor. | Confirmado por lectura: falta conectar la actualización automática de las vistas. Una BD correcta puede coexistir con una pantalla vieja; no demuestra corrupción de SQLite. |
| S2 | `clients/field-app/src/screens/SyncStatusScreen.tsx:63` y `:72` actualizan resultado y contadores propios; `onModulesChanged` se llama para cambios de módulo, no al terminar sync o rehacer descarga. | La misma separación existe en sincronización manual y recuperación. |
| S3 | `src/Hato.Api/Sync/SyncPullQueries.cs:552` usa `IgnoreQueryFilters`; `:383` proyecta `DeletedAt`. `clients/field-app/src/services/syncEngine.ts:279` procesa `isDeleted: true`. | El soporte de borrados existe. No se especifica reconstruirlo suponiendo que falta. |
| S4 | `src/Modules/Livestock/Hato.Modules.Livestock.Domain/Animal.cs:57` tiene `DisposedAt`, distinto de `DeletedAt`; `SyncPullQueries.cs:50` y `clients/field-app/src/database/models.ts:13` no lo incluyen en el animal sincronizado. | Una baja no es un borrado lógico. `herdQueries.ts:93` filtra solo `isDeleted`; el estado de baja no participa en la selección. Lo resuelve [feature-0005](../feature-0005-field-app-activity-validation/spec.md). |
| S5 | `clients/field-app/src/services/syncEngine.ts:38` calcula backoff y `:87` lo expone. Búsqueda `rg -n 'retryDelayMs|setTimeout|setInterval|AppState' clients/field-app/src` no encuentra consumidor del retraso ni planificación de reintento. | No hay reintento programado por ese cálculo. Un error con conectividad estable puede requerir otro disparador o acción manual. |
| S6 | `clients/field-app/src/services/syncEngine.ts:218` limita el pull a 200 páginas y `:242` retorna éxito al agotarlas. | Reproducido con el motor real y API falsa: 200 respuestas `hasMore: true` terminan con `ok: true`. No demuestra que el volumen de producción alcance ese límite. |
| S7 | `clients/field-app/src/services/loggerService.ts:59` persiste únicamente si existe `localStorage`; `:65` recupera desde el mismo medio. | Reproducido sin `localStorage`: una instancia registra un error y la siguiente recupera cero. La prueba simula esa ausencia; no ejecuta un teléfono. |
| S8 | `clients/field-app/src/services/milkingService.ts:98` siempre envía `isPlausibilityConfirmed`. `src/Modules/Production/Hato.Modules.Production.Application/Milking/RecordMilkingSessionCommand.cs:13` no lo declara. `src/Hato.Api/Sync/PushSyncCommands.cs:78` usa `UnmappedMemberHandling.Disallow` y `:208` deserializa a ese comando. | Incompatibilidad confirmada por lectura del contrato. Se espera rechazo incluso con `false`; falta reproducir contra API real. No se corrige tolerando silenciosamente todos los campos desconocidos. |
| S9 | `PushSyncCommands.cs:116` responde `Duplicate` ante cualquier operación existente, sin discriminar su estado. `clients/field-app/src/services/syncEngine.ts:172` trata `Duplicate` como enviada. | Riesgo confirmado por lectura: si se perdió la respuesta de un rechazo y el teléfono reintenta, puede presentar ese rechazo como sincronizado. También debe investigarse una reserva aún pendiente. |
| S10 | `clients/field-app/src/services/syncEngine.ts:188` permite `resetMirror` sin coordinarse con `running`; `SyncStatusScreen.tsx:77` lo invoca antes de comprobar conectividad o completar descarga. | Existe una recuperación que vacía espejos antes de saber si podrá reconstruirlos. Puede competir con sync automático. El outbox está excluido, lo cual debe preservarse. |

### 2.1 Verificación ejecutada y sus límites

Desde `clients/field-app`, `npm test -- --runInBand` reportó **35 suites aprobadas,
234 pruebas aprobadas y 6 omitidas**. Jest quedó abierto con el aviso de operaciones
asíncronas pendientes; se interrumpió después del resumen, con salida 130. No se
presenta esta ejecución como un comando finalizado limpiamente.

Las seis omisiones están en `MilkingScreen.test.tsx` y `AnimalEditScreen.test.tsx`.
Las pruebas del motor usan WatermelonDB con **LokiJS y API falsa**; la aplicación usa
SQLite nativo (`clients/field-app/src/database/index.ts:30`). Las pruebas de tombstones y cursor del
backend existen en `tests/Hato.Sync.IntegrationTests`, pero no se ejecutaron en esta
investigación. Tampoco se ejecutó la suite backend completa.

Se prepararon dos diagnósticos temporales fuera del repositorio, usando el motor y
logger reales, con la misma configuración Babel y fixtures de `syncEngine.test.ts`:

```text
./node_modules/.bin/jest --config /tmp/hato-spec-investigation/jest.config.json \
  --runInBand --testNamePattern 'investigation 2026-09-07' --silent --forceExit
Resultado: 2 passed; 22 skipped por filtro de nombre; salida 0.
```

Son pruebas que afirman el comportamiento defectuoso observado, no regresiones de
una solución. Para reproducirlas sin los archivos temporales: (a) responder al pull
siempre con cursor nuevo, `hasMore: true` y colecciones vacías; comprobar que tras 200
llamadas el resultado es `ok: true`; (b) hacer `localStorage` ausente, registrar un
error en un `LoggerService` nuevo y construir otro: sus logs están vacíos.

## 3. Decisiones fijadas para la propuesta

- **D1 — Conservar el protocolo incremental y los UUID.** No sustituirlo por descargas
  completas rutinarias, ni limpiar el dispositivo para ocultar divergencias.
- **D2 — Separar envío, recepción y actualización visible.** «Guardado en el teléfono»,
  «pendiente de enviar», «rechazado» y «datos actualizados» representan hechos distintos.
- **D3 — Ningún éxito falso.** `hasMore`, errores de aplicación, rechazos y operaciones
  pendientes impiden afirmar que todo quedó actualizado.
- **D4 — Conservar el resultado real de cada operación al reintentar.** Una reserva o
  rechazo previo no equivale a escritura aceptada. El protocolo concreto para recuperar
  reservas interrumpidas exige ADR antes de implementación si modifica ADR-0008.
- **D5 — Diagnóstico persistente en el teléfono.** Usar almacenamiento nativo disponible;
  ninguna dependencia nueva queda aprobada por este spec.
- **D6 — La confirmación de plausibilidad del ordeño se persiste.** El precedente ya
  existe en el propio código: `TreatmentCourseApplication` guarda `IsPlausibilityConfirmed`
  con su migración (`20260809165838_AddTreatmentApplicationPlausibilityConfirmed`). Que el
  operador viera la advertencia de volumen y confirmara igual es un hecho auditable. El
  arreglo declara el campo en `RecordMilkingSessionCommand`, lo persiste en la sesión de
  ordeño y añade migración EF (regla 7). Aceptar el campo y descartarlo en silencio queda
  descartado: equivaldría a eliminar la confirmación del operador, que D3 de
  [0005](../feature-0005-field-app-activity-validation/spec.md) prohíbe.
- **D7 — La deriva de contratos se vigila para los diez tipos de operación, no para uno.**
  Los payloads de prueba no se escriben a mano en el backend, porque volverían a divergir
  del cliente. Se generan desde los servicios móviles reales: una prueba en
  `clients/field-app` ejercita cada servicio, captura lo que encola en el outbox y lo
  escribe en un fixture versionado; una prueba de integración lee ese mismo fixture y
  afirma que cada payload deserializa contra su comando con `Disallow` activo. Se añade
  comprobación de completitud en ambos sentidos: todo `case` del despachador tiene fixture
  y todo `enqueue` del cliente tiene `case`. Así se detectan las cuatro derivas posibles
  —campo nuevo en el cliente, campo eliminado en el servidor, tipo sin ruta y ruta sin
  cobertura— sin depender de que alguien recuerde escribir la prueba.

## 4. Alcance y comportamiento requerido

Incluye coordinación de sync automático/manual, reintentos mientras la app está activa,
actualización de las vistas, borrados lógicos, recuperación segura, compatibilidad de
payloads, resultado idempotente y diagnóstico. No promete ejecución continua con el
sistema operativo suspendiendo la app ni incorpora otro motor de sincronización.

La BD confirmada es la fuente de las vistas. Tras aplicar un cambio, la pantalla abierta
debe reflejarlo sin reiniciar, cambiar pestaña ni pulsar un segundo botón. Si desaparece
el animal seleccionado, se informa y se impide enviar contra una selección obsoleta;
el formulario ya escrito no se descarta silenciosamente.

Debe existir una sola ejecución coordinada, compartida por sus disparadores. La app
reintenta errores transitorios con backoff acotado; recupera al volver a primer plano
y al recuperar conexión. Un rechazo de negocio no entra en un bucle de reintentos.
El cursor solo avanza después de aplicar la página correspondiente; una interrupción
admite replay idempotente. El límite de páginas expresa trabajo pendiente y continuidad.

El payload producido por cada servicio móvil debe probarse contra el servidor real,
incluidos campos opcionales y confirmaciones de plausibilidad. Se conserva el rechazo
explícito de contratos incompatibles. La confirmación del operador no se elimina para
hacer pasar una prueba; si requiere persistencia nueva, se define ADR/migración primero.

La recuperación manual conserva outbox, formularios pendientes y registros exclusivamente
locales. No vacía el único catálogo utilizable sin conexión. Se excluye mutuamente con
sync y debe tolerar corte de red o cierre de app sin pérdida del trabajo local.

El diagnóstico registra fecha UTC, versión de app y esquema, identificador de intento,
etapa fallida, colección, conteos y operación correlacionable. Debe sobrevivir reinicios,
tener retención acotada y permitir al usuario compartirlo deliberadamente. No registra
JWT, contraseñas ni payloads completos. No se envía información a terceros automáticamente.

## 5. Riesgos, dependencias y preguntas abiertas

- Falta confirmar build de teléfonos/backend y si «eliminar» significa baja, borrado
  lógico, desactivación o SQL manual. La ausencia de una fila en un delta no prueba borrado.
- Compatibilidad con outboxes y esquemas de las versiones instaladas es obligatoria;
  no se fuerza reinstalación como mecanismo de migración.
- Una reserva persistida antes de una escritura en otro módulo necesita comprobar sus
  efectos antes de reejecutar. No asumir atomicidad entre todos los DbContext existentes.
- Los permisos y su efecto sobre operaciones pendientes se especifican en
  [feature-0008](../feature-0008-people-permission-enforcement/spec.md).
- La validación de animales, los aretes y la interacción se especifican separadamente
  en [0005](../feature-0005-field-app-activity-validation/spec.md),
  [0007](../feature-0007-field-app-individual-tagged-livestock/spec.md) y
  [0006](../feature-0006-field-app-interaction-reliability/spec.md).

## 6. Criterios de aceptación

1. Dos dispositivos convergen tras registro offline, edición en web y borrado lógico,
   tanto en SQLite como en la pantalla abierta; ningún reinicio es necesario.
2. Un cambio de arete, membresía o módulo llega y se muestra; una baja deja de ofrecerse
   para nuevas actividades según 0005, conservando su historial.
3. Corte de red antes/después del commit o del cursor permite continuar sin pérdida,
   duplicados ni volver a mostrar una entidad borrada por un replay antiguo.
4. Rechazo con respuesta perdida conserva el rechazo al reintentar; una reserva pendiente
   nunca figura como aceptada sin comprobar que se materializó el efecto.
5. Más de 200 páginas y errores parciales no muestran «todo actualizado»; los disparadores
   concurrentes y la recuperación no compiten entre sí.
6. Fallo transitorio con red estable vuelve a intentarse mientras la app está activa;
   caducidad de sesión informa cómo recuperarla y conserva los registros locales.
7. El ordeño real producido por `MilkingService`, con confirmación `true` y `false`, se
   procesa o rechaza por una regla de negocio explícita, no por deriva del contrato JSON.
   La confirmación queda persistida y consultable, no solo aceptada.
8. Los diez tipos de operación de push tienen cobertura de contrato alimentada por los
   payloads que generan los servicios móviles. Añadir un campo en el cliente sin
   declararlo en el servidor, o al revés, hace fallar la suite antes de distribuir la app.
9. Una operación rechazada por el servidor no puede aparecer como enviada tras un
   reintento: el resultado `Duplicate` que arrastra `errorDetails` conserva el rechazo.
10. Un error se puede consultar después de cerrar y abrir la app sin exponer secretos.
11. Las regresiones cubren API/PostgreSQL real, servicios móviles y contenedor de pantallas.
    La aceptación en campo exige además SQLite nativo y las versiones instaladas.

