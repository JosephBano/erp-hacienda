# test-e2e.md — Verificación manual extremo a extremo

> Escenarios que se ejecutan **sobre un dispositivo real** (o emulador con base persistente y
> SQLite nativo) contra una instancia del servidor con datos, después de que la suite
> automatizada esté en verde. Las pruebas unitarias y de integración prueban las piezas; esto
> prueba que el empleado no puede registrar algo biológicamente imposible.
>
> Cada escenario dice cómo prepararlo, qué hacer y qué debe pasar. Un escenario que falla se
> reporta con el paso exacto donde falló, no como "no anda".

**Antes de empezar:**

- Servidor corriendo con la rama mergeada localmente, contra PostgreSQL real.
- Dos dispositivos, A y B, con la app de la rama. Anotar modelo, SO y build.
- Datos preparados: **un macho de especie ordeñable**, una hembra de especie ordeñable, un
  animal de especie **no** habilitada para ordeño, y una hembra con preñez activa.
- Una herramienta para llamar a la API directamente (curl o similar): varios escenarios
  verifican el servidor sin pasar por la app.

---

## E2E-1 — El macho no se puede ordeñar, por ninguna vía

> El caso concreto que el dueño reportó. Los tres pasos son el mismo requisito en tres capas.

**Pasos:**
1. En A, abrir el registro de ordeño y buscar al macho de especie ordeñable.
2. Llamar directamente al servicio local de ordeño con el id de ese macho.
3. Llamar directamente a la API REST de ordeño con ese mismo animal.
4. Empujar una operación `recordMilking` con ese animal por sync.

**Debe pasar:**
- Paso 1: **no aparece** como candidato.
- Pasos 2, 3 y 4: rechazo con **motivo legible**, no un error genérico ni un silencio.
- El macho sigue siendo consultable en su expediente, con su historial completo.

## E2E-2 — La hembra apta sí puede, offline

**Pasos:**
1. Poner A en modo avión.
2. Registrar un ordeño de la hembra de especie ordeñable.
3. Intentar registrar un ordeño del animal de especie no habilitada.
4. Restaurar conexión y sincronizar.

**Debe pasar:**
- El ordeño de la hembra se registra sin conexión y se acepta al sincronizar.
- La especie no habilitada no lo permite, con motivo legible.
- Habilitar esa especie **desde configuración** la vuelve ordeñable sin instalar otra build
  (art. 8): ninguna regla depende del nombre de la especie.

## E2E-3 — La baja deja de ofrecerse sin perder el expediente

**Preparación:** un animal con actividad registrada en su historial.

**Pasos:**
1. Dar de baja a ese animal desde la web.
2. Sincronizar A.
3. Buscar al animal en captura de una actividad.
4. Abrir su expediente.

**Debe pasar:**
- Ya no se ofrece para captura actual.
- Su expediente y su historial siguen completos y consultables.
- El estado de baja aparece con su fecha, no como un borrado.

## E2E-4 — Un hecho anterior a la baja sigue siendo válido

> D5: la cronología importa. Este escenario distingue «no apto hoy» de «nunca fue apto».

**Pasos:**
1. Con A sin conexión, registrar un pesaje con **fecha anterior** a la baja del animal de E2E-3.
2. Sincronizar.

**Debe pasar:**
- El registro recibe evaluación temporal correcta: no se rechaza solo porque el animal esté
  dado de baja hoy.
- Si el servidor detecta una contradicción, la muestra **sin borrar el hecho pendiente**.

## E2E-5 — El animal cambia mientras el formulario está abierto

**Pasos:**
1. En A, abrir un formulario de actividad sobre un animal y llenarlo **sin confirmar**.
2. Desde B o desde la web, cambiar su grupo y darlo de baja.
3. Sincronizar A con el formulario todavía abierto.
4. Pulsar confirmar.

**Debe pasar:**
- **No** se envía un registro contra la selección obsoleta.
- Se explica el impedimento con claridad.
- **Lo escrito sigue ahí**: peso, dosis y demás valores no se perdieron.

## E2E-6 — Parto: madre coherente con su preñez

**Pasos:**
1. En A, iniciar el registro de parto.
2. Revisar la lista de madres ofrecidas.
3. Completar un parto con la hembra de preñez activa.
4. Volver a iniciar un parto.

**Debe pasar:**
- Solo se ofrecen hembras con preñez activa; **ningún macho** aparece.
- La preñez ya completada en el paso 3 **no vuelve a ofrecerse**.
- El asistente conserva su secuencia de cuatro pasos: esta rama no lo rediseña.

## E2E-7 — Las demás actividades aceptan ambos sexos

**Pasos:**
1. Registrar un pesaje sobre un macho y sobre una hembra.
2. Registrar un tratamiento sobre cada uno.
3. Intentar un movimiento cuyo destino sea igual al origen.
4. Intentar una segunda baja sobre un animal ya dado de baja.
5. Intentar un pesaje con unidad inválida y con un número imposible.

**Debe pasar:**
- Pasos 1 y 2: ambos sexos se registran sin obstáculo. Las reglas de ordeño **no** se aplican aquí.
- Pasos 3, 4 y 5: rechazo con motivo legible, sin efectos parciales.
- Un valor raro pero posible sigue pidiendo confirmación en vez de bloquear (D4, ADR-0022).

## E2E-8 — Actualización desde la versión instalada

> El commit 1 sube `SCHEMA_VERSION`. Este escenario exige un dispositivo con la app de campo
> actual y registros pendientes reales; no se puede simular.

**Pasos:**
1. Anotar las operaciones pendientes del dispositivo.
2. Actualizar a la build de esta rama **sin desinstalar**.
3. Abrir y sincronizar.

**Debe pasar:**
- Las operaciones pendientes siguen ahí, con su contenido y su autoría.
- Los datos locales existentes sobrevivieron la migración.
- La app no pide reinstalar ni «empezar limpio».

---

## Cierre de la verificación

Estos escenarios en verde **no cierran la rama por sí solos**. Faltan:

- Los criterios numerados de [`spec.md` sec. 6](./spec.md#6-criterios-de-aceptación), 1 a 7.
- La suite automatizada completa ([`tasks.md`](./tasks.md) TC.1 y TC.2).
- El resultado de la Compuerta 0 registrado en el PR.
- La lista de registros ya inválidos entregada al dueño ([`tasks.md`](./tasks.md) TC.4),
  **para revisión, no para corrección automática**.

**Se anota siempre:** modelo, SO y build de cada dispositivo usado.
