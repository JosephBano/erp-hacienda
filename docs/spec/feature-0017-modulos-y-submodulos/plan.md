# plan.md — Ejecución de la rama `feature/farm-submodules`

> **Qué es este documento.** Dice en qué orden y en qué commits se construye lo que
> [`spec.md`](./spec.md) decidió. Las casillas están en [`tasks.md`](./tasks.md) y la
> verificación en [`test-e2e.md`](./test-e2e.md).

**Objetivo:** que el cliente oculte, desde una sección propia del panel, los módulos y partes
de módulo que no usa, y que el panel y el teléfono lo respeten igual.

**Enfoque:** una rama y **cuatro commits** secuenciales. No depende del inventario nuevo:
es el **incremento 0** del bloque 1 y puede promoverse antes.

**Spec:** [`spec.md`](./spec.md). **Decisión:** [ADR-0042](../../adr/0042-submodulos-de-la-finca.md).

---

## Restricciones globales

- **Una clave sin fila cuenta como encendida** (S2, falla abierta, como hoy).
- **Visible = conjunción de toda la cadena de ancestros** (ADR-0042, decisión 2).
- **Apagar oculta la entrada, nunca bloquea un endpoint** (ADR-0019, regla 4).
- **La sección "Módulos" depende solo del permiso de administrador**, de ningún interruptor
  (ADR-0042, decisión 6).
- **En el teléfono la visibilidad no toca la red** (Art. 9, regla 10).
- **No se divide ningún módulo existente** (regla 9).

## Foco de revisión

1. **Un submódulo encendido con su padre apagado.** Tiene que quedar oculto, y al volver a
   encender el padre, reaparecer con su propio estado → T2.2 y T4.2.
2. **Una clave con mayúsculas o espacios** que llega desde una fila vieja o creada a mano.
   Se normaliza igual en servidor y clientes → T1.1.
3. **Entrar por URL a una pantalla oculta del panel.** Redirige al inicio y no muestra una
   pantalla a medio cargar → T3.3.
4. **Un teléfono que nunca recibió un pull.** Sin filas, todo visible (S2) → T4.1.
5. **Apagar un módulo mientras un `registrar` tiene la pantalla abierta.** Lo pendiente se
   sigue subiendo → E2E-4.

---

## Índice

1. [Commit 1 — Claves jerárquicas en el servidor](#commit-1--claves-jerárquicas-en-el-servidor)
2. [Commit 2 — Visibilidad en el panel](#commit-2--visibilidad-en-el-panel)
3. [Commit 3 — Sección "Módulos" y menú filtrado](#commit-3--sección-módulos-y-menú-filtrado)
4. [Commit 4 — Visibilidad en el teléfono](#commit-4--visibilidad-en-el-teléfono)
5. [Orden, dependencias y puntos de no retorno](#orden-dependencias-y-puntos-de-no-retorno)
6. [Descripción del PR](#descripción-del-pr)

---

## Commit 1 — Claves jerárquicas en el servidor

`feat(people): accept hierarchical farm module keys`

**Por qué acá:** los clientes necesitan que el servidor acepte y devuelva submódulos.

**Archivos:**
- Modificar: `src/Modules/People/Hato.Modules.People.Domain/FarmModule.cs` (validación de
  segmentos).
- Modificar: la consulta y el DTO de `FarmModulesEndpoints.cs` (`parentKey`).
- Crear: `tests/Hato.Modules.People.UnitTests/FarmModuleKeyTests.cs` y ampliar las pruebas
  de integración de `farm-modules`.

**Verificación:** `dotnet test tests/Hato.Modules.People.UnitTests tests/Hato.Modules.People.IntegrationTests` en verde.

## Commit 2 — Visibilidad en el panel

`feat(admin-web): evaluate module visibility along the key chain`

**Archivos:**
- Crear: `clients/admin-web/src/app/services/module-visibility.ts` (función pura
  `isVisible`) y `module-visibility.service.ts` (estado con `signal`), cada uno con su
  `.spec.ts`.

## Commit 3 — Sección "Módulos" y menú filtrado

`feat(admin-web): add the modules section and hide switched-off entries`

**Archivos:**
- Crear: `components/farm-modules/` (árbol, motivo al apagar, estado heredado) con su
  `.spec.ts`, y `guards/module.guard.ts`.
- Modificar: `app.component.html` (cada entrada declara su clave), `app.routes.ts` (ruta
  `/modules` y guards) y `components/catalogs/` (se retira la pestaña de módulos).

## Commit 4 — Visibilidad en el teléfono

`feat(field-app): evaluate module visibility along the key chain`

**Archivos:**
- Modificar: `clients/field-app/src/services/moduleVisibility.ts` (`ModuleKey` a `string`
  validado; `canShow` evalúa la cadena con la misma función pura).
- Modificar: `screens/SyncStatusScreen.tsx` y `screens/ModuleToggle.tsx` (solo lectura sin
  `settings.farm-modules.manage`, S3).
- Crear o ampliar: `src/services/__tests__/moduleVisibility.test.ts`.

**Verificación:** `npm run typecheck`, `npm run lint` y `npm test` en `clients/field-app`.

## Orden, dependencias y puntos de no retorno

```
1 servidor
  ├─ 2 visibilidad web ── 3 sección y menú
  └─ 4 teléfono
```

2–3 y 4 son independientes entre sí. No hay punto de no retorno: no hay migración de
esquema, y revertir deja las filas de submódulo como claves que nadie consulta.

## Descripción del PR

**Título:** `feat: hierarchical farm modules and a dedicated modules section`

**Cuerpo:** qué (submódulos, sección "Módulos", menú filtrado, solo lectura en el teléfono);
por qué (spec sec. 1, ADR-0042); decisiones S1–S4 y D12/D13; qué **no** incluye (crear
submódulos concretos, dividir módulos existentes); cómo probar (`test-e2e.md`); riesgo (dos
implementaciones de la regla, mitigado con la misma tabla de pruebas). S3 confirmada por el
dueño el 2026-09-24.
