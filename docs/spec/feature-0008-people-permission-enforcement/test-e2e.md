# test-e2e.md — Verificación manual extremo a extremo

> Escenarios que se ejecutan contra una instancia del servidor con datos y PostgreSQL real,
> después de que la suite automatizada esté en verde. La mayor parte se verifica con una
> herramienta de llamadas HTTP (curl o similar): el objetivo es comprobar el servidor **sin
> pasar por la app**, que es exactamente el camino que hoy no está protegido.
>
> Cada escenario dice cómo prepararlo, qué hacer y qué debe pasar. Un escenario que falla se
> reporta con el paso exacto donde falló, no como "no anda".

**Antes de empezar:**

- Servidor corriendo con la rama mergeada localmente, contra PostgreSQL real.
- **Cuatro identidades** (criterio 1):
  - `admin` — con `people.users.manage`.
  - `empleado-ok` — con permisos de campo, **sin** `people.users.manage`.
  - `empleado-limitado` — autenticado, **sin** permisos de escritura de animales ni ordeño.
  - `anónimo` — sin token.
- Un dispositivo de campo con la app, para E2E-5 y E2E-6.
- Una copia del estado de dominio antes de empezar, para comprobar que las denegaciones no
  dejaron rastro.

---

## E2E-1 — La matriz normativa, fila por fila

**Pasos:** para **cada fila** de la matriz de [`spec.md` sec. 4](./spec.md#4-matriz-normativa),
llamar a la ruta REST correspondiente con las cuatro identidades.

**Debe pasar:**
- `anónimo` → **401** en todas.
- `empleado-limitado` → **403** en las que exigen permiso que no tiene.
- `empleado-ok` y `admin` → éxito en las que les corresponden.
- Los errores usan Problem Details (RFC 7807), como manda la convención de API.
- **Ninguna fila queda sin probar.** Una fila sin prueba es un hueco, no un detalle.

## E2E-2 — El push exige lo mismo que REST

> El hueco de mayor consecuencia. Añadir políticas solo a REST no habría protegido esto.

**Pasos:**
1. Con `empleado-limitado`, empujar por `/sync` una operación de escritura de animal.
2. Repetir con una operación de ordeño.
3. Repetir con cada tipo de operación de la matriz.
4. Inventar un `operationType` que no exista en el mapa y empujarlo.

**Debe pasar:**
- Cada operación se rechaza con motivo individual legible.
- **El estado de dominio no cambió**: comparar contra la copia previa.
- El paso 4 **se rechaza**; no pasa por omisión.
- `anónimo` recibe 401 en el push, no 403.

## E2E-3 — Push mixto: lo válido pasa, lo denegado no

**Pasos:**
1. Construir un lote con tres operaciones: dos que `empleado-ok` puede hacer y una que no.
2. Empujarlo con `empleado-ok`.

**Debe pasar:**
- Las dos autorizadas se procesan y quedan registradas.
- La denegada se rechaza con **su motivo individual**.
- La denegación **no impide** las otras dos.
- La denegada no produjo ningún efecto de dominio parcial ni reserva.

## E2E-4 — El pull no se puede forzar

**Pasos:**
1. Con `empleado-limitado`, hacer un pull normal y anotar qué colecciones llegan.
2. Pedir **explícitamente** una colección que no le corresponde.

**Debe pasar:**
- El paso 2 **no** entrega la colección.
- Pedirla explícitamente no evita el filtro (criterio 3).
- El filtro que ya existía sigue funcionando: esta rama lo refuerza, no lo debilita.

## E2E-5 — Cada quien ve sus operaciones

**Pasos:**
1. Con `empleado-ok`, registrar algunas operaciones desde el dispositivo y sincronizar.
2. Con `admin`, registrar otras.
3. Consultar `/sync/operations` con `empleado-ok`.
4. Consultarlo con `admin`.
5. Abrir en la app la pantalla que muestra operaciones.

**Debe pasar:**
- El paso 3 devuelve **solo** las de `empleado-ok`.
- El paso 4 devuelve las globales, por tener `people.users.manage`.
- El paso 5: **la pantalla no se rompe** por recibir menos filas de las que recibía antes.

## E2E-6 — Lo denegado se conserva en el teléfono

**Pasos:**
1. Con `empleado-limitado`, registrar offline una operación que no tiene permiso de hacer.
2. Sincronizar.
3. Leer lo que muestra la app.
4. Forzar un reintento.
5. Refrescar los permisos del dispositivo y volver a intentar.

**Debe pasar:**
- La operación se muestra **denegada**, con motivo legible.
- Su **contenido y su autoría siguen intactos** (regla dura 10).
- Los pasos 4 y 5 **no** la convierten en aceptada (criterio 5).
- El empleado entiende qué pasó sin abrir una herramienta técnica.

## E2E-7 — Consumo de alimento con el permiso mínimo

> Solo ejecutable tras el commit 5. Es la razón por la que el permiso existe.

**Preparación:** un usuario con el permiso nuevo de consumo y **sin** `inventory.items.manage`.

**Pasos:**
1. Registrar un consumo de alimento por REST.
2. Registrarlo por push.
3. Intentar administrar el catálogo de inventario con ese mismo usuario.

**Debe pasar:**
- Los pasos 1 y 2 funcionan.
- El paso 3 devuelve **403**: alimentar animales no da administración de catálogo.
- Ningún usuario recibió el permiso de forma indiscriminada (D4).

## E2E-8 — Revocación

> El comportamiento exacto depende de la Compuerta 0, decisión B. Este escenario **verifica lo
> que se decidió**, no un comportamiento asumido.

**Pasos:**
1. Con `empleado-ok` autenticado y trabajando, revocar uno de sus permisos.
2. Intentar una escritura online que requiera ese permiso.
3. Poner el teléfono en modo avión, registrar algo que requiera ese permiso, y sincronizar
   después.

**Debe pasar:**
- Lo que la decisión B haya definido, verificado paso a paso.
- **No se promete revocación instantánea en un teléfono sin red**: el escenario comprueba que
  el comportamiento real coincide con lo documentado, no con una expectativa optimista.
- Lo registrado offline **no se pierde**: se conserva y se explica.

## E2E-9 — Operaciones pendientes de versiones anteriores

**Preparación:** un dispositivo con operaciones pendientes creadas antes de esta rama.

**Pasos:**
1. Sincronizar tras el cierre de permisos.
2. Revisar lo que quedó.

**Debe pasar:**
- Las que ahora resultan no autorizadas quedan **revisables y atribuibles**.
- **Ninguna fue borrada** por la actualización (spec sec. 5, regla dura 1).
- Se puede saber quién las creó y qué contenían.

---

## Cierre de la verificación

Estos escenarios en verde **no cierran la rama por sí solos**. Faltan:

- Los criterios numerados de [`spec.md` sec. 6](./spec.md#6-criterios-de-aceptación), 1 a 6.
- La suite automatizada completa contra PostgreSQL real ([`tasks.md`](./tasks.md) TC.1).
- El resultado de la Compuerta 0 registrado en el PR.
- La revisión de que **los comentarios de seguridad describen el código real**
  ([`tasks.md`](./tasks.md) TC.5, criterio 6).
- `docs/SEGURIDAD.md` actualizado con lo que se cierra y lo que sigue abierto.
