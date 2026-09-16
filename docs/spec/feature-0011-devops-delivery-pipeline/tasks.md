# tasks.md — Desglose ejecutable

> Checklist de la rama `feature/devops-delivery-pipeline`. Cada tarea es una unidad de trabajo
> con criterio de terminado verificable, agrupada por el commit de [`plan.md`](./plan.md) al
> que pertenece. Las decisiones están en [`spec.md`](./spec.md) y la verificación manual en
> [`test-e2e.md`](./test-e2e.md).
>
> Convención: `[ ]` pendiente · `[x]` hecho · `[!]` bloqueada.

---

## Compuerta TG0 — antes del primer commit

- [ ] **TG0.1** Verificar que los cuatro ADR están mergeados en `develop`.
      **Terminado:** `ls docs/adr/0029* docs/adr/0030* docs/adr/0031* docs/adr/0032*` los
      encuentra estando en `develop`, y `grep -l "Estado:.*Aceptado" docs/adr/003[012]* docs/adr/0029*`
      devuelve los cuatro.
- [ ] **TG0.2** Verificar que los tres jobs actuales pasan hoy en `develop`.
      **Terminado:** `gh run list --branch develop --workflow ci.yml --limit 1 --json conclusion`
      devuelve `success`.
- [ ] **TG0.3** Decidir explícitamente el estado de `ng test` en `admin-web` y anotarlo.
      **Terminado:** `cd clients/admin-web && npx ng test --watch=false` corre y se registra su
      resultado en este archivo, junto a la salida elegida: **(a)** `admin-web-ci` queda fuera
      de la lista obligatoria con la deuda anotada con fecha en `docs/BACKLOG.md`, o **(b)** se
      abre rama aparte para arreglarlo y **esta rama se detiene**.
      **Decisión tomada:** ______ (fecha: ______)

---

## Paso operativo O1 — línea base, antes de todo

> D10. Es lo único irreversible del trabajo: después de activar los checks, el "antes" ya no
> es observable.

- [ ] **O1.1** Calcular lead time de cambio sobre los PRs mergeados (primer commit de la rama
      → fecha de merge).
      **Terminado:** existe un archivo con una fila por PR y su lead time, derivado de
      `gh pr list --state merged --limit 200 --json number,createdAt,mergedAt,headRefName`.
- [ ] **O1.2** Calcular frecuencia de integración: merges a `develop` por semana.
      **Terminado:** serie semanal derivada de `git log --merges --first-parent develop --date=short`.
- [ ] **O1.3** Clasificar a mano la tasa de fallo del cambio y el tiempo de restauración sobre
      los incidentes documentados.
      **Terminado:** tabla con los incidentes de `docs/ROADMAP.md` ("Fase 3 — cierre revertido"
      y la serie `feature-0004`) y **la nota explícita de que estas dos métricas son de
      fiabilidad baja**, tal como declara `spec.md` sec. 9.
- [ ] **O1.4** Archivar el resultado fuera del alcance de esta rama, para que el commit 11 lo
      recoja ya calculado.
      **Terminado:** el archivo existe y su ruta está anotada aquí: ______

---

## Commit 1 — la versión tiene una sola fuente

- [ ] **T1.1** Acordar con el dueño el valor inicial de `VERSION` y crear el archivo.
      **Terminado:** `cat VERSION` devuelve una sola línea que cumple
      `^[0-9]+\.[0-9]+\.[0-9]+$`, verificable con
      `grep -Eq '^[0-9]+\.[0-9]+\.[0-9]+$' VERSION && echo ok`.
- [ ] **T1.2** Estampar la versión en el build de .NET.
      **Terminado:** el job `backend-build-and-test` pasa `-p:Version=$(cat VERSION)` y el
      ensamblado resultante reporta esa versión.
- [ ] **T1.3** Estampar la versión en los dos clientes durante el build, sin editar
      `package.json` a mano.
      **Terminado:** tras el build, ni `admin-web` reporta `0.0.0` ni `field-app` reporta
      `1.0.0` escritos a mano.
- [ ] **T1.4** Añadir hash corto del commit y fecha de build al estampado.
      **Terminado:** ambos valores llegan al artefacto; son lo que distingue dos builds que
      comparten número de versión.
- [ ] **T1.5** Documentar en `README.md` dónde vive la versión.
      **Terminado:** `grep -n "VERSION" README.md` lo encuentra.

## Commit 2 — lo desplegado dice qué versión es

- [ ] **T2.1** Añadir el endpoint de versión a `src/Hato.Api/`, de solo lectura y sin
      autenticación.
      **Terminado:** responde versión, hash y fecha de build, **y ningún dato de negocio** —
      verificable leyendo el contrato de respuesta.
