# test-e2e.md — Verificación manual extremo a extremo

> **Plantilla.** Calcada de `docs/planes/field-app-parto-redesign/test-e2e.md` (verificación
> de app, sobre dispositivo real) y de `docs/planes/reestructura-documentacion/test-e2e.md`
> (verificación documental, sobre comandos). Usá la que corresponda al tipo de rama — no
> ambas. Borrá este bloque al usarla.

> Escenarios que se ejecutan después de que la suite automatizada esté en verde. Las pruebas
> unitarias y de integración prueban las piezas; esto prueba que el flujo completo funciona
> tal como lo va a usar quien lo use — un empleado de campo, o quien lea la documentación.
>
> Cada escenario dice cómo prepararlo, qué hacer y qué debe pasar. Un escenario que falla se
> reporta con el paso exacto donde falló, no como "no anda".

**Antes de empezar:**

- `<precondición: rama, servidor corriendo, usuario con qué permisos, commit mínimo>`.

---

## `<ID>-1` — `<nombre corto del escenario>`

> Ejemplo real, `field-app-parto-redesign/test-e2e.md` E2E-2, completo:
>
> ```
> ## E2E-2 — Solo aparecen las preñadas
>
> **Pasos:**
> 1. Sincronizar.
> 2. Inicio → "Un parto".
>
> **Debe pasar:**
> - Aparecen únicamente las hembras con preñez activa.
> - La hembra sin preñez **no aparece**.
> - La hembra con preñez completada **no aparece**.
> - Cada fila muestra su fecha probable de parto.
> - El orden es por fecha probable de parto, la más próxima primero.
> ```

**Preparación:** `<datos o estado que hace falta antes de empezar, si no es el general>`.

**Pasos:**
1. `<acción concreta>`.
2. `<...>`.

**Debe pasar:**
- `<resultado esperado, verificable por observación directa o por comando>`.

---

## Cierre de la verificación

> Los escenarios en verde no cierran la rama por sí solos si `spec.md` tiene criterios de
> aceptación adicionales (suite automatizada, criterios numerados de la sec. de aceptación).
> Enlazalos acá para que no queden solo en la cabeza de quien ejecuta.
