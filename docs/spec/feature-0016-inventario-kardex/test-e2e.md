# test-e2e.md — Verificación manual del núcleo del kardex

> Escenarios que se ejecutan **en staging** después de que la suite automatizada esté en
> verde. Las pruebas automáticas verifican las piezas; esto verifica el flujo tal como lo va
> a usar el administrador de la finca.
>
> **Este documento cumple tres funciones:**
> 1. Es la verificación antes de promover cada incremento.
> 2. Es el guion de la demostración de aceptación con el cliente: él acepta usando, no
>    revisando pruebas.
> 3. Es el borrador de los manuales por acción del documento de entrega. Cada escenario
>    marcado **📘** es un manual.
>
> Un escenario que falla se reporta con el paso exacto donde falló, no como "no anda".

**Antes de empezar:**

- Staging con el incremento correspondiente desplegado y `verificacion-migracion.sql` sin
  diferencias (TC1.2).
- Un usuario `admin` y un usuario `registrar`.
- Ítems de prueba: "Maíz" (alimento, unidad kg, conversión `saco40kg` = 40 kg, mínimo
  200 kg) e "Ivermectina 1 %" (fármaco, unidad ml).

---

## Incremento 1

## E2E-1 — Conteo inicial 📘 "Levantamiento de inventario inicial"

**Pasos:**
1. Entrar como `admin` → Inventario → Conteo inicial.
2. Maíz: 10 sacos de 40 kg a $16,00 el saco. Ivermectina: 250 ml a $0,08 el ml. Dejar vacías
   las demás filas.
3. Guardar.

**Debe pasar:**
- Saldos muestra Maíz 400 kg, valor $160,00, e Ivermectina 250 ml, valor $20,00.
- Ambos aparecen con la marca de "costo estimado", y el % de valor estimado es 100 %.
- Las filas vacías no crearon nada.

## E2E-2 — El kardex explica el saldo

**Pasos:**
1. Saldos → Maíz → Kardex.

**Debe pasar:**
- Una línea "Saldo inicial", 400 kg, costo $0,40/kg, saldo 400, valor $160,00.
- Las fechas se muestran en hora de Ecuador.

## E2E-3 — Una categoría nueva sin desarrollador 📘 "Crear una categoría"

**Pasos:**
1. Inventario → Categorías → Nueva: "Material genético", sin la marca de alimento.
2. Crear un ítem "Pajuela Duroc" en esa categoría.

**Debe pasar:**
- El ítem aparece en Saldos con saldo 0.
- Asignar una etapa de alimento a ese ítem no es posible.

## E2E-4 — Los datos migrados cuadran (solo en staging, con los datos del piloto anterior)

**Pasos:**
1. Elegir tres ítems que ya existían antes de la migración.
2. Comparar el saldo de cada uno en Saldos con la cantidad que mostraba la sección de lotes
   antes del despliegue (captura tomada en TC1.2).

**Debe pasar:**
- Los tres coinciden.
- Si alguno tiene una línea de ajuste "migración", su cantidad explica exactamente la
  diferencia por lote, y el total coincide igual.

---

## Incremento 2

## E2E-5 — Recepción con factura 📘 "Registrar una compra con factura"

**Pasos:**
1. Inventario → Nueva recepción → con factura: número 001-001-000123, proveedor "Agro
   Andes".
2. Línea: Maíz, 5 sacos a $18,00 el saco. Guardar.

**Debe pasar:**
- Maíz: saldo 600 kg, promedio $0,4167/kg y valor $250,00.
- El % estimado de Maíz baja a 64,0 %.
- El kardex muestra la línea con el número de factura.

## E2E-6 — Recepción sin factura 📘 "Registrar una compra sin factura"

**Pasos:**
1. Nueva recepción → sin factura, respaldo "nota de venta". Ivermectina 100 ml a $0,10.

**Debe pasar:**
- Se guarda sin pedir factura.
- Ivermectina: saldo 350 ml, promedio $0,0857/ml.

## E2E-7 — Salida desde la web y saldo negativo 📘 "Registrar una salida"

**Pasos:**
1. Inventario → Salida → Ivermectina 400 ml, nota "baño lote 3".
2. Aceptar el aviso de saldo insuficiente.
3. Ir a Alertas (tras la generación periódica, o forzarla desde el panel).

**Debe pasar:**
- La salida se guarda y la Ivermectina queda en −50 ml, marcada en rojo.
- Hay una alerta crítica "Saldo negativo: Ivermectina 1 %".
- Tras una recepción de 100 ml y la siguiente generación, la alerta aparece como
  **resuelta**, sin que nadie la haya descartado.

## E2E-8 — Ajuste por merma, solo administrador 📘 "Ajustar el inventario"

**Pasos:**
1. Como `admin`: Inventario → Ajuste → Maíz, disminuir 2 kg, motivo "diferencia de pesaje".
2. Cerrar sesión, entrar como `registrar` y buscar la opción Ajuste.

**Debe pasar:**
- El ajuste aparece en el kardex con su motivo, valorado al promedio.
- El motivo "migración" no está en la lista.
- Como `registrar`, la opción Ajuste no aparece. Llamar a `POST /adjustments` con su token
  responde 403.

## E2E-9 — Stock bajo

**Pasos:**
1. Registrar salidas de Maíz hasta dejarlo en 150 kg (el mínimo es 200 kg).
2. Generar alertas.

**Debe pasar:**
- Hay una alerta de advertencia "Stock bajo: Maíz".
- Después de una recepción que lo deja por encima de 200 kg, la alerta queda resuelta.
