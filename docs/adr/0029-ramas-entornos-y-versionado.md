# ADR-0029 — Dos ramas de larga vida, entornos para la promoción, y una sola fuente de versión

- **Estado:** Aceptado
- **Fecha:** 2026-09-15
- **Fase del roadmap:** Transversal. No abre ni cierra fase; habilita el criterio "en
  producción real" (Art. 11) de todas las que vengan.

## Contexto

Hechos verificados contra este repositorio y contra la API de GitHub el 2026-09-15
(develop en `5afbb72`):

- **El Artículo 12 de la Constitución es hoy falso.** Dice textual: *«El CI corre todo en
  cada PR; CI en rojo bloquea el merge a `develop` y `main`. Sin excepciones»*. La
  plataforma no lo aplica:

  ```
  $ gh api repos/:owner/:repo/branches/develop/protection/required_status_checks
  {"message":"Required status checks not enabled","status":"404"}
  ```

  La protección existe y lo que tiene configurado es correcto —`enforce_admins: true`,
  `allow_force_pushes: false`, `allow_deletions: false`, PR obligatorio con 0 aprobaciones
  requeridas— pero **sin checks obligatorios el resultado del CI es informativo**. Un PR en
  rojo se mergea igual.

- **No existe ningún entorno de GitHub:** `gh api repos/:owner/:repo/environments` devuelve
  `{"total_count":0}`. Tampoco hay rulesets: `gh api repos/:owner/:repo/rulesets` devuelve `[]`.

- **No existe ninguna versión declarada, y las tres que hay se contradicen:**

  ```
  $ ls VERSION                              → no existe
  $ grep '"version"' clients/*/package.json → admin-web: 0.0.0 · field-app: 1.0.0
  $ grep -rn "<Version>" Directory.Build.props src/  → sin resultados
  ```

  El "Paso 0 — Recolección de evidencia" de `docs/spec/README.md` documenta el costo ya
  pagado por esto: cuatro specs (0004, 0005, 0006, 0007) quedaron bloqueados porque *«no se
  conocen build de los teléfonos, versión del backend, ni qué significó "eliminar"»*, y
  media hora con el dueño y un teléfono los desbloqueaba.

- **El Art. 13 ya fija el modelo de ramas vigente:** `main` (releases etiquetadas) ←
  `develop` ← `feature/*`, `release/*`, `hotfix/*`.

- El dueño del proyecto trabaja con agentes de IA de forma deliberada e intensiva (así lo
  declara la retrospectiva de Fase 0 en `docs/ROADMAP.md`), y **los agentes commitean con su
  cuenta**. El historial dice qué ocurre sin compuerta: la Fase 3 se declaró cerrada con
  `clients/field-app/` sin una sola pantalla, y la auditoría posterior halló tres defectos
  que habrían perdido datos en producción sin dar ningún error.

La fuerza que motiva esta decisión es esa combinación: una regla constitucional que la
plataforma no aplica, un desarrollo acelerado por agentes con la autoridad del dueño, y
ningún mecanismo para saber qué versión corre en ninguna parte.

## Decisión

**Se mantienen dos ramas de larga vida, la promoción entre entornos la dan los
*environments* de GitHub y no una rama más, y la versión tiene una sola fuente.**

### 1. Ramas

`develop` y `main`, exactamente como ya manda el Art. 13. **No se crean ramas `staging` ni
`production`.**

| Rama | Despliega a | Gate |
|---|---|---|
| `develop` | staging | CI verde |
| `main` | producción | CI verde **+ revisor obligatorio del entorno** |

### 2. La compuerta

Los checks del CI pasan a ser **obligatorios** en ambas ramas, con `strict: true` (la rama
debe estar al día con su base), y se conserva `enforce_admins: true`.

El catálogo completo de qué verifica cada check y cuáles bloquean vive en
`docs/spec/feature-0011-devops-delivery-pipeline/spec.md` sec. 5.5, porque cambia más a
menudo que esta decisión. Lo que este ADR fija es el principio: **los metadatos de gestión
nunca bloquean, y el análisis estático entra informativo antes de bloquear.**

`required_approving_review_count` se queda en `0`. Con un único mantenedor, exigir una
aprobación que solo él puede dar enseña a aprobarse a uno mismo por trámite. Lo que
sustituye a la revisión humana aquí es la compuerta automática.

