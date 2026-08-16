# spec.md — Fase 4: Dinero completo (ventas, compras, costos)

> **Qué es este documento.** Fija qué debe ser verdad para que la Fase 4 pueda cerrarse y qué
> deuda de la Fase 3.5 tiene que jubilar en el camino. **No es un plan.** Esta fase no tiene
> ni ADR ni código propio todavía — está a uno o dos años de distancia. Por eso esta carpeta
> tiene **solo `spec.md`**: nada de `plan.md`, `tasks.md` ni `test-e2e.md`. Un checklist de
> micro-tareas para un trabajo que "llegará cuando deba llegar" es ficción que envejece mal y
> da una falsa sensación de plan.
>
> **Por qué esta carpeta y no un documento suelto.** Sigue el mismo patrón de
> `docs/planes/<fase>/` que el resto del ROADMAP, para que cuando la Fase 4 sí se planifique,
> `plan.md` y `tasks.md` tengan dónde nacer sin reorganizar nada.

- **Fase del ROADMAP:** Fase 4 — Dinero completo: ventas, compras, costos
  (`docs/ROADMAP.md:220-231`), **no iniciada**.
- **ADRs vigentes que respalda:** ninguno todavía. Los que este trabajo necesitará (elección
  del proveedor SRI, modelo contable) se citan como pendientes en la sec. 3.
- **Reglas duras que gobiernan este trabajo:** Art. 6 (contrato público entre módulos, cero
  SQL cruzado — en particular, Accounting solo recibe, no consulta a nadie), Art. 18 (el
  contador no se reemplaza; el SRI no se integra directamente), Art. 19 (retiro bloqueante,
  no advertencia), Art. 1 (historia inmutable, corrección con evento nuevo).

---

## Índice

