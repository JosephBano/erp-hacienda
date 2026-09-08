# test-e2e.md — Verificación manual extremo a extremo

> Escenarios que se ejecutan **sobre la aplicación completa, con datos persistidos y servidor
> real**, después de que la suite automatizada esté en verde. Los defectos de scroll y de
> sincronización **no se cierran solo con un prototipo** (`spec.md` criterio 11).
>
> Cada escenario dice cómo prepararlo, qué hacer y qué debe pasar. Un escenario que falla se
> reporta con el paso exacto donde falló, no como "no anda".

**Antes de empezar:**

- Servidor corriendo con la rama mergeada localmente, contra PostgreSQL real, con las
  capacidades de 0004, 0005, 0007, 0008 y 0009 disponibles.
- **[0006](../feature-0006-field-app-interaction-reliability/spec.md) mergeado.** Sus criterios
  se reverifican aquí (criterio 10).
- Al menos un teléfono real usado por los empleados. Anotar modelo, SO y build.
- La matriz de `spec.md` sec. 4: áreas útiles de ~360 × 640 y ~412 × 915 unidades lógicas,
  texto al 100 % y 150 %, teclado abierto/cerrado, modo avión/conectado.
- Datos de estrés: 200 animales, 20 grupos, nombres de 100 caracteres, un parto de 20 crías,
  catálogo de tratamiento con 8 vías y 6 motivos. **Fixtures de UI, no cifras de la finca.**
- **Dos empleados de campo disponibles** para E2E-9. Sin ellos ese escenario no se puede cerrar.
- Compilación equivalente a producción. **No medir fluidez en build de desarrollo.**

---

## E2E-1 — Los cuatro destinos, de punta a punta

**Pasos:**
1. Desde Inicio, buscar un animal por su arete y abrir su ficha.
2. Registrar una actividad desde la ficha.
3. Ir a Actividad y encontrar lo que se acaba de registrar.
4. Recorrer Animales, Lotes y volver a Inicio.

**Debe pasar:**
- Los cuatro destinos son reconocibles **por texto y símbolo**.
- El estado de sincronización es accesible desde cualquiera de los cuatro.
- La navegación es consistente: se sabe siempre dónde se está y cómo volver.
- Ninguna ruta queda inaccesible; el atrás de Android y el de pantalla coinciden.

## E2E-2 — El acceso rápido y el de ficha llegan al mismo sitio

**Pasos:**
1. Iniciar un tratamiento **desde el acceso rápido de Inicio**.
2. Anotar qué formulario aparece y qué pide.
3. Iniciar un tratamiento **desde la ficha de un animal**.
4. Comparar.

**Debe pasar:**
- Ambos terminan en el **mismo formulario canónico**.
- El de la ficha **conserva el sujeto elegido**.
- El de Inicio **solicita un sujeto apto** para esa actividad.
- No hay dos formularios distintos ni dos conjuntos de reglas.

## E2E-3 — La ficha no inventa nada

**Preparación:** un animal **sin** período de retiro registrado, otro **con** retiro activo, y
uno dado de baja.

**Pasos:**
1. Abrir las tres fichas.
2. Leer qué dice cada una sobre retiro, baja y preñez.
3. Abrir la ficha de un grupo `Headcount` y otra de un grupo `Individual`.

**Debe pasar:**
- El animal sin datos de retiro **no dice «sano» ni «disponible»**: la ausencia de información
  se muestra como tal.
- Retiro, baja y preñez aparecen **solo cuando hay datos que los respaldan**.
- El grupo `Headcount` **no muestra datos que implicarían identidad individual**.
- **Ningún peso promedio, existencia, dosis o indicador fabricado** que el backend no dé.

## E2E-4 — El historial no duplica

**Pasos:**
1. Sin conexión, registrar dos actividades sobre un animal.
2. Abrir su ficha y observar el historial.
3. Sincronizar.
4. Volver a observar el historial.

**Debe pasar:**
- El paso 2 distingue **hechos confirmados de registros locales pendientes**.
- Tras el paso 3, cada registro aparece **una sola vez**: la confirmación del servidor **no lo
  duplica**.

## E2E-5 — Los siete estados se distinguen

**Pasos:** provocar cada situación de la tabla de `spec.md` sec. 3.6 y leer lo que muestra:
1. Registro guardado sin red.
2. Envío o descarga en curso.
3. Registro aceptado con descarga pendiente.
4. Operación rechazada.
5. Catálogo vacío, filtro sin coincidencias, y datos no descargados.
6. Sesión expirada.
7. Error de almacenamiento local.

**Debe pasar:**
- Los siete se distinguen entre sí y se entienden **sin vocabulario técnico**: nada de cursors,
  UUID ni nombres de tablas.
- El paso 1 **no se presenta como fracaso**: dice guardado y pendiente de enviar.
- El paso 5 diferencia los **tres** casos, no los junta en uno.
- El paso 7 **no afirma «guardado»** y **no sugiere borrar la app**.
- **Ningún estado de éxito oculta un defecto de 0004** (criterio 4).

## E2E-6 — Buscar por arete conserva el contexto

**Pasos:**
1. En Animales, buscar un texto, aplicar filtros por grupo y sexo, y desplazarse.
2. Abrir una ficha.
3. Volver.
4. Buscar un arete con ceros iniciales, por ejemplo `007`.
5. Buscar un valor de arete ambiguo.