### 3. Versionado

Un archivo `VERSION` en la raíz, una línea, versionado semántico, **fuente única**. El CI la
estampa en los tres artefactos junto con el hash corto del commit y la fecha de build:

| Artefacto | Cómo la recibe |
|---|---|
| Backend .NET | `-p:Version=$(cat VERSION)` |
| `admin-web` | escrita en `package.json` durante el build |
| `field-app` | ídem, y llega a la build del teléfono |

**Y la API expone versión, commit y fecha de build en un endpoint de solo lectura.** Eso es
lo que convierte a `VERSION` en algo más que higiene: lo desplegado sabe decir qué es.

`main` es la única rama que mueve el número, en el mismo commit que crea el tag de release.

## Alternativas consideradas

- **Tres ramas de larga vida (`develop` → `staging` → `production`).** Es lo que el dueño
  propuso inicialmente. Descartada: obliga a un merge de promoción que no aporta ninguna
  información que el tag de release no dé, multiplica por tres el costo de sincronización
  para una sola persona, y contradice el Art. 13, que ya fija el modelo vigente. El corte
  entre entornos es un problema de credenciales y aprobación, no de ramas.

- **Dejar el Art. 12 como disciplina personal y no activar checks obligatorios.** El dueño
  lo planteó, con el argumento de que *«los agentes usan mi cuenta para los commits y por
  ende pueden llegar a hacer cosas que no deben»*. Descartada, y el argumento es el
  contrario: un actor que tiene la autoridad del dueño, trabaja más rápido de lo que el
  dueño puede revisar, y ya cerró una fase con una app sin pantallas, es exactamente el
  actor para el que existe una compuerta automática. Retirar el bloqueo porque el que entra
  tiene llave es quitar la cerradura.

- **Exigir una aprobación en el PR.** Descartada mientras haya un solo mantenedor, por lo
  dicho en la sec. 2. Se reabre el día que haya una segunda persona.

- **Versionar de forma independiente cada artefacto.** Es lo que hay hoy de facto, y produjo
  tres números que no concuerdan y ninguna forma de correlacionarlos con un backend. Para
  un monolito modular que se despliega junto (Art. 5), una sola versión es más simple y más
  útil que tres correctas por separado.

- **Derivar la versión de tags de git en vez de un archivo.** Es elegante y evita el commit
  de bump, pero deja el número invisible al leer el repositorio y depende de que el clon
  traiga los tags —que es justo lo que no ocurre en un runner con `fetch-depth: 1`. Un
  archivo se lee siempre.

## Consecuencias

**Lo bueno.** El Art. 12 deja de ser aspiracional y pasa a ser un mecanismo. Un agente con
la cuenta del dueño ya no puede mergear en rojo. La promoción a producción tiene un punto de
parada explícito. Y cualquiera puede preguntarle a una instancia desplegada qué versión es,
que es lo que faltó cuando llegó el reporte de campo.

**Lo malo.** Cada PR pasa a costar el tiempo del CI completo, sin excepción, incluso para
cambiar una línea de documentación. Es el precio y se paga a sabiendas. El bump de versión
añade un paso al release.

**Lo que hay que vigilar.**

1. **Que la compuerta no empuje a saltársela.** Si el CI se vuelve lento o ruidoso, la
   reacción natural es desactivar la protección "solo por hoy". Ese riesgo se mitiga en
   [ADR-0030](./0030-secretos-y-acceso-de-despliegue.md) con el check de deriva.
2. **Activar un check hoy roto deja el repositorio sin poder mergear nada.** Por eso el spec
   0011 sec. 5.4 define una compuerta TG0 que verifica el verde actual antes de activar, con
   decisión explícita sobre `ng test` de `admin-web`, que `docs/BACKLOG.md` declara roto.
3. **Que los tres artefactos se estampen de verdad** y no queden dos con la versión correcta
   y uno con la escrita a mano.

**Condición de reversa.** Se reabre esta decisión si: aparece una segunda persona en el
proyecto (cambia el cálculo de las aprobaciones), o si la compuerta obliga sistemáticamente
a esperas que hacen inviable trabajar y la medición lo demuestra —no la impresión, la
medición de lead time de `docs/spec/feature-0011-devops-delivery-pipeline/spec.md` sec. 9.
