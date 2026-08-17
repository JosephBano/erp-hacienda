# spec.md — Fase 5: Transformación y potreros a fondo

> **Qué es este documento.** Fija el objetivo, el criterio de salida y las preguntas que hay
> que responder antes de poder planificar la Fase 5. **No es un plan** — esta fase no tiene
> ni ADR ni código propio, y está más lejos en el tiempo que la Fase 4. Por eso esta carpeta
> tiene **solo `spec.md`**: nada de `plan.md`, `tasks.md` ni `test-e2e.md`.
>
> **Por qué es corto y por qué eso es intencional.** Este documento es deliberadamente breve.
> No hay apéndice de planificación previa que trasladar — a diferencia de la Fase 4, nadie
> diseñó bloques ni ramas para la Fase 5 todavía. Escribir ese diseño ahora, sin ADR y sin una
> sola línea de código de Transformation o Grazing, sería inventar un plan de mentira. La
> brevedad de este documento no es una carpeta incompleta a la espera de que alguien la
> rellene: es el tamaño correcto para lo que hoy se sabe.

- **Fase del ROADMAP:** Fase 5 — Transformación y potreros a fondo
  (`docs/ROADMAP.md:235-243`), **no iniciada**.
- **ADRs vigentes que respalda:** ninguno todavía.
- **Reglas duras que gobiernan este trabajo:** Art. 6 (contrato público entre módulos, cero
  SQL cruzado), Art. 8 (filtrado por capacidad), Art. 10 (toda cantidad física lleva su
  unidad — relevante para BOM y costeo), Art. 18 (el registro sanitario ARCSA y el costeo no
  se improvisan sin el proceso real).

---

## 1. Objetivo y criterio de salida

**Objetivo** (`docs/ROADMAP.md:236`): activar el seguro de extensibilidad.

El ROADMAP describe la fase en tres piezas (`docs/ROADMAP.md:238-241`):

- **Transformation**: BOM + órdenes (primer caso real: queso fresco, con suero como
  subproducto y costeo completo). Registro sanitario ARCSA gestionado en paralelo.
- **Grazing**: potreros, rotaciones, aforos, carga animal; registro de lluvias.
- Calidad de leche formal (CMT, resultados de la procesadora) ligada a precio.

**Criterio de salida** (`docs/ROADMAP.md:243`): un lote real de queso producido, costeado y
vendido por el sistema.

## 2. Preguntas abiertas antes de planificar

- **¿Cuál es la receta real del queso fresco?** El criterio de salida exige un lote *real*,
  no uno de referencia genérica. Falta la receta del cliente: insumos, rendimiento, y el
  proceso tal como se hace hoy en la hacienda — sin eso no hay BOM que modelar.
- **¿Cuánto suero sale y qué se hace con él?** El ROADMAP marca el suero como subproducto
  explícito del queso fresco. Falta saber el volumen real por lote y su destino (venta,
  descarte, insumo de otro proceso) para que el costeo completo tenga sentido.
- **¿Qué exige el registro sanitario ARCSA para este producto y este productor?** El ROADMAP
  dice que se gestiona "en paralelo" a la construcción de Transformation — falta saber qué
  trámite es, cuánto tarda, y si condiciona la fecha en que un lote puede venderse de verdad
  por el sistema.
- **¿Grazing entra completo o recortado?** El ROADMAP lista potreros, rotaciones, aforos,
  carga animal y registro de lluvias como un bloque. Falta decidir si los cinco son
  necesarios para el criterio de salida de la fase (que es sobre el queso, no sobre potreros)
  o si Grazing puede recortarse a lo mínimo que Transformation necesita y completarse después.
