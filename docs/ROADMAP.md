# ROADMAP.md — Fases del Proyecto HATO

> Regla de oro (Art. 11): **una fase se cierra cuando algo se usa de verdad en la finca.**
> Este archivo se actualiza al cerrar cada fase (fecha real + retrospectiva de 5 líneas).
> Estado actual: `Fase 3 — reabierta` (iniciada 2026-08-02, cerrada en falso 2026-08-02, reabierta 2026-08-03).
>
> **Retrospectiva Fase 0:** el esqueleto se construyó con asistencia intensiva de un agente de
> IA (Claude Code) siguiendo al pie de la letra `AGENTS.md` y la Constitución; el costo
> en tiempo humano fue mínimo comparado con montar esto a mano. El propio pipeline hizo
> su trabajo: GitGuardian atrapó una contraseña de desarrollo commiteada por descuido
> antes de llegar a `develop`, y se resolvió aplastando la rama antes del merge — la
> disciplina de PR + CI de Fase 0 se pagó sola en la primera pasada. Lección para
> fases futuras: dejarle a la IA el andamiaje mecánico (proyectos, referencias,
> configuración) y reservar la revisión humana para las decisiones de dominio, que es
> donde el criterio "úsalo de verdad en la finca" no admite atajos.
>
> **Retrospectiva Fase 1:** La Fase 1 (MVP: Expediente Vivo) se implementó mediante una tubería
> de 6 ramas feature consecutivas y revisadas por PRs (Livestock Identity, Groups, Immutable Events,
> Production Milking, Inventory Basics, People Auth, Admin Web MVP y Backup Scripts). El dominio
> garantiza la inmutabilidad de eventos, cálculo bloqueante de retiros de medicamentos (Art. 19),
> el ordeño diario por grupo/vaca y el panel web Angular. Se mantuvieron 51 pruebas automáticas
> (unitarias + integradas con Testcontainers y PostgreSQL real) pasando al 100%.
>
> **Retrospectiva Fase 2:** La Fase 2 (Reproducción, Genética y Alertas) se completó mediante dos
> PRs integrados (#11 y #12). Se implementó el módulo de dominio `Breeding` (servicios, pajuelas
> de semen con stock, diagnósticos de preñez, gestaciones con FPP por especie, partos y camadas),
> árbol genealógico por CTE recursiva en PostgreSQL (`Pedigree`), KPIs reproductivos de madres
> (`CalvingInterval`, `DaysOpen`) y el motor de alertas `Tasks/Alertas` v1. El panel web Angular
> expone la gestión reproductiva, catálogo de semen y centro de alertas activas. Se alcanzaron
> 71 pruebas automáticas al 100%.
>
> **Fase 3 — cierre revertido (2026-08-03).** La fase se había marcado como cerrada, pero su criterio de
> salida ("una semana completa de registros de campo hechos solo desde el móvil") era imposible de haber
> cumplido: `clients/field-app/` no contenía ni una sola pantalla, no había `App.tsx` ni assets, y `app.json`
> apuntaba a imágenes inexistentes, de modo que la app no arrancaba. Una auditoría posterior encontró además
> tres defectos que habrían perdido datos en producción sin dar ningún error:
>
> 1. **Ninguna entidad de Livestock recibía `created_at`/`updated_at`**: el interceptor de auditoría estaba
>    registrado sólo en `PeopleDbContext`. Todas las filas quedaban en `0001-01-01`, así que el pull incremental
>    no volvía a entregar nada a un cliente cuyo cursor ya hubiera avanzado.
> 2. **El parto perdía la genealogía en silencio**: la app encolaba `createAnimal` con `motherId` en el payload,
>    pero ese comando no tiene ese campo; el JSON se descartaba y el servidor respondía `Accepted` con una cría
>    huérfana.
> 3. **El outbox del cliente vivía en `localStorage`**, una API que no existe en React Native: en un teléfono real
>    todo lo pendiente moría al cerrar la app.
>
> Lección: el criterio del Art. 11 —"algo se usa de verdad en la finca"— no admite cierre por avance parcial, y
> una suite verde no prueba nada si no ejercita el camino que el usuario recorre. Las pruebas de sync que existían
> cubrían 3 de los 10 escenarios obligatorios del plan, y ninguna tocaba el borde del cursor.
>
> **Trabajo de corrección (rama `fix/fase-3-sync-correctness`):** sellado universal de marcas de tiempo, cursor
> `(timestamp, id)` con orden determinista y frontera por colección, `recordBirth`/`moveAnimal` en el push,
> idempotencia por reserva previa con índice único, UUID de cliente aceptado por el servidor (Art. 3), outbox
> real en WatermelonDB, motor de sync con backoff y detección de conectividad, y las pantallas de campo. Suite
> dedicada `Hato.Sync.IntegrationTests`.

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

> **Pendiente para cerrar (2026-08-03):** el piloto con empleados reales no ha ocurrido. Ya
> resuelto desde la reapertura: borrado lógico real (`Animal.Delete()`, con invariante de
> "no eliminar con historia" y filtro de query), filtrado del pull por permisos (una
> colección sólo baja si el rol tiene el permiso correspondiente), la bitácora de
> conflictos LWW (`Animal.LastEditedAt` + `GET /api/v1/sync/conflicts`, para la única
> entidad genuinamente editable del modelo), los endpoints de lectura de especies/razas/
> categorías que faltaban para poder registrar un animal desde cualquier cliente, y las
> pantallas del panel Angular que consumen todo lo anterior (alta de animal, roles y
> permisos, bitácora de auditoría, bandeja de sincronización). Sigue faltando: la subida
> de fotos (depende del módulo de adjuntos de la Fase 4).

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
