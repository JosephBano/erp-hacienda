# PROTOCOLO-DE-TRABAJO.md — Cómo se lleva una rama de la idea al merge

> Este documento responde **¿cómo se lleva una rama de la idea al merge?** Es transversal:
> aplica a toda rama del proyecto, no solo a las de una fase. Rescata, sin cambios de fondo,
> lo que antes vivía enterrado dentro de `docs/planes/PLAN-FASE-3-4.md` secs. 1 y 2 — el
> protocolo se repetía idéntico en cada una de esas ramas y no tenía casa propia. `AGENTS.md`
> conserva el resumen de cinco pasos y enlaza aquí para el detalle; no lo dupliques (D1).

## 1. Cómo se trabaja: el protocolo de una feature

Esto es lo que se repite **idéntico** en cada rama del proyecto. Si en algún momento te
pierdes, vuelve a esta sección: el ciclo siempre es el mismo.

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
El cuerpo del PR lleva **siempre** estas cuatro secciones (`AGENTS.md` sec.5):
1. **Propósito** — qué problema de la finca resuelve.
2. **Decisiones** — lo que elegiste y lo que descartaste (enlaza el ADR si existe).
3. **Cómo probarlo manualmente** — comandos/pasos copiables.
4. **Qué NO incluye** — el alcance que dejaste fuera a propósito (y dónde quedó anotado).

**Paso 9 — Revisión y merge.** CI verde es requisito, no logro (Art. 12). Autorrevisión
con la checklist de sec.1.2, o `/code-review` si quieres una segunda pasada. Merge a
`develop` por PR (nunca push directo), y luego:
```bash
git switch develop && git pull
git branch -d feature/people-permissions
git push origin --delete feature/people-permissions
```
Actualiza `docs/BACKLOG.md` con la deuda que detectaste y no arreglaste (Art. 9 de
`AGENTS.md`: un PR = un propósito; lo que ves de paso se anota, no se arregla).

### 1.2 Checklist de autorrevisión (antes de pedir merge)

- [ ] ¿Ningún `if`/`switch` por especie, raza o producto en el dominio? (Art. 8)
- [ ] ¿Ningún borrado físico ni edición de eventos históricos? (Art. 1)
- [ ] ¿Dinero en `decimal`, cantidades con unidad, fechas persistidas en UTC? (Art. 10)
- [ ] ¿Los módulos se hablan solo por `Contracts` o eventos MediatR? (Art. 6)
- [ ] ¿Términos nuevos agregados a `GLOSSARY.md`? (Art. 20)
- [ ] ¿Migración nueva (no editada) y reproducible desde cero? (Art. 15)
- [ ] ¿Ninguna dependencia NuGet/npm nueva sin ADR aprobado? (`AGENTS.md` sec.2)
- [ ] ¿Ningún secreto, contraseña o clave de firma en el diff?
- [ ] ¿Las pruebas cubren los caminos de error, no solo el feliz?
- [ ] ¿Si la feature toca registro de campo, sigue funcionando sin red? (Art. 9)

Y los cinco disparadores de actualización documental, en forma de checklist. La fuente
canónica es `DOCUMENTACION.md` sec.3: quien cambie un disparador, lo cambia allá primero.

- [ ] ¿Entró una migración? → se revisa el DER del núcleo afectado y `DATA-MODEL.md`.
- [ ] ¿Nació un endpoint? → se revisa `SEGURIDAD.md` (qué permiso exige).
- [ ] ¿Nació un término de dominio? → `GLOSSARY.md` (ya es la regla del Art. 20 de
      `CONSTITUTION.md`).
- [ ] ¿Se cierra una fase? → retrospectiva en `ROADMAP.md`, `tasks.md` de la fase cerrado, y
      revisión de `BACKLOG.md` para promover o descartar.
- [ ] ¿Se decidió algo estructural? → ADR, antes de implementar.

### 1.3 Ritmo y bloques

Cada fase se organiza en **bloques**. Dentro de un bloque las ramas son secuenciales
(cada una asume la anterior mergeada). Entre bloques hay un **hito de validación**: no se
empieza el bloque siguiente hasta que el anterior esté mergeado, en verde y probado a mano.

Cuando una feature se alarga más de ~1 semana de trabajo real, **pártela** y anota el resto
en `docs/BACKLOG.md`. Cuando una fase pasa de ~3 meses sin uso real, **recorta alcance**
(Art. 11); en `PLAN-FASE-3-4.md` sec.6 están marcadas cuáles features son sacrificables para
esa fase (secciones de riesgo específicas de cada plan de fase no se trasladan aquí).

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
pruebas triviales. La regla dura sigue siendo la de sec.2.1: caminos de error cubiertos.

### 2.5 Refuerzos de CI a introducir

Se agregan como parte de la primera rama de cada fase:

- **Fase 3, en `feature/people-permissions`:** job de lint/format del backend
  (`dotnet format --verify-no-changes`).
- **Fase 3, en `feature/field-app-scaffolding`:** job de CI para `clients/field-app`
  (typecheck + lint + jest) y para `clients/admin-web` si aún no existe.
- **Fase 4, en `feature/accounting-core`:** job que falla si algún asiento de las suites
  queda descuadrado (ya cubierto por sec.2.3, pero visible como check propio en el PR).
