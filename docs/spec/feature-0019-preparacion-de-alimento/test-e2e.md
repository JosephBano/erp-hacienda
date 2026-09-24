# test-e2e.md — Verificación manual de la preparación de alimento

> Se ejecuta en staging después de que la suite automatizada esté en verde. También es el
> guion de aceptación con el cliente, y los escenarios marcados **📘** son el borrador del
> manual "Preparar alimento".
>
> Un escenario que falla se reporta con el paso exacto donde falló.

**Antes de empezar:**

- Staging con feature-0016 (incrementos 1 y 2) y esta rama.
- Ítems con saldo: Maíz 500 kg a $0,40, Soya 200 kg a $0,60 y Núcleo vitamínico 20 kg a
  $2,00. "Balanceado engorde" creado, sin saldo.
- Un usuario `admin` y uno `registrar`.

---

## E2E-1 — Preparar una mezcla con servicio 📘

**Pasos:**
1. Como `admin`: Inventario → Preparar alimento.
2. Insumos: Maíz 300 kg, Soya 100 kg, Núcleo 10 kg.
3. Servicio: $20,00, "peletizado".
4. Producto: Balanceado engorde, 400 kg.
5. Revisar el costo que muestra antes de guardar, y guardar.

**Debe pasar:**
- Antes de guardar muestra **$0,55 por kg**.
- Saldos: Maíz 200 kg, Soya 100 kg, Núcleo 10 kg y Balanceado engorde 400 kg por $220,00.
- La orden muestra una merma implícita de 10 kg.

## E2E-2 — El kardex explica de dónde sale el costo

**Pasos:**
1. Kardex de Balanceado engorde. Kardex de Maíz.

**Debe pasar:**
- Balanceado: una línea "Producto de preparación", 400 kg a $0,55, con enlace a la orden.
- Maíz: una línea "Insumo de preparación", −300 kg a $0,40.

## E2E-3 — Preparar sin servicio 📘

**Pasos:**
1. Preparar: Maíz 100 kg y Soya 50 kg, sin servicio, producto Balanceado engorde 150 kg.

**Debe pasar:**
- Se guarda con servicio en $0,00.
- El costo de esa orden es (40 + 30) ÷ 150 = **$0,4667 por kg**.

## E2E-4 — Solo el administrador prepara

**Pasos:**
1. Entrar como `registrar` y buscar "Preparar alimento".

**Debe pasar:**
- La opción no aparece. `POST /transformations` con su token responde 403.

## E2E-5 — Una finca que no fabrica alimento

**Pasos:**
1. Módulos → Inventario → apagar "Transformaciones", con motivo.

**Debe pasar:**
- "Preparar alimento" desaparece del menú. El resto del inventario sigue visible.
