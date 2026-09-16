# plan.md — Ejecución de la rama `feature/devops-delivery-pipeline`

> **Qué es este documento.** El orden y los commits en que se construye lo que
> [`spec.md`](./spec.md) decidió. El desglose ejecutable con casillas está en
> [`tasks.md`](./tasks.md) y la verificación manual en [`test-e2e.md`](./test-e2e.md).
> Las decisiones no se relitigan acá: si algo no cuadra, se corrige el spec primero.

**Objetivo:** que el Artículo 12 deje de ser una frase y pase a ser una compuerta, que exista
un entorno donde probar antes de la finca, y que lo desplegado sepa decir qué versión es.

**Enfoque:** una rama, **once commits secuenciales** por propósito, más **dos compuertas** que
detienen el trabajo si su verificación falla (TG0 antes de empezar, TG1 antes de activar los
checks obligatorios) y **cuatro pasos operativos** que no son commits porque no viven en el
repositorio: se aplican contra la API de GitHub y contra el servidor.

**Spec:** [`spec.md`](./spec.md).

---

## Restricciones globales

Copiadas de las decisiones de `spec.md` que aplican a **todos** los commits:

- **Ningún secreto entra al repositorio en ninguna forma**, ni de ejemplo, ni en un log, ni en
  una línea de comandos que quede registrada (D4, sec. 6.2).
- **No se registra ningún runner self-hosted** (D5). El repositorio es público.
- **El workflow de despliegue se dispara solo en `push` a `develop`**, nunca en
  `pull_request`, para que un fork no reciba secretos (sec. 6.2 regla 2).
- **`.env` y `.env.example` no se tocan** (sec. 6.3). Este trabajo añade un segundo mecanismo
  donde no había ninguno; no reemplaza el de desarrollo.
- **Esta rama no toca `src/` ni `clients/` salvo configuración de despliegue, versionado y
  formato** (sec. 4, "No entra"). La excepción declarada es el endpoint de versión del
  commit 2, que sí es código de aplicación y por eso lleva prueba.
- **Un PR = un propósito** (`AGENTS.md` regla 9). La deuda ajena que aparezca se anota en
  `docs/BACKLOG.md`, no se arregla aquí.

---

## Índice

