# ROADMAP.md — Fases del Proyecto HATO

> Regla de oro (Art. 11): **una fase se cierra cuando algo se usa de verdad en la finca.**
> Este archivo se actualiza al cerrar cada fase (fecha real + retrospectiva de 5 líneas).
> Estado actual: `Fase 3 — reabierta` (iniciada 2026-08-02, cerrada en falso 2026-08-02, reabierta 2026-08-03),
> con la `Fase 3.5 — Adaptación porcina` insertada el 2026-08-05 antes de la Fase 4.
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

> **Pendiente para cerrar (2026-08-03):** de todo lo que quedaba abierto al reabrir la
> fase, sólo falta una cosa y es deliberadamente ajena al código: **el piloto real** —
> una semana de registros hechos por un empleado desde un teléfono de verdad, sin señal,
> tal como exige el criterio de salida. Nada de trabajo de ingeniería puede sustituir esa
> semana.
>
> Todo lo demás ya está resuelto: borrado lógico real (`Animal.Delete()`, con invariante
> de "no eliminar con historia" y filtro de query), filtrado del pull por permisos, la
> bitácora de conflictos LWW (`Animal.LastEditedAt` + `GET /api/v1/sync/conflicts`) con
> una pantalla en `field-app` que la hace alcanzable en uso real (antes, ningún cliente
> podía disparar un conflicto LWW fuera de una prueba) y otra en `admin-web` que la
> expone, los endpoints de lectura de especies/razas/categorías que faltaban para poder
> registrar un animal desde cualquier cliente con su pantalla correspondiente, los 10
> escenarios obligatorios de sincronización de `docs/spec/plan-0001-fase-3/spec.md` sec.2.2 (los últimos dos —
> token expirado a mitad de push y corte de red a mitad de un lote — encontraron y
> corrigieron un bug real en el cliente), la prueba de convergencia end-to-end con dos
> dispositivos simulados, y las pantallas de roles/permisos (con edición), auditoría y
> sincronización en el panel. `docs/BACKLOG.md` recoge lo que se dejó fuera a propósito
> (extender borrado lógico y LWW a otras entidades, resolución manual de operaciones
> rechazadas, `ng test` roto en `admin-web`) y por qué. Sigue pendiente, heredado y sin
> relación con esta fase: la subida de fotos (Fase 4) y el ciclo de vida de `Lactation`
> (Fase 2, nunca implementado).

---

## Fase 3.5 — Adaptación porcina (insertada 2026-08-05)

