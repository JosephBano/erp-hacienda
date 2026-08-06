# PLAN-FASE-3-4.md — Plan de ejecución de las Fases 3 y 4

> **Qué es este documento.** El mapa operativo para construir la Fase 3 (app móvil
> offline-first) y la Fase 4 (dinero: ventas, compras, costos). Dice *qué* ramas existen,
> *en qué orden*, *qué entra en cada una*, *qué pruebas se exigen* y *cómo se trabaja el
> día a día*. No sustituye a `ROADMAP.md` (el qué y el porqué) ni a `CONSTITUTION.md`
> (las reglas). Si algo aquí contradice la Constitución, gana la Constitución.
>
> **Estado de partida (2026-08-02):** Fase 2 cerrada. `develop` en `b987e4b`. 6 módulos
> (Livestock, Production, Inventory, People, Breeding, Tasks), 89 pruebas automáticas,
> panel Angular con 7 componentes, sin app móvil, sin módulos de dinero.
>
> **Regla de oro que aplica a todo lo de abajo (Art. 11):** una fase no se cierra cuando
> el código está listo, sino cuando alguien en la finca la usa de verdad. Y este documento no se sube nunca a una rama remota o entra a un commit
---

## Índice

1. [Cómo se trabaja: el protocolo de una feature](#1-cómo-se-trabaja-el-protocolo-de-una-feature)
2. [Nivel de exigencia en pruebas](#2-nivel-de-exigencia-en-pruebas)
3. [Fase 3 — App móvil offline-first](#3-fase-3--app-móvil-offline-first)
4. [Fase 4 — Dinero completo](#4-fase-4--dinero-completo)
5. [ADRs que hay que escribir](#5-adrs-que-hay-que-escribir)
6. [Riesgos y frenos de emergencia](#6-riesgos-y-frenos-de-emergencia)

---

## 1. Cómo se trabaja: el protocolo de una feature

Esto es lo que se repite **idéntico** en cada una de las ~21 ramas del plan. Si en algún
momento te pierdes, vuelve a esta sección: el ciclo siempre es el mismo.

### 1.1 El ciclo completo (9 pasos)

```
develop actualizado → rama → diseño → TDD → validación local → commits → push → PR → merge
```

**Paso 1 — Partir limpio.**
```bash
git switch develop
git pull origin develop
dotnet test                      # develop siempre debe estar en verde antes de ramificar
```
Si `develop` está en rojo, **eso es el trabajo de hoy**, no la feature nueva.

**Paso 2 — Crear la rama.** Nombre exacto según la tabla de la fase correspondiente.
```bash
git switch -c feature/people-permissions
```
Convención (Art. 13 + `AGENTS.md`): `feature/<módulo>-<descripción-corta-en-inglés>`.
Nunca se commitea a `develop` ni a `main` directamente. Nunca.

**Paso 3 — Diseñar antes de escribir.** Para toda feature marcada como **estructural**
en las tablas: escribe primero, en un archivo temporal o en el borrador del PR, las
entidades, eventos, endpoints y migraciones que vas a crear. Si toca una decisión
transversal (dependencia nueva, esquema compartido, protocolo), **el ADR se escribe y se
mergea antes que el código** (Art. 14). El ADR puede ir en su propia rama `docs/adr-00XX-*`
para no bloquear.

**Paso 4 — Migración de esquema, si aplica.** EF Core, una migración por feature, nombre
descriptivo. Nunca editar una migración ya mergeada (Art. 15). Verifica que corre desde
cero:
```bash
docker compose down -v && docker compose up -d
dotnet ef database update --project src/Modules/<Módulo>/Hato.Modules.<Módulo>.Infrastructure --startup-project src/Hato.Api
```

**Paso 5 — TDD, de verdad.** Prueba roja → implementación mínima → refactor. En orden:
dominio (invariantes) → aplicación (handlers + validadores) → infraestructura/API
(integración con Testcontainers) → cliente (Angular/React Native).
Una tarea de la lista de la feature = un ciclo rojo-verde-refactor = idealmente un commit.

**Paso 6 — Validación local ANTES de commitear en serio.** No se abre PR sin esto:
```bash
dotnet build  -c Release
dotnet test   -c Release           # 100% verde, sin tests saltados nuevos
```
Más la **prueba manual** que corresponda: `curl` contra el endpoint nuevo, o el flujo en
el panel Angular, o el flujo en el móvil con el modo avión activado. Escribe los comandos
que usaste — van tal cual en el PR, en la sección "cómo probarlo".

**Paso 7 — Commits.** Una vez la feature está validada, se ordenan los commits:
Conventional Commits en inglés, scope = módulo, uno por unidad lógica coherente
(no un commit gigante "feat: todo", no 40 commits de "fix typo").
```
feat(people): add database-backed roles and permissions
test(people): cover permission resolution and role assignment
docs(adr): accept ADR-0007 on the permission model
```

**Paso 8 — Push + PR.**
```bash
git push -u origin feature/people-permissions
gh pr create --base develop --title "feat(people): permisos finos en base de datos"
```
El cuerpo del PR lleva **siempre** estas cuatro secciones (`AGENTS.md` §5):
1. **Propósito** — qué problema de la finca resuelve.
2. **Decisiones** — lo que elegiste y lo que descartaste (enlaza el ADR si existe).
3. **Cómo probarlo manualmente** — comandos/pasos copiables.
4. **Qué NO incluye** — el alcance que dejaste fuera a propósito (y dónde quedó anotado).

**Paso 9 — Revisión y merge.** CI verde es requisito, no logro (Art. 12). Autorrevisión
con la checklist de §1.2, o `/code-review` si quieres una segunda pasada. Merge a
`develop` por PR (nunca push directo), y luego:
```bash
git switch develop && git pull
git branch -d feature/people-permissions
git push origin --delete feature/people-permissions
```
Actualiza `BACKLOG.md` con la deuda que detectaste y no arreglaste (Art. 9 de `AGENTS.md`:
un PR = un propósito; lo que ves de paso se anota, no se arregla).

### 1.2 Checklist de autorrevisión (antes de pedir merge)

- [ ] ¿Ningún `if`/`switch` por especie, raza o producto en el dominio? (Art. 8)
- [ ] ¿Ningún borrado físico ni edición de eventos históricos? (Art. 1)
- [ ] ¿Dinero en `decimal`, cantidades con unidad, fechas persistidas en UTC? (Art. 10)
- [ ] ¿Los módulos se hablan solo por `Contracts` o eventos MediatR? (Art. 6)
- [ ] ¿Términos nuevos agregados a `GLOSSARY.md`? (Art. 20)
- [ ] ¿Migración nueva (no editada) y reproducible desde cero? (Art. 15)
- [ ] ¿Ninguna dependencia NuGet/npm nueva sin ADR aprobado? (`AGENTS.md` §2)
- [ ] ¿Ningún secreto, contraseña o clave de firma en el diff?
- [ ] ¿Las pruebas cubren los caminos de error, no solo el feliz?
- [ ] ¿Si la feature toca registro de campo, sigue funcionando sin red? (Art. 9)

### 1.3 Ritmo y bloques

Cada fase se organiza en **bloques**. Dentro de un bloque las ramas son secuenciales
(cada una asume la anterior mergeada). Entre bloques hay un **hito de validación**: no se
empieza el bloque siguiente hasta que el anterior esté mergeado, en verde y probado a mano.

Cuando una feature se alarga más de ~1 semana de trabajo real, **pártela** y anota el resto
en `BACKLOG.md`. Cuando una fase pasa de ~3 meses sin uso real, **recorta alcance**
(Art. 11); en §6 están marcadas cuáles features son sacrificables.

---

## 2. Nivel de exigencia en pruebas

Punto de partida: 89 pruebas. Estas son las reglas que rigen de aquí en adelante.

### 2.1 Regla general por capa

| Capa | Tipo de prueba | Exigencia |
|---|---|---|
| Domain | xUnit unitarias, sin infraestructura | **Toda invariante y todo `DomainException` tiene su prueba.** Un método público de dominio sin prueba = PR rechazado. |
| Application (handlers/validadores) | xUnit unitarias con dobles | Camino feliz + **cada rama de error** del validador. |
| Infrastructure / API | Integración con Testcontainers (PostgreSQL real) | Cada endpoint nuevo: 200/201 feliz, 400 validación, 401/403 autorización, 404 inexistente. Prohibido InMemory para verificar comportamiento final. |
| Migraciones | Integración | La suite corre sobre una BD creada desde cero por migraciones en cada ejecución. |
| Angular | Jest/Karma | Servicios con lógica: unitarias. Componentes: al menos render + interacción principal. |
| React Native | Jest + React Native Testing Library | Lógica de sync y de base local: unitarias exhaustivas. Cada pantalla de registro: una prueba de "registro sin red". |

### 2.2 Exigencias especiales de la Fase 3 (sincronización)

La sincronización es donde el proyecto se puede morir en silencio: los bugs no se ven, se
descubren cuando faltan datos. Por eso, además de lo anterior:

- **Toda entidad que se vuelva sincronizable estrena una prueba de push duplicado.**
  Enviar la misma operación dos veces debe producir exactamente un registro. Sin excepción.
- **Suite dedicada `Hato.Modules.Sync.IntegrationTests` con, como mínimo, estos escenarios:**
  1. Push idempotente: misma operación ×3 → un registro, misma respuesta.
  2. Push parcialmente fallido: 10 operaciones, la #4 inválida → las 9 válidas persisten,
     la #4 vuelve con error tipado, el reintento del lote completo no duplica nada.
  3. Push desordenado: el evento hijo llega antes que el padre → se resuelve o se
     encola, nunca se pierde.
  4. Pull incremental: cursor `since` no pierde ni repite registros en el borde exacto
     del timestamp (el caso de dos escrituras en el mismo milisegundo).
  5. Pull con tombstones: un borrado lógico llega al cliente y desaparece de su base.
  6. Reloj del dispositivo desfasado ±2 días → el servidor manda en el orden, el
     `occurred_at` del cliente se conserva como dato declarado.
  7. Lote grande: 500 operaciones en un push → sin timeout, transaccional por lote.
  8. Conflicto en entidad editable: dos dispositivos editan el mismo campo → LWW aplicado
     y **entrada en la bitácora de conflictos**.
  9. Token expirado a mitad del push → reintento tras refresh sin duplicar.
  10. Corte de red simulado a mitad de push → el cliente reintenta y converge.
- **Prueba de convergencia end-to-end** (`field-app` contra API real en Docker): dos
  dispositivos simulados registran offline, sincronizan y terminan con estado idéntico.

### 2.3 Exigencias especiales de la Fase 4 (dinero)

- **Redondeo**: una única clase de política de redondeo, con pruebas de tabla que incluyan
  los casos de medio-arriba/medio-par y valores con 3+ decimales (Art. 10).
- **Partida doble**: prueba invariante "todo asiento cuadra" ejecutada sobre **todos** los
  asientos generados en cada suite de integración; si algún módulo emite un asiento
  descuadrado, la suite entera falla.
- **Idempotencia contable**: reprocesar el mismo evento de dominio no duplica el asiento.
- **Golden test de mes contable**: un escenario fijo (ventas de leche, venta de un animal,
  compras de insumos, consumos de grupo) cuyos totales por centro de costo están escritos
  a mano en el test. Si cambian, alguien tiene que justificarlo.
- **Bloqueo por retiro punta a punta**: prueba de integración que intenta vender leche y
  un animal bajo período de retiro y verifica el **rechazo**, no la advertencia (Art. 19).

### 2.4 Umbrales de salida

| Momento | Backend | Clientes |
|---|---|---|
| Hoy | 89 | — |
| Cierre de Fase 3 | **≥ 170** pruebas, ≥ 25 de ellas de sincronización | **≥ 60** pruebas en `field-app` |
| Cierre de Fase 4 | **≥ 280** pruebas | ≥ 90 (`field-app` + `admin-web`) |

Los números son indicativos de *cobertura de comportamiento*, no una meta a inflar con
pruebas triviales. La regla dura sigue siendo la de §2.1: caminos de error cubiertos.

### 2.5 Refuerzos de CI a introducir

Se agregan como parte de la primera rama de cada fase:

- **Fase 3, en `feature/people-permissions`:** job de lint/format del backend
  (`dotnet format --verify-no-changes`).
- **Fase 3, en `feature/field-app-scaffolding`:** job de CI para `clients/field-app`
  (typecheck + lint + jest) y para `clients/admin-web` si aún no existe.
- **Fase 4, en `feature/accounting-core`:** job que falla si algún asiento de las suites
  queda descuadrado (ya cubierto por §2.3, pero visible como check propio en el PR).

---

## 3. Fase 3 — App móvil offline-first

**Objetivo (roadmap):** que los empleados registren desde el potrero, sin señal.
**Criterio de salida:** una semana completa de registros de campo hechos solo desde el
móvil, incluyendo días sin señal, sin pérdida ni duplicación de datos.

**La idea clave del plan:** la Fase 3 **no empieza en React Native**. Empieza en el
backend. Hoy no existe protocolo de sync, los roles son un `enum` compilado (viola Art. 8)
y `AnimalEvent.RecordedBy` es un `string` libre, no un usuario real — con eso no se puede
sostener una bitácora de "quién registró qué". Primero se construye el suelo; después se
camina sobre él.

### Bloque 3.A — Suelo en el backend (4 ramas)

#### `feature/people-permissions` — permisos finos en base de datos · **estructural**

*Por qué:* el roadmap pide "roles y permisos finos por empleado" y el Art. 8 prohíbe que
los roles vivan como `enum` en el código. Hoy `UserRole` es `{Admin, Registrar,
Veterinarian}` compilado.

Tareas:
1. ADR-0007: modelo de permisos (roles en BD, permisos granulares tipo
   `livestock.animals.write`, asignación N:N, decisión sobre permisos por grupo/lote).
2. Dominio: `Role`, `Permission`, `RolePermission`, `UserRole` (entidad, no enum).
3. Migración con **datos semilla** que reproducen los 3 roles actuales y backfill de los
   usuarios existentes — nadie debe perder acceso al desplegar.
4. Autorización basada en políticas en ASP.NET Core: un `PermissionAuthorizationHandler`
   que resuelve contra BD con caché por request; sustituir los `RequireRole` actuales.
5. Endpoints `GET/POST/PUT /api/v1/roles`, asignación de roles a usuarios.
6. Angular: pantalla de administración de roles y permisos.
7. CI: job `dotnet format --verify-no-changes`.

Pruebas: resolución de permisos (usuario con 2 roles, permiso heredado, permiso revocado),
403 por permiso faltante en cada endpoint tocado, invariante "no se puede quedar sin ningún
Admin" ya existente sigue verde, semilla idempotente.

#### `feature/people-audit-trail` — bitácora de quién registró qué

*Por qué:* requisito del roadmap y de la LOPDP (`LEGAL-ECUADOR.md` §5): la app registra
actividad de empleados. Hoy `RecordedBy` es texto libre.

Tareas:
1. `RecordedBy` pasa de `string` a `Guid` (FK a `users`) **conservando** el texto original
   en una columna `recorded_by_label` para no perder historia (Art. 1). Migración con
   backfill por coincidencia de nombre; lo que no casa, queda etiquetado.
2. `ICurrentUser` en SharedKernel + interceptor de EF Core que rellena
   `CreatedBy`/`UpdatedBy` automáticamente en toda `AuditableEntity`.
3. Endpoint `GET /api/v1/audit?userId=&from=&to=` con paginación.
4. Angular: vista de bitácora filtrable.
5. `docs/LEGAL-ECUADOR.md`: nota de la información que se dará por escrito al empleado.

Pruebas: backfill sobre datos legacy, interceptor rellena en create y update, la bitácora
no permite edición ni borrado, filtro por usuario y rango de fechas.

#### `feature/sync-protocol-pull` — el lado de lectura · **estructural**

*Por qué:* el cliente necesita bajarse el estado de la finca y mantenerlo fresco con
tráfico mínimo.

Tareas:
1. ADR-0008: protocolo de sincronización (formato de cursor, granularidad por colección,
   tombstones, ventana máxima, comportamiento ante *reset* del cliente). Este ADR es el
   documento más importante de la fase: escríbelo con calma.
2. Índices `(updated_at)` y `deleted_at` en todas las tablas sincronizables; convención
   verificada por una prueba que recorre el modelo de EF.
3. `GET /api/v1/sync/pull?since=<cursor>&collections=` → `{ collections: {...}, cursor,
   hasMore }`, con paginación por tamaño de página y tombstones incluidos.
4. Filtrado por permisos: el empleado solo baja lo que le corresponde.
5. Colecciones de la v1: `animals`, `animal_identifiers`, `animal_groups`,
   `group_memberships`, `species/breeds/categories`, `medications/items`, `alerts`.

Pruebas: los 4 escenarios de pull de §2.2 + filtrado por permisos + cursor estable ante
escrituras concurrentes.

#### `feature/sync-protocol-push` — el lado de escritura · **estructural**

*Por qué:* aquí es donde se pierden o duplican datos. Es la rama más delicada del proyecto.

Tareas:
1. Tabla `sync_operations` (`client_operation_id` UUID **unique**, `user_id`, `device_id`,
   `received_at`, `status`, `result_ref`) — la garantía de idempotencia.
2. `POST /api/v1/sync/push` con lote de operaciones tipadas
   (`recordMilking`, `recordAnimalEvent`, `createAnimal`, `recordBirth`, `moveAnimal`…),
   cada una con su `clientOperationId` y su `occurredAt` declarado por el cliente.
3. Respuesta **por operación**: aceptada / duplicada (devuelve el resultado original) /
   rechazada con Problem Details. Un lote nunca falla entero por una operación mala.
4. Transaccionalidad por operación, orden estable, límite de tamaño de lote configurable.
5. Reglas de negocio idénticas a las de la API normal: el push **no** es una puerta trasera
   que salta validaciones ni el bloqueo por retiro.

Pruebas: **los 10 escenarios de §2.2 son obligatorios en esta rama.** Es el único punto del
plan donde exijo que las pruebas se escriban antes de mirar siquiera la firma del endpoint.

### Hito 3.A ✅ — antes de seguir

Con Docker levantado: crear un usuario con rol limitado, hacer `pull`, hacer `push` de un
lote con duplicados desde `curl`, volver a hacer `pull` y verificar que los datos están una
sola vez. Si esto no se siente sólido, **no se avanza al móvil**.

### Bloque 3.B — La app (5 ramas)

#### `feature/field-app-scaffolding` — esqueleto React Native · **estructural**

1. ADR-0009: React Native (Expo con dev-client vs bare), WatermelonDB, y la lista de
   dependencias npm iniciales — **todas** las dependencias del cliente entran por este ADR
   de una vez, para no pedir aprobación cada semana.
2. `clients/field-app/` con estructura, esquema local de WatermelonDB, navegación.
3. Login con credenciales del backend + **sesión offline**: token cacheado con expiración
   larga y desbloqueo por PIN local; la app arranca y permite registrar sin red.
4. Job de CI para el cliente (typecheck, lint, jest).
5. README del cliente: cómo levantarlo contra el backend local.

Pruebas: arranque sin red, login offline con token cacheado, esquema local se crea y migra.

#### `feature/field-app-sync-engine` — el motor de sincronización en el cliente · **estructural**

1. Outbox local: toda escritura de UI crea una operación con `clientOperationId` (UUID) y
   estado `pendiente`.
2. Sincronizador: pull incremental + push del outbox, con backoff exponencial, detección de
   conectividad, y sincronización oportunista (al recuperar red, al abrir la app, manual).
3. Estados visibles: pendiente / sincronizado / rechazado, con contador siempre a la vista.
4. Manejo de rechazos: la operación no se borra en silencio jamás — va a una bandeja de
   "registros con problema" que el empleado o el admin puede revisar.
5. Migraciones del esquema local versionadas.

Pruebas: cola persiste entre reinicios de la app, reintento tras fallo no duplica,
rechazo del servidor deja el registro visible, convergencia de dos clientes simulados.

#### `feature/field-app-milking` — ordeño en ≤3 toques

*Por qué primero:* es el registro más frecuente (5 AM, todos los días). Si esto no gana al
cuaderno, nada lo hace.

1. Flujo por grupo y por vaca, con la lista precargada del pull.
2. ≤3 toques por registro; botones grandes, alto contraste (guantes y sol).
3. Marca visible de vaca en período de retiro con leche no vendible (dato bajado del pull).
4. Resumen del día y edición del registro **del día en curso** antes de sincronizar
   (después, corrección = evento nuevo, Art. 1).

Pruebas: registro completo en modo avión, conteo de toques del flujo principal, marca de
retiro presente, registro del día persiste tras cerrar la app.

#### `feature/field-app-events` — tratamientos, pesajes y movimientos

1. Tratamiento con medicamento del inventario, dosis, costo y **cálculo local del retiro**
   a partir de los datos sincronizados (el servidor recalcula y manda la verdad).
2. Pesaje y movimiento entre grupos.
3. Selector de animal por arete, nombre o escaneo/búsqueda rápida — asumiendo animales
   **sin** arete también (Art. 3).
4. Adjuntar foto al evento, encolada para subir cuando haya red (usa el mismo outbox).

Pruebas: los tres flujos en modo avión, retiro calculado local coincide con el del servidor
tras sincronizar, foto grande no bloquea el push de los datos.

#### `feature/field-app-births` — partos y crías offline

*Por qué su propia rama:* es el caso donde el cliente **crea una entidad nueva** (la cría)
con un UUID local que después tiene que existir en el servidor con su genealogía intacta.
Es el escenario que más puede doler si sale mal.

1. Registro de parto con creación de la cría (UUID de cliente, Art. 3).
2. Madre obligatoria; padre opcional: animal o pajuela (padre dual).
3. Camadas (múltiples crías) sin `if` por especie: el número viene de configuración.
4. Push que crea animal + evento de parto en una sola operación atómica.

Pruebas: parto offline → sync → la cría existe una sola vez con su genealogía; parto
duplicado por doble toque no crea dos crías; camada de N crías converge completa.

### Hito 3.B ✅

Instalar la app en un teléfono real de gama media, poner el teléfono en modo avión un día
entero, registrar el ordeño real y un par de eventos, y sincronizar al volver a la casa.

### Bloque 3.C — Piloto y cierre (2 ramas)

#### `feature/field-app-pilot-hardening` — lo que el piloto pida

1. Pantalla de estado de sincronización comprensible para un empleado (no para ti).
2. Bandeja de conflictos y rechazos, con resolución manual desde el panel Angular
   (implementa aquí la bitácora de conflictos LWW del ADR-0005 y del ADR-0008).
3. Registro de errores local exportable (sin telemetría en la nube: LOPDP, no recolectar de
   más).
4. Ajustes de UX salidos del piloto con 1–2 empleados reales. **Esta rama se itera varias
   veces**: es legítimo que sean 2 o 3 PRs pequeños seguidos con el mismo prefijo
   (`feature/field-app-pilot-hardening-2`).

#### `docs/fase-3-cierre` — retrospectiva

Actualizar `ROADMAP.md` (fecha real + retrospectiva de 5 líneas), `ARCHITECTURE.md` (el
protocolo de sync ya construido), `GLOSSARY.md`, `BACKLOG.md`. Etiquetar release desde
`main` vía `release/*` si ya hay uso productivo.

**Cierre de Fase 3 = una semana completa de registros de campo hechos solo desde el móvil,
con días sin señal, sin pérdida ni duplicación.** No antes.

---

## 4. Fase 4 — Dinero completo: ventas, compras, costos

**Objetivo (roadmap):** saber cuánto cuesta y cuánto deja cada cosa.
**Criterio de salida:** cerrar un mes contable real y que el contador acepte el reporte.

**La idea clave del plan:** `Accounting` se construye **antes** de tener a quién escucharle,
no después. Si Sales y Purchasing nacen primero y la contabilidad llega al final, terminas
retro-encajando asientos y el Art. 6 (contabilidad solo recibe, no conoce a nadie) se
rompe. Orden: cimientos transversales → contabilidad receptora → emisores → SRI → reportes.

### Bloque 4.A — Cimientos transversales (2 ramas)

#### `feature/shared-attachments` — adjuntos documentales

*Por qué ahora:* las guías de movilización (venta de animales), las facturas de compra y
los comprobantes SRI necesitan archivos. Está en `ARCHITECTURE.md` como transversal y aún
no existe. También lo aprovecha el móvil (fotos de eventos).

1. ADR-0010: almacenamiento de archivos (disco local con ruta configurable vs. S3
   compatible), nombrado, retención, y cómo se respalda (Art. 2).
2. `attachments` (id, tipo MIME, tamaño, hash, ruta, subido por, entidad referenciada).
3. Endpoints de subida/descarga con control de permisos; límite de tamaño y tipos.
4. Integración con el outbox del móvil (fotos encoladas).

Pruebas: subida, descarga con y sin permiso, hash correcto, referencia a entidad
inexistente rechazada, borrado lógico nunca destruye el archivo (Art. 1).

#### `feature/shared-money-units` — política de dinero y unidades

*Por qué:* antes de que tres módulos inventen tres redondeos distintos.

1. `Money` y `Quantity` en SharedKernel (si aún no están completos): `decimal`, moneda,
   unidad explícita, conversiones desde tabla de unidades en BD (Art. 8/10).
2. Una única `RoundingPolicy` con las reglas contables del proyecto.
3. Tabla de unidades y conversiones con semilla (litro, kg, unidad, saco…).

Pruebas: tabla de casos de redondeo, conversión con unidad desconocida falla, aritmética
con monedas distintas prohibida, `double` prohibido por prueba de arquitectura.

### Bloque 4.B — Contabilidad receptora (3 ramas)

#### `feature/accounting-core` — plan de cuentas y asientos · **estructural**

1. ADR-0011: modelo contable (plan de cuentas configurable, partida doble, períodos,
   cierre, qué NO hace el sistema — Art. 18: el contador no se reemplaza).
2. `accounts` (plan de cuentas **configurable en BD**, jerárquico), `journal_entries`,
   `journal_lines` (débito/haber, `decimal`), `accounting_periods` con estado abierto/cerrado.
3. Invariante de dominio: **todo asiento cuadra**, y no se puede postear en período cerrado.
4. Asientos **inmutables**: correcciones por asiento de reversión (Art. 1).
5. Semilla de un plan de cuentas base agropecuario, revisable con el contador.
6. Check de CI de asientos descuadrados (§2.5).

Pruebas: asiento descuadrado rechazado, período cerrado rechaza, reversión genera el
espejo correcto, plan de cuentas jerárquico consulta saldos por rama.

#### `feature/accounting-cost-centers` — centros de costo y costo por animal-día

1. `cost_centers` configurables (lechería, porcinos, quesería…), asignables a grupos.
2. Prorrateo de costos de grupo por **animal-día** a partir de `group_memberships`
   (`ARCHITECTURE.md` §4) — nunca costos por animal individual en la captura.
3. Consulta de costo por animal, por grupo y por centro en un rango.

Pruebas: prorrateo con altas y bajas a mitad de mes, animal en dos grupos en el mismo mes,
grupo vacío no divide por cero, suma de prorrateos = costo total (invariante).

#### `feature/accounting-postings` — el puente desde los módulos · **estructural**

1. Handlers MediatR que escuchan eventos de dominio (venta, compra, consumo de inventario,
   tratamiento, ordeño) y emiten asientos. **Accounting no consulta a nadie** (Art. 6).
2. Mapeo evento→cuentas **configurable en BD**, no en código (Art. 8).
3. Idempotencia: `source_event_id` unique — reprocesar no duplica.
4. Cola/reintento para asientos que fallen, con bandeja de revisión.

Pruebas: idempotencia por evento, mapeo faltante produce error visible (no silencio),
todos los asientos generados cuadran, reproceso masivo converge.

### Bloque 4.C — Los emisores (4 ramas)

#### `feature/sales-customers-milk` — clientes y venta de leche

1. `customers` (con datos fiscales para el SRI), `products` vendibles configurables.
2. Venta de leche con volumen, calidad y precio; cuentas por cobrar básicas.
3. **Bloqueo por período de retiro punta a punta** (Art. 19): la leche marcada no vendible
   no puede entrar en una venta — rechazo, no advertencia.
4. Emisión del evento de dominio que Accounting convierte en asiento.

Pruebas: venta con leche en retiro **rechazada**, CxC se genera y se abona, precio por
calidad, asiento cuadrado generado.

#### `feature/sales-animals` — venta de animales

1. Venta que **genera la baja** del animal como evento (nunca borrado, Art. 1).
2. Adjunto de guía de movilización obligatorio o justificado (`LEGAL-ECUADOR.md` §1).
3. Bloqueo por retiro de carne.
4. Baja de membresías de grupo y cierre de lactancia si aplica.

Pruebas: animal vendido queda inactivo pero con historial completo, venta con retiro de
carne rechazada, sin guía adjunta se avisa/bloquea según lo decidido en el PR.

#### `feature/purchasing-suppliers` — compras y cuentas por pagar

1. `suppliers`, órdenes de compra y recepciones que **alimentan lotes de Inventory** por
   contrato público (Art. 6), con vencimientos y costos reales.
2. Cuentas por pagar y pagos.
3. Adjunto de factura del proveedor.

Pruebas: recepción crea lote en Inventory con costo y vencimiento, CxP se salda, compra
parcial, asiento generado.

#### `feature/sales-sri-invoicing` — comprobantes vía proveedor autorizado · **estructural**

1. ADR-0012: elección del proveedor autorizado de facturación electrónica (API, costos,
   sandbox, qué pasa si se cae) — Art. 18: **no se integra al SRI directamente**.
2. Cliente HTTP con reintentos, estados del comprobante (borrador → enviado → autorizado →
   rechazado → anulado) y persistencia de XML/RIDE como adjuntos.
3. Modo sandbox por configuración; el entorno de desarrollo jamás emite contra producción.
4. Bandeja de comprobantes rechazados.

Pruebas: contra un doble del proveedor — autorización, rechazo con motivo, timeout con
reintento que no duplica comprobantes, anulación. **Ninguna prueba llama a la API real.**

### Bloque 4.D — Salida y cierre (3 ramas)

#### `feature/accounting-reports` — los reportes del contador

1. Libro diario y mayor, balance de comprobación, estado de resultados por centro de costo.
2. Margen por línea de negocio y costo por animal-día en el período.
3. Exportación CSV/Excel (formato acordado **con el contador antes de codificar**).

Pruebas: **golden test de mes contable** (§2.3), export con separadores y decimales
correctos, período sin movimientos no rompe.

#### `feature/admin-web-finance` — el panel de dinero

1. Angular: ventas, compras, clientes, proveedores, comprobantes, reportes.
2. Bandejas de revisión: asientos fallidos, comprobantes rechazados.
3. Puede partirse en 2 PRs (ventas/compras y contabilidad/reportes) si crece.

Pruebas: servicios con lógica cubiertos, componentes con render + interacción principal.

#### `docs/fase-4-cierre` — retrospectiva

`ROADMAP.md` con fecha y retrospectiva, `LEGAL-ECUADOR.md` §3 con fecha de verificación
real con el contador, `ARCHITECTURE.md` actualizado, `BACKLOG.md` depurado.

**Cierre de Fase 4 = un mes contable real cerrado y un reporte que el contador acepta usar.**

---

## 5. ADRs que hay que escribir

| ADR | Tema | Antes de la rama | Fase |
|---|---|---|---|
| 0007 | Modelo de roles y permisos en base de datos | `feature/people-permissions` | 3 |
| 0008 | Protocolo de sincronización (cursor, tombstones, conflictos) | `feature/sync-protocol-pull` | 3 |
| 0009 | Stack del cliente móvil y dependencias npm iniciales | `feature/field-app-scaffolding` | 3 |
| 0010 | Almacenamiento de adjuntos y su respaldo | `feature/shared-attachments` | 4 |
| 0011 | Modelo contable: plan de cuentas, partida doble, períodos | `feature/accounting-core` | 4 |
| 0012 | Proveedor autorizado de facturación electrónica | `feature/sales-sri-invoicing` | 4 |

Recuerda el Art. 14 y la regla de "dormir una noche sobre la decisión": el ADR se escribe,
se deja reposar, se acepta, y **después** se codifica.

---

## 6. Riesgos y frenos de emergencia

| Riesgo | Señal temprana | Qué hacer |
|---|---|---|
| La sincronización se come la fase | Bloque 3.A pasa de 6 semanas | Recortar colecciones sincronizables a lo mínimo (animales + ordeño) y dejar el resto para Fase 3.5 |
| El empleado no adopta la app | Piloto con más de 2 iteraciones sin mejora de adopción | Volver a observar el trabajo real en el potrero antes de escribir una línea más; el problema es de UX, no de código |
| La contabilidad crece sin control | Empiezas a querer reemplazar al contador | Art. 18: el sistema genera datos limpios, no declara impuestos |
| El proveedor SRI resulta caro o malo | Sandbox frustrante | La feature es aislable: el resto de la Fase 4 cierra sin ella y la facturación se hace fuera del sistema un mes más |
| Deuda técnica arrastrada | Ganas de "refactorizar de paso" | A `BACKLOG.md`. Un PR = un propósito |

**Features sacrificables si hay que recortar alcance (Art. 11):**
Fase 3 → `feature/field-app-births` (los partos pueden registrarse en el panel web un
tiempo más) y los adjuntos de foto de `feature/field-app-events`.
Fase 4 → `feature/sales-sri-invoicing` y `feature/accounting-cost-centers` (el margen por
centro puede esperar; el libro diario no).

**Deuda ya detectada y anotada (no se arregla en estas fases salvo que estorbe):**
`EventType` sigue siendo un `enum` compilado en `Livestock.Domain`, lo que roza el Art. 8;
convertirlo a configuración en BD merece su propia rama y su propio ADR. Anótalo en
`BACKLOG.md` en la primera rama que toque ese archivo.

---

## Resumen de ramas, en orden

**Fase 3** (11 ramas)
```
feature/people-permissions
feature/people-audit-trail
feature/sync-protocol-pull
feature/sync-protocol-push
── Hito 3.A ──
feature/field-app-scaffolding
feature/field-app-sync-engine
feature/field-app-milking
feature/field-app-events
feature/field-app-births
── Hito 3.B ──
feature/field-app-pilot-hardening   (iterable: -2, -3…)
docs/fase-3-cierre
```

**Fase 4** (12 ramas)
```
feature/shared-attachments
feature/shared-money-units
feature/accounting-core
feature/accounting-cost-centers
feature/accounting-postings
feature/sales-customers-milk
feature/sales-animals
feature/purchasing-suppliers
feature/sales-sri-invoicing
feature/accounting-reports
feature/admin-web-finance
docs/fase-4-cierre
```