1. [Compuerta TG0 — antes del primer commit](#compuerta-tg0--antes-del-primer-commit)
2. [Commit 1 — la versión tiene una sola fuente](#commit-1--la-versión-tiene-una-sola-fuente)
3. [Commit 2 — lo desplegado dice qué versión es](#commit-2--lo-desplegado-dice-qué-versión-es)
4. [Commit 3 — higiene de rama, commits y PR](#commit-3--higiene-de-rama-commits-y-pr)
5. [Commit 4 — herramientas de formato y lint](#commit-4--herramientas-de-formato-y-lint)
6. [Commit 5 — el primer formateo, mecánico y solo](#commit-5--el-primer-formateo-mecánico-y-solo)
7. [Commit 6 — formato y lint exigidos en el CI](#commit-6--formato-y-lint-exigidos-en-el-ci)
8. [Commit 7 — análisis estático informativo](#commit-7--análisis-estático-informativo)
9. [Commit 8 — Dependabot acotado y etiquetado de metadatos](#commit-8--dependabot-acotado-y-etiquetado-de-metadatos)
10. [Commit 9 — la configuración esperada, versionada y vigilada](#commit-9--la-configuración-esperada-versionada-y-vigilada)
11. [Commit 10 — despliegue a staging](#commit-10--despliegue-a-staging)
12. [Commit 11 — línea base de métricas y documentación](#commit-11--línea-base-de-métricas-y-documentación)
13. [Pasos operativos (no son commits)](#pasos-operativos-no-son-commits)
14. [Orden, dependencias y puntos de no retorno](#orden-dependencias-y-puntos-de-no-retorno)
15. [Descripción del PR](#descripción-del-pr)

---

## Compuerta TG0 — antes del primer commit

**No se escribe una línea hasta que las tres se cumplan.** Es la compuerta de `spec.md`
sec. 5.4, ampliada con los ADR.

1. **Los cuatro ADR están mergeados** (D0): 0029 ramas/entornos/versionado, 0030
   secretos/acceso, 0031 staging, 0032 formato/lint. Art. 14 y `AGENTS.md` regla 2 — sin
   ellos, este trabajo añade dependencias y cambia protocolo sin decisión escrita.
2. **Los tres jobs actuales pasan hoy en `develop`.** Si alguno está en rojo, arreglarlo es
   el trabajo previo y no parte de esta rama.
3. **Estado real de `ng test` en `admin-web` decidido explícitamente.** `docs/BACKLOG.md` lo
   declara roto. Salidas admitidas: (a) `admin-web-ci` se excluye de la lista obligatoria y
   queda como deuda anotada con fecha, o (b) se arregla primero en su propia rama. **No se
   admite** volverlo obligatorio y descubrir el bloqueo con el primer PR.

> **Si TG0.3 resulta ser (b)**, esta rama se detiene hasta que aquella mergee. Es una
> detención legítima, no un retraso: activar una compuerta rota deja el repositorio sin poder
> mergear nada.

---

## Commit 1 — la versión tiene una sola fuente

`chore: add VERSION as the single source of the build version`

**Por qué acá:** el commit 2 y el commit 10 leen `VERSION`. Nada más depende de esto, así que
va primero y sin riesgo.

**Archivos:**
- Crear: `VERSION` — una línea, versionado semántico. Valor inicial acordado con el dueño.
- Modificar: `.github/workflows/ci.yml` — los tres jobs leen `VERSION` y la estampan:
  `-p:Version=` en el build de .NET, y escritura en `package.json` durante el build de cada
  cliente. Se añaden el hash corto del commit y la fecha de build.
- Modificar: `README.md` — una línea diciendo dónde vive la versión y que no se edita a mano
  en tres sitios.

**Verificación:** `cat VERSION` devuelve una sola línea que cumple `^[0-9]+\.[0-9]+\.[0-9]+$`.
El build de `admin-web` en CI emite esa misma cadena, no `0.0.0`.

## Commit 2 — lo desplegado dice qué versión es

`feat(api): expose build version, commit and date on a read-only endpoint`

**Por qué acá:** necesita el commit 1. Y va antes del despliegue (commit 10) porque el smoke
test de D8 lo usa como señal de vida.

**Archivos:**
- Crear/Modificar en `src/Hato.Api/` — endpoint de solo lectura, sin autenticación y **sin
  ningún dato de negocio**: versión, hash corto y fecha de build.
- Crear: prueba de integración que lo ejerza contra PostgreSQL real, como manda
  `AGENTS.md` regla 5.
- Modificar: pantalla de diagnóstico de `clients/field-app` — muestra la versión junto a la
  bitácora que `feature-0004` ya dejó implementada.

**Por qué esto justifica tocar código de aplicación:** `spec.md` sec. 2.9 — cuatro specs
quedaron bloqueados porque nadie podía decir qué build corría en los teléfonos. El archivo
`VERSION` no resuelve eso; el endpoint sí.

**Verificación:** `dotnet test` en verde, y `curl` local contra el endpoint devuelve la cadena
de `VERSION`.

## Commit 3 — higiene de rama, commits y PR

`ci: add the pr-hygiene job`

**Por qué acá:** independiente de todo lo demás y de los más baratos. Empieza a dar valor de
inmediato, y conviene que exista antes de los commits grandes para que los verifique.

**Archivos:**
- Modificar: `.github/workflows/ci.yml` — job `pr-hygiene`, sin red ni dependencias pesadas:
  1. nombre de rama contra `feature/*`, `release/*`, `hotfix/*`, `docs/*`
     (`AGENTS.md` regla 4, Art. 13);
  2. mensajes de commit en Conventional Commits con ámbito = módulo;
  3. título del PR en el mismo formato, porque es lo que queda en el historial al mergear.

**Verificación:** el job falla contra una rama de prueba mal nombrada y pasa contra esta.

## Commit 4 — herramientas de formato y lint

`chore(clients): add prettier and eslint tooling`

**Por qué acá:** ADR-0032. Se separa deliberadamente del formateo masivo (commit 5) y de la
exigencia en CI (commit 6): tres cosas distintas, tres commits.

**Archivos:**
- Modificar: `clients/field-app/package.json` — `prettier` y `eslint` con config de Expo.
- Modificar: `clients/admin-web/package.json` — `eslint` con config de Angular; `prettier` ya
  está declarado (`^3.8.1`) y hoy **nada lo invoca**.
- Crear: configuración **única** de `prettier` compartida en `clients/`. Dos formatos en un
  mismo repositorio son dos formatos que alguien va a confundir (ADR-0032 sec. 1).
- Crear: scripts `format:check` y `lint` en ambos `package.json`.

**Verificación:** `npm run format:check` corre en ambos clientes y **falla**, porque el
formateo todavía no se aplicó. Eso es lo esperado en este punto.

## Commit 5 — el primer formateo, mecánico y solo

`style(clients): apply the first prettier pass`

**Por qué acá:** ADR-0032 sec. 3. Reescribe cientos de archivos y **no comparte commit con
ningún cambio funcional**: un cambio de lógica escondido en un diff de ese tamaño es
irrevisable.

**Archivos:**
- Modificar: todo `clients/**` que `prettier` toque. Nada más.
- Crear: `.git-blame-ignore-revs` con el hash de este commit, para que `git blame` siga
  sirviendo.

**Verificación:** `npm run format:check` pasa en ambos clientes. `git show --stat` de este
commit no lista ningún archivo fuera de `clients/` salvo `.git-blame-ignore-revs`.

> **Este es el commit costoso de revertir.** Ver sec. "Orden".

## Commit 6 — formato y lint exigidos en el CI

`ci: enforce formatting and lint in the client jobs`

**Por qué acá:** después del commit 5. Al revés, el primer PR de cualquiera fallaría por
archivos que él no tocó.

**Archivos:**
- Modificar: `.github/workflows/ci.yml` — pasos dentro de `field-app-ci` y `admin-web-ci`.
  **No se crean jobs nuevos** (ADR-0032 sec. 2): el costo real es el número de jobs, no el de
  verificaciones.

**Verificación:** ambos jobs en verde en este PR.

## Commit 7 — análisis estático informativo

`ci: add CodeQL as an advisory check`

**Por qué acá:** independiente. Va antes del commit 9 para que la configuración esperada de
ramas ya lo conozca y lo declare **no obligatorio**.

**Archivos:**
- Crear: `.github/workflows/codeql.yml` — C# y TypeScript/JavaScript.
- Modificar: `docs/BACKLOG.md` — la promoción a obligatorio, anotada **con fecha**, para que
  no quede al criterio del momento (D16, riesgo declarado en sec. 10).

**Verificación:** CodeQL aparece entre los checks del PR y **no** entre los obligatorios.

## Commit 8 — Dependabot acotado y etiquetado de metadatos

`ci: add dependabot for security updates and label missing PR metadata`

**Por qué acá:** dos añadidos pequeños, ninguno bloqueante, sin dependencias entre sí ni con
el resto.

**Archivos:**
- Crear: `.github/dependabot.yml` — **solo** actualizaciones de seguridad y acciones de
  GitHub Actions (D9). Nada de versiones de NuGet ni npm: con `AGENTS.md` regla 2 exigiendo
  ADR por dependencia, un flujo de PRs automáticos de versiones sería ruido que nadie puede
  mergear.
- Modificar: `.github/workflows/ci.yml` — etiquetado automático cuando falta hito o
  etiquetas. **Señala, no bloquea** (D15).

**Verificación:** un PR sin hito recibe la señal **y se puede mergear igual**.

## Commit 9 — la configuración esperada, versionada y vigilada

`ci: declare the expected branch and environment configuration and check for drift`

**Por qué acá:** necesita que existan todos los jobs (commits 3, 6, 7, 8) para poder
declararlos. Y el **paso operativo 3** (aplicar la configuración) tiene que ocurrir antes de
que este job pase; hasta entonces falla, y eso es correcto.

**Archivos:**
- Crear: `.github/branch-protection.expected.json` (o equivalente declarativo) — protección de
  `develop` y `main`, entornos, y qué checks son obligatorios y cuáles no.
- Crear: `scripts/apply-repo-config.sh` — aplica esa configuración por API. Principio 3 de
  `home-server`: *«la configuración se versiona, no se hace clic»*.
- Modificar: `.github/workflows/ci.yml` — job de deriva que compara lo declarado contra lo que
  devuelve la API y **nombra el campo que cambió**.

**Lo que este job no hace:** impedir el cambio. No existe forma de impedirlo en un repositorio
personal (ADR-0030 sec. 3), y escribirlo aquí de otro modo sería mentir en el documento que se
lee cuando algo falla.

**Verificación:** cambiar a mano un campo de la protección de `develop` hace fallar el job
nombrando ese campo; revertirlo lo devuelve a verde.

## Commit 10 — despliegue a staging

`ci: deploy develop to staging over tailscale`

**Por qué acá:** es el commit que más depende de los demás — necesita la versión (1 y 2) y los
entornos y secretos creados en el **paso operativo 2**.

**Archivos:**
- Crear: `.github/workflows/deploy-staging.yml` — `push` a `develop`, entorno `staging`,
  runner **alojado** que se une al tailnet como nodo efímero, SSH, despliegue, smoke test.
- Crear: `compose.staging.yml` — `ASPNETCORE_ENVIRONMENT: Staging`, **sin publicar puertos al
  host** (lo expone el Caddy que ya corre), volumen bajo `/srv`, imágenes con versión fija,
  nunca `latest` (ADR-0031).
- Modificar: `scripts/smoke-api-container.sh` si hace falta apuntarlo al despliegue remoto.

**Verificación:** el job termina en verde **solo** después de que el smoke test responda. Desde
un dispositivo del tailnet la API responde; desde fuera no resuelve, y el servidor no tiene
ningún puerto nuevo publicado hacia el router.

**Dependencia externa:** el stack tiene que declararse en el repositorio `home-server`
(ADR-0031). Es un commit **en aquel repositorio** y esta rama no se considera terminada sin él.

## Commit 11 — línea base de métricas y documentación

`docs: record the delivery baseline and update the documentation map`

**Por qué acá:** último, para que la documentación describa lo que realmente quedó. Pero el
**cálculo** de la línea base ocurre en el paso operativo 1, antes de todo (D10).

**Archivos:**
- Crear: script re-ejecutable que calcula las métricas desde git y la API, y el documento con
  los resultados. **Las dos métricas de fiabilidad baja se declaran como tales** (sec. 9);
  inflarlas sería peor que no tenerlas.
- Modificar: `docs/DOCUMENTACION.md` — fila por documento nuevo, en este mismo commit, como
  manda su propia regla.
- Modificar: `docs/SEGURIDAD.md` — modelo de acceso de despliegue.
- Modificar: `docs/BACKLOG.md` — deuda ajena tocada y no arreglada: `ng test` de `admin-web`
  y la rotación de `SERVER_PASSWORD` en `home-server`.
- Modificar: `README.md` — entornos y cómo se despliega.

**Verificación:** `ls docs/*.md` y la taxonomía de `DOCUMENTACION.md` siguen cuadrando sin
resto.

---

## Pasos operativos (no son commits)

No viven en el repositorio: se aplican contra la API de GitHub y contra el servidor. Van en
`tasks.md` con casilla, porque olvidarlos deja el trabajo a medias sin que nada falle.

| # | Paso | Cuándo | Por qué ahí |
|---|---|---|---|
| **O1** | Calcular y archivar la línea base de métricas | **Antes del commit 1** | D10: la compuerta destruye la posibilidad de observar el "antes" |
| **O2** | Crear entornos `staging` y `production`, secretos, revisor obligatorio, clave SSH dedicada y usuario restringido en el servidor | Antes del commit 10 | El workflow no puede desplegar sin ellos |
| **O3** | Aplicar la configuración declarada con `scripts/apply-repo-config.sh` | Después del commit 9 | El job de deriva falla hasta que se aplique |
| **O4** | Registrar los checks obligatorios y **reducir el permiso de la credencial de los agentes** | **Compuerta TG1**, al final | Es lo que cambia cómo funciona todo PR futuro |

### Compuerta TG1 — antes de O4

1. Todos los jobs que se van a exigir están **verdes en este PR**.
2. La decisión de TG0.3 sobre `admin-web-ci` está aplicada en la configuración declarada.
3. La credencial nueva de los agentes está creada y probada **antes** de reducir la vieja, o
   el siguiente trabajo empieza sin poder empujar nada.

---

## Orden, dependencias y puntos de no retorno

```
TG0 (4 ADR + CI verde + decisión sobre ng test)
  └─ O1  línea base ─── IRREVERSIBLE si se omite: después ya no es observable
       └─ Commit 1  VERSION
            ├─ Commit 2  endpoint de versión ── lo necesita el smoke test del 10
            └─ Commit 10 despliegue (también necesita O2)
       └─ Commit 3  pr-hygiene            (independiente)
       └─ Commit 4  herramientas
            └─ Commit 5  formateo masivo  ── COSTOSO DE REVERTIR (git blame)
                 └─ Commit 6  exigir formato y lint
       └─ Commit 7  CodeQL                (independiente)
       └─ Commit 8  dependabot + etiquetas (independiente)
            └─ Commit 9  configuración declarada + deriva
                 └─ O3  aplicar configuración
                      └─ Commit 11 documentación
                           └─ TG1 → O4  activar compuerta y reducir credencial
```

**Puntos de no retorno, y son dos de naturaleza distinta:**

- **O1 es el único irreversible de verdad.** No por lo que hace, sino por lo que se pierde si
  se omite: una vez activados los checks, el "antes" deja de existir. Ningún commit posterior
  lo reconstruye.
- **El commit 5 es el costoso de revertir**: `git revert` funciona, pero el ruido en
  `git blame` ya quedó. Por eso lleva `.git-blame-ignore-revs` en el mismo commit.
- **O4 no es irreversible** —se desactiva por API en un minuto— pero es el que cambia cómo
  funciona todo PR futuro, y por eso lleva compuerta propia.

---

## Descripción del PR

**Título:** `ci: gate the merges, version the builds and deploy develop to staging`

**Cuerpo:**

- **Qué:** convierte el Art. 12 en una compuerta real (checks obligatorios en `develop` y
  `main`), da al repositorio una sola fuente de versión con un endpoint que la responde, añade
  formato y lint a los clientes, y despliega `develop` a un entorno de staging sobre Tailscale
  sin abrir un puerto ni registrar un runner propio.
- **Por qué:** el Artículo 12 dice que un CI en rojo bloquea el merge, y la plataforma no lo
  aplicaba — `required_status_checks` devolvía 404 en ambas ramas. El desarrollo está
  deliberadamente acelerado con agentes de IA que commitean con la cuenta del dueño, y el
  historial ya registra qué pasa sin compuerta: una fase cerrada con una app sin pantallas y
  tres defectos que habrían perdido datos sin dar ningún error.
- **Decisiones:** [`spec.md` sec. 3](./spec.md#3-decisiones-fijadas), respaldadas por
  ADR-0029 (ramas, entornos y versionado), ADR-0030 (secretos y acceso), ADR-0031 (staging) y
  ADR-0032 (formato y lint).
- **Qué NO incluye:** despliegue a producción — no hay destino y el servidor doméstico está
  descalificado por ADR-0031; migrar los secretos de `home-server`; arreglar `ng test` de
  `admin-web`; backups de staging; ningún registro de imágenes; mover el repositorio a una
  organización.
- **Cómo probarlo:** ejecutar [`test-e2e.md`](./test-e2e.md).
- **Riesgo declarado:** el commit 5 reescribe cientos de archivos de forma mecánica y ensucia
  `git blame` (mitigado con `.git-blame-ignore-revs`). La activación de O4 cambia cómo
  funciona todo PR futuro y se hace al final, tras la compuerta TG1.
