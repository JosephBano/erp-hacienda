# test-e2e.md — Verificación manual de módulos y submódulos

> Se ejecuta en staging y en un teléfono físico, después de que la suite automatizada esté
> en verde. También es el guion de aceptación con el cliente, y los escenarios marcados
> **📘** son el borrador del manual "Activar y desactivar módulos".
>
> Un escenario que falla se reporta con el paso exacto donde falló.

**Antes de empezar:**

- Staging con esta rama desplegada.
- Un usuario `admin` y un usuario `registrar`, ambos con sesión en el panel y en un teléfono
  con la app de esta rama.
- Para probar la jerarquía sin depender del inventario nuevo, crear a mano la fila
  `inventory.test` con `PATCH /api/v1/farm-modules/inventory.test`.

---

## E2E-1 — La sección "Módulos" 📘

**Pasos:**
1. Como `admin`: menú → Módulos.

**Debe pasar:**
- Aparecen los seis módulos, y `inventory.test` anidado bajo Inventario.
- La pestaña de módulos ya no está en Catálogos.

## E2E-2 — Apagar un módulo lo oculta en el panel 📘

**Pasos:**
1. Módulos → Reproducción → apagar, con motivo "no se usa en esta finca".
2. Mirar el menú.
3. Escribir a mano `/breeding` en la barra del navegador.

**Debe pasar:**
- Pide el motivo antes de apagar.
- "Reproducción" desaparece del menú.
- La URL redirige al inicio.

## E2E-3 — El teléfono lo refleja al sincronizar 📘

**Pasos:**
1. En el teléfono: sincronizar.
2. Buscar las actividades de reproducción.
3. Módulos del dispositivo, con la sesión de `registrar`.

**Debe pasar:**
- Las actividades de reproducción ya no aparecen.
- Como `registrar`, los interruptores se ven pero no se pueden cambiar.

## E2E-4 — Lo registrado antes de apagar no se pierde

**Pasos:**
1. En el teléfono, en modo avión, registrar un evento de un módulo encendido (por ejemplo,
   un evento de lote).
2. En el panel, apagar ese módulo.
3. Quitar el modo avión y sincronizar.

**Debe pasar:**
- El registro sube y aparece en el panel.
- Después de sincronizar, la entrada del módulo ya no se ve en el teléfono.

## E2E-5 — El padre manda sobre el hijo 📘

**Pasos:**
1. Módulos → dejar `inventory.test` encendido y apagar Inventario.
2. Volver a encender Inventario.

**Debe pasar:**
- Con Inventario apagado, `inventory.test` se muestra "apagado por su padre".
- Al encender Inventario, `inventory.test` vuelve encendido, con su propio estado.
