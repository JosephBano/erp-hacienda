# test-e2e.md — Verificación en el teléfono del uso de inventario

> Se ejecuta **en un teléfono Android físico** contra staging, después de que la suite
> automatizada esté en verde. También es el guion de aceptación con el personal del cliente,
> y los escenarios marcados **📘** son el borrador del manual "Registrar uso desde el
> teléfono".
>
> Un escenario que falla se reporta con el paso exacto donde falló.

**Antes de empezar:**

- Staging con feature-0016 (incrementos 1 y 2) y esta rama.
- Maíz con 400 kg y conversión `saco40kg`. Ivermectina con 250 ml.
- Un teléfono con la **versión actualmente instalada** de la app, sesión de `registrar` y un
  consumo por lote pendiente en su outbox (para E2E-5).

---

## E2E-1 — Registrar sin señal 📘

**Pasos:**
1. Sincronizar con señal. Activar el modo avión.
2. Actividades → Registrar uso → Maíz, 2 sacos, nota "lote engorde 1". Guardar.

**Debe pasar:**
- Se guarda al instante, sin esperar red.
- El saldo mostrado de Maíz pasa de 400 kg a 320 kg y dice "al sincronizar de hoy, HH:MM".
- La pantalla de sync muestra 1 pendiente.

## E2E-2 — Aviso sin bloqueo 📘

**Pasos:**
1. Sin señal: Registrar uso → Ivermectina 300 ml.

**Debe pasar:**
- Aparece el aviso "Según lo último que sabe este teléfono, no alcanza".
- "Registrar igual" guarda el uso. "Revisar" vuelve sin guardar.

## E2E-3 — Sincroniza y cuadra

**Pasos:**
1. Quitar el modo avión y sincronizar.
2. En el panel, abrir el kardex de Maíz y de Ivermectina.

**Debe pasar:**
- Los dos usos aparecen con la fecha y hora en que se registraron en el teléfono, no con la
  de sincronización.
- Ivermectina queda en −50 ml y aparece la alerta crítica en el panel.
- En el teléfono, el saldo mostrado se actualiza y los pendientes quedan en 0.

## E2E-4 — Uso con fecha pasada

**Pasos:**
1. Sin señal, registrar un uso de Maíz con la fecha de ayer.
2. Sincronizar hoy.

**Debe pasar:**
- En el kardex del panel, la línea queda en la posición de ayer, no al final.
- El teléfono no permite elegir una fecha futura.

## E2E-5 — Actualizar la app sin perder nada (compuerta)

**Pasos:**
1. Con la versión instalada y un consumo por lote pendiente, sin señal, instalar encima la
   versión de esta rama.
2. Abrir la app y recuperar la señal.

**Debe pasar:**
- La app abre sin reinstalar ni borrar datos.
- El consumo pendiente sigue en el outbox y sube al sincronizar.
- En el panel aparece como una salida con su grupo.

## E2E-6 — El módulo apagado oculta la opción

**Pasos:**
1. En el panel, apagar `inventory.usages`.
2. Sincronizar el teléfono.

**Debe pasar:**
- "Registrar uso" desaparece de Actividades.
- Un uso registrado justo antes, todavía pendiente, sube igual.
