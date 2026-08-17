# test-e2e.md — Verificación de la Fase 3.5 (Adaptación porcina)

> Este documento reúne dos cosas distintas, en dos partes: **V** son las verificaciones
> documentales de este traslado (secciones citadas, tamaño de archivo); **E2E** son los
> guiones de verificación manual del criterio de salida de 3.5a y de 3.5b, más las pruebas
> declaradas por rama en el `PLAN-FASE-3-5-PORCINO.md` original, trasladadas aquí íntegras
> porque `spec.md`/`spec-3.5a.md` se quedan con la narrativa y las tareas, no con la
> verificación (T6.1).

**Antes de empezar:** rama `docs/reestructura-documentacion` o `develop` con esta carpeta
mergeada; para las secciones E2E de campo, servidor y app corriendo contra una base con
semillas de porcino (especies, catálogos de mortalidad, rangos de plausibilidad).

---

## Parte V — Verificación documental de este traslado

### V-1 — Numeración preservada

**Debe pasar:** cada sección de la lista de citas (`sec.2.3`, `sec.3.5a.3`, `sec.3.5a.4`
—incl. sus tareas 3 y 5—, `sec.3.5a.5` —incl. sus tareas 1, 2 y 3—, `sec.3.5a.7` —incl. sus
tareas 1, 5 y 6—, `sec.3.5a.8`, `sec.7`) existe con el mismo número en `spec.md` o
`spec-3.5a.md`.

### V-2 — Tamaño de archivo

**Comando:** `wc -l docs/spec/plan-0002-fase-3-5/*.md`
**Debe pasar:** cada archivo ronda las ~400 líneas; si alguno se dispara muy por encima,
se reparte, conservando la numeración (D6).

### V-3 — Los sub-planes se movieron pero no se absorbieron

Los ocho sub-planes viven hoy en [`sub-planes/`](./sub-planes/) (commit `d9699a4`); antes
colgaban de `docs/spec/sub_planes/` con el prefijo `PLAN-FASE-3-5-PORCINO-`. El
movimiento y el renombrado **no podían tocar el cuerpo** de los archivos: sólo las rutas
relativas de sus enlaces y el encabezado.

**No se verifica con un `diff` completo**, porque el cuerpo sí cambió en dos puntos
legítimos: los enlaces a pares perdieron el prefijo del nombre y las citas al
`PLAN-FASE-3-4.md` borrado se repuntaron a `PROTOCOLO-DE-TRABAJO.md`. Lo que debe
demostrarse es que **no se perdió ni se movió contenido**, y eso se verifica por
estructura:

**1. Los ocho existen y la estructura de secciones de cada uno es idéntica a la de antes
del movimiento** (`875c65f` es el commit inmediatamente anterior):

```bash
for f in 3.5a.2-A 3.5a.2-B 3.5a.2-C 3.5a.9-A 3.5a.9-B 3.5b.5-A 3.5b.5-B 3.5b.5-C; do
  diff <(git show 875c65f:docs/spec/sub_planes/PLAN-FASE-3-5-PORCINO-$f.md | grep '^##') \
       <(grep '^##' docs/spec/plan-0002-fase-3-5/sub-planes/$f.md) >/dev/null \
    && echo "OK  $f" || echo "DIFF $f"
done
```

**Debe pasar:** ocho `OK`. Sólo el `#` de nivel 1 cambió —lleva el nombre nuevo— y por eso
el filtro es `^##`.

**2. Cada archivo creció exactamente 2 líneas**, las del blockquote de procedencia que
reemplaza la cita al plan borrado. Cualquier otro delta es contenido perdido o añadido:

```bash
for f in 3.5a.2-A 3.5a.2-B 3.5a.2-C 3.5a.9-A 3.5a.9-B 3.5b.5-A 3.5b.5-B 3.5b.5-C; do
  a=$(git show 875c65f:docs/spec/sub_planes/PLAN-FASE-3-5-PORCINO-$f.md | wc -l)
  b=$(wc -l < docs/spec/plan-0002-fase-3-5/sub-planes/$f.md)
  echo "$f $((b-a))"
done
```

**Debe pasar:** `2` en los ocho.

**3. Todo enlace relativo resuelve** — el movimiento cambió la profundidad (`../../adr/` →
`../../../adr/`, `../fase-3-5/spec.md` → `../spec.md`, `../../src/` → `../../../../src/`):

```bash
python3 - <<'PY'
import re, os, glob
bad = [(f, m.group(1))
       for f in sorted(glob.glob("docs/spec/plan-0002-fase-3-5/sub-planes/*.md"))
       for m in re.finditer(r'\]\((\.[^)#]*?)(#[^)]*)?\)', open(f).read())
       if not os.path.exists(os.path.normpath(os.path.join(os.path.dirname(f), m.group(1))))]
print(*bad, sep="\n") if bad else print("todos los enlaces relativos resuelven")
PY
```

**4. Y siguen sin absorberse:** ocho archivos, uno por rama Git, sólo **enlazados** desde
[`plan.md`](./plan.md) sec. 5, `spec-3.5a.md` y `spec.md` — nunca copiados dentro de ellos.

