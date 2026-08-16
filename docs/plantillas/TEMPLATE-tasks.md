# tasks.md — Desglose ejecutable

> **Plantilla.** Calcada de `docs/planes/field-app-parto-redesign/tasks.md` y de
> `docs/planes/reestructura-documentacion/tasks.md`. Borrá este bloque al usarla.

> Checklist de la rama `<nombre-de-la-rama>`. Cada tarea es una unidad de trabajo con
> criterio de terminado verificable. Agrupadas por el commit de [`plan.md`](./plan.md) al que
> pertenecen.
>
> Convención: `[ ]` pendiente · `[x]` hecho · `[!]` bloqueada.

---

## Commit 1 — `<propósito, igual que en plan.md>`

> Cada tarea lleva un identificador corto y estable (`T1.1`, `T1.2`, ...) porque otros
> documentos y el código la citan por ese nombre, no por su posición en la lista.
>
> **El criterio "Terminado:" es obligatorio cuando la tarea no es autoevidente** — es decir,
> cuando "hice el cambio" no basta para saber si quedó bien. Se escribe como algo que se
> puede correr o leer, no como una opinión.

- [ ] **T1.1** `<qué hay que hacer, en imperativo>`.
      **Terminado:** `<comando exacto y su salida esperada, o el archivo y la condición
      exacta que cumple>`.

> Ejemplo real, `docs/planes/reestructura-documentacion/tasks.md` T5.9:
>
> ```
> - [ ] **T5.9** Verificar que las secciones `2.2` y `3.x` existen con esos números exactos.
>       **Terminado:** `grep -nE '^#+ .*(2\.2|3\.[ABC])' docs/planes/fase-3/spec.md` las
>       encuentra.
> ```

- [ ] **T1.2** `<...>`.

---

## Cierre

> Sección final opcional, si la rama tiene un cierre común a todos los commits (correr la
> suite completa, abrir el PR). Ejemplo real, `reestructura-documentacion/tasks.md`:
> `TC.3` — *"`dotnet test` en verde. Esta rama no toca código ejecutable: cualquier fallo es
> una regresión ajena que hay que detectar antes del merge."*

- [ ] **TC.1** Ejecutar [`test-e2e.md`](./test-e2e.md) completo.
