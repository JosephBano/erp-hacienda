# test-e2e.md — Verificación manual extremo a extremo

> Escenarios que se ejecutan **sobre un dispositivo real** (o emulador con base persistente
> y SQLite nativo) contra una instancia del servidor con datos, después de que la suite
> automatizada esté en verde. Las pruebas del motor corren sobre LokiJS con API falsa
> (`spec.md` sec. 2.1): no bastan para aceptar en campo.
>
> Cada escenario dice cómo prepararlo, qué hacer y qué debe pasar. Un escenario que falla se
> reporta con el paso exacto donde falló, no como "no anda".

**Antes de empezar:**

- Servidor corriendo con la rama mergeada localmente, contra PostgreSQL real.
- Dos dispositivos, A y B, con la app de la rama instalada. Anotar modelo, SO y build de cada uno.
- Un usuario de campo con permisos de lectura y escritura de animales y de ordeño.
- Al menos un animal hembra de especie ordeñable, con datos suficientes para registrar un ordeño.
- **Un tercer dispositivo, C, con la versión de campo instalada hoy y registros pendientes
  en su outbox.** Es el único que puede verificar E2E-9; no se reinstala ni se limpia.

---

## E2E-1 — El ordeño llega al servidor

> El defecto principal de `spec.md` sec. 1.1. Si este escenario falla, nada más importa.

**Pasos:**
1. En el dispositivo A, sin conexión, registrar un ordeño individual con un volumen normal.
2. Registrar un segundo ordeño con un volumen fuera de rango y **confirmar** la advertencia.
3. Recuperar conexión y esperar la sincronización.

**Debe pasar:**
- Ambos ordeños quedan aceptados, no rechazados.
- En el servidor, el primero tiene la confirmación en `false` y el segundo en `true`.
- Ninguno produjo un error de payload ilegible.

## E2E-2 — Convergencia de dos dispositivos

**Preparación:** A y B sincronizados y mostrando el mismo hato.

**Pasos:**
1. En A, sin conexión, registrar una actividad sobre un animal.
2. En la web, editar ese animal y aplicar un borrado lógico a otro distinto.
3. Sincronizar A y luego B, con la pantalla del hato abierta en ambos.

**Debe pasar:**
- Ambos dispositivos convergen: mismo contenido en SQLite y en la pantalla abierta.
- El cambio se ve **sin reiniciar la app, sin cambiar de pestaña y sin pulsar un segundo botón**.
- El animal con borrado lógico deja de ofrecerse; su historial no desaparece.
- Ningún registro local se perdió ni se duplicó.

## E2E-3 — Corte de red en el peor momento

**Pasos:**
1. En A, encolar varias operaciones sin conexión.
2. Recuperar conexión y cortarla a mitad del envío, antes de que el servidor responda.
3. Restaurar conexión y dejar que reintente.
4. Repetir cortando durante la descarga, después de aplicar una página y antes de guardar el cursor.

**Debe pasar:**
- Ninguna operación se pierde ni se duplica.
- Un replay antiguo no vuelve a mostrar una entidad ya borrada.
- El envío continúa desde donde quedó, sin reiniciar la descarga completa.

## E2E-4 — Un rechazo sigue siendo un rechazo

**Preparación:** provocar una operación que el servidor rechace por regla de negocio.

**Pasos:**
1. En A, registrar esa operación sin conexión.
2. Sincronizar y confirmar que aparece como rechazada, con motivo legible.
3. Forzar un reintento de la misma operación.

**Debe pasar:**
- Sigue mostrándose como **rechazada**, con el motivo original.
- **No** aparece como enviada ni como sincronizada.
- Su contenido sigue conservado y consultable.

## E2E-5 — Descarga larga y errores parciales

**Preparación:** volumen de datos suficiente para superar el presupuesto de páginas del pull,
o un servidor de prueba que responda `hasMore: true` de forma sostenida.

**Pasos:**
1. Sincronizar en A.
2. Observar el estado que muestra la pantalla de sincronización.

**Debe pasar:**
- **No** dice «todo actualizado».
- Expresa que queda trabajo pendiente y permite continuar.
- Lanzar sincronización manual mientras la automática corre no produce dos ejecuciones
  compitiendo, ni un estado contradictorio.

## E2E-6 — Reintento con red estable

**Pasos:**
1. Provocar un error transitorio del servidor con el teléfono conectado y la app abierta.
2. **No** tocar nada. Esperar.
3. Enviar la app a segundo plano y traerla de vuelta.

**Debe pasar:**
- El motor vuelve a intentar por sí solo mientras la app está activa; el empleado no tiene
  que pulsar nada.
- La vuelta a primer plano también dispara un intento.
- Una operación rechazada por negocio **no** entra en ese bucle.

## E2E-7 — Recuperación manual sin daño

**Preparación:** dispositivo A con registros pendientes en el outbox y un formulario a medio llenar.

**Pasos:**
1. Ejecutar «rehacer descarga».
2. Cortar la red a mitad de la reconstrucción.
3. Cerrar la app por completo y volver a abrirla.

**Debe pasar:**
- El outbox sobrevive intacto: los registros pendientes siguen ahí (regla dura 10).
- El formulario a medio llenar no se perdió sin aviso.
- El dispositivo no queda sin catálogo utilizable sin conexión.
- La recuperación y la sincronización no corren a la vez.

## E2E-8 — El diagnóstico sobrevive y no filtra

**Pasos:**
1. Provocar un error de sincronización en A.
2. Cerrar la app por completo y volver a abrirla.
3. Abrir el diagnóstico y leerlo entero.
4. Usar la acción de compartir.

**Debe pasar:**
- El error registrado antes del reinicio sigue consultable.
- Cada entrada trae fecha UTC, versión de app y esquema, etapa fallida y colección.
- **No** aparecen JWT, contraseñas ni payloads completos.
- Nada se envió a ningún destino sin que el usuario lo pidiera explícitamente.

## E2E-9 — Actualización desde la versión instalada

> El único escenario que no se puede simular. Exige el dispositivo C, con la app de campo
> actual y registros pendientes reales.

**Pasos:**
1. Anotar cuántas operaciones pendientes tiene C y cuáles son.
2. Actualizar C a la build de esta rama **sin desinstalar**.
3. Abrir la app y sincronizar.

**Debe pasar:**
- Las operaciones pendientes siguen siendo las mismas, con su contenido y su autoría.
- Se envían correctamente; ninguna se pierde en la migración de esquema local.
- La app no pide reinstalar ni «empezar limpio».
- Ninguna operación creada con la versión anterior aparece como aceptada sin haberlo sido.

---

## Cierre de la verificación

Estos escenarios en verde **no cierran la rama por sí solos**. Faltan:

- Los criterios numerados de [`spec.md` sec. 6](./spec.md#6-criterios-de-aceptación), 1 a 11.
- La suite automatizada completa: `dotnet test` contra PostgreSQL real y `npm test` en
  `clients/field-app` ([`tasks.md`](./tasks.md) TC.1 y TC.2).
- El resultado de la Compuerta 0 registrado en el PR.

**Se anota siempre:** modelo, SO y build de cada dispositivo usado. Un escenario aprobado sin
esa anotación no es evidencia de campo (spec sec. 6, criterio 11).
