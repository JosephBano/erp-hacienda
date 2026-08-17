# test-e2e.md — Verificación manual extremo a extremo

> Escenarios que se ejecutan **sobre un dispositivo real** (o emulador con base persistente)
> contra una instancia del servidor con datos, después de que la suite automatizada esté en
> verde. Las pruebas unitarias y de integración prueban las piezas; esto prueba que el
> empleado puede hacer su trabajo.
>
> Cada escenario dice cómo prepararlo, qué hacer y qué debe pasar. Un escenario que falla se
> reporta con el paso exacto donde falló, no como "no anda".

**Antes de empezar:**

- Servidor corriendo con la rama mergeada localmente.
- Un usuario de campo con permisos `livestock.animals.read` y `breeding.events.read`.
- Un usuario de campo **sin** `breeding.events.read`, para E2E-7.
### Estado real de la base al 2026-08-16

Verificado por consulta directa. Sirve para saber qué escenarios ya son ejecutables y
cuáles exigen preparar datos primero.

| Dato | Valor |
|---|---|
| Animales | 67 (45 hembras, 22 machos), ninguno borrado |
| Identificadores vigentes | 14, todos de tipo `Name`. **Ningún `FarmTag`** |
| Animales sin ningún identificador | 53 |
| Preñeces | 6, **todas `Active`**, las 6 con servicio asociado |
| Servicios de monta | 6, **todos `ArtificialInsemination`** con pajuela. Ninguno natural |
| Partos registrados | 3, **los 3 con `pregnancy_id` nulo** |
| Lotes | 8 (`A1`–`A4`, `B1`–`B4`), todos `Headcount` |

Las seis preñadas y su fecha probable de parto:

| Madre | Fecha probable |
|---|---|
| Alfonsina | 2026-08-19 |
| Muñeca | 2026-08-24 |
| Gringa | 2026-09-05 |
| Maya | 2026-09-06 |
| Carlota | 2026-10-08 |
| Mocha | 2026-11-20 |

**Lo que esto implica para las pruebas:**

- **E2E-2 es ejecutable tal cual**, y es una verificación fuerte: el paso 1 debe pasar de
  **45 hembras a exactamente 6**, en ese orden de fechas.
- **E2E-5 (monta natural) NO es ejecutable sin preparar datos.** No existe ni un servicio
  natural en la base. Hay que crear en admin-web un servicio de monta natural con semental
  registrado, y su preñez, antes de correrlo.
- **E2E-8 (sin preñeces) exige un entorno aparte** o completar las 6 preñeces existentes.
- Los 3 partos ya registrados tienen `pregnancy_id` nulo: se registraron contra madres sin
  preñez activa, que es justo lo que esta rama impide. No sirven como evidencia de E2E-3;
  ese escenario hay que provocarlo con una de las 6 preñadas.
- Ninguna de las 6 preñadas cae en el hallazgo 2.8 del spec (las 6 tienen nombre), así que
  el paso 1 mostrará etiquetas legibles.

### Datos a preparar

- Una hembra con preñez activa **por monta natural**, con semental registrado (para E2E-5).
- Una hembra **sin** preñez activa, para confirmar que no aparece (ya hay 39).
- Una hembra con preñez **completada**, para confirmar que no aparece (hoy no hay ninguna;
  la deja E2E-3 al ejecutarse).
- Un usuario de campo **sin** `breeding.events.read` (para E2E-7).

---

## E2E-1 — Datos huérfanos y "Rehacer descarga"

**Es el escenario que valida el reporte original del cliente (los `101`/`102`/`103`).
No se puede omitir.**

**Preparación:** un teléfono cuya base local traiga animales que ya no existen en Postgres.
Si no se tiene uno a mano, se reproduce: sincronizar el teléfono, borrar físicamente esos
animales de Postgres con `DELETE` (sin pasar por borrado lógico), y volver a sincronizar.

**Pasos:**
1. Abrir la app y entrar a cualquier lista de animales. Anotar los animales fantasma.
2. Sincronizar normalmente. Confirmar que **siguen ahí** — esto demuestra el hallazgo.
3. Registrar algo cualquiera para dejar el outbox con al menos una operación pendiente y
   **poner el teléfono en modo avión** para que no se envíe.
4. Anotar el número de registros sin enviar que muestra la pantalla.
5. Salir del modo avión. Ir a Sincronización → "Rehacer descarga". Leer la confirmación.
6. Confirmar y esperar a que termine.

**Debe pasar:**
- La confirmación dice en español llano qué se conserva y qué se vuelve a bajar.
- Tras terminar, los animales fantasma **desaparecieron**.
- El hato real está completo, no vacío.
- **El contador de registros sin enviar es el mismo del paso 4, o menor porque se enviaron.
  Nunca cero por pérdida.** Si se perdió una operación pendiente, el escenario falla y el
  commit 2 se devuelve.

---

## E2E-2 — Solo aparecen las preñadas

**Pasos:**
1. Sincronizar.
2. Inicio → "Un parto".

**Debe pasar:**
- Aparecen únicamente las hembras con preñez activa.
- La hembra sin preñez **no aparece**.
- La hembra con preñez completada **no aparece**.
- Cada fila muestra su fecha probable de parto.
- El orden es por fecha probable de parto, la más próxima primero.

---

## E2E-3 — La que ya parió desaparece sola

**Pasos:**
1. Registrar un parto completo de una de las preñadas (E2E-4).
2. Sincronizar.
3. Volver a entrar a "Un parto".

**Debe pasar:**
- Esa hembra **ya no está en la lista**, sin que nadie haya hecho nada en admin-web.
- Las demás preñadas siguen ahí.

---

## E2E-4 — Registrar un parto por inseminación artificial