```bash
ls docs/spec/plan-0002-fase-3-5/sub-planes/*.md | wc -l   # 8
```

### V-4 — Comando de verificación sección-por-sección (citado desde `spec.md`)

```bash
for sec in "2\.3" "3\.5a\.3" "3\.5a\.4" "3\.5a\.5" "3\.5a\.7" "3\.5a\.8" "^## 7"; do
  echo "== $sec =="
  grep -nE "$sec" docs/spec/plan-0002-fase-3-5/spec.md docs/spec/plan-0002-fase-3-5/spec-3.5a.md
done
```

**Debe pasar:** cada bloque imprime al menos un encabezado (`###` o `##`) que empieza
exactamente con ese número, en `spec.md` o `spec-3.5a.md`.

---

## Parte E2E — Criterio de salida de 3.5a y 3.5b, como guion

### E2E-1 — Cierre del bloque 3.5a: una camada real, de nacimiento a clasificación

> Guion del criterio completo de `spec-3.5a.md` sec. "Criterio completo (cierre del bloque
> 3.5a)".

**Preparación:** una cerda madre real registrada, con su cohorte de lactancia activa.

**Pasos:**
1. Registrar el parto: sexo y peso por cría (3.5a.4 tarea 3).
2. Durante los ~24 días, registrar al menos un tratamiento con vía y motivo sobre un lechón
   o sobre la madre (3.5a.2-B/C).
3. Cometer un error de dedo real en un registro del día y corregirlo desde el teléfono
   (3.5a.8).
4. Al cumplirse los 24 días de la cohorte completa (la última camada, no la primera —
   3.5a.4 tarea 2), ejecutar la clasificación por peso (3.5a.4 tarea 4).

**Debe pasar:**
- La camada aparece con peso individual por lechón, no un promedio inventado.
- El tratamiento registrado muestra vía y motivo, no texto libre.
- La corrección queda como evento nuevo (Art. 1): el registro original permanece y el
  corregido lo referencia.
- La clasificación reparte exactamente N cabezas en M lotes, sin perder ni duplicar
  ninguna, y cada animal conserva su `mother_id`.

### E2E-2 — Apertura del piloto real (sub-criterio ADR-0024)

**Pasos:**
1. Verificar en `develop` que las ocho piezas de la tabla "Para abrir el piloto real"
   (`spec-3.5a.md`) están mergeadas.
2. Verificar que lo que falta (3.5a.7.1–5 UI del lote, 3.5a.8 corrección) está anotado como
   deuda en `docs/BACKLOG.md` sección 3.5, no oculto.

**Debe pasar:** las ocho piezas presentes; la deuda restante tiene entrada explícita en
`docs/BACKLOG.md` con disparador.

### E2E-3 — Cierre del bloque 3.5b: FCR con decisión real

> Guion del criterio de salida de 3.5b (`docs/ROADMAP.md:215-216`).

**Pasos:**
1. Con un lote de engorde real, cargar consumo de alimento (en sacos, 3.5a.5) y pesajes
   muestrales (3.5a.7 tarea 1) durante el ciclo.
2. Calcular el FCR del lote (4.4).
3. Confirmar con el cliente una decisión de manejo tomada usando ese número.

**Debe pasar:** el FCR sale en kg (no en dinero); la decisión del cliente queda registrada
en la bitácora del piloto.

---

## Parte E2E — Pruebas declaradas por rama (trasladadas de `PLAN-FASE-3-5-PORCINO.md`)

> Cada línea es la verificación que la rama original exigía. Se agrupan por rama para no
> perder ninguna en el traslado (regla "trasladar significa trasladar").

**3.5a.0** — 0 litros rechazado · quitar la cría #3 de 5 deja las 4 correctas y no
descoloca los sexos · cancelar tras quitar no deja estado sucio · el resumen refleja
exactamente lo que se envía.

**3.5a.1** — evento sin animal ni grupo rechazado · evento con ambos rechazado · el CHECK
de BD se verifica en integración, no sólo el dominio · `LiveHeadCount` tras altas, bajas y
salidas · baja parcial no cierra ninguna fila de `Animal` · al llegar a cero cabezas se
cierran todas las membresías restantes y el conteo global de animales vivos no deja
fantasmas · push duplicado de un evento grupal → un registro (exigencia de
`docs/spec/plan-0001-fase-3/spec.md` sec.2.2) · migración corre desde cero.

**3.5a.2 (A/B/C)** — catálogos configurables desde el panel (A) · `applied_by` ≠
`recorded_by` (A) · `health_plan_item_id` nullable ahora (A) · dosis con valor y sin unidad
rechazada (B) · dosis ausente aceptada, con o sin observación (B) · dosis por peso
resuelta contra el último pesaje (B) · dosis por peso rechazada si el animal no tiene
pesaje (B) · dosis por peso sobre lote usa promedio muestral y queda `is_estimated` (B) ·
calculada ≠ administrada persiste sin corregir ninguna (B) · ruta inexistente o
`is_active = false` rechazada (A y B) · serie de 3 días produce 3 aplicaciones y un solo
retiro (B) · no regresión del cálculo de retiro, Art. 19 (B) · tres toques para
vacunación, cuatro para tratamiento (C) · cancelar no deja estado sucio (C).

