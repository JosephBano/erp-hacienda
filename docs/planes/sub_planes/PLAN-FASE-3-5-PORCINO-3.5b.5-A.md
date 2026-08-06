# PLAN-FASE-3-5-PORCINO-3.5b.5-A.md — Núcleo de AnimalTrait y TraitObservation

> **Sub-plan extraído de `PLAN-FASE-3-5-PORCINO.md` §3.5b.5.**
> Este archivo **es ejecutable de forma independiente** del macro plan y de sus
> pares 3.5b.5-B (absorción de `SelectionCriterion`) y 3.5b.5-C (advertencias y
> versionado). El macro plan sigue siendo la fuente de verdad para el resto del
> proyecto; toda decisión que aplique a varias ramas vive allá. Acá viven sólo
> las decisiones y el alcance de esta sub-rama.

- **Rama Git:** `feature/livestock-animal-traits-core`
- **ADR que la respalda:** [ADR-0018](../adr/0018-caracteristicas-observables-del-animal.md)
- **Pares del split:**
  - [`3.5b.5-B`](./PLAN-FASE-3-5-PORCINO-3.5b.5-B.md) (rama `feature/livestock-selection-criterion-deprecation`): absorbe `SelectionCriterion` y depreca la tabla paralela.
  - [`3.5b.5-C`](./PLAN-FASE-3-5-PORCINO-3.5b.5-C.md) (rama `feature/livestock-trait-alerts-and-versioning`): advertencias visibles en la ficha + versionado de definiciones usadas.
