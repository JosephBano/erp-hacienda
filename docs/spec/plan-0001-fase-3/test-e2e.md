# test-e2e.md — Verificación manual de sincronización (archivado)

> **Documento archivado.** Estos son los 10 escenarios obligatorios de
> [`spec.md` sec. 2.2](./spec.md#22-exigencias-especiales-de-la-fase-3-sincronización)
> convertidos en guion ejecutable, tal como se corrieron para cerrar (y luego reforzar,
> ver `spec.md` sec. 4) la Fase 3. Se conservan como referencia de regresión manual, no
> como trabajo pendiente. Marcado según la regla de `docs/DOCUMENTACION.md` sec. 4.
>
> La versión automatizada de estos mismos 10 escenarios vive en
> `Hato.Modules.Sync.IntegrationTests` (backend) y en los archivos citados desde
> `spec.md` sec. 2.2 en `clients/field-app`. Este documento es la versión que un humano
> puede correr contra un entorno real con `curl` y un dispositivo, cuando la sospecha es
> que la suite automatizada no está ejercitando el camino real.

**Antes de empezar:**

- Backend levantado (`docker compose up -d`, migraciones al día) y un usuario de prueba
  con rol de registrador.
- Cliente HTTP (`curl` o equivalente) contra `POST /api/v1/sync/push` y
  `GET /api/v1/sync/pull`.
- Para SYNC-9 y SYNC-10 (token/red): capacidad de expirar un token de prueba y de cortar
  la conexión a mitad de una llamada (proxy o modo avión en el móvil).

---

## `SYNC-1` — Push idempotente

**Pasos:**
1. Enviar la misma operación (mismo `clientOperationId`) tres veces seguidas a
   `POST /api/v1/sync/push`.

**Debe pasar:**
- Se crea exactamente un registro.
- Las tres respuestas son equivalentes (la 2ª y 3ª devuelven el resultado ya persistido,
  marcado como duplicado, no un error).

---

## `SYNC-2` — Push parcialmente fallido

**Pasos:**
1. Enviar un lote de 10 operaciones donde la operación #4 es inválida (p. ej. referencia a
   un animal inexistente).

**Debe pasar:**
- Las 9 operaciones válidas persisten.
- La #4 vuelve con un error tipado (Problem Details), no con un 500 genérico.
- Reenviar el lote completo (incluida la #4 corregida o no) no duplica ninguna de las 9
  ya aceptadas.

---

## `SYNC-3` — Push desordenado

**Pasos:**
1. Enviar en el mismo lote un evento hijo (p. ej. un pesaje de una cría) antes que el
   evento padre que la crea (el parto).

**Debe pasar:**
- El servidor resuelve el orden o encola el hijo hasta que el padre exista.
- Ningún evento se pierde ni queda huérfano de forma silenciosa.

---

## `SYNC-4` — Pull incremental en el borde del cursor

**Pasos:**
1. Provocar dos escrituras en el mismo milisegundo (o lo más cerca posible) en el
   backend.
2. Hacer `pull` con `since` apuntando exactamente a ese instante.

**Debe pasar:**
- Ningún registro se pierde.
- Ningún registro se repite en el `pull` siguiente con el cursor devuelto.

---

## `SYNC-5` — Pull con tombstones

**Pasos:**
1. Borrar lógicamente una entidad sincronizable en el servidor.
2. Hacer `pull` desde un cliente que ya tenía esa entidad.

**Debe pasar:**
- La entidad desaparece de la base local del cliente tras aplicar el `pull`.
- El borrado no revive en un `pull` posterior.

---

## `SYNC-6` — Reloj del dispositivo desfasado

**Pasos:**
1. Simular un cliente con el reloj adelantado o atrasado ±2 días.
2. Registrar un evento y sincronizarlo.

**Debe pasar:**
- El servidor ordena por su propio reloj de recepción, no por el del cliente.
- El `occurred_at` declarado por el cliente se conserva tal cual, como dato, no como
  criterio de orden.

---

## `SYNC-7` — Lote grande

**Pasos:**
1. Enviar un push de 500 operaciones en un solo lote.

**Debe pasar:**
- No hay timeout.
- El lote es transaccional: si algo falla a nivel de lote (no de operación individual), no
  queda un estado a medias.

---

## `SYNC-8` — Conflicto LWW en entidad editable

**Pasos:**
1. Dos dispositivos (o dos sesiones) editan el mismo campo del mismo animal mientras
   ambos están offline.
2. Sincronizar ambos.

**Debe pasar:**
- Se aplica *last-write-wins* según la marca de tiempo del servidor.
- Queda una entrada en la bitácora de conflictos (`GET /api/v1/sync/conflicts`), visible
  desde `admin-web` y desde la pantalla de sincronización de `field-app`.

---

## `SYNC-9` — Token expirado a mitad del push

**Pasos:**
1. Iniciar un push de varias operaciones con un token que expira antes de que termine el
   lote.

**Debe pasar:**
- El cliente refresca el token y reintenta.
- Ninguna operación ya aceptada antes de la expiración se duplica en el reintento.

---

## `SYNC-10` — Corte de red a mitad de push

**Pasos:**
1. Iniciar un push de un lote y cortar la conexión a mitad de la respuesta del servidor
   (el servidor puede haber procesado el lote sin que el cliente lo sepa).
2. Restaurar la red y dejar que el cliente reintente.

**Debe pasar:**
- El cliente reintenta automáticamente al recuperar conectividad.
- El estado converge: ninguna operación queda duplicada ni perdida, sin importar si el
  servidor había terminado de procesar el lote antes del corte.

---

## `SYNC-11` — Convergencia end-to-end

**Preparación:** dos dispositivos simulados (`field-app` contra la API real en Docker),
ambos offline al empezar.

**Pasos:**
1. Registrar datos distintos en cada dispositivo mientras ambos están offline.
2. Sincronizar ambos, en cualquier orden.

**Debe pasar:**
- Los dos dispositivos terminan con estado idéntico.
- Nada de lo registrado en cada uno se pierde en el otro.

---

## Cierre de la verificación

Estos 11 escenarios (los 10 obligatorios de `spec.md` sec. 2.2 más la prueba de
convergencia) son el criterio de aceptación 2 y 3 de `spec.md` sec. 7. Su versión
automatizada corre en `Hato.Modules.Sync.IntegrationTests` en cada PR; esta versión manual
se reserva para cuando haga falta verificar el camino real de un dispositivo, no solo el
contrato de la API.