**3.5a.3** — baja individual con causa · baja grupal con causa · causa inexistente
rechazada · la mortalidad predestete por madre se agrega correctamente sobre datos
sembrados.

**3.5a.4** — cohorte con 3 camadas de días distintos desteta por la última · peso al nacer
persiste y sincroniza · clasificación reparte N cabezas en M lotes sin perder ni duplicar
cabezas · los animales conservan `mother_id` tras la clasificación (el linaje **no** se
pierde).

**3.5a.5** — conversión ida y vuelta sin pérdida de precisión (`decimal`, Art. 10) ·
consumo en sacos descuenta los kilos correctos del batch · factor 0 o negativo rechazado ·
item sin conversión definida consumido en su unidad base sigue funcionando.

**3.5a.6** — valor dentro de rango pasa sin fricción · valor improbable exige confirmación
explícita · valor imposible se rechaza · **sin rangos configurados no se bloquea nada**
(fail-open acá es correcto) · funciona sin red.

**3.5a.7** — una prueba de "registro sin red" por pantalla (exigencia de
`docs/PROTOCOLO-DE-TRABAJO.md` sec.2.1 para React Native) · el promedio calculado coincide con el
enviado · la ficha refleja las bajas · **cada actividad del árbol se resuelve en los
toques que se contaron en sec.2.3** — si la implementación excede lo dibujado, se corrige
el flujo, no se relaja el número.

**3.5a.8** — **forzar la carrera** (cancelar mientras un push está en vuelo) y verificar
que no queda registro fantasma, en `Hato.Sync.IntegrationTests` · cancelación no borra la
fila · corrección de evento sincronizado deja original **y** corrección · corregir fuera
de ventana se rechaza en el móvil.

**3.5a.9 (A/B)** — búsqueda con 200 animales sembrados devuelve el correcto (B) · filtro
por lote (B) · módulo apagado: pantalla de ordeño no alcanzable desde ninguna ruta (A) ·
módulo apagado: ordeño en outbox sigue sincronizando, única pérdida silenciosa posible
según ADR-0019 sec.4 (A) · encender el módulo lo devuelve sin tocar código (A) · pantalla
que administra los módulos no puede ocultarse a sí misma (A) · la navegación resuelve el
interruptor sin red (ambos).

**4.1** — resolución de fecha teórica por cada ancla · ítem cumplido por un evento que lo
referencia · ítem vencido fuera de ventana · plan de sexo macho no genera pendientes en
hembras.

**4.2** — no duplica alerta activa · se genera al entrar en ventana · no se genera para un
ítem ya cumplido.

**4.3** — rangos de peso solapados rechazados · la ración de la cerda topa en 9 kg con 15
crías · lote sin pesaje reciente no inventa una recomendación.

**4.4** — FCR sobre un lote sembrado con valores conocidos, a mano en el test · lote sin
pesaje inicial no produce un FCR falso · la divergencia dispara sobre umbral configurable.

**4.5 (A/B/C)** — guardarraíl estructural: característica no puede declarar unidad ni
aceptar decimal libre, test de arquitectura (A) · `TraitValueType` tiene exactamente 4
filas (A) · `EscalaOrdinal` rechaza valores fuera del conjunto / fuera del string (A) ·
`ConteoAcotado` rechaza fuera de rango, sin decimal (A) · `CurrentDisposition` derivado y
refleja la última observación (A, completado por C con versionado) · drenaje exacto e
idempotente de `SelectionCriterion` (B) · `MaternalBehaviorAssessment` queda vacía y se
elimina (B) · sesión → `gilt_evaluation` context preservado (B) · regresión del
`MaternalIndex`: lee lo drenado, no se pisa (B) · versionado por clonado, transaccional,
no edición (C) · atomicidad y concurrencia del versionado (C) · interpretación preservada
tras versionado, la prueba más importante del ADR-0018 (C) · alertas visibles en la ficha,
sin red (C) · toggle de alerta en el panel, sin tocar código (C).

**4.6** — KPIs derivados contra datos sembrados con resultados escritos a mano · cambiar
los pesos reordena el ranking · una madre sin partos no aparece con índice 0 (aparece sin
índice) · el índice no lee ninguna característica que duplique un evento contable.

**4.7** — alerta de celo en la ventana correcta tras el destete · **intento de disposición
de un lote en retiro de carne se rechaza**, no se advierte (mismo estándar que
`docs/PROTOCOLO-DE-TRABAJO.md` sec.2.3 exige para leche).

---

## Cierre de la verificación

Los escenarios E2E en verde, más las cuatro verificaciones V, cierran la revisión de esta
carpeta. El criterio de aceptación numerado completo está en `spec.md` sec. 6 (riesgos) y
en la tabla de `spec-3.5a.md` "Para abrir el piloto real". Este documento no marca ninguna
casilla — la checklist ejecutable con `[ ]`/`[x]` vive en [`tasks.md`](./tasks.md) y la
marca el agente de la compuerta A contra el código, no este traslado.
