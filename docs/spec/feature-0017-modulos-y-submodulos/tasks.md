# tasks.md — Desglose ejecutable de `feature/farm-submodules`

> Checklist agrupada por el commit de [`plan.md`](./plan.md). Cada tarea sigue el ciclo de
> pruebas primero. Las restricciones globales y el foco de revisión de `plan.md` aplican a
> todas.
>
> Convención: `[ ]` pendiente · `[x]` hecho · `[!]` bloqueada.

**Tabla de visibilidad compartida.** Se copia **idéntica** en las pruebas de T2.1 y T4.1
(spec sec. 5):

| Filas | Consulta | Resultado |
|---|---|---|
| ninguna | `inventory.transformations` | visible |
| `inventory`=off | `inventory.transformations` | oculto |
| `inventory`=on, `inventory.transformations`=off | `inventory.transformations` | oculto |
| `inventory`=on, `inventory.transformations`=off | `inventory` | visible |
| `inventory`=off, `inventory.transformations`=on | `inventory.transformations` | oculto |
| `inventory`=on | `inventory.usages` (sin fila) | visible |

**Contrato común de los clientes:**

```ts
export interface ModuleRow { key: string; enabled: boolean }
export function normalizeModuleKey(raw: string): string;              // "Inventory.X " → "inventory.x"
export function isVisible(key: string, rows: readonly ModuleRow[]): boolean;
```

---

## Commit 1 — Claves jerárquicas en el servidor

- [ ] **T1.1** Pruebas en `FarmModuleKeyTests`: `"Inventory.X "` → `"inventory.x"`;
      `"inventory..x"`, `".inventory"`, `"inventory."` y `"inv entory"` se rechazan;
      `"inventory.transformations"` se acepta. Implementar la validación en
      `FarmModule.Create`.
- [ ] **T1.2** `GET /api/v1/farm-modules` devuelve `parentKey` (`null` para módulos de primer
      nivel). Prueba de integración.
- [ ] **T1.3** Prueba: `PATCH /farm-modules/inventory` a `false` **no** cambia la fila de
      `inventory.transformations`.
- [ ] **T1.4** Commit `feat(people): accept hierarchical farm module keys`.

## Commit 2 — Visibilidad en el panel

- [ ] **T2.1** `module-visibility.spec.ts` con la tabla compartida. Verla fallar e
      implementar `isVisible` y `normalizeModuleKey`.
- [ ] **T2.2** `module-visibility.service.spec.ts`: carga las filas al iniciar sesión, las
      recarga tras un cambio, y `isVisible` es reactivo (`computed`).
- [ ] **T2.3** Commit `feat(admin-web): evaluate module visibility along the key chain`.

## Commit 3 — Sección "Módulos" y menú filtrado

- [ ] **T3.1** `farm-modules.component.spec.ts`: los hijos se muestran bajo su padre; apagar
      pide motivo; un hijo con el padre apagado dice "apagado por su padre".
- [ ] **T3.2** Pruebas de `app.component`: con `breeding` apagado, la entrada "Reproducción"
      no se renderiza; "Módulos", "Roles", "Auditoría" y "Sync" siempre se ven.
- [ ] **T3.3** `module.guard.spec.ts`: navegar a `/breeding` con `breeding` apagado redirige
      a `/`.
- [ ] **T3.4** Retirar la pestaña de módulos de `catalogs` y sus pruebas.
- [ ] **T3.5** **Terminado:** lint, pruebas y build del panel en verde.
- [ ] **T3.6** Commit `feat(admin-web): add the modules section and hide switched-off entries`.

## Commit 4 — Visibilidad en el teléfono

- [ ] **T4.1** `moduleVisibility.test.ts` con la tabla compartida, más "sin ninguna fila en
      la base local → visible". Verla fallar.
- [ ] **T4.2** `canShow` lee de WatermelonDB las filas de la cadena (`inventory` y
      `inventory.transformations`) y aplica `isVisible`. Sin llamadas de red.
- [ ] **T4.3** `SyncStatusScreen`/`ModuleToggle`: prueba que con una sesión sin
      `settings.farm-modules.manage` los interruptores están deshabilitados y `setEnabled` no
      se llama.
- [ ] **T4.4** **Terminado:** `npm run typecheck && npm run lint && npm test` en
      `clients/field-app` en verde.
- [ ] **T4.5** Commit `feat(field-app): evaluate module visibility along the key chain`.

---

## Cierre

- [ ] **TC.1** `dotnet test Hato.sln` completo en verde.
- [ ] **TC.2** [`test-e2e.md`](./test-e2e.md) completo en staging y en un teléfono físico.
- [ ] **TC.3** PR con la descripción de `plan.md`.