1. [Objetivo y criterio de salida](#1-objetivo-y-criterio-de-salida)
2. [Deuda heredada de la Fase 3.5](#2-deuda-heredada-de-la-fase-35)
3. [Preguntas abiertas antes de planificar](#3-preguntas-abiertas-antes-de-planificar)
4. [Apéndice — Planificación previa (2026-08-02): insumo, no compromiso](#4-apéndice--planificación-previa-2026-08-02-insumo-no-compromiso)

---

## 1. Objetivo y criterio de salida

**Objetivo** (`docs/ROADMAP.md:221`): saber cuánto cuesta y cuánto deja cada cosa.

El ROADMAP describe la fase en tres módulos y una regla transversal
(`docs/ROADMAP.md:223-228`):

- **Sales**: ventas de leche (con calidad/precio), de animales (genera baja), clientes,
  cuentas por cobrar. Comprobantes vía **proveedor autorizado SRI** (API).
- **Purchasing**: proveedores, compras de insumos, cuentas por pagar.
- **Accounting**: asientos automáticos desde todos los módulos, centros de costo (lechería,
  porcinos…), costo por animal-día, margen por línea. Reportes para el contador.
- Bloqueo de venta por período de retiro operativo de punta a punta.

**Criterio de salida** (`docs/ROADMAP.md:230-231`): cerrar un mes contable real — ingresos,
costos por centro, y el reporte que el contador acepta usar.

Las exigencias de pruebas de dinero para esta fase (golden test de mes contable, umbral de
suites, checks de CI sobre asientos descuadrados) ya viven en
`docs/PROTOCOLO-DE-TRABAJO.md` sec. 2.3–2.5 y no se repiten aquí — duplicarlas sería la
misma falla de documentación que esta reestructura vino a eliminar.

## 2. Deuda heredada de la Fase 3.5

La Fase 3.5 adelantó cinco piezas de Fase 4 porque el piloto porcino no podía esperar a
Purchasing. Están documentadas, con su contrato de caducidad citado en código, en
[`docs/planes/fase-3-5/spec.md` sec. 2.9 — "Adelantos de Fase 4 que viven aquí"](../fase-3-5/spec.md#29-adelantos-de-fase-4-que-viven-aquí).
Esta sección **no repite esa tabla** — solo resume qué debe jubilar la Fase 4 antes de poder
cerrarse, y remite a la fuente para el detalle exacto (archivo, línea, cita literal).

Las cinco piezas, en orden de aparición en esa tabla:

1. **Recepción de inventario pre-Purchasing** (ADR-0026, PR #94). Caduca cuando exista una
   orden de compra real que la reemplace; el propio ADR y `docs/ROADMAP.md:167` lo declaran.
2. **Aviso en la UI de admin-web** sobre el flujo de recepción. Caduca junto con el punto 1 —
   es su contraparte visible para el usuario.
3. **Evento de recepción sin outbox** (`InventoryReceptionRecorded`). Caduca si Fase 4
   introduce un consumidor downstream durable que lo necesite; hasta entonces, MediatR
   in-process alcanza.
4. **FK cross-schema diferida** (`AnimalEvent.BatchId` hacia `inventory_batches`, y hacia
   `people.users`). Caduca cuando la arquitectura de Fase 4 decida cómo conectar los módulos
   con una FK estricta entre esquemas.
5. **Subida de fotos en tratamientos sin almacenamiento remoto**
   (`clients/field-app/src/services/eventService.ts:163`). Caduca con el módulo de adjuntos
   (`feature/shared-attachments`, bloque 4.A del apéndice, sec. 4).

Ninguna de las cinco bloquea el piloto de la Fase 3.5. Pero **la Fase 4 no puede darse por
cerrada mientras alguna siga viva** en su forma de adelanto: cerrar la fase implica jubilar
las cinco, reemplazándolas por el mecanismo real (orden de compra, outbox, FK estricta,
almacenamiento remoto de adjuntos) que el criterio de salida de la sec. 1 exige.

## 3. Preguntas abiertas antes de planificar

Estas preguntas tienen que responderse — con el proveedor, el contador, o una decisión del
dueño del proyecto documentada en un ADR — antes de que exista un `plan.md` real para esta
fase. No son una lista exhaustiva de todo lo que la fase tocará; son los bloqueos conocidos
hoy para poder empezar a planificar en serio.

- **¿Qué proveedor autorizado del SRI se usa?** El ROADMAP y el Art. 18 exigen que la
  facturación electrónica pase por un proveedor autorizado, no una integración directa al
  SRI. Falta elegir cuál (costos, sandbox, comportamiento ante caídas) — es la base de
  ADR-0012 en el apéndice (sec. 4, bloque 4.C).
- **¿Qué plan de cuentas se usa?** `Accounting` necesita un plan de cuentas configurable en
  BD, revisado con el contador antes de sembrarlo — no un plan agropecuario genérico elegido
  sin validación externa.
- **¿Qué formato de reporte acepta realmente el contador?** El criterio de salida de la fase
  es que el contador use el reporte, no que el sistema lo produzca. El formato de exportación
  (CSV/Excel, columnas, separadores, decimales) se acuerda con el contador antes de
  codificar, no después.
- **¿Cómo se conecta la FK cross-schema diferida (deuda de la sec. 2, punto 4) sin romper el
  Art. 6?** La arquitectura de Fase 4 tiene que decidir el mecanismo antes de que
  `accounting-postings` pueda escuchar eventos de todos los módulos sin consultarlos.
- **¿Qué proveedor de almacenamiento de adjuntos se usa** (disco local con ruta configurable
  vs. S3 compatible)? Es la base de ADR-0010 en el apéndice — condiciona tanto las guías de
  movilización de Fase 4 como las fotos de tratamientos que hoy quedan solo en el teléfono.

## 4. Apéndice — Planificación previa (2026-08-02): insumo, no compromiso

> **Este apéndice es trabajo de diseño conservado para no tirarlo — no es un plan
> aprobado.** Se escribió el 2026-08-02 en el plan superado de las Fases 3 y 4, sec. 4, antes de
> que existiera ni un ADR ni una línea de código de Fase 4. Ningún bloque, orden de bloques,
> ni número de rama listado aquí está comprometido: son ideas de forma, no un compromiso de
> ejecución. Cuando la Fase 4 se planifique de verdad, ese `plan.md` puede confirmar este
> orden, cambiarlo por completo, o ignorarlo — la decisión se toma entonces, con el contexto
> de ese momento, no con este.

El plan previo proponía cuatro bloques y once ramas, en este orden:

**Bloque 4.A — Cimientos transversales (2 ramas).** `feature/shared-attachments` (adjuntos
documentales: ADR-0010, tabla `attachments`, endpoints de subida/descarga con permisos,
integración con el outbox del móvil) y `feature/shared-money-units` (política de dinero:
`Money`/`Quantity` en SharedKernel, `RoundingPolicy` única, tabla de unidades y conversiones).

**Bloque 4.B — Contabilidad receptora (3 ramas).** `feature/accounting-core` (ADR-0011,
plan de cuentas configurable, partida doble, períodos, asientos inmutables con reversión —
rama marcada **estructural**), `feature/accounting-cost-centers` (centros de costo,
prorrateo por animal-día) y `feature/accounting-postings` (handlers que escuchan eventos de
dominio y emiten asientos sin que Accounting consulte a nadie, mapeo evento→cuentas
configurable en BD, idempotencia por `source_event_id` — también **estructural**).

**Bloque 4.C — Los emisores (4 ramas).** `feature/sales-customers-milk` (clientes, venta de
leche con bloqueo de retiro punta a punta), `feature/sales-animals` (venta con baja del
animal, guía de movilización), `feature/purchasing-suppliers` (proveedores, compras que
alimentan lotes de Inventory por contrato público) y `feature/sales-sri-invoicing` (ADR-0012,
elección del proveedor SRI, cliente HTTP con reintentos, modo sandbox obligatorio en
desarrollo — **estructural**).

**Bloque 4.D — Salida y cierre (3 ramas).** `feature/accounting-reports` (libro diario y
mayor, balance de comprobación, estado de resultados, exportación en el formato acordado con
el contador), `feature/admin-web-finance` (panel Angular de ventas, compras, contabilidad) y
`docs/fase-4-cierre` (retrospectiva: ROADMAP, `LEGAL-ECUADOR.md`, `ARCHITECTURE.md`,
`docs/BACKLOG.md`).

El texto completo, con cada tarea numerada y las pruebas propuestas por rama, no se
trasladó a esta carpeta (D5 de `docs/planes/reestructura-documentacion/spec.md`: escribir
`plan.md`/`tasks.md`/`test-e2e.md` de la Fase 4 no entra en esta rama) y solo sigue
disponible en el historial de git, en la versión del plan superado de las Fases 3 y 4
previa a su borrado (commit 11 de `docs/reestructura-documentacion`).

Esa misma versión, en su sec. 5, listaba los ADRs que anticipaba para esta fase — también
insumo, no compromiso:

| ADR | Tema | Antes de la rama |
|---|---|---|
| 0010 | Almacenamiento de adjuntos y su respaldo | `feature/shared-attachments` |
| 0011 | Modelo contable: plan de cuentas, partida doble, períodos | `feature/accounting-core` |
| 0012 | Proveedor autorizado de facturación electrónica | `feature/sales-sri-invoicing` |

Recuerda el Art. 14 y la regla de "dormir una noche sobre la decisión": el ADR se escribe,
se deja reposar, se acepta, y **después** se codifica.

> **Nota de discrepancia (no del contenido original, agregada al trasladarlo):** los
> números `ADR-0010`–`ADR-0012` ya están tomados en `docs/adr/` por ADRs reales de la Fase
> 3 (EAS Build, revisión de dependencias de navegación, WatermelonDB JSI diferido) — la
> numeración se reutilizó porque estos tres nunca se escribieron. La sec. 1 y la sec. 3 de
> este mismo documento ya asumían esa misma numeración reservada para Fase 4 antes de este
> traslado, así que no es una inconsistencia nueva; cuando esta fase se planifique de
> verdad, estos tres ADRs necesitarán números nuevos.

Esa misma versión también anotaba, en su sec. 6, dos riesgos propios de esta fase y una
pieza de alcance sacrificable si hay que recortar:

| Riesgo | Señal temprana | Qué hacer |
|---|---|---|
| La contabilidad crece sin control | Empiezas a querer reemplazar al contador | Art. 18: el sistema genera datos limpios, no declara impuestos |
| El proveedor SRI resulta caro o malo | Sandbox frustrante | La feature es aislable: el resto de la Fase 4 cierra sin ella y la facturación se hace fuera del sistema un mes más |

**Features sacrificables si hay que recortar alcance (Art. 11):**
`feature/sales-sri-invoicing` y `feature/accounting-cost-centers` (el margen por centro
puede esperar; el libro diario no).

Y, en su resumen de ramas, el orden propuesto para esta fase:

**Fase 4** (12 ramas)
```
feature/shared-attachments
feature/shared-money-units
feature/accounting-core
feature/accounting-cost-centers
feature/accounting-postings
feature/sales-customers-milk
feature/sales-animals
feature/purchasing-suppliers
feature/sales-sri-invoicing
feature/accounting-reports
feature/admin-web-finance
docs/fase-4-cierre
```