- [ ] **T2.2** Cubrirlo con prueba de integración contra PostgreSQL real (`AGENTS.md` regla 5).
      **Terminado:** `dotnet test -c Release` en verde con la prueba nueva incluida.
- [ ] **T2.3** Mostrar la versión en la pantalla de diagnóstico de `field-app`, junto a la
      bitácora que `feature-0004` dejó implementada.
      **Terminado:** `npm test` y `npm run typecheck` en verde en `clients/field-app`.
- [ ] **T2.4** Comprobar el endpoint a mano contra la API local.
      **Terminado:** `curl` devuelve la misma cadena que `cat VERSION`.

## Commit 3 — higiene de rama, commits y PR

- [ ] **T3.1** Añadir el job `pr-hygiene` que valida el nombre de la rama contra
      `feature/*`, `release/*`, `hotfix/*`, `docs/*`.
      **Terminado:** el job falla en una rama de prueba mal nombrada y pasa en esta.
- [ ] **T3.2** Validar los mensajes de commit contra Conventional Commits, con ámbito = módulo.
      **Terminado:** un commit de prueba con mensaje inválido hace fallar el job.
- [ ] **T3.3** Validar el título del PR con el mismo formato.
      **Terminado:** cambiar el título a uno inválido pone el check en rojo; corregirlo lo
      devuelve a verde. Es el criterio 11 de `spec.md` sec. 11.
- [ ] **T3.4** Verificar que el job no necesita red ni dependencias pesadas.
      **Terminado:** su duración en la ejecución de CI es inferior a la de cualquier otro job
      por al menos un orden de magnitud.

## Commit 4 — herramientas de formato y lint

> Requiere ADR-0032 mergeado (TG0.1). Son dependencias nuevas: `AGENTS.md` regla 2.

- [ ] **T4.1** Añadir `prettier` y `eslint` a `clients/field-app` con configuración de Expo.
      **Terminado:** ambos aparecen en `devDependencies` y `npx prettier --version` responde
      dentro de ese cliente.
- [ ] **T4.2** Añadir `eslint` a `clients/admin-web` con configuración de Angular.
      **Terminado:** ídem. (`prettier` ya estaba declarado como `^3.8.1` y **nada lo
      invocaba** — `spec.md` sec. 2.10.)
- [ ] **T4.3** Crear **una sola** configuración de `prettier` compartida en `clients/`.
      **Terminado:** existe un único archivo de configuración y ningún cliente define el suyo
      propio, verificable con `find clients -maxdepth 2 -name ".prettierrc*"`.
- [ ] **T4.4** Añadir scripts `format:check` y `lint` a ambos `package.json`.
      **Terminado:** `npm run format:check` existe en los dos y **falla** — el formateo aún no
      se aplicó, y eso es lo esperado aquí.

## Commit 5 — el primer formateo, mecánico y solo

- [ ] **T5.1** Ejecutar `prettier --write` sobre ambos clientes.
      **Terminado:** `npm run format:check` pasa en los dos.
- [ ] **T5.2** Comprobar que este commit **no** lleva ningún cambio funcional.
      **Terminado:** `git show --stat` no lista ningún archivo fuera de `clients/` salvo
      `.git-blame-ignore-revs`, y la suite de ambos clientes sigue en verde sin tocar una
      prueba.
- [ ] **T5.3** Crear `.git-blame-ignore-revs` con el hash de este commit.
      **Terminado:** el archivo existe y `git blame --ignore-revs-file .git-blame-ignore-revs`
      sobre un archivo reformateado atribuye las líneas al autor original.

## Commit 6 — formato y lint exigidos en el CI

- [ ] **T6.1** Añadir los pasos de formato y lint **dentro** de `field-app-ci` y
      `admin-web-ci`.
      **Terminado:** `grep -c "^  [a-z-]*:$" .github/workflows/ci.yml` no aumenta respecto al
      commit anterior — **no se crearon jobs nuevos** (ADR-0032 sec. 2).
- [ ] **T6.2** Verificar que ambos jobs quedan en verde en este PR.
      **Terminado:** la ejecución de CI de este commit es `success`.

## Commit 7 — análisis estático informativo

- [ ] **T7.1** Crear `.github/workflows/codeql.yml` para C# y TypeScript/JavaScript.
      **Terminado:** el workflow aparece en la ejecución del PR.
- [ ] **T7.2** Dejar CodeQL **fuera** de los checks obligatorios.
      **Terminado:** criterio 16 de `spec.md` sec. 11 — CodeQL figura entre los checks del PR
      y no en la lista obligatoria.
