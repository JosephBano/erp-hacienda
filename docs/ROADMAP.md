# ROADMAP.md — Fases del Proyecto HATO

> Regla de oro (Art. 11): **una fase se cierra cuando algo se usa de verdad en la finca.**
> Este archivo se actualiza al cerrar cada fase (fecha real + retrospectiva de 5 líneas).
> Estado actual: `Fase 0 — cerrada` (iniciada 2026-08-01, cerrada 2026-08-01).
>
> **Retrospectiva:** el esqueleto se construyó con asistencia intensiva de un agente de
> IA (Claude Code) siguiendo al pie de la letra `AGENTS.md` y la Constitución; el costo
> en tiempo humano fue mínimo comparado con montar esto a mano. El propio pipeline hizo
> su trabajo: GitGuardian atrapó una contraseña de desarrollo commiteada por descuido
> antes de llegar a `develop`, y se resolvió aplastando la rama antes del merge — la
> disciplina de PR + CI de Fase 0 se pagó sola en la primera pasada. Lección para
> fases futuras: dejarle a la IA el andamiaje mecánico (proyectos, referencias,
> configuración) y reservar la revisión humana para las decisiones de dominio, que es
> donde el criterio "úsalo de verdad en la finca" no admite atajos.

---

## Fase 0 — Fundaciones (≈ 1 mes)
**Objetivo:** que el proyecto exista con calidad industrial antes de la primera feature.

- Repo Git con GitFlow (`main`, `develop`, plantilla de PR) y Conventional Commits.
- CI en GitHub Actions: build + tests en cada PR; badge en README.
- Esqueleto del monolito modular (SharedKernel + primer módulo vacío compilando).
- `docker-compose` con PostgreSQL local; migración inicial EF Core.
- Estos documentos versionados en `docs/`; ADRs 0001–0006 aceptados.
- Estrategia de backups definida (aunque aún no haya producción).

**Criterio de salida:** un PR de juguete pasa por rama → CI verde → merge a `develop`.

---

## Fase 1 — MVP: el expediente vivo (el core)
**Objetivo:** reemplazar el cuaderno. Registrar animales y su vida.

- Módulo **Livestock**: animales, especies/razas/categorías configurables,
  identificaciones (FarmTag + SIFAE opcional), grupos y membresías.
- Módulo de **eventos**: pesajes, tratamientos (con medicamento, costo y período de
  retiro), vacunaciones, diagnósticos, movimientos, bajas (venta simple/muerte).
- **Producción de leche**: registro de ordeño diario (por vaca o por grupo), lactancias.
- **Inventario básico**: medicamentos y alimentos con lotes, vencimientos y consumo por grupo.
- Panel **Angular mínimo**: ficha del animal (historial completo), registro rápido de
  ordeño y eventos, listados con filtros.
- Auth con roles básicos (admin, registrador).

**Criterio de salida:** 30 días seguidos registrando el ordeño y los eventos reales de la
finca en el sistema. **Backups automáticos activos y una restauración probada.**

---

## Fase 2 — Reproducción, genética y alertas
**Objetivo:** gestionar madres y que el sistema avise, no solo registre.

- Módulo **Breeding**: servicios (monta/IA con pajuelas y catálogo de toros), diagnóstico
  de preñez, gestaciones con fecha probable de parto, partos (creación de crías
  enlazadas), camadas, destetes.
- **Genealogía**: árbol por animal (CTE), padre dual (animal o pajuela).
- **KPI de madres**: intervalo entre partos, días abiertos, servicios/concepción,
  destetados por año.
- Motor de **Tareas/Alertas v1**: palpaciones pendientes, partos próximos, fin de períodos
  de retiro, vencimientos de inventario, calendario sanitario (aftosa, desparasitación).

**Criterio de salida:** el próximo parto de la finca fue anticipado por una alerta del
sistema, y la cría nació "dentro" del sistema con su genealogía.

---

## Fase 3 — App móvil offline-first
**Objetivo:** que los empleados registren desde el potrero, sin señal. La fase más difícil.

- React Native + SQLite/WatermelonDB; sync bidireccional con endpoints idempotentes.
- Flujos de campo: ordeño, tratamientos, pesajes, partos, movimientos — en ≤3 toques,
  usable con guantes y sol.
- Roles y permisos finos por empleado; bitácora de quién registró qué.
- Piloto con 1–2 empleados reales; iterar UX hasta que prefieran la app al cuaderno.

**Criterio de salida:** una semana completa de registros de campo hechos solo desde el
móvil, incluyendo días sin señal, sin pérdida ni duplicación de datos.

---

## Fase 4 — Dinero completo: ventas, compras, costos
**Objetivo:** saber cuánto cuesta y cuánto deja cada cosa.

- **Sales**: ventas de leche (con calidad/precio), de animales (genera baja), clientes,
  cuentas por cobrar. Comprobantes vía **proveedor autorizado SRI** (API).
- **Purchasing**: proveedores, compras de insumos, cuentas por pagar.
- **Accounting**: asientos automáticos desde todos los módulos, centros de costo
  (lechería, porcinos…), costo por animal-día, margen por línea. Reportes para el contador.
- Bloqueo de venta por período de retiro operativo de punta a punta.

**Criterio de salida:** cerrar un mes contable real: ingresos, costos por centro,
y el reporte que el contador acepta usar.

---

## Fase 5 — Transformación y potreros a fondo
**Objetivo:** activar el seguro de extensibilidad.

- **Transformation**: BOM + órdenes (primer caso real: queso fresco, con suero como
  subproducto y costeo completo). Registro sanitario ARCSA gestionado en paralelo.
- **Grazing**: potreros, rotaciones, aforos, carga animal; registro de lluvias.
- Calidad de leche formal (CMT, resultados de la procesadora) ligada a precio.

**Criterio de salida:** un lote real de queso producido, costeado y vendido por el sistema.

---

## Fase 6 — Analítica e IA
**Objetivo:** que años de datos limpios empiecen a pagar dividendos.

- Dashboards: curvas de lactancia, ranking de madres, rentabilidad por animal/centro.
- Detección de anomalías (caída de producción por vaca → alerta temprana de mastitis).
- Predicciones: celos/partos, proyección de flujo de caja.
- **LLM aplicado**: registro por voz/texto libre → eventos estructurados
  ("la Pinta recibió 10 ml de oxitetraciclina" → `TreatmentEvent` + retiro).

**Criterio de salida:** una decisión real de la finca (descarte, cruce, compra) tomada
con un análisis del sistema.

---

## Fase 7+ — Horizonte (sin fecha, sin culpa)
- Facturación electrónica SRI directa (si el volumen lo justifica) · Módulo de
  maquinaria/activos · Módulo de turismo (bounded context nuevo sobre Accounting/People) ·
  Anclaje blockchain de hashes del historial para certificación de trazabilidad ·
  Interoperabilidad con SIFAE · RRHH completo (roles IESS, nómina).

---

## Reglas anti-estancamiento

1. Si una fase pasa de ~3 meses sin uso real → **recortar alcance**, no extender plazo.
2. Ideas nuevas van a `BACKLOG.md`, no a la fase actual. Se revisan al cerrar fase.
3. Cada cierre de fase: retrospectiva de 5 líneas aquí mismo (qué costó, qué aprendí).
4. Está permitido pausar el proyecto; está prohibido abandonarlo en silencio: se escribe
   una nota con fecha y estado, para que el regreso sea fácil.
