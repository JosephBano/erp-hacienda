# spec.md — Tubería de entrega: compuerta de CI, entornos, secretos y despliegue a staging

> **Qué es este documento.** Decide cómo se protege `develop` y `main`, cómo se separan los
> entornos y sus secretos, y cómo se despliega automáticamente a un entorno de staging sobre
> la red privada del servidor doméstico. El *cómo* día a día irá en `plan.md`, el desglose
> ejecutable en `tasks.md` y la verificación manual en `test-e2e.md`; **ninguno de los tres
> existe todavía** — este spec se escribió solo, siguiendo el precedente de la serie
> 0004–0010 (`docs/spec/README.md`).
>
> **Por qué esta subcarpeta.** El trabajo toca cuatro superficies que solo tienen sentido
> juntas: las reglas de la rama, los checks que esas reglas exigen, los secretos que esos
> checks y el despliegue consumen, y el entorno al que se despliega. Documentarlas sueltas en
> `docs/spec/` las dejaría huérfanas entre sí: la compuerta de CI sin el workflow que la
> produce no significa nada, y el despliegue sin entornos no tiene dónde guardar sus
> credenciales.

- **Rama Git:** `feature/devops-delivery-pipeline` (desde `develop`, en `5afbb72`).
- **Fecha:** 2026-09-15.
- **Fase del ROADMAP:** transversal. No abre ni cierra ninguna fase; habilita el criterio
  de salida "en producción real" (Art. 11) de todas las que vengan.
- **ADRs vigentes que respalda o respeta:** ninguno cubre entrega ni despliegue. Este spec
  **exige cuatro ADR nuevos antes de escribir código** (sec. 3, D0) — Art. 14.
- **Reglas duras que gobiernan este trabajo:** `AGENTS.md` regla 4 (nada directo a `main`
  ni `develop`), regla 9 (un PR = un propósito), regla 2 (dependencia nueva = ADR primero).
  Constitución: **Art. 12** (CI en rojo bloquea el merge), Art. 13 (GitFlow), Art. 14 (ADR),
  Art. 2 (respaldos probados), Art. 17 (nada de tecnología por curiosidad en `main`).

---

## Índice