- [ ] **T7.3** Anotar en `docs/BACKLOG.md` su promoción a obligatorio, **con fecha**.
      **Terminado:** `grep -n "CodeQL" docs/BACKLOG.md` encuentra la entrada con fecha. Sin
      esto, el riesgo declarado en `spec.md` sec. 10 se materializa: queda informativo para
      siempre y nadie lo mira.

## Commit 8 — Dependabot acotado y etiquetado de metadatos

- [ ] **T8.1** Crear `.github/dependabot.yml` limitado a seguridad y a acciones de GitHub
      Actions.
      **Terminado:** criterio 9 de `spec.md` sec. 11 — el archivo **no** contiene ecosistemas
      de actualización de versión para NuGet ni npm.
- [ ] **T8.2** Añadir el etiquetado automático de metadatos faltantes.
      **Terminado:** un PR sin hito recibe la señal.
- [ ] **T8.3** Verificar que ese etiquetado **no bloquea**.
      **Terminado:** criterio 17 — ese mismo PR se puede mergear igual.

## Commit 9 — la configuración esperada, versionada y vigilada

- [ ] **T9.1** Declarar la configuración esperada de protección de `develop` y `main`,
      entornos, y qué checks son obligatorios y cuáles no.
      **Terminado:** el archivo existe, es válido, y la decisión de TG0.3 sobre `admin-web-ci`
      está reflejada en él.
- [ ] **T9.2** Escribir `scripts/apply-repo-config.sh`, que la aplica por API.
      **Terminado:** el script corre y es idempotente — dos ejecuciones seguidas dejan el
      mismo estado.
- [ ] **T9.3** Añadir el job de deriva.
      **Terminado:** criterio 12 — un cambio manual en la protección de `develop` hace fallar
      el job **nombrando el campo que cambió**; revertirlo lo devuelve a verde.
- [ ] **T9.4** Comprobar que el job **no** afirma impedir el cambio.
      **Terminado:** su mensaje de fallo y el comentario del workflow dicen que detecta, no que
      bloquea (ADR-0030 sec. 3).

## Paso operativo O2 — entornos, secretos y acceso al servidor

- [ ] **O2.1** Crear los entornos `staging` y `production`, este último con revisor
      obligatorio.
      **Terminado:** criterio 4 — `gh api repos/:owner/:repo/environments --jq '.total_count'`
      devuelve `2` y `production` tiene al menos un revisor.
- [ ] **O2.2** Generar una clave SSH **dedicada** al despliegue y crear en el servidor un
      usuario restringido al stack de staging, **sin sudo**.
      **Terminado:** con esa clave se puede operar el stack y `sudo -n true` falla.
- [ ] **O2.3** Cargar los secretos **a nivel de entorno**, nunca de repositorio.
      **Terminado:** criterio 5 — la lista de secretos de repositorio está vacía.
- [ ] **O2.4** Confirmar que la `SERVER_PASSWORD` existente no se usa en ninguna parte de este
      trabajo, y anotar su rotación como deuda de `home-server`.
      **Terminado:** `grep -ri "SERVER_PASSWORD" .github/` no devuelve nada.

## Commit 10 — despliegue a staging

- [ ] **T10.1** Crear `compose.staging.yml`.
      **Terminado:** declara `ASPNETCORE_ENVIRONMENT: Staging`, **no publica puertos al host**,
      usa volumen bajo `/srv` y **ninguna imagen con `latest`** — principios 4 y 7 de
      `home-server`.
- [ ] **T10.2** Crear `.github/workflows/deploy-staging.yml`, disparado **solo** en `push` a
      `develop`.
      **Terminado:** `grep -A3 "^on:" .github/workflows/deploy-staging.yml` no menciona
      `pull_request` — regla 2 de `spec.md` sec. 6.2.
- [ ] **T10.3** Unir el runner alojado al tailnet como nodo efímero.
      **Terminado:** criterio 8 — no hay ningún runner self-hosted registrado en el
      repositorio.
- [ ] **T10.4** Encadenar el smoke test como condición de éxito del job.
      **Terminado:** criterio 6 — el job termina en verde **solo** después de que el smoke test
      responda contra la API desplegada. Se comprueba rompiendo el despliegue a propósito una
      vez y viendo el job en rojo.
- [ ] **T10.5** Verificar el aislamiento de red.
      **Terminado:** criterio 7 — desde el tailnet la API responde, desde fuera no resuelve, y
      el servidor no tiene ningún puerto nuevo publicado hacia el router.
- [ ] **T10.6** Comprobar que un despliegue fallido deja staging en la versión anterior.
      **Terminado:** tras un fallo provocado, el contenedor anterior sigue sirviendo
      (`plan.md` commit 10; ADR-0031: no hay rollback automático a propósito).
