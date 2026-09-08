# plan.md — Ejecución de la rama `feature/field-app-redesign`

> **Qué es este documento.** Cómo se hace el trabajo que [`spec.md`](./spec.md) decidió:
> orden de los commits, qué archivos toca cada uno, qué pruebas exige y cómo se mergea.
> Desglose ejecutable en [`tasks.md`](./tasks.md), verificación en [`test-e2e.md`](./test-e2e.md).
> Las decisiones no se relitigan acá — si algo no cuadra, se corrige el spec primero.

**Objetivo:** una experiencia completa y coherente — entender dónde estoy, encontrar al animal
por arete, registrar una actividad con confianza y saber qué quedó guardado en el teléfono y
qué recibió el servidor.

**Enfoque:** una rama larga, **nueve commits secuenciales**, más **dos compuertas**. La
Compuerta 0 detiene el desarrollo de todas las pantallas hasta que se revisen cinco
representaciones concretas: rediseñar veinte pantallas sobre una dirección visual no validada
es la forma más cara de descubrir que no gustaba. El punto de no retorno es el commit 9, si
resulta necesaria una migración local.

**Spec:** [`spec.md`](./spec.md).

**Es el último de la serie.** Requiere [0006](../feature-0006-field-app-interaction-reliability/spec.md)
mergeado, e integra las capacidades de 0004, 0005, 0007, 0008 y 0009. Ver el
[índice](../README.md).

---

## Restricciones globales

- **Hereda los criterios de 0006** (D8). Rediseñar sobre pantallas que atrapan el scroll
  reproduciría el defecto con otra apariencia. Ningún commit puede reintroducir un scroll
  atrapado ni un control tapado por el teclado.
- **Cierre visual sobre capacidades comprobadas** (D7). Este spec gobierna cómo encontrar,
  entender y operar lo que los otros specs implementan. **No declara resueltos sus defectos
  por cambiar el mensaje o esconder el control.**
- **Respetar captura offline y lo ya escrito** (D2, regla dura 10). Retroceder conserva
  valores; abandonar con cambios advierte antes.
- **Sin dependencias nuevas** (D3, regla dura 2). Ni librería de animaciones, ni sustituir
  React Native. Una dependencia exige necesidad medida y ADR.
- **No se cambia el protocolo de sync ni el modelo de permisos por motivos visuales**
  (spec sec. 3.7).
- **Ningún dato inventado** (spec sec. 3.2). Ausencia de información no se traduce en «sano» o
  «disponible»; no se fabrican pesos promedio, existencias, dosis ni indicadores que el
  backend no proporciona.
- **Un sistema visual compartido** (D6). No se entrega una mezcla permanente de pantallas
  nuevas y formularios antiguos con convenciones diferentes.
- **Nada se marca completo** sin `npm test` en `clients/field-app` en verde, la suite entera.

---

## Índice