1. [Por qué existe este spec](#1-por-qué-existe-este-spec)
2. [Hallazgos verificados](#2-hallazgos-verificados)
3. [Decisiones fijadas](#3-decisiones-fijadas)
4. [Alcance](#4-alcance)
5. [Diseño: protección de ramas y compuerta de CI](#5-diseño-protección-de-ramas-y-compuerta-de-ci)
6. [Diseño: entornos y gestión de secretos](#6-diseño-entornos-y-gestión-de-secretos)
7. [Diseño: despliegue a staging sobre Tailscale](#7-diseño-despliegue-a-staging-sobre-tailscale)
8. [Diseño: versionado y trazabilidad del build](#8-diseño-versionado-y-trazabilidad-del-build)
9. [Diseño: línea base y métricas de entrega](#9-diseño-línea-base-y-métricas-de-entrega)
10. [Riesgos y deuda](#10-riesgos-y-deuda)
11. [Criterios de aceptación](#11-criterios-de-aceptación)

---

## 1. Por qué existe este spec

El Artículo 12 de la Constitución dice, textualmente:

> «El CI corre todo en cada PR; **CI en rojo bloquea el merge a `develop` y `main`. Sin
> excepciones** "porque es un cambio chiquito".»

**Esa frase es falsa hoy.** La plataforma no la aplica: las ramas están protegidas, pero
sin *required status checks*, de modo que un PR con el CI en rojo se puede mergear igual
(sec. 2.1). El artículo existe como disciplina personal, no como compuerta.

Eso importaría poco si el desarrollo fuera lento y enteramente humano. No lo es: el dueño
del proyecto trabaja con agentes de IA de forma intensiva y deliberada —la retrospectiva de
Fase 0 lo declara como estrategia— y en sus propias palabras, *«el desarrollo se acelera
bastante con agentes de IA, así que con eso puedo acortar tiempos. Lo que sí me gustaría
hacer es poner guardarraíles para esto»*.

El historial dice qué pasa sin esos guardarraíles. La Fase 3 se declaró cerrada con
`clients/field-app/` sin una sola pantalla, y una auditoría posterior encontró tres defectos
que **habrían perdido datos en producción sin dar ningún error**: marcas de tiempo nunca
escritas, genealogía descartada en silencio por un campo que el comando no aceptaba, y el
outbox del cliente en una API que no existe en React Native (`docs/ROADMAP.md`,
"Fase 3 — cierre revertido"). La lección que el propio roadmap extrae es la que este spec
convierte en mecanismo: *«una suite verde no prueba nada si no ejercita el camino que el
usuario recorre»*. Y una suite verde que ni siquiera es obligatoria, menos.

A esto se suma que hoy **no hay ningún entorno donde probar antes de la finca**. El único
despliegue documentado es el de la máquina del cliente. Cualquier cambio se valida en el
portátil del desarrollador y después en animales reales.

## 2. Hallazgos verificados

Todo lo que sigue se comprobó ejecutando comandos contra este repositorio y contra la API de
GitHub el 2026-09-15.

### 2.1 Las ramas están protegidas, pero el CI no es obligatorio

```
$ gh api repos/:owner/:repo/branches/develop/protection/required_status_checks
{"message":"Required status checks not enabled","status":"404"}
```

La protección sí existe en `develop` y en `main`, y lo que tiene configurado es correcto:

| Ajuste | `develop` | `main` |
|---|---|---|
| `required_pull_request_reviews` | presente, `required_approving_review_count: 0` | ídem |
| `enforce_admins` | `true` | `true` |
| `allow_force_pushes` | `false` | `false` |
| `allow_deletions` | `false` | `false` |
| **`required_status_checks`** | **ausente (404)** | **ausente (404)** |
| `required_linear_history` | `false` | `false` |
| `required_conversation_resolution` | `false` | `false` |

`enforce_admins: true` es un acierto y conviene no perderlo: significa que el dueño tampoco
puede saltarse la regla. El hueco es exactamente uno y es el que contradice al Art. 12.

```
$ gh api repos/:owner/:repo/rulesets
[]
```

No hay rulesets: toda la protección vive en el mecanismo clásico de *branch protection*.

### 2.2 El repositorio es público y la rama por defecto es `main`

```
$ gh repo view --json nameWithOwner,visibility,defaultBranchRef,isFork
{"defaultBranchRef":{"name":"main"},"isFork":false,
 "nameWithOwner":"JosephBano/erp-hacienda","visibility":"PUBLIC"}
```

Dos consecuencias, y las dos gobiernan el diseño:

1. **GitHub Actions con runners alojados es gratuito e ilimitado en repositorios públicos.**
   No hacen falta los créditos de GitHub Education que el dueño creía necesitar.
2. **Un runner self-hosted en el servidor doméstico queda descartado por seguridad**
   (D5, sec. 3): en un repo público, un PR desde un fork puede ejecutar código arbitrario en
   la máquina que hospeda el runner.

### 2.3 El CI existe, corre tres jobs y ninguno es exigible

`.github/workflows/ci.yml` (118 líneas antes de la rama `docs/tesis-framework`) define:

| Job | Qué corre |
|---|---|
| `backend-build-and-test` | `dotnet restore`, `dotnet format --verify-no-changes`, `dotnet build -c Release`, `dotnet test -c Release` con PostgreSQL 16 de servicio |
| `field-app-ci` | `npm ci --legacy-peer-deps`, `npm run typecheck`, `npm test` |
| `admin-web-ci` | `npx ng build --configuration production`, `npx ng test --watch=false` |

Se dispara en `pull_request` hacia `develop` y `main`, y en `push` a esas ramas. La cobertura
es buena; el problema es sec. 2.1: **ninguno de estos tres nombres está registrado como check
obligatorio**, así que su resultado es informativo.

> Nota heredada de `docs/BACKLOG.md`: `ng test` en `admin-web` está declarado como roto. Si
> sigue roto, volverlo obligatorio bloquea todo merge. Es la compuerta TG0.2 de sec. 5.4.

### 2.4 No hay entornos ni despliegue

```
$ gh api repos/:owner/:repo/environments
{"total_count":0,"environments":[]}
```

No existe ningún *environment* de GitHub, por lo tanto no hay secretos por entorno ni
revisores obligatorios. `.github/workflows/` contiene un solo archivo, `ci.yml`: **no hay
ningún workflow de despliegue**. `ls .github/` devuelve `PULL_REQUEST_TEMPLATE.md` y
`workflows`, sin `dependabot.yml`.

### 2.5 El empaquetado ya está resuelto

```
$ find . -iname "Dockerfile*" -not -path "*/node_modules/*"
./clients/admin-web/Dockerfile
./src/Hato.Api/Dockerfile
$ ls src/Hato.Api/*.Dockerfile
src/Hato.Api/migrate.Dockerfile
```

`docker-compose.yml` orquesta cuatro servicios —`postgres`, `migrate`, `api`, `web`— con
`migrate` corriendo a `service_completed_successfully` antes de que arranque `api`. **Las
migraciones ya están separadas del arranque de la aplicación**, que es la pieza que suele
faltar para automatizar un despliegue. `scripts/smoke-api-container.sh` ya existe.

Lo que ese compose **no** sirve tal cual para staging: `ASPNETCORE_ENVIRONMENT` está fijo en
`Development`, los puertos se publican en el host y la contraseña sale de un `.env` local.

### 2.6 Los secretos viven en archivos `.env` locales

`.env.example` declara tres variables (`POSTGRES_PASSWORD`, `POSTGRES_PORT`, `WEB_PORT`) y
`.gitignore` excluye `.env`. El modelo está bien para desarrollo y **no tiene equivalente
para un despliegue automatizado**: no hay dónde poner la clave SSH ni las credenciales de
staging sin ponerlas en un archivo.

El escaneo de secretos de la plataforma sí está activo:

```
$ gh api repos/:owner/:repo --jq '.security_and_analysis'
secret_scanning: enabled · secret_scanning_push_protection: enabled
dependabot_security_updates: disabled
```

### 2.7 El servidor doméstico: qué es y qué no

Verificado en `/home/joeman/Documents/home-server/docs/SISTEMA.md` (ficha del 2026-09-01):

| Componente | Valor | Consecuencia para este spec |
|---|---|---|
| CPU | Intel i5-7200U, **2 núcleos / 4 hilos**, 2017 | Suficiente para una API y un PostgreSQL de staging |
| RAM | 7.5 GB (6.8 GB libres) | **No es el cuello de botella**, y el dueño lo confirma en su propia ficha |
| Disco | SSD 931 GB, **uno solo, sin redundancia** | Descalifica el equipo para producción bajo Art. 1 y Art. 2 |
| Red | **WiFi**; el ethernet `enp2s0` está caído | Latencia y caídas aceptables en staging, no en campo |
| Acceso | **SSH solo por Tailscale**; sin IP pública ni port forwarding | Un runner alojado no lo alcanza sin unirse al tailnet |
| SO | Ubuntu 24.04.4 LTS, Docker y LVM con `/srv` en volumen propio | Listo para recibir un stack de compose |

El repositorio `home-server` declara como principio 1: *«Ningún servicio escucha en la IP
pública ni en el router»*. **Este spec no lo rompe.**

Consecuencia que el dueño no había considerado: **la app de campo no puede alcanzar un
servicio que solo vive en el tailnet.** Los empleados en el potrero no están en esa red. Por
eso este equipo es staging y no producción (D3).

### 2.8 Hay línea base suficiente para medir

```
$ gh pr list --state merged --limit 200 --json number --jq 'length'
97
$ git rev-list --count develop
325
```

97 PRs mergeados y 325 commits, con fechas, más 27 ADR y las retrospectivas por fase del
`ROADMAP.md`. Es material suficiente para reconstruir un "antes" de las métricas de entrega
(sec. 9) sin instrumentación adicional.

### 2.9 No existe ninguna versión declarada, y las que hay se contradicen

```
$ ls VERSION
ls: cannot access 'VERSION': No such file or directory
$ grep -n '"version"' clients/*/package.json
clients/admin-web/package.json:3:  "version": "0.0.0"
clients/field-app/package.json:3:  "version": "1.0.0"
$ grep -rn "<Version>" Directory.Build.props src/
(sin resultados)
```

Tres fuentes y ninguna concuerda: el panel dice `0.0.0`, el móvil dice `1.0.0`, y el backend
no declara versión en absoluto. **Ninguna instancia desplegada puede decir qué es.**

Esto no es higiene teórica. El "Paso 0 — Recolección de evidencia" de `docs/spec/README.md`
declara que cuatro specs —0004, 0005, 0006 y 0007— quedaron bloqueados por la misma laguna:
*«no se conocen build de los teléfonos, versión del backend, ni qué significó "eliminar"»*, y
que media hora con el dueño y un teléfono los desbloqueaba. Ese costo se pagó por no poder
preguntarle a lo desplegado qué versión era.

### 2.10 No hay herramienta de formato ni de lint en los clientes

```
$ python3 -c "import json; d=json.load(open('clients/admin-web/package.json')); print(d['scripts'])"
{'ng': 'ng', 'start': 'ng serve', 'build': 'ng build',
 'watch': 'ng build --watch --configuration development', 'test': 'ng test'}
```

| Cliente | `prettier` | `eslint` | Script que los ejecute |
|---|---|---|---|
| `admin-web` | `^3.8.1` declarado | ausente | **ninguno** |
| `field-app` | ausente | ausente | **ninguno** |

`prettier` está instalado en `admin-web` y **nada lo invoca**. En el backend la situación es
la opuesta y ya está resuelta: `dotnet format --verify-no-changes` corre en el CI (sec. 2.3) y
`Directory.Build.props` fija `TreatWarningsAsErrors: true`, que es una compuerta de calidad
más severa que la mayoría.

Consecuencia: **un check de formato en los clientes no es configuración, es una dependencia
nueva** —`prettier` en `field-app`, `eslint` en ambos— y `AGENTS.md` regla 2 exige ADR antes.
Ver D13.

### 2.11 El CI institucional de GitLab y Harbor ya no existen

Verificado: no hay `.gitlab-ci.yml`, ni `.gitleaks.toml`, ni `plantilla.version`, ni
referencia a Harbor o GitLab en ningún `*.yml`, `*.toml` o `Dockerfile` del repositorio. Esa
tubería fue una prueba y se retiró por completo el 2026-09-15, junto con el runner local de
systemd y la URL de push a GitLab del remote `origin`.

Se deja escrito aquí porque el dueño preguntó por Harbor al especificar este trabajo: **es
historia, no una opción abierta**. D7 (construir en el servidor, sin registro) sigue siendo
la decisión vigente, y sobre un i5 de dos núcleos un registro propio sería además
desproporcionado.

## 3. Decisiones fijadas

- **D0 — Cuatro ADR se escriben y se mergean antes que cualquier código de esta rama.**
  Art. 14 y `AGENTS.md` regla 2. Son decisiones transversales y ninguna está escrita:
  1. *Estrategia de ramas, entornos, promoción y versionado* (respalda D1, D2 y D14).
  2. *Gestión de secretos y acceso de despliegue* (respalda D4, D5, D6, y la credencial sin
     permiso de administración de D12.2).
  3. *Servidor de staging: alcance, límites y por qué no es producción* (respalda D3).
  4. *Formato y lint en los clientes* (respalda D13) — es el único que añade dependencias, y
     por eso `AGENTS.md` regla 2 lo exige sin discusión.

  Pueden ir en su propia rama `docs/adr-00XX-*` para no bloquear, como permite
  `PROTOCOLO-DE-TRABAJO.md` paso 3.

- **D1 — No se crean ramas `staging` ni `production`. Se mantienen dos ramas de larga vida.**
  *(Decidido con el dueño, que había propuesto tres.)* `develop` despliega a staging;
  `main` despliega a producción. La separación de entornos la dan los *environments* de
  GitHub, no una rama más. Tres ramas de larga vida para un desarrollador solo son costo de
  sincronización sin beneficio, y contradicen el Art. 13, que ya fija el modelo de ramas
  vigente. **Alternativa descartada:** `develop → staging → main`, que obliga a un merge de
  promoción que no aporta información que el tag de release no dé.

- **D2 — El catálogo de checks se fija en sec. 5.5, y no todos bloquean.** Los tres jobs
  actuales de `ci.yml` se vuelven obligatorios —única forma de que el Art. 12 sea verdad
  (sec. 2.1)— junto con un job nuevo de higiene de PR y la guarda de datos personales. **El
  análisis de código arranca informativo y los metadatos no bloquean nunca.** Se conserva
  `enforce_admins: true`.
  *(El dueño pidió ocho verificaciones separadas y preguntó si se sobrecargaba. Se
  sobrecargaba: la mitad son pasos dentro de jobs que ya existen. El resultado es **un job
  nuevo**, dos pasos añadidos y un análisis informativo.)*

- **D3 — El servidor doméstico es el entorno de *staging*, y solo eso.** Un disco sin
  redundancia (sec. 2.7) es incompatible con el Art. 1 ("los datos jamás se destruyen") y con
  el Art. 2 ("respaldos probados o nada") para datos de una finca real. Además, siendo
  accesible solo por Tailscale, la app de campo no llegaría. **Producción espera a un VPS y
  queda fuera de este spec** (sec. 4).

- **D4 — Los secretos se migran a *environment secrets* de GitHub, uno por entorno.**
  El `.env` local sigue existiendo para desarrollo y no se toca. El entorno `production` se
  crea con **revisor obligatorio**, de modo que un merge a `main` no despliega solo.

- **D5 — No se instala un runner self-hosted.** El repositorio es público (sec. 2.2) y un
  runner propio expondría la laptop que hospeda Firefly, Portainer y el tailnet a la
  ejecución de código venido de un fork. **Alternativa elegida:** runner alojado por GitHub
  que se une al tailnet con una credencial efímera mediante la acción oficial de Tailscale, y
  desde ahí despliega por SSH. Sin abrir un puerto y sin tocar el principio 1 de
  `home-server`.

- **D6 — El acceso de despliegue usa clave SSH dedicada, nunca contraseña.**
  El `.env.example` de `home-server` marca `SERVER_PASSWORD` como *«⚠ PENDIENTE: rotar.
  Contraseña corta, reutilizada, y sirve también para sudo»*. La clave de despliegue es nueva,
  exclusiva de este uso, y el usuario al que da acceso solo puede operar el stack de staging.

- **D7 — El despliegue a staging es por `git pull` + `docker compose up -d` en el servidor,
  no por registro de imágenes.** Construir en el servidor usa un i5 de dos núcleos, pero
  evita montar y mantener un registro y credenciales adicionales para un único entorno. Es la
  opción más simple y reversible (Art. 17). **Si el tiempo de build resulta inaceptable**, se
  revisa hacia GitHub Container Registry, y eso sería un ADR nuevo, no un parche.

- **D8 — El despliegue se verifica solo, y si no verifica, falla.** El workflow no termina en
  verde por haber ejecutado el comando: termina en verde cuando un *smoke test* contra la API
  desplegada responde. `scripts/smoke-api-container.sh` ya existe (sec. 2.5) y es el punto de
  partida.

- **D9 — Se activa Dependabot solo para actualizaciones de seguridad y para las acciones de
  GitHub Actions.** Hoy está desactivado (sec. 2.6). No se activan actualizaciones de versión
  de NuGet ni npm: con `AGENTS.md` regla 2 exigiendo ADR por dependencia, un flujo de PRs
  automáticos de versiones sería ruido que nadie puede mergear.

- **D10 — La línea base de métricas de entrega se calcula y se archiva antes de activar la
  compuerta.** Sec. 9. Una vez que los checks son obligatorios, el "antes" deja de ser
  observable.

- **D11 — La compuerta existe *porque* los agentes de IA usan la cuenta del dueño, no a
  pesar de ello.** *(El dueño planteó lo contrario: «no sé si sean necesarios... los agentes
  usan mi cuenta para los commits y por ende pueden llegar a hacer cosas que no deben».)* Un
  actor que tiene la autoridad del dueño, trabaja más rápido de lo que el dueño puede
  revisar, y ya cerró una fase con `clients/field-app/` sin una sola pantalla (sec. 1), es
  exactamente el actor para el que una compuerta automática existe. Retirar el bloqueo porque
  el que entra tiene llave es quitar la cerradura.

- **D12 — Contra la deriva de la configuración se aplican tres capas, y ninguna finge ser un
  candado.** **GitHub no permite bloquear los ajustes de un repositorio personal**: el dueño
  siempre puede cambiarlos, y un agente con su credencial también. Lo que sí se hace,
  en orden de costo:
  1. `enforce_admins: true` (ya activo, sec. 2.1) y un **ruleset con lista de bypass vacía**,
     como segunda cerradura independiente de la protección clásica.
  2. **La credencial que usan los agentes no tiene permiso de administración del
     repositorio.** Es la única barrera real: el agente no cambia lo que su token no alcanza.
  3. **Check de deriva:** la configuración esperada vive versionada (sec. 5.3 y 5.6) y un job la
     compara contra la real. No lo impide — **lo delata**, que es lo que sí se puede
     garantizar.

  **Alternativa evaluada y diferida:** mover el repositorio a una organización gratuita para
  tener registro de auditoría y separar la cuenta dueña de la cuenta de trabajo. Es el arreglo
  de fondo y queda fuera de esta rama (sec. 4).

- **D13 — El formato y el lint de los clientes necesitan ADR previo, porque son dependencias
  nuevas.** Sec. 2.10: `prettier` está instalado en `admin-web` y nada lo ejecuta, y
  `field-app` no tiene ninguna de las dos herramientas. `AGENTS.md` regla 2 no admite "una
  librería chiquita para esto". El ADR entra en el conjunto de D0 como cuarto.

- **D14 — Existe un archivo `VERSION` en la raíz como fuente única, y lo desplegado sabe
  decir qué versión es.** Sec. 8. Lo que resuelve el problema no es el archivo: es el
  endpoint que responde. Sec. 2.9 muestra el costo ya pagado por no tenerlo.

- **D15 — Los metadatos de gestión (hito, etiquetas) se señalan, nunca bloquean.** Un merge
  detenido porque se olvidó poner un hito es fricción sin beneficio para un desarrollador
  solo, y enseña a saltarse la compuerta — que es justo lo que este spec intenta impedir.

- **D16 — El análisis estático de seguridad y calidad entra como CodeQL, informativo.**
  Gratuito en repositorios públicos (sec. 2.2). Arranca sin bloquear: volverlo obligatorio el
  primer día significa que un único hallazgo dudoso deja el repositorio sin poder mergear. Se
  promueve a obligatorio cuando se conozca su comportamiento sobre *este* código, y esa
  promoción se anota con fecha.

## 4. Alcance

### Entra

- Registro de los tres jobs de `ci.yml` como checks obligatorios en `develop` y `main`, con
  `enforce_admins` conservado.
- Endurecimiento del resto de la protección de ramas: resolución de conversaciones e
  historial lineal (sec. 5.2).
- Creación de los *environments* `staging` y `production`, con sus secretos y con revisor
  obligatorio en `production`.
- Workflow `deploy-staging.yml`: se dispara al hacer push a `develop`, se une al tailnet con
  credencial efímera, despliega por SSH y verifica con smoke test.
- Stack de compose específico de staging en el servidor (`ASPNETCORE_ENVIRONMENT=Staging`,
  sin publicar puertos al host, servido por el Caddy que ya corre allí).
- Clave SSH dedicada y usuario de despliegue restringido en el servidor.
- `dependabot.yml` con el alcance de D9.
- Job nuevo `pr-hygiene`: nombre de rama, formato de commits y título del PR (sec. 5.5).
- Pasos de formato y lint dentro de los jobs de cliente que ya existen (sec. 5.5), con su ADR
  previo (D13).
- CodeQL como análisis informativo (D16).
- Etiquetado automático por metadatos faltantes, sin bloquear (D15).
- Ruleset con bypass vacío y check de deriva de configuración (D12).
- Archivo `VERSION`, su estampado en los tres artefactos y el endpoint que lo expone (sec. 8).
- Cálculo y archivo de la línea base de métricas de entrega (sec. 9).
- Los **cuatro** ADR de D0 (los tres originales más el de formato/lint, D13).
- Actualización de `docs/DOCUMENTACION.md`, `docs/SEGURIDAD.md` y `README.md` en lo que este
  trabajo cambie.

### No entra

- **Despliegue a producción.** No hay VPS y el servidor doméstico está descalificado por D3.
  El entorno `production` se crea **vacío y sin workflow que lo consuma**: existe para que la
  protección y el revisor obligatorio estén listos el día que haya destino, no para desplegar
  a ninguna parte. Añadir el destino será su propia rama.
- **Migrar los secretos de `home-server`.** Ese repositorio es otro proyecto con su propio
  modelo (`.env` + `scripts/sync-env.sh`), y funciona. Este spec solo añade lo que el
  despliegue de HATO necesita. Rotar `SERVER_PASSWORD` es trabajo de aquel repositorio, y se
  anota allí.
- **Arreglar `ng test` de `admin-web`.** Está en `docs/BACKLOG.md` como deuda declarada. Este
  spec solo decide qué pasa con él en la compuerta (sec. 5.4); arreglarlo es otra rama,
  por `AGENTS.md` regla 9.
- **Backups del entorno de staging.** Staging no guarda datos que importen; por definición se
  puede reconstruir. El Art. 2 aplica a producción.
- **Cualquier cambio de código de dominio, esquema o cliente.** Esta rama no toca `src/` ni
  `clients/` salvo archivos de configuración de despliegue.
- **Registro de imágenes (GHCR, Harbor o cualquier otro).** Descartado por D7 hasta tener
  evidencia de que hace falta. Harbor en particular ya fue probado y retirado (sec. 2.11): no
  es una opción abierta, es historia.
- **Mover el repositorio a una organización.** Es el arreglo de fondo contra la deriva de
  configuración (D12, alternativa diferida), pero cambia la identidad del repositorio y
  merece su propia decisión. No entra aquí.
- **Un tercer escáner de secretos.** El escaneo con protección de push ya está activo
  (sec. 2.6) y GitGuardian ya atrapó una contraseña antes de `develop` en Fase 0
  (`docs/ROADMAP.md`). Añadir `gitleaks` sería la tercera herramienta sobre la misma
  superficie. Lo que sí se conserva es `tesis-privacy-guard`, porque ningún escáner de
  secretos cubre nombres de personas.
- **Rotar la versión de las dependencias.** D9 limita Dependabot a seguridad y acciones.

## 5. Diseño: protección de ramas y compuerta de CI

### 5.1 La compuerta

Los tres jobs de sec. 2.3 se registran como *required status checks* en ambas ramas, por sus
nombres exactos: `backend-build-and-test`, `field-app-ci`, `admin-web-ci`. Se añade
`strict: true` (la rama debe estar al día con la base antes de mergear), que es lo que impide
el merge semánticamente roto: dos PRs verdes por separado que se rompen al juntarse.

El job `tesis-privacy-guard`, introducido en la rama `docs/tesis-framework`, se añade a la
lista **si esa rama ya está en `develop`** cuando se ejecute esta. Si no, se anota en
`tasks.md` como seguimiento.

### 5.2 El resto de la protección

| Ajuste | Hoy (sec. 2.1) | Queda | Por qué |
|---|---|---|---|
| `required_status_checks` | ausente | los 3 jobs, `strict: true` | Art. 12 |
| `enforce_admins` | `true` | `true` | No se toca. Es lo que hace real la regla para el dueño |
| `required_conversation_resolution` | `false` | **`true`** | Un comentario de revisión sin resolver es la forma más común de perder un hallazgo |
| `required_linear_history` | `false` | **`true` en `main`**, `false` en `develop` | El historial de release se lee; el de integración no necesita la restricción |
| `allow_force_pushes` / `allow_deletions` | `false` | `false` | Ya está bien |
| `required_approving_review_count` | `0` | `0` | Un solo desarrollador. Exigir una aprobación que solo él puede dar es teatro |

> **Sobre el `0` de aprobaciones.** No es un descuido y conviene dejarlo escrito para que
> nadie lo "arregle" más adelante: con un único mantenedor, exigir aprobación solo enseña a
> aprobarse a uno mismo por trámite. Lo que sustituye a la revisión humana aquí es la
> compuerta automática, y por eso esta sección importa tanto.

### 5.3 Cómo se aplica

Las reglas se aplican por API, no a mano en la interfaz, y el comando queda versionado en
`scripts/` para que la configuración sea reproducible tras cualquier accidente. Es el mismo
principio 3 de `home-server`: *«La configuración se versiona, no se hace clic»*.

### 5.4 Compuerta TG0 — antes de activar nada

Dos cosas se comprueban **antes** de volver obligatorios los checks, porque activarlos con
un job roto deja el repositorio sin poder mergear nada:

- **TG0.1 — Los tres jobs pasan hoy en `develop`.** Se verifica con la última ejecución del
  workflow. Si alguno está en rojo, arreglarlo es el trabajo previo.
- **TG0.2 — El estado real de `ng test` en `admin-web`.** `docs/BACKLOG.md` lo declara roto
  (sec. 2.3). Si lo está, hay dos salidas y hay que elegir una explícitamente: (a) se excluye
  `admin-web-ci` de la lista obligatoria y queda como deuda anotada con fecha, o (b) se
  arregla primero en su propia rama. **No se elige la tercera**, que es volverlo obligatorio
  y descubrir el bloqueo con el primer PR.

### 5.5 Catálogo de checks: qué se verifica y qué bloquea

El dueño pidió ocho verificaciones separadas. Cuatro de ellas ya existen o son pasos dentro
de jobs existentes; convertirlas en jobs propios multiplicaría los nombres a registrar sin
verificar nada nuevo.

| Verificación | Dónde vive | ¿Bloquea? |
|---|---|---|
| Build y pruebas del backend | `backend-build-and-test` — **ya existe** | **sí** |
| Formato del backend (`dotnet format`) | paso dentro de ese job — **ya existe** | **sí** |
| Advertencias como errores | `Directory.Build.props` — **ya existe**, no es un job | **sí, en compilación** |
| Typecheck y pruebas del móvil | `field-app-ci` — **ya existe** | **sí** |
| Build y pruebas del panel | `admin-web-ci` — **ya existe** | **sí** (ver TG0.2) |
| Formato y lint de clientes | **pasos nuevos** dentro de los dos jobs anteriores (D13) | **sí** |
| Nombre de rama, commits y título de PR | **job nuevo** `pr-hygiene` | **sí** |
| Datos personales de la tesis | `tesis-privacy-guard` — existe en `docs/tesis-framework` | **sí** |
| Secretos y credenciales | escaneo de la plataforma con protección de push — **ya activo** | sí, antes del push |
| Análisis de seguridad y calidad | CodeQL (D16) | **no al principio** |
| Hito, etiquetas y metadatos | etiquetado automático (D15) | **nunca** |
| Deriva de la configuración de ramas | job de D12.3 | **sí** |

Balance: **un job nuevo obligatorio**, dos pasos añadidos a jobs existentes, un job de
deriva, un análisis informativo y un etiquetador. No ocho jobs.

`pr-hygiene` verifica tres cosas y ninguna necesita red ni dependencias pesadas:

1. **Nombre de rama** contra `AGENTS.md` regla 4 y Art. 13:
   `feature/*`, `release/*`, `hotfix/*`, `docs/*`.
2. **Mensajes de commit** en Conventional Commits, con el ámbito igual al módulo (Art. 13,
   `PROTOCOLO-DE-TRABAJO.md` paso 7).
3. **Título del PR** en el mismo formato, porque es lo que termina en el historial al
   mergear.

### 5.6 Deriva de la configuración

La configuración esperada de protección y de entornos se guarda versionada en `.github/` como
un archivo declarativo, y el job de D12.3 la compara contra lo que devuelve la API. Si no
coincide, falla y dice qué campo cambió.

**Lo que este job no hace es impedir el cambio.** No existe forma de impedirlo en un
repositorio personal, y escribir aquí que sí la hay sería mentir en el único documento que se
va a leer cuando algo falle. Lo que garantiza es que un cambio silencioso —hecho por el dueño
distraído o por un agente con su credencial— aparezca en rojo en el siguiente PR en lugar de
descubrirse cuando algo ya llegó a producción.

## 6. Diseño: entornos y gestión de secretos

### 6.1 Entornos

| Entorno | Rama que lo despliega | Revisor obligatorio | Destino |
|---|---|---|---|
| `staging` | `develop` | no | `joemanserver` por tailnet |
| `production` | `main` | **sí** | ninguno todavía (sec. 4, "No entra") |

El revisor obligatorio en `production` es el mecanismo que sustituye a la rama `production`
que D1 descartó: el merge a `main` no despliega por sí solo, se detiene a esperar una
aprobación explícita.

### 6.2 Secretos por entorno

Ninguno de estos valores entra al repositorio en ninguna forma.

| Secreto | Entorno | Qué es |
|---|---|---|
| `TS_OAUTH_CLIENT_ID` / `TS_OAUTH_SECRET` | staging | Credencial para que el runner se una al tailnet como nodo efímero |
| `DEPLOY_SSH_KEY` | staging | Clave privada dedicada al despliegue (D6) |
| `DEPLOY_HOST` / `DEPLOY_USER` | staging | Nombre MagicDNS y usuario de despliegue |
| `STAGING_POSTGRES_PASSWORD` | staging | Contraseña del PostgreSQL de staging, distinta de cualquier otra |

Reglas que gobiernan esta tabla:

1. **Ningún secreto se define a nivel de repositorio**, todos a nivel de entorno. Un secreto
   de repositorio es visible para cualquier workflow, incluidos los que se añadan después.
2. **`pull_request` desde un fork nunca recibe secretos.** El workflow de despliegue se
   dispara solo en `push` a `develop` (D5, y refuerzo de sec. 2.2).
3. **Nada se imprime.** Los valores no se hacen `echo` ni se pasan por línea de comandos
   visible en el log.
4. El escaneo de secretos con protección de push ya está activo (sec. 2.6) y se conserva.

### 6.3 Qué NO cambia

`.env` y `.env.example` siguen siendo la fuente para desarrollo local, exactamente como los
describe el `README.md`. Este spec no toca ese flujo: añade un segundo mecanismo para lo que
antes no tenía ninguno.

## 7. Diseño: despliegue a staging sobre Tailscale

### 7.1 El camino

```
push a develop
  └─ CI verde (los 3 jobs obligatorios)
       └─ workflow deploy-staging  [environment: staging]
            ├─ runner alojado por GitHub
            ├─ se une al tailnet como nodo efímero (acción oficial de Tailscale)
            ├─ ssh a joemanserver con DEPLOY_SSH_KEY
            │    ├─ git pull de develop en /srv/hato-staging
            │    ├─ docker compose -f compose.staging.yml up -d --build
            │    └─ el servicio migrate corre antes que api (ya resuelto, sec. 2.5)
            ├─ smoke test contra la API desplegada  (D8)
            └─ el nodo efímero desaparece al terminar el job
```

Ningún puerto se abre, ninguna IP se publica, no hay runner en casa. El principio 1 de
`home-server` queda intacto.

### 7.2 El stack de staging en el servidor

Un `compose.staging.yml` derivado del `docker-compose.yml` actual, con estas diferencias
respecto a lo verificado en sec. 2.5:

- `ASPNETCORE_ENVIRONMENT: Staging`, no `Development`.
- **No se publican puertos al host.** El Caddy que ya corre en el servidor (`stacks/caddy`)
  expone el servicio dentro del tailnet, como hace con Firefly y Beszel.
- La contraseña sale de un archivo de entorno que el despliegue escribe en el servidor con
  permisos restringidos, no de un `.env` versionado ni de la línea de comandos.
- Volumen de datos bajo `/srv`, siguiendo el principio 4 de `home-server` (*«Datos fuera de
  la raíz»*).
- Imágenes con versión fija, nunca `latest` (principio 7 de `home-server`).

### 7.3 Qué pasa si el despliegue falla

El job falla y **staging queda como estaba**: `docker compose up -d` no reemplaza un
contenedor sano por uno que no arranca. No se diseña un rollback automático — en staging el
costo de quedarse en la versión anterior es cero, y un mecanismo de rollback que nunca se
ejercita es peor que no tenerlo. Cuando exista producción, eso sí tendrá que resolverse, y
será su propio ADR.

### 7.4 Lo que este despliegue cambia en el servidor

El servidor pasa a hospedar un stack más. Hay que declararlo en `home-server` —su README dice
*«si no está en este repo, no debería estar en el servidor»*— y eso es un commit en **aquel**
repositorio, no en este. Queda anotado como dependencia externa de esta rama.

## 8. Diseño: versionado y trazabilidad del build

### 8.1 Una fuente, tres artefactos

Un archivo `VERSION` en la raíz del repositorio, con una sola línea en versionado semántico,
es la fuente única. El CI la lee y la estampa:

| Artefacto | Cómo recibe la versión |
|---|---|
| Backend .NET | `dotnet build -p:Version=$(cat VERSION)` — hoy no declara ninguna (sec. 2.9) |
| `admin-web` | se escribe en `package.json` durante el build, no a mano |
| `field-app` | ídem, y llega a la build del teléfono |

Se estampan además el hash corto del commit y la fecha de build, que son lo que de verdad
identifica un artefacto cuando dos builds comparten número de versión.

### 8.2 Lo que resuelve el problema no es el archivo, es el endpoint

La API expone la versión, el commit y la fecha de build en un endpoint de solo lectura, sin
autenticación y sin datos de negocio. El móvil la muestra en su pantalla de diagnóstico, junto
a la bitácora que `feature-0004` ya dejó implementada.

Esto es lo que convierte a `VERSION` en algo más que higiene. Cuando llegó el reporte de tres
semanas de fallos intermitentes desde la finca, **nadie podía decir qué versión corría en los
teléfonos ni en el backend**, y cuatro specs quedaron bloqueados esperando media hora de
evidencia que solo se podía obtener en persona (sec. 2.9). Con el endpoint, esa media hora es
una petición.

### 8.3 Cuándo sube la versión

Lo decide el ADR de D0.1 junto con la estrategia de ramas, porque es la misma pregunta: qué
significa un merge a `main`. Lo que este spec fija es solo que **la versión no se edita a
mano en tres archivos distintos**, que es el estado de hoy y la razón de que los tres números
no concuerden.

## 9. Diseño: línea base y métricas de entrega

La compuerta destruye la posibilidad de observar cómo se trabajaba sin ella. Por eso D10: la
línea base se calcula **antes** de activarla, con lo que ya existe (sec. 2.8).

| Métrica | Cómo se obtiene del "antes" | Fiabilidad |
|---|---|---|
| Lead time de cambio | primer commit de la rama → fecha de merge del PR, sobre los 97 PRs | **Alta.** Está todo en git |
| Frecuencia de integración | merges a `develop` por semana | **Alta** |
| Tasa de fallo del cambio | PRs cuyo cambio provocó un arreglo posterior | **Baja.** Requiere clasificar a mano leyendo el historial |
| Tiempo de restauración | de la detección al arreglo mergeado | **Baja.** Solo estimable en los incidentes documentados |
| Defectos que llegaron a uso real | los 3 de Fase 3 + los 7 de `feature-0004` | **Alta.** Documentados con causa raíz en `ROADMAP.md` |

Las dos filas de fiabilidad baja se declaran como tales; inflarlas sería peor que no tenerlas.

La salida es un documento con los números y el script que los calcula, ambos versionados, de
modo que el mismo script pueda re-ejecutarse dentro de seis meses y producir el "después"
comparable. **Dónde vive ese documento es una pregunta abierta de `plan.md`**: es material
que alimenta la tesis del dueño (`docs/tesis/`), pero no contiene ningún dato personal, así
que no está obligado a vivir en `privado/`.

## 10. Riesgos y deuda

| Riesgo | Mitigación |
|---|---|
| Volver obligatorio un job que hoy está roto deja el repo sin poder mergear nada | Compuerta TG0 (sec. 5.4): se verifica el verde antes de activar, y `ng test` tiene una decisión explícita |
| La credencial de despliegue da acceso al servidor que hospeda Firefly, Portainer y el tailnet | Clave dedicada, usuario restringido al stack de staging, sin sudo, y nunca disparada desde `pull_request` (D5, D6, sec. 6.2) |
| Construir en un i5 de 2 núcleos hace el despliegue lento | Aceptado a propósito (D7). Si molesta, se mide y se decide GHCR con un ADR, no con un parche |
| El WiFi del servidor se cae y el despliegue falla de forma intermitente | Es staging: un fallo de despliegue no afecta a nadie. Si se vuelve frecuente, el job se marca `continue-on-error` y se investiga, no se ignora |
| El entorno `production` existe pero no despliega, y alguien lo toma por funcional | Declarado en sec. 4 y en el ADR de D0.3. El entorno se crea sin ningún workflow que lo referencie |
| La acción de Tailscale es una dependencia externa nueva | `AGENTS.md` regla 2 → entra en el ADR de D0.2, con la alternativa (runner self-hosted) y por qué se descartó |
| Se confía en staging como si fuera producción y se guardan datos reales allí | D3 lo prohíbe por escrito; el ADR de D0.3 existe precisamente para que quede citable |
| Un agente con la credencial del dueño desactiva la protección y mergea algo roto | Tres capas de D12: `enforce_admins`, credencial sin permiso de administración, y check de deriva que lo delata en el siguiente PR. **Ninguna lo impide del todo**, y eso está declarado |
| Demasiados checks obligatorios vuelven costoso cada PR y empujan a saltarse la compuerta | D2, D15 y D16: un solo job nuevo obligatorio, metadatos que nunca bloquean, análisis estático informativo al principio |
| Añadir `prettier` y `eslint` reformatea medio repositorio en un PR gigante | El ADR de D13 decide el alcance del primer formateo; si es masivo, va en su propio commit separado del cambio funcional, y `AGENTS.md` regla 9 lo exige |
| CodeQL se queda informativo para siempre y nadie lo mira | La promoción a obligatorio se anota con fecha en `docs/BACKLOG.md` al crearlo, no queda al criterio del momento |

**Deuda declarada que esta rama crea:** ninguna intencionada. **Deuda ajena que toca y no
arregla:** `ng test` de `admin-web` (sec. 5.4, TG0.2) y la rotación de `SERVER_PASSWORD` en
`home-server` (sec. 4). Ambas quedan anotadas en `docs/BACKLOG.md` con fecha.

## 11. Criterios de aceptación

1. `gh api repos/:owner/:repo/branches/develop/protection/required_status_checks` devuelve
   200 y lista los jobs decididos en sec. 5.4, con `strict: true`. Lo mismo para `main`.
2. `gh api repos/:owner/:repo/branches/develop/protection --jq '.enforce_admins.enabled'`
   sigue devolviendo `true`.
3. Un PR de prueba con un test deliberadamente roto **no se puede mergear**, y el botón de
   merge lo dice. Es la verificación de que el Art. 12 dejó de ser aspiracional.
4. `gh api repos/:owner/:repo/environments --jq '.total_count'` devuelve `2`, y
   `production` tiene al menos un revisor obligatorio.
5. Ningún secreto está definido a nivel de repositorio: la lista de secretos de repositorio
   está vacía y todos viven en su entorno.
6. Un push a `develop` despliega a staging sin intervención, y el job termina en verde
   **solo** después de que el smoke test responda contra la API desplegada.
7. Desde un dispositivo del tailnet, la API de staging responde; desde fuera del tailnet,
   no resuelve. El servidor no tiene ningún puerto nuevo publicado hacia el router.
8. No existe ningún runner self-hosted registrado en el repositorio.
9. `.github/dependabot.yml` existe y su alcance es el de D9, sin actualizaciones de versión
   de NuGet ni npm.
10. El documento de línea base existe, con sus números, su script re-ejecutable, y las dos
    métricas de fiabilidad baja declaradas como tales.
11. `pr-hygiene` rechaza un PR cuyo título no cumpla Conventional Commits, y lo acepta
    cuando se corrige.
12. Un cambio manual en la protección de `develop` hace fallar el job de deriva en el
    siguiente PR, nombrando el campo que cambió.
13. La credencial usada por los agentes no puede modificar la configuración del repositorio:
    una llamada de administración con esa credencial es rechazada.
14. `VERSION` existe en la raíz, y `curl` contra el endpoint de la API desplegada en staging
    devuelve esa misma cadena junto al hash del commit.
15. `admin-web` y `field-app` declaran la versión de `VERSION`, no `0.0.0` ni `1.0.0`
    escritos a mano.
16. CodeQL aparece en los checks del PR y **no** figura entre los obligatorios; su promoción
    está anotada con fecha en `docs/BACKLOG.md`.
17. Un PR sin hito recibe una señal automática y **se puede mergear igual**.
18. Los **cuatro** ADR de D0 están mergeados y este spec los cita por número.
19. `docs/DOCUMENTACION.md` tiene fila para cada documento nuevo, y `docs/SEGURIDAD.md`
    describe el modelo de acceso de despliegue.