- [ ] **T10.7** Declarar el stack en el repositorio `home-server`.
      **Terminado:** existe el commit **en aquel repositorio**. Su README manda: *«si no está
      en este repo, no debería estar en el servidor»*. **Sin esto la rama no está terminada.**

## Paso operativo O3 — aplicar la configuración

- [ ] **O3.1** Ejecutar `scripts/apply-repo-config.sh`.
      **Terminado:** el job de deriva de T9.3 pasa a verde.

## Commit 11 — línea base de métricas y documentación

- [ ] **T11.1** Versionar el script re-ejecutable de métricas y el documento con los resultados
      de O1.
      **Terminado:** criterio 10 — el documento existe con sus números, el script vuelve a
      producirlos, y las dos métricas de fiabilidad baja están declaradas como tales.
- [ ] **T11.2** Decidir dónde vive ese documento.
      **Terminado:** está resuelto y anotado. No contiene ningún dato personal, así que **no
      está obligado** a vivir en `docs/tesis/privado/` — es la pregunta abierta de `spec.md`
      sec. 9.
- [ ] **T11.3** Añadir fila en `docs/DOCUMENTACION.md` por cada documento nuevo, **en este
      mismo commit**.
      **Terminado:** criterio 19 — la taxonomía cubre sin resto lo que devuelven `ls *.md` y
      `ls docs/*.md`, y el recuento de entradas del párrafo posterior a la tabla concuerda.
- [ ] **T11.4** Describir el modelo de acceso de despliegue en `docs/SEGURIDAD.md`.
      **Terminado:** `grep -n "despliegue" docs/SEGURIDAD.md` lo encuentra.
- [ ] **T11.5** Anotar en `docs/BACKLOG.md` la deuda ajena tocada y no arreglada.
      **Terminado:** figuran `ng test` de `admin-web` y la rotación de `SERVER_PASSWORD`, con
      fecha.
- [ ] **T11.6** Actualizar `README.md` con los entornos y cómo se despliega.
      **Terminado:** `grep -n "staging" README.md` lo encuentra.

---

## Compuerta TG1 — antes de activar la compuerta

- [ ] **TG1.1** Todos los jobs que se van a exigir están **verdes en este PR**.
      **Terminado:** la última ejecución de CI de la rama es `success`.
- [ ] **TG1.2** La decisión de TG0.3 sobre `admin-web-ci` está aplicada en la configuración
      declarada de T9.1.
      **Terminado:** se lee en el archivo declarativo, no en la memoria de nadie.
- [ ] **TG1.3** La credencial nueva de los agentes está creada y **probada** antes de reducir
      la vieja.
      **Terminado:** con la credencial nueva se puede clonar, ramificar y empujar. Al revés,
      el siguiente trabajo empieza sin poder empujar nada.

## Paso operativo O4 — activar

- [ ] **O4.1** Registrar los checks obligatorios en `develop` y `main` con `strict: true`.
      **Terminado:** criterio 1 —
      `gh api repos/:owner/:repo/branches/develop/protection/required_status_checks` devuelve
      200 y lista los jobs decididos. Ídem para `main`.
- [ ] **O4.2** Confirmar que `enforce_admins` sigue activo.
      **Terminado:** criterio 2 —
      `gh api repos/:owner/:repo/branches/develop/protection --jq '.enforce_admins.enabled'`
      devuelve `true`.
- [ ] **O4.3** Reducir el permiso de la credencial de los agentes: sin administración del
      repositorio.
      **Terminado:** criterio 13 — una llamada de administración con esa credencial es
      rechazada.
- [ ] **O4.4** Probar la compuerta con un PR de juguete que rompa una prueba a propósito.
      **Terminado:** criterio 3 — **no se puede mergear**, y el botón de merge lo dice. Es la
      verificación de que el Art. 12 dejó de ser aspiracional.

---

## Cierre

- [ ] **TC.1** Ejecutar [`test-e2e.md`](./test-e2e.md) completo.
- [ ] **TC.2** `dotnet test -c Release` en verde. Esta rama toca código de aplicación solo en
      el commit 2: cualquier otro fallo es una regresión ajena que hay que detectar antes del
      merge.
- [ ] **TC.3** Verificar que **ningún secreto** entró al repositorio en ninguna forma.
      **Terminado:** la protección de push no reportó nada y una revisión del diff completo del
      PR no encuentra valores, solo nombres de secreto.
- [ ] **TC.4** Repasar los 19 criterios de aceptación de [`spec.md` sec. 11](./spec.md) uno por
      uno.
      **Terminado:** los 19 marcados, o los que no se cumplan anotados en `docs/BACKLOG.md`
      con su razón.
