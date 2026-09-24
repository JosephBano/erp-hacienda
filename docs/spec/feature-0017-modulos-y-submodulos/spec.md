# spec.md — Módulos y submódulos (bloque 1, incremento 0)

> **Qué es este documento.** Fija cómo se amplían los interruptores de módulo del ADR-0019
> con submódulos ([ADR-0042](../../adr/0042-submodulos-de-la-finca.md)), la sección
> "Módulos" del panel y el filtrado del menú web. `plan.md`, `tasks.md` y `test-e2e.md` de
> esta carpeta dicen en qué orden, con qué casillas y cómo se verifica.
>
> **Parte del bloque 1.** Las decisiones del bloque viven en
> [feature-0016](../feature-0016-inventario-kardex/spec.md) sec. 3 y aquí se citan por
> número. Esta spec implementa **D12** (el mecanismo) y **D13**.

- **Rama Git:** `feature/farm-submodules` (desde `develop`).
- **Fecha:** 2026-09-23.
- **Fase del ROADMAP:** transversal. Lo estrena el bloque 1 de la Fase 4 adelantada.
- **ADRs que respalda o respeta:** ADR-0042 (el que implementa), ADR-0019 (sus reglas no
  cambian), ADR-0007 (permisos).
- **Reglas duras de `AGENTS.md`:** 3 (nada de `if` por módulo concreto en el dominio), 5,
  7, 9 (no se dividen módulos ajenos) y 10 (la visibilidad en el teléfono no depende de la
  red).

---

## Índice