**Debe pasar:**
- El paso 3 conserva **texto, filtros y posición**.
- El paso 4 **no pierde los ceros iniciales**.
- El paso 5 exige selección consciente, según
  [0007](../feature-0007-field-app-individual-tagged-livestock/spec.md).
- Un animal sin arete conserva identificación alternativa legible.

## E2E-7 — Parto, cría con arete y registro individual

> El recorrido completo del criterio 6. Depende de las capacidades de 0007.

**Pasos:**
1. Sin conexión, registrar un parto con varias crías, aretando al menos una.
2. Buscar esa cría por su arete.
3. Registrarle un pesaje.
4. Sincronizar.
5. Consultar el resultado en Actividad y en la ficha de la cría.

**Debe pasar:**
- El asistente de parto **conserva su secuencia de cuatro pasos**, con el lenguaje visual nuevo.
- El paso 2 la encuentra antes de sincronizar.
- Tras el paso 4, **identidad y contenido se preservan**.
- El paso 5 muestra con claridad qué se envió y qué falta.

## E2E-8 — Módulos y permisos no ocupan espacio inútil

**Preparación:** un usuario **sin** permiso para alguna actividad, y un módulo desactivado.

**Pasos:**
1. Abrir Inicio con ese usuario.
2. Abrir la ficha de un animal.
3. Buscar la actividad no autorizada.
4. Consultar el historial de esa misma actividad.

**Debe pasar:**
- **Un módulo desactivado no domina Inicio** (criterio 5).
- Las acciones no autorizadas **no ocupan accesos operativos**.
- **La consulta de historia permitida se mantiene** aunque la actividad no esté disponible.
- Llegar por una ruta antigua **no evita la validación** del servicio ni del servidor.

## E2E-9 — Dos empleados, sin ayuda

> El criterio 1. Es el escenario que decide si el rediseño sirve.

**Preparación:** dos empleados de campo que usen la app habitualmente. **Sin instrucciones
previas.**

**Pasos:**
1. Pedirle a cada uno que complete los flujos que usa normalmente.
2. Observar **sin intervenir**.
3. Anotar cada vez que dudan, se detienen o preguntan.

**Debe pasar:**
- Ambos encuentran **la acción final** de cada flujo sin ayuda.
- Se documentan las dificultades restantes **antes de aceptar** el rediseño.
- El vocabulario les resulta natural; si no, se corrige antes de cerrar.

## E2E-10 — Los criterios de 0006 siguen en pie

> El rediseño no puede reintroducir lo que 0006 arregló (criterio 10, D8).

**Pasos:** repetir sobre las pantallas **rediseñadas** los escenarios E2E-1 a E2E-5 de
[`0006/test-e2e.md`](../feature-0006-field-app-interaction-reliability/test-e2e.md), en toda
la matriz.

**Debe pasar:**
- **Ningún scroll atrapado.**
- **Ningún control tapado por el teclado.**
- Deslizar no registra; doble toque no duplica.
- Retroceder conserva lo escrito.
- Un refresco no cambia la selección, la posición ni el texto escrito.

## E2E-11 — Ambos temas, sol y poca luz

**Pasos:**
1. Recorrer los flujos principales en tema claro, **bajo el sol**.
2. Repetir en tema oscuro, **con poca luz**.
3. Alternar el tema en modo avión.
4. Poner el texto al 150 % y repetir.
5. Activar lector de pantalla y reducción de movimiento.

**Debe pasar:**
- Ambos temas se leen en sus condiciones.
- El paso 3 funciona **sin conexión**: ninguna variante la requiere.
- Los pasos 4 y 5: contraste, etiquetas accesibles, orden de foco y texto ampliado funcionan.
- Con reducción de movimiento, **ninguna animación es necesaria** para entender el estado.

## E2E-12 — Actualizar sin perder trabajo

> Exige un dispositivo con la versión de campo actual y **registros pendientes reales**.

**Pasos:**
1. Anotar las operaciones pendientes y sus referencias.
2. Actualizar a la build de esta rama **sin desinstalar**.
3. Abrir, sincronizar y revisar.
4. Empezar una captura y **reiniciar el teléfono a mitad**.

**Debe pasar:**
- Las operaciones pendientes y sus referencias siguen ahí.
- La app **no pide reinstalar ni «empezar limpio»**.
- El paso 4 **no presenta como guardado un dato no persistido** (criterio 9).
- Si el flujo viejo no guardaba borradores, la app **no promete recuperarlos**.

---

## Cierre de la verificación

Estos escenarios en verde **no cierran la rama por sí solos**. Faltan:

- Los criterios numerados de [`spec.md` sec. 5](./spec.md#5-criterios-de-aceptación), 1 a 11.
- `npm test` y `dotnet test` completos ([`tasks.md`](./tasks.md) TC.1 y TC.2).
- Los resultados de la Compuerta 0 y la Compuerta 1 registrados en el PR.
- La verificación de que **ningún defecto de 0004, 0005, 0007, 0008 o 0009 se declara resuelto
  por este PR** ([`tasks.md`](./tasks.md) TC.6, D7).

**Se anota siempre:** build, teléfono y observaciones de campo. **«Spec final» significa cierre
de la experiencia de esta serie; no declara terminada la Fase 3/3.5** mientras falte evidencia
(spec sec. 6).