**Pasos:**
1. Inicio → "Un parto" → elegir la hembra preñada por IA.
2. Leer el paso 2.
3. Avanzar al paso 3. Agregar dos crías, una de cada sexo. Ponerle arete a una y peso a la
   otra.
4. Avanzar al paso 4 y confirmar.

**Debe pasar:**
- El paso 2 muestra **`Padre: Inseminación artificial`**, sin lote de pajuela y **sin
  posibilidad de elegir**.
- En ningún paso hay lista de sementales.
- El paso 4 resume madre, padre y crías correctamente.
- Al confirmar vuelve a Inicio y el contador de registros sin enviar sube en uno.
- Tras sincronizar, el parto aparece en admin-web con el padre correcto según el servicio de
  IA registrado.

---

## E2E-5 — Registrar un parto por monta natural

**Pasos:** igual a E2E-4, con la hembra preñada por monta natural.

**Debe pasar:**
- El paso 2 muestra `Padre: <identificación del semental>`, de solo lectura.
- Tras sincronizar, el padre del parto en admin-web es ese animal, no una pajuela.

---

## E2E-6 — Camada grande (20 crías)

**Pasos:**
1. Entrar a un parto y llegar al paso 3.
2. Agregar 20 crías.
3. Desplazarse hasta el final de la lista y volver arriba.
4. Quitar la cría número 3.
5. Cambiarle el sexo a la número 15.
6. Retroceder al paso 2 y volver al paso 3.
7. Confirmar.

**Debe pasar:**
- El contador y los botones `+Hembra`/`+Macho` **siguen visibles en todo momento**, sin
  importar dónde esté el scroll.
- La lista se desplaza con soltura; nada queda cortado ni inalcanzable.
- Al quitar la número 3, las demás conservan su orden y su renumeración es correcta.
- **Al volver del paso 2, las 19 crías siguen cargadas con sus datos.** Perderlas es falla
  crítica.
- Al confirmar, el registro lleva 19 crías con el desglose correcto de sexos.

---

## E2E-7 — Empleado sin permiso de reproducción

**Preparación:** iniciar sesión con el usuario sin `breeding.events.read`.

**Pasos:**
1. Sincronizar.
2. Inicio → "Un parto".

**Debe pasar:**
- No se descargan preñeces ni servicios.
- La pantalla muestra el `EmptyState` explicando que no hay hembras con preñez activa
  confirmada, **no un error crudo ni una pantalla en blanco**.
- El resto de la app funciona con normalidad.

---

## E2E-8 — Sin preñeces registradas

**Preparación:** un servidor donde ninguna hembra tenga preñez activa.

**Pasos:** Inicio → "Un parto".

**Debe pasar:**
- `EmptyState` que explique que solo aparecen hembras con preñez activa confirmada y que la
  preñez se registra en el panel.
- El texto debe ser accionable: alguien que lo lee sabe qué hacer para desbloquearse.
- **No hay forma de registrar el parto de todas maneras** (bloqueo estricto, decisión D2).

---

## E2E-9 — Inicio en teléfono chico

**Preparación:** dispositivo o emulador de pantalla pequeña (~5"), con el módulo de
Producción activo para que aparezca el máximo de botones.

**Pasos:**
1. Abrir la app en Inicio.
2. Desplazarse hasta abajo.
3. Tocar el último botón de la lista.

**Debe pasar:**
- Todas las opciones son alcanzables por scroll.
- Nada queda cortado por el borde inferior.
- El orden de los sujetos es el mismo de antes de la rama.
- Los objetivos táctiles se mantienen cómodos (mínimo 64pt).

---

## E2E-10 — Migración de base existente

**Es la prueba que protege el trabajo no sincronizado del empleado durante la
actualización.**

**Preparación:** un teléfono con la versión **anterior** de la app instalada, base local
poblada y **al menos una operación pendiente en el outbox**.

**Pasos:**
1. Anotar el número de registros sin enviar.
2. Instalar encima la versión de esta rama, **sin desinstalar**.
3. Abrir la app.

**Debe pasar:**
- La app abre sin error de base de datos.
- **El contador de registros sin enviar es idéntico al del paso 1.**
- El hato local sigue completo.
- Tras sincronizar, las preñeces bajan y el paso 1 del parto se puebla.

---

## E2E-11 — Registro offline completo

**Preparación:** teléfono sincronizado, luego en modo avión.

**Pasos:**
1. En modo avión, registrar un parto completo de principio a fin.
2. Verificar que el contador de pendientes sube.
3. Salir del modo avión y sincronizar.

**Debe pasar:**
- **El flujo completo funciona sin red** (regla dura 10): la lista de preñadas, el padre y
  las crías salen todos de datos locales.
- Al recuperar la señal, el parto se envía y aparece en admin-web con el padre correcto.

---

## Cierre de la verificación

Los once escenarios en verde **no cierran la rama por sí solos**. También hacen falta:

- `dotnet test` y `npm test` en `clients/field-app`, la suite completa, en verde
  ([`tasks.md`](./tasks.md) TC.1 y TC.2).
- Los nueve criterios de aceptación de [`spec.md`](./spec.md) sec. 10, verificados uno por
  uno (TC.4).

**Registro de ejecución:**

| Escenario | Fecha | Dispositivo | Resultado | Notas |
|---|---|---|---|---|
| E2E-1 | | | | |
| E2E-2 | | | | |
| E2E-3 | | | | |
| E2E-4 | | | | |
| E2E-5 | | | | |
| E2E-6 | | | | |
| E2E-7 | | | | |
| E2E-8 | | | | |
| E2E-9 | | | | |
| E2E-10 | | | | |
| E2E-11 | | | | |