1. [Por qué existe este spec](#1-por-qué-existe-este-spec)
2. [Hallazgos verificados](#2-hallazgos-verificados)
3. [Decisiones fijadas](#3-decisiones-fijadas)
4. [Alcance](#4-alcance)
5. [Diseño: regla de visibilidad](#5-diseño-regla-de-visibilidad)
6. [Diseño: servidor](#6-diseño-servidor)
7. [Diseño: panel web](#7-diseño-panel-web)
8. [Diseño: teléfono](#8-diseño-teléfono)
9. [Riesgos y deuda](#9-riesgos-y-deuda)
10. [Criterios de aceptación](#10-criterios-de-aceptación)

---

## 1. Por qué existe este spec

El cliente del bloque 1 pidió activar y desactivar lo que no usa, en una sección propia del
panel, y que el teléfono lo refleje al sincronizar. El inventario nuevo tiene partes, como
"Preparar alimento", que una finca puede no necesitar. Hoy los interruptores son de módulo
completo, y el panel ni siquiera los respeta.

## 2. Hallazgos verificados

Leyendo `develop` en `5276e14`:

1. **El teléfono evalúa por clave exacta, con una lista cerrada.** `ModuleKey` es la unión
   `'production' | 'livestock' | 'inventory' | 'breeding' | 'tasks' | 'people'`, y
   `canShow` busca la fila con esa clave exacta. Si no existe fila, muestra la entrada (falla
   abierta, a propósito)
   (`clients/field-app/src/services/moduleVisibility.ts:14`, `:42-53`).
2. **Cualquier usuario puede encender y apagar módulos desde el teléfono.** "Módulos del
   dispositivo", en `SyncStatusScreen.tsx:131` y `:257`, llama a `setEnabled`, que escribe
   la fila local e intenta avisar al servidor. El `PATCH /api/v1/farm-modules/{key}` exige
   `SettingsFarmModulesManage` (`src/Hato.Api/Endpoints/FarmModulesEndpoints.cs:26`). Para un
   usuario sin ese permiso, el cambio queda solo en ese teléfono hasta que el siguiente pull
   lo corrija.
3. **El panel no respeta los interruptores.** Los interruptores están en una pestaña de
   Catálogos (`clients/admin-web/src/app/components/catalogs/catalogs.component.ts:125`,
   `:453`), y el menú es una lista fija de `routerLink` en
   `clients/admin-web/src/app/app.component.html` que nada filtra.
4. **Siembra actual:** los seis módulos, con `production` apagado
   (`…/Migrations/20260807031453_AddFarmModulesAndSettingsPermissions.cs:74-86`).

## 3. Decisiones fijadas

Además de D12 y D13 de feature-0016:

- **S1 — La regla vive en un solo lugar por cliente.** Una función pura,
  `isVisible(key, rows)`, en el teléfono y otra idéntica en el panel. Las dos llevan la misma
  tabla de pruebas.
- **S2 — Falla abierta, igual que hoy.** Una clave sin fila cuenta como encendida en todos
  los niveles. Si un submódulo no tiene fila, lo decide su padre.
- **S3 — En el teléfono, los interruptores solo se pueden cambiar con permiso de
  administrador.** Para los demás usuarios, "Módulos del dispositivo" se muestra en solo
  lectura. *(Propuesta de esta spec, pendiente de confirmar con el dueño: evita que un
  empleado apague un módulo en su teléfono sin que el panel lo sepa.)*
- **S4 — Ocultar en el panel también protege la ruta.** Un guard de Angular redirige al
  inicio si se entra por URL a una pantalla oculta. Es una decisión de navegación, no de
  seguridad: los permisos siguen siendo los de ADR-0007.

## 4. Alcance

### Entra

- Claves jerárquicas en `FarmModule` (validación del formato con punto, sin cambiar el
  esquema).
- `GET /api/v1/farm-modules` devuelve también los submódulos, con su `parentKey` derivada.
- Sección **"Módulos"** en el panel: árbol con padres e hijos, motivo obligatorio al apagar
  (ADR-0019) y permiso `SettingsFarmModulesManage`. La pestaña de Catálogos se retira.
- Menú y rutas del panel filtrados por `isVisible`.
- En el teléfono: `canShow` evalúa la cadena, y `ModuleKey` admite claves de submódulo.
  "Módulos del dispositivo" pasa a solo lectura sin el permiso (S3).

### No entra

- **Crear submódulos concretos.** Los de inventario los siembran feature-0016 y
  feature-0019.
- **Dividir `breeding`, `livestock` u otros.** Regla 9 (ADR-0042, decisión 5).
- **Filtrar el menú del panel por permisos de usuario.** No está pedido, y mezclaría dos
  ejes.

## 5. Diseño: regla de visibilidad

```
isVisible("a.b.c") = on("a") ∧ on("a.b") ∧ on("a.b.c")
on(k) = fila(k)?.enabled ?? true
```

Tabla mínima de pruebas, idéntica en los dos clientes:

| Filas | Clave consultada | Resultado |
|---|---|---|
| ninguna | `inventory.transformations` | visible |
| `inventory`=off | `inventory.transformations` | oculto |
| `inventory`=on, `inventory.transformations`=off | `inventory.transformations` | oculto |
| `inventory`=on, `inventory.transformations`=off | `inventory` | visible |
| `inventory`=off, `inventory.transformations`=on | `inventory.transformations` | oculto |
| `inventory`=on | `inventory.usages` (sin fila) | visible |

## 6. Diseño: servidor

- `FarmModule.Create` acepta claves con punto: segmentos en minúsculas, `[a-z0-9_]`, sin
  segmentos vacíos. La validación va en el dominio.
- **No hay migración de esquema:** `key` ya es texto.
- La consulta del listado devuelve `parentKey` (lo que está antes del último punto), para
  que los clientes no tengan que calcularlo.
- `PATCH /farm-modules/{key}` funciona igual para cualquier nivel.
- Apagar un padre **no escribe** en los hijos (ADR-0042, decisión 2).

## 7. Diseño: panel web

- **Servicio de visibilidad** (Angular, `signal`): carga los interruptores al iniciar sesión
  y después de cada cambio en "Módulos", y expone `isVisible(key)`.
- **Menú:** cada entrada de `app.component.html` declara su clave. Las que no dependen de un
  módulo (inicio, roles, auditoría, sync, Módulos) no la llevan y siempre se ven.
- **Rutas:** un guard `moduleGuard(key)` en las rutas que dependen de un módulo.
- **Sección "Módulos":** ruta `/modules`, un árbol con cada módulo y sus submódulos, y el
  estado heredado visible ("apagado por su padre").

## 8. Diseño: teléfono

- `ModuleKey` pasa de unión cerrada a `string` validado. Las pantallas siguen pidiendo
  claves conocidas, pero la regla ya no las restringe.
- `canShow(key)` lee las filas de la cadena y aplica `isVisible`. Sigue sin tocar la red
  (Art. 9).
- "Módulos del dispositivo": los interruptores solo se pueden tocar si el usuario tiene
  `SettingsFarmModulesManage` en su sesión local (S3).
- El pull de `farmModules` no cambia: ya baja todas las filas.

## 9. Riesgos y deuda

| Riesgo | Mitigación |
|---|---|
| Las dos implementaciones de la regla divergen | Misma tabla de pruebas en ambos clientes (sec. 5) |
| Un teléfono con la app vieja ignora los submódulos | Los endpoints aceptan igual (ADR-0019 regla 4). Se avisa en la nota de versión |
| Se oculta la sección "Módulos" por error | No depende de ningún interruptor, solo del permiso (ADR-0042, decisión 6) |
| Un empleado apaga un módulo en su teléfono | S3: solo lectura sin permiso |

Deuda anotada en `docs/BACKLOG.md`: dividir otros módulos en submódulos cuando un cliente lo
pida.

## 10. Criterios de aceptación

1. Las pruebas de `isVisible` pasan con la tabla de la sec. 5, en el panel y en el teléfono.
2. Con `inventory` apagado, ninguna entrada de inventario aparece en el menú del panel, y
   entrar por URL a una de ellas redirige al inicio.
3. La sección "Módulos" muestra los submódulos anidados bajo su padre, y apagar uno pide
   motivo.
4. `FarmModule.Create("inventory..x")` y `FarmModule.Create("Inventory.X ")` se comportan
   así: la primera se rechaza y la segunda se normaliza a `inventory.x`.
5. Con un usuario `registrar`, los interruptores de "Módulos del dispositivo" no se pueden
   cambiar.
6. Una operación registrada en el teléfono antes de apagar su módulo sube y se acepta.