> **Desacople del inicio del piloto del cierre completo del bloque 3.5a (ADR-0024,
> 2026-08-07; ADR-0021, 2026-08-07, sobre la compuerta sec.2.3).** El inicio del piloto
> real no exige cerrar 3.5a como bloque: exige un sub-conjunto mínimo de captura +
> tratamiento + primer nivel del árbol + visibilidad de módulos + rangos de plausibilidad,
> todos mergeados a integration. **Estado al 2026-08-09:** 3.5a.2 (A/B/C) mergeado
> (B/C vía `feature/livestock-treatment-dose-logic` + `feature/field-app-treatment-ui`),
> 3.5a.6 mergeado (PR #73), 3.5a.7 tasks 1–5 mergeado
> (`feature/field-app-lot-registration`). Lo que queda como deuda rastreable
> (no bloqueo): 3.5a.8 correcciones desde el teléfono, 3.5a.3 causas de muerte
> (la lógica de dominio está; falta la pantalla de admin-web), 3.5a.4 task 4
> (clasificación por peso — pieza que cierra el criterio de salida completo de 3.5a).
> El criterio completo de salida de 3.5a sigue exigiendo clasificación por peso +
> tratamento con vía/motivo + corrección desde el teléfono como bloque.

**Recepción de inventario (ADR-0026, PR #94, mergeada 2026-08-13):** cierre
del bloqueante transversal "no hay forma trazable de rellenar inventario
de comida" detectado durante el piloto. Endpoint `POST /receptions`,
comando `RecordInventoryReceptionCommand`, evento `InventoryReceptionRecorded`,
UI admin-web "Recibir alimento" con badge "Sin declaración completa" (issue
#93). Vida útil declarada: deprecado cuando llegue Purchasing (Fase 4). El
consumo desde lote (3.5a.7 mergeado) ya descuenta de `InventoryBatch`; este
feature cierra la otra mitad del flujo (entrada con fecha declarada, proveedor,
factura, autor).

**Objetivo:** que el sistema represente el negocio real del cliente del piloto, que no es
una lechería sino una **granja porcina**.

> **Por qué existe esta fase y por qué va antes de la Fase 4.** El levantamiento del
> 2026-08-05 con el cliente expuso ocho frentes donde el modelo no representa su operación.
> Dos chocan con supuestos que atraviesan todo el sistema: los porcinos de engorde **no
> tienen arete** (y el modelo asume *animal = individuo con UUID*), y el manejo es por lote
> con conteo, no por individuo. La Fase 4 necesita volumen de datos cargados que hoy no
> existe porque nadie puede usar el sistema para lo que esta finca hace. Adaptar el dominio
> primero es lo que habilita esa carga — el orden que pide el Art. 11.

Se ejecuta en dos bloques. **El piloto real puede arrancar con un sub-conjunto de 3.5a
mergeado a develop** (sub-criterio "Para abrir el piloto real" en el plan), siempre que la
deuda restante quede documentada como tal — ver
[ADR-0024](adr/0024-pilot-decoupling-from-3-5a.md). El desacople es deliberado: no hay
valor en dejar al cliente sin sistema mientras la UI del sujeto "lote" (3.5a.7.1–5)
termina de implementarse, y la conversación de frecuencias con el cliente (sec.7-C del
plan) puede ocurrir en paralelo al uso real:

- **3.5a — Captura.** Lote por conteo y eventos grupales (ADR-0015) · parto con peso por
  lechón y cohorte de lactancia · tratamientos con vía, motivo y dosis con unidad (Art. 10)
  · vacunación como evento propio · muerte con causa · alimento en sacos con conversión de
  unidades · rangos de plausibilidad configurables · corrección de registros desde el campo
  (ADR-0017) · buscador de animales · **Ordeño oculto por interruptor explícito** (ADR-0019):
  el módulo no aplica a esta finca, se apaga desde el panel y **no se borra nada** — código,
  pruebas, endpoints y datos quedan intactos hasta que el dueño lo encienda.
- **3.5b — Análisis.** Plan sanitario configurable con alertas (ADR-0016) · estándares de
  alimentación · **conversión alimenticia (FCR) por lote** · **características observables
  del animal** (ADR-0018) · índice de madres · alertas destete–celo y retiro en carne.

> **El ADR-0018 no es porcino.** Salió de generalizar la calificación de madres —que estaba
> modelada como una tabla que sólo servía para cerdas, violando el Art. 8— y terminó siendo
> el mecanismo para cualquier juicio sobre cualquier animal: "este caballo patea", "esta vaca
> se escapa del corral", "esta cerda no deja mamar". Incluye advertencias visibles en la
> ficha del animal en el móvil, que transfieren el conocimiento del empleado veterano al que
> recién entra — valor que no tiene nada que ver con esta fase ni con esta especie.

El plan de ejecución detallado está en `docs/spec/plan-0002-fase-3-5/spec.md`.

**Criterio de salida (3.5a):** una camada real nacida, pesada y seguida dentro del sistema
hasta su clasificación por peso a los 24 días, con sus tratamientos registrados con vía y
motivo, y al menos una **corrección hecha desde el teléfono por un error de dedo real**.

**Criterio de salida (3.5b):** un ciclo de engorde con su conversión alimenticia calculada
por el sistema, y una decisión de manejo del cliente tomada con ese número.

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
2. Ideas nuevas van a `docs/BACKLOG.md`, no a la fase actual. Se revisan al cerrar fase.
3. Cada cierre de fase: retrospectiva de 5 líneas aquí mismo (qué costó, qué aprendí).
4. Está permitido pausar el proyecto; está prohibido abandonarlo en silencio: se escribe
   una nota con fecha y estado, para que el regreso sea fácil.
