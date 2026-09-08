# Índice de `docs/spec/`

Las carpetas `feature-*` numeran **trabajos**, no su orden de ejecución. El número se
asigna cuando el trabajo se especifica y no se renumera después, para no romper las
referencias cruzadas entre documentos. Este índice existe porque en la serie 0004–0010 el
número y el orden real dejaron de coincidir.

## Serie de estabilización en producción (2026-09-07)

Siete features especificadas sobre `0faf483` a partir del reporte del dueño tras tres
semanas de uso real. **Ninguna está implementada.** Cada carpeta tiene los cuatro documentos
de la convención —`spec.md`, `plan.md`, `tasks.md` y `test-e2e.md`— siguiendo el precedente de
[`feature-0002`](./feature-0002-field-app-parto-redesign/) y las plantillas de
`docs/plantillas/`.

Los `spec.md` se escribieron primero y solos, por petición expresa («solo spec no plan ni
task»); los otros tres documentos se añadieron después, a pedido del dueño. Que existan **no
cierra ningún criterio de producción**.

**Cada rama arranca con una compuerta.** Todas las features tienen al menos una decisión o
evidencia pendiente que puede recortar su alcance —o detenerla— antes de escribir código. Las
compuertas están al principio de cada `plan.md` y como tareas `T0.x` / `TG0.x` en cada
`tasks.md`. No son trámite: la Compuerta 0 de 0007 puede dejar esa rama en tres commits, y la
de 0010 detiene el desarrollo de veinte pantallas.

| # | Carpeta | Propósito |
|---|---|---|
| 0004 | [`field-app-sync-reliability`](./feature-0004-field-app-sync-reliability/spec.md) | Enviar, recibir y mostrar producen un estado consistente sin perder registros. |
| 0005 | [`field-app-activity-validation`](./feature-0005-field-app-activity-validation/spec.md) | Cada actividad ofrece sujetos válidos; invariantes repetidas en el servidor. |
| 0006 | [`field-app-interaction-reliability`](./feature-0006-field-app-interaction-reliability/spec.md) | Todo control es alcanzable; un gesto no altera el hecho registrado. |
| 0007 | [`field-app-individual-tagged-livestock`](./feature-0007-field-app-individual-tagged-livestock/spec.md) | Operar con individuos identificados por arete conservando el manejo por grupo. |
| 0008 | [`people-permission-enforcement`](./feature-0008-people-permission-enforcement/spec.md) | Permisos aplicados de forma equivalente en REST y en push. |
| 0009 | [`inventory-consumption-batch-attribution`](./feature-0009-inventory-consumption-batch-attribution/spec.md) | Saber de qué compra salió el alimento que consumió un grupo. |
| 0010 | [`field-app-redesign`](./feature-0010-field-app-redesign/spec.md) | Rediseño integral de la experiencia móvil. |

### Orden de implementación propuesto

No es el orden de los números. Se deriva de las dependencias que cada spec declara.

**Paso 0 — Recolección de evidencia.** Cuatro specs repiten la misma laguna: no se conocen
build de los teléfonos, versión del backend, ni qué significó «eliminar» (baja, borrado
lógico, desactivación o SQL manual). Media hora con el dueño y un teléfono desbloquea
0004, 0005, 0006 y 0007. Es lo más barato de la serie y lo primero.

1. **0006** — Independiente y entregable de inmediato. Corrige la queja concreta de los
   empleados sin esperar a ningún otro spec.
2. **0004** — Núcleo. Contiene el defecto vivo de ordeño (sec. 1.1) y el arnés de contrato
   que impide que la clase de defecto se repita. Todo lo demás del móvil se apoya aquí.
3. **0008** — Transversal. Define el permiso de consumo que 0009 necesita y la semántica
   de rechazo que 0004 muestra al empleado.
4. **0005** — Requiere que 0004 transporte el estado de baja en el contrato de sync.
5. **0007** — Requiere 0004 y 0005; su continuidad offline de crías necesita ADR previo.
6. **0009** — Requiere el permiso definido en 0008. Su desglose por partida exige
   migración, término de glosario y probablemente ADR.
7. **0010** — Último. Hereda los criterios de 0006 e integra las capacidades de los demás.
   Su cierre se prueba sobre esas capacidades, no sobre respuestas simuladas.

### Cambios de alcance registrados

- **0006 → 0010 (2026-09-07).** El 0006 original mezclaba la corrección de scroll, teclado
  y gestos con un rediseño integral. Eran entregables de tamaño incompatible y la queja
  real de los empleados quedaba detrás de cinco features. El rediseño se separó a 0010; el
  0006 conserva la corrección y su nombre, que ya la describía.
- **0009 ampliado (2026-09-07).** El dueño confirmó que necesita saber de qué compra salió
  el alimento. El desglose por partida de inventario pasó de límite explícito a capacidad
  requerida.

## Trabajos anteriores

| # | Carpeta | Estado |
|---|---|---|
| 0001 | [`admin-web-animal-groups`](./feature-0001-admin-web-animal-groups/) | Grupos de animales en el panel Angular. |
| 0002 | [`field-app-parto-redesign`](./feature-0002-field-app-parto-redesign/) | Asistente de parto en cuatro pasos. Precedente de la convención de cuatro documentos. |
| 0003 | [`reestructura-documentacion`](./feature-0003-reestructura-documentacion/) | Reestructura de `docs/` y plantillas. |

Los planes por fase (`plan-0001-fase-3` … `plan-0004-fase-5`) viven en esta misma carpeta
y describen fases del `ROADMAP.md`, no features concretas.