1. [Compuerta 0 — Revisión de las representaciones de diseño](#compuerta-0--revisión-de-las-representaciones-de-diseño)
2. [Commit 1 — Sistema visual](#commit-1--sistema-visual)
3. [Commit 2 — Los cuatro destinos](#commit-2--los-cuatro-destinos)
4. [Commit 3 — Estados y mensajes compartidos](#commit-3--estados-y-mensajes-compartidos)
5. [Commit 4 — Animales: búsqueda y ficha](#commit-4--animales-búsqueda-y-ficha)
6. [Commit 5 — Lotes: grupos y ficha](#commit-5--lotes-grupos-y-ficha)
7. [Commit 6 — Formularios canónicos](#commit-6--formularios-canónicos)
8. [Commit 7 — Inicio](#commit-7--inicio)
9. [Commit 8 — Actividad](#commit-8--actividad)
10. [Compuerta 1 — ¿Hace falta migración local?](#compuerta-1--hace-falta-migración-local)
11. [Commit 9 — Actualización desde la versión instalada](#commit-9--actualización-desde-la-versión-instalada)
12. [Orden, dependencias y puntos de no retorno](#orden-dependencias-y-puntos-de-no-retorno)
13. [Cómo se prueba](#cómo-se-prueba)
14. [Descripción del PR](#descripción-del-pr)

---

## Compuerta 0 — Revisión de las representaciones de diseño

**Antes de desarrollar todas las pantallas.** El spec sec. 3.4 es explícito: la dirección
visual «no se presenta como una preferencia estética ya validada por empleados».

Producir y revisar una representación concreta de: **Inicio, ficha de animal, tratamiento,
parto y rechazo de sincronización**, en **ambos temas**.

- **Se aprueba** → los commits 2 a 8 desarrollan sobre esa dirección.
- **Se rechaza o cambia** → se ajusta la representación antes de tocar más pantallas. Es más
  barato que rehacer veinte.
- La priorización fina de accesos y el vocabulario **se validan con los empleados**
  (spec sec. 6). No se atribuyen frecuencias ni preferencias a conversaciones que no ocurrieron.
- **No se presume que el ordeño sea central** para esta operación porcina (spec sec. 3).

## Commit 1 — Sistema visual

`feat(field-app): evolve the theme into a shared visual system`

**Por qué primero:** todo lo demás lo consume. Hacerlo pantalla por pantalla produciría la
mezcla de convenciones que D6 prohíbe.

**Archivos:**
- `clients/field-app/src/ui/theme.ts` — ya existe un tema central con `color`, `font`, `space`
  y `touchTarget`. **Se evoluciona, no se sustituye por estilos por pantalla.**
- Superficies claras cálidas, texto oscuro, verde profundo de acento; ámbar para atención,
  rojo para errores o acciones irreversibles. Tema oscuro coherente para poca luz.
- Escala central de espaciado y tamaño. **Objetivos táctiles de al menos 64 unidades lógicas.**
- Preferencia de tema que puede seguir al sistema o fijarse localmente. **Ninguna variante
  requiere conexión.**
- `clients/field-app/src/ui/components.tsx` — listas para colecciones, tarjetas para resúmenes
  con significado. **Evitar que cada campo y cada acción sean una tarjeta con igual peso.**

**Verificación:** contraste comprobado en ambos temas. Si se incorpora una familia tipográfica
nueva, su distribución offline y su licencia quedan resueltas **sin descargas durante el uso**.

## Commit 2 — Los cuatro destinos

`feat(field-app): restructure navigation into Inicio, Animales, Lotes and Actividad`

**Archivos:**
- `clients/field-app/src/screens/navigation.ts` y `clients/field-app/src/App.tsx` — hoy la
  navegación se organiza por claves de pestaña y renderizado condicional.
- Cuatro destinos reconocibles **por texto y símbolo**. Estado de sincronización accesible
  desde cualquiera; ajustes y sesión secundarios.
- `clients/field-app/src/screens/ActivitiesHub.tsx` — hoy coexisten recorridos por animal con
  accesos separados, y la pantalla dedica espacio a explicar categorías internas. Se resuelve
  aquí.
- Detalle de animal y pasos de registro conservan **regreso claro a su origen**.

**Verificación:** el botón atrás de Android y el de pantalla siguen coherentes (heredado de
0006). Ninguna ruta queda inaccesible.

**Si cambiar la infraestructura de navegación exige un ADR** (spec sec. 6), se escribe antes.
Esta rama **no autoriza instalar librerías**.

## Commit 3 — Estados y mensajes compartidos

`feat(field-app): give every record state one shared, plain-language presentation`

**Por qué acá:** las pantallas de los commits 4 a 8 los consumen. Definirlos después
produciría siete vocabularios distintos para el mismo hecho.

**Archivos:**
- Componentes de estado, con la tabla de `spec.md` sec. 3.6: guardado local sin red, envío o
  descarga en curso, aceptado con descarga pendiente, rechazado, catálogo vacío o incompleto,
  sesión expirada, error de almacenamiento local.
- **«Sin señal» no se presenta como fracaso** de un registro guardado localmente (D5).
- **Un error de almacenamiento local no afirma «guardado»** y no sugiere borrar la app.
- El diagnóstico persistente de 0004 es alcanzable, pero **el empleado no necesita entender
  cursors, UUID ni nombres de tablas** para registrar una actividad.

**Verificación:** los siete estados de la tabla tienen presentación y se distinguen entre sí.
**Ningún estado de éxito oculta los defectos de 0004** (criterio 4).

## Commit 4 — Animales: búsqueda y ficha

`feat(field-app): redesign animal search and the animal record`

**Archivos:**
- Búsqueda por identificadores y nombre, filtros por grupo y sexo. La coincidencia por arete
  es reconocible y **no pierde ceros iniciales**; la ambigüedad exige selección consciente,
  **según [0007](../feature-0007-field-app-individual-tagged-livestock/spec.md)** — no se
  duplica esa lógica aquí.
- La ficha destaca arete vigente, nombre si existe, sexo, grupo y estados relevantes.
- **Retiro, baja y preñez solo se muestran cuando hay datos que los respaldan.**
- El historial separa hechos confirmados de registros locales pendientes, **sin duplicarlos**
  al llegar la confirmación del servidor.
- Búsqueda que **mantiene texto, filtros y posición** al regresar de la ficha.

**Verificación:** un animal sin arete conserva identificación alternativa legible. Las
actividades ofrecidas respetan aptitud (0005) y permisos (0008).

## Commit 5 — Lotes: grupos y ficha

`feat(field-app): redesign the animal group list and record`

**Archivos:**
- Grupos de animales y su modo de seguimiento; **distinguir identificación individual de
  conteo**.
- Solo resúmenes **calculables**, declarando fecha o limitación de actualización.
- Si un resumen requiere conexión, su indisponibilidad **no bloquea la captura local**.
- **No se fabrican** pesos promedio, existencias, dosis ni indicadores que el backend no
  proporciona.

**Verificación:** un grupo `Headcount` no muestra datos que implicarían identidad individual.
Sin conexión, se puede seguir registrando aunque falte el resumen.

## Commit 6 — Formularios canónicos

`feat(field-app): unify the activity forms around one canonical flow`

**Archivos:**
- Encabezado de actividad y sujeto, campos con **unidad visible**, ayuda breve, validación
  cerca del campo, acción principal inequívoca.
- Campos opcionales distinguidos. Catálogos largos con **búsqueda o selección progresiva**, no
  toda la pantalla llena de botones.
- **Ningún valor esencial depende solo del color.**
- Confirmación final que resume lo revisable: animal o grupo, fecha, cantidad/unidad, producto
  o causa.
- Las acciones frecuentes **no reciben pasos decorativos**. El asistente de parto **conserva
  su secuencia funcional de cuatro pasos** mientras recibe el lenguaje nuevo (D1).
- Los accesos rápidos y los de ficha terminan en el **mismo flujo canónico**, con el contexto
  correcto. No duplican formularios ni reglas.

**Verificación:** tras el guardado local, la respuesta indica el hecho registrado y permite
consultar el detalle o continuar con otro sujeto **sin duplicarlo**. Los errores mantienen los
valores editables.

**No se promete corregir desde el teléfono** un tipo de registro que el dominio todavía no
permite corregir.

## Commit 7 — Inicio

`feat(field-app): build an operational home screen`

**Archivos:**
- Estado de trabajo local y de envío, acceso destacado a **buscar arete**, accesos de registro
  y registros recientes propios.
- Una acción iniciada en Inicio **solicita un sujeto apto** para esa actividad. El registro de
  parto ofrece madres elegibles **sin pedir recorrer antes todo el hato**.
- **Los módulos desactivados y las acciones no autorizadas no ocupan accesos operativos.**
  Un módulo desactivado no domina Inicio.

**Verificación:** no añade analítica productiva, métricas financieras ni funciones de fases
futuras. Los accesos usan datos disponibles, no inventados.

## Commit 8 — Actividad

`feat(field-app): show the employee what they recorded and what happened to it`

**Archivos:**
- Lo registrado por el empleado, ordenado por fecha, con estado y detalle.
- Comprobar guardado, entender un rechazo y corregir **cuando esté permitido**.
- Consume el alcance propio de `/sync/operations` que define
  [0008](../feature-0008-people-permission-enforcement/spec.md): la pantalla debe tolerar ver
  solo lo suyo.

**Verificación:** el usuario distingue una escritura guardada localmente de una enviada, una
rechazada y una descarga incompleta (criterio 4).

## Compuerta 1 — ¿Hace falta migración local?

**Antes del commit 9.** Determinar si el comportamiento nuevo exige cambiar el esquema local.

- **No hace falta** → el commit 9 se limita a verificar la actualización.
- **Hace falta** → se define y prueba **antes de distribuirla** (spec sec. 3.7), preservando
  outbox, datos locales e identificadores. Pasa a ser el punto de no retorno de la rama.

## Commit 9 — Actualización desde la versión instalada

`fix(field-app): preserve pending work when updating from the field build`

**Archivos:**
- La actualización conserva sesión según la política vigente, **outbox, datos locales e
  identificadores**. **No exige reinstalar ni «empezar limpio».**
- Un borrador persistido tiene recuperación explícita. **Si el flujo viejo no guardaba
  borradores, no se promete recuperar información que nunca se almacenó.**

**Verificación:** una instalación con registros pendientes conserva esos registros y sus
referencias. Un reinicio durante captura o envío **no presenta como guardado un dato no
persistido**.

## Orden, dependencias y puntos de no retorno

```
0006 mergeado + capacidades de 0004, 0005, 0007, 0008, 0009
  └─ Compuerta 0 (revisión de cinco representaciones, ambos temas)
       └─ Commit 1 (sistema visual)
            ├─ Commit 2 (cuatro destinos)
            └─ Commit 3 (estados compartidos)
                 ├─ Commit 4 (Animales)
                 ├─ Commit 5 (Lotes)
                 └─ Commit 6 (formularios)
                      ├─ Commit 7 (Inicio)
                      └─ Commit 8 (Actividad)
                           └─ Compuerta 1 (¿migración local?)
                                └─ Commit 9 (actualización)
```

- **Punto de no retorno:** el commit 9, **solo si** la Compuerta 1 determina que hace falta
  migración local. Sin migración, la rama es reversible.
- **Los commits 4, 5 y 6 son paralelizables** una vez cerrados el 1, 2 y 3.
- **La Compuerta 0 es la más valiosa de la rama.** Saltarla significa descubrir en el commit 8
  que la dirección visual no servía.

## Cómo se prueba

1. `npm test` completo en `clients/field-app`.
2. `dotnet test` completo. Esta rama no debería tocar backend; cualquier fallo es una regresión
   ajena que hay que detectar antes del merge.
3. [`test-e2e.md`](./test-e2e.md) **sobre la aplicación completa, datos persistidos y servidor
   real**. Los defectos de scroll y sincronización **no se cierran solo con un prototipo**
   (criterio 11).
4. **Dos usuarios de campo** completan los flujos que usan habitualmente **sin ayuda**
   (criterio 1). Se documentan las dificultades restantes **antes** de aceptar.

## Descripción del PR

**Título:** `feat(field-app): redesign the field application around tags, records and state`

**Cuerpo:**

- **Qué:** sistema visual compartido, navegación en cuatro destinos, estados de registro con
  presentación única, búsqueda y fichas de animal y grupo, formularios canónicos, Inicio
  operativo, pantalla de Actividad y actualización sin pérdida.
- **Por qué:** el dueño pidió un rediseño general de la app tras el reporte de los empleados
  ([`spec.md` sec. 1](./spec.md#1-encargo-y-límites-de-lo-conocido)).
- **Decisiones:** [`spec.md` sec. 2](./spec.md#2-decisiones-fijadas-para-la-propuesta), D1–D8.
- **Qué NO incluye:** rediseño del panel Angular, lectores QR/RFID, nueva autenticación,
  modificación de reglas de dominio, analítica productiva ni métricas financieras. Tampoco
  cambia el protocolo de sync ni el modelo de permisos por motivos visuales.
- **Riesgo declarado:** rama larga con superficie amplia. La Compuerta 0 la acota; los commits
  permiten revisarla por partes.
- **Lo que este PR NO cierra:** los defectos de 0004, 0005, 0007, 0008 y 0009. Este PR gobierna
  cómo se encuentran y se operan, **no los declara resueltos** (D7).
- **Resultado de Compuerta 0:** Aprobada formalmente en `design-preview.md` (Inicio, Ficha, Tratamiento, Parto y Rechazo de sincronización en temas claro y oscuro; modelo de 4 destinos; ergonomía táctil >= 64pt; validación de prioridades y vocabulario de campo).