- **Fuente original:** [`PLAN-FASE-3-5-PORCINO.md` §3.5b.5](../PLAN-FASE-3-5-PORCINO.md#45--featurelivestock-animal-traits--estructural-adr-0018)

---

## Por qué existe esta sub-rama

La evaluación de futuras madres y la "calificación materna" eran dos subsistemas
distintos. El segundo, `MaternalBehaviorAssessment`, era — en palabras del macro
plan — **un `if (especie == "cerdo")` disfrazado de tabla**: servía sólo para
cerdas, vivía aislado del resto del dominio, y rompía el Art. 8 ("los
`if` por especie son configuración, no código").

La solución que el ADR-0018 fija es **un solo mecanismo**: un juicio tipado,
fechado y firmado sobre un animal. Esto es transversal: "este caballo patea",
"esta vaca se escapa del corral", "esta cerda no deja mamar" son exactamente el
mismo problema con la misma forma. Implementarlo todo en un PR mezclaba:

1. **El núcleo** (definición + observación + guardarraíl estructural).
2. **Una migración** de datos de `SelectionCriterion` al nuevo esquema.
3. **La UI de advertencias visibles** en la ficha del animal.
4. **El versionado** de definiciones usadas (cambio de invariante).

Cada uno merece su propia sub-rama, porque tienen blast radius distintos y un
defecto en cualquiera de los tres oculta a los otros dos. Esta sub-rama entrega
el **núcleo** (puntos 1–4 del macro plan). Sin esto, no hay nada que migrar ni
nada que versionar.

## Decisiones tomadas en el macro plan y que aplican a esta sub-rama

- **Guardarraíl estructural del ADR-0018 §4:** los cuatro tipos de valor son
  literalmente los únicos permitidos. Sin campo de unidad y sin decimal libre
  en la tabla `animal_traits`. Es lo que hace que `peso = 35.4 kg` sea
  **inexpresable** en este mecanismo, no meramente desaconsejado.
- **`especie` nula = aplica a todas.** El default razonable es global;
  especificidad sólo se añade cuando hace falta.
- **Se observan, no se asignan.** Nunca una columna editable sobre `Animal`.
  "Esta yegua es mansa" es un resumen derivado de la serie.
- **Contexto opcional** apuntando al hecho durante el cual se observó.

## Tareas

1. **Tabla `animal_traits`** (definición configurable de una característica):
   ```
   {
     id, key, label_es, kind, value_type, value_options_json?, value_min?, value_max?,
     species_id?, applies_to_category_id?, is_visible_as_alert,
     version, is_active, created_at, created_by_user_id, ...
   }
   ```
   Los campos `value_options_json`, `value_min`, `value_max` viven sólo si el
   `value_type` los exige (`EscalaOrdinal` o `ConteoAcotado`). **No hay campo de
   unidad y no hay campo de decimal libre** — invariante estructural fija por
   test.
2. **`TraitKind` ∈ {`Conductual`, `Morfológica`, `Manejo`}.** Tabla configurable
   (mismo motivo que en 3.5a.2-A: no `enum` compilado). Semilla con los tres.
3. **`TraitValueType` ∈ {`Booleano`, `EscalaOrdinal`, `ConteoAcotado`,
   `TextoLibre`}.** Tabla. Cuatro filas — la quinta no entra por
   configuración ni por código.
4. **Tabla `trait_observations`** (registro fechado y firmado):
   ```
   {
     id, trait_id, animal_id, observed_at,
     value_bool?, value_ordinal?, value_count?, value_text?,
     observation_context_id?, observation_context_type?,
     recorded_by_user_id, recorded_at,
     trait_version_at_observation INT NOT NULL    ← guarda la versión usada
   }
   ```
   **`trait_version_at_observation` es la columna que el versionado de
   [3.5b.5-C](./PLAN-FASE-3-5-PORCINO-3.5b.5-C.md) explota.** Se setea en el
   momento del INSERT y no se puede cambiar después (Art. 1).
5. **`observation_context` polimórfico liviano.** Una sola columna textual
   `observation_context_type` (e.g., `"birthing"`, `"weaning"`, `"handling"`)
   y un `observation_context_id` (uuid nullable). Sin FK fuerte para no
   encadenar este PR a otros dominios. El contexto es opcional y se ignora
   silenciosamente si el lector no lo entiende.
6. **Resumen derivado `CurrentDisposition(animalId)`** — la "disposición
   actual" del glosario. Una sola función en el dominio que devuelve la
   última observación por cada característica relevante del animal, o `null`
   si no hay observaciones. **No se almacena**: es una vista derivada. Si
   3.5b.5-C introduce más elaborados (filtros por categoría, etc.) esta
   función absorbe la nueva lógica.
7. **Endpoints REST** para CRUD de `animal_traits` (sólo lectura por parte del
   móvil — `GET /api/v1/animal-traits`) y para crear observaciones (`POST
   /api/v1/animals/{id}/trait-observations`). Endpoints equivalentes en
   `/api/v1/sync/push` con su `ClientOperationId` (idempotencia obligatoria;
   exigencia del `PLAN-FASE-3-4.md` §2.2).
8. **Migración EF Core:** tablas `animal_traits`, `trait_kinds`,
   `trait_value_types`, `trait_observations`. La tabla `SelectionCriterion`
   **no se toca acá** — vive hasta que 3.5b.5-B la drene. `MaternalBehaviorAssessment`
   ya no se usa (lo cierra el mismo 3.5b.5-B).
9. **Pruebas críticas** (detalladas más abajo).

## Pruebas

Las pruebas mínimas obligatorias:

1. **Invariante estructural: no se puede declarar unidad ni aceptar decimal
   libre.** Un test de **arquitectura** (escaneo del modelo EF) verifica que
   no exista ninguna columna llamada `unit` o `decimal_value` en
   `animal_traits` ni en `trait_observations`. La trampa explícita del ADR-0018
   §4 es que **la petición misma** ("quiero un valor numérico libre") es la
   señal de que el dato debería ir al esquema y no a una característica. Este
   test es la barrera.
2. **`TraitValueType` tiene exactamente cuatro filas.** Un test cuenta filas
   en `trait_value_types` y exige que sean 4, con etiquetas exactas. Si alguien
   intenta insertar una quinta, falla.
3. **`EscalaOrdinal` rechaza valores fuera del conjunto.** Sembrar una
   característica con `EscalaOrdinal { levels: [manso, normal, nervioso] }`.
   Intentar registrar `"agresivo"`: rechazado. Intentar `"3"` (numérico
   cuando se pidió string): rechazado con Problem Details.
4. **`ConteoAcotado` rechaza valores fuera de rango.** Sembrar
   `ConteoAcotado { min: 0, max: 20 }`. Registrar `25`: rechazado. Registrar
   `-1`: rechazado. Registrar `decimal (3.5)`: rechazado (no se admite
   decimal, por el guardarraíl).
5. **`especie` nula aplica a todas las especies.** Sembrar característica
   global. Verificar que aparece en observaciones para un bovino y para un
   porcino, sin distinción.
6. **`CurrentDisposition` refleja la última observación.** Sembrar tres
   observaciones sobre el mismo animal/trait en orden cronológico. Verificar
   que `CurrentDisposition(animalId, traitId)` devuelve la última.
7. **`trait_version_at_observation` se fija en INSERT.** Sembrar definición
   v1. Crear observación. Cambiar definición a v2. La observación sigue
   diciendo v1 al consultarla.
8. **Idempotencia de sync.** Misma operación push × 3 → una sola fila en
   `trait_observations`. Test obligatorio por `PLAN-FASE-3-4.md` §2.2.

Adicional recomendado:

- Auditoría `who/when` sobre cambios en definiciones.
- `filtro por categoría` activa (campo nullable en la definición) si la
  categoría lo requiere.
- Listas semilla con `kind = Morfológica` para tetas funcionales (mínimo
  configurable: por defecto 6, máximo 24).

## Lo que NO incluye (queda para 3.5b.5-B y 3.5b.5-C)

- **Absorción de `SelectionCriterion`** y deprecación de
  `MaternalBehaviorAssessment` → va en
  [`3.5b.5-B`](./PLAN-FASE-3-5-PORCINO-3.5b.5-B.md).
- **`visible_como_advertencia`** y la ficha del animal en el móvil → va en
  [`3.5b.5-C`](./PLAN-FASE-3-5-PORCINO-3.5b.5-C.md).
- **Versionado explícito de definiciones usadas** (clonar al editar, preservar
  la versión referenciada por observaciones viejas) → también en 3.5b.5-C.
- La evaluación **funcional de futuras madres** (sesión con captura masiva de
  morfológicas) — eso consume lo de esta sub-rama pero vive en una rama
  posterior a [3.5b.5-B](./PLAN-FASE-3-5-PORCINO-3.5b.5-B.md).
- El **índice de madre** del §4.6 — vive en el macro plan §4.6, depende de
  3.5b.5-A y 3.5b.5-B.

## Cómo probarlo

```bash
git fetch origin
git switch feature/livestock-animal-traits-core
dotnet test --configuration Release
dotnet ef database update --project src/Modules/Livestock/Hato.Modules.Livestock.Infrastructure --startup-project src/Hato.Api

# Test de arquitectura explícito
dotnet test --filter "Architecture|TraitArchitecture"
```

Para el flujo manual:

```bash
psql -d hato -c "SELECT key, kind, value_type FROM animal_traits ORDER BY key;"
# Insertar una nueva característica por SQL (no API). Verificar que aparece
# en el siguiente pull sin tocar código (Art. 8).
```

## Riesgos específicos de esta sub-rama

| Riesgo | Mitigación |
|---|---|
| Alguien agrega un quinto `TraitValueType` "porque hace falta" | Test #2 cuenta filas. Insertar la 5ª falla en CI antes de llegar al catálogo. |
| Decimal libre se cuela por una migración | Test #1 de arquitectura escanea el modelo entero. |
| `current_disposition` se almacena en una tabla | Implementación en una sola función derivada. Code review explícito del §Tarea 6. |
| `trait_version_at_observation` se llena mal | Test #7 explícito + invariante SQL (`NOT NULL`). |
| La migración rompe consumidores que leían `SelectionCriterion` | `SelectionCriterion` no se toca acá; vive hasta 3.5b.5-B. |
