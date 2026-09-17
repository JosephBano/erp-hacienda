# ADR-0030 — Secretos por entorno, runner alojado sobre Tailscale, y credenciales que no pueden cambiar la configuración

- **Estado:** Aceptado
- **Fecha:** 2026-09-15
- **Fase del roadmap:** Transversal.

## Contexto

Hechos verificados el 2026-09-15:

- **El repositorio es público.** `gh repo view --json visibility` devuelve `"PUBLIC"` para
  `JosephBano/erp-hacienda`. De ahí se siguen dos cosas que gobiernan todo este ADR:
  1. GitHub Actions con runners alojados es **gratuito e ilimitado**. Los créditos de
     GitHub Education que el dueño creía necesitar no hacen falta.
  2. **Un runner self-hosted sería un agujero.** En un repositorio público, un pull request
     desde un fork puede ejecutar código arbitrario en la máquina que hospeda el runner.

- **No hay dónde poner un secreto de despliegue.** `gh api repos/:owner/:repo/environments`
  devuelve `{"total_count":0}`. `.env.example` declara tres variables
  (`POSTGRES_PASSWORD`, `POSTGRES_PORT`, `WEB_PORT`) y `.gitignore` excluye `.env`: el modelo
  está bien para desarrollo local y **no tiene equivalente para un despliegue automatizado**.

- **El escaneo de secretos ya está activo**, con protección de push:
  `secret_scanning: enabled`, `secret_scanning_push_protection: enabled`,
  `dependabot_security_updates: disabled`. Y GitGuardian ya hizo su trabajo una vez: la
  retrospectiva de Fase 0 en `docs/ROADMAP.md` registra que atrapó una contraseña de
  desarrollo commiteada por descuido antes de llegar a `develop`.

- **El servidor de destino solo es alcanzable por Tailscale.** `home-server/docs/SISTEMA.md`
  (ficha del 2026-09-01) da `Acceso: SSH sobre Tailscale, únicamente`, y el principio 1 de
  aquel repositorio dice: *«Ningún servicio escucha en la IP pública ni en el router»*. Un
  runner alojado por GitHub no lo alcanza sin unirse a esa red.

- **La contraseña de acceso al servidor está marcada como deuda por su propio dueño.**
  `home-server/.env.example` anota junto a `SERVER_PASSWORD`: *«⚠ PENDIENTE: rotar.
  Contraseña corta, reutilizada, y sirve también para sudo»*.

- **Los agentes de IA commitean con la cuenta del dueño.** El dueño lo plantea así:
  *«los agentes usan mi cuenta para los commits y por ende pueden llegar a hacer cosas que no
  deben»*, y propone *«poner una restricción de no editar configuración de ramas una vez se
  aplique la configuración»*.

## Decisión

**Los secretos viven por entorno, el despliegue corre en un runner alojado que se une al
tailnet de forma efímera, y la credencial con la que trabajan los agentes no tiene permiso
para administrar el repositorio.**

### 1. Entornos y secretos

Se crean dos *environments*: `staging` y `production`, este último con **revisor
obligatorio**. Todo secreto vive a nivel de entorno, con cuatro reglas duras:

1. **Ningún secreto se define a nivel de repositorio.** Un secreto de repositorio es visible
   para cualquier workflow, incluidos los que se añadan después sin pensarlo.
2. **`pull_request` desde un fork nunca recibe secretos.** El workflow de despliegue se
   dispara solo en `push` a `develop`.
3. **Nada se imprime.** Los valores no pasan por `echo` ni por una línea de comandos que
   quede en el log.
4. El `.env` local **no se toca**: sigue siendo la fuente para desarrollo, como describe el
   `README.md`. Esto añade un segundo mecanismo donde no había ninguno; no reemplaza el
   primero.

### 2. Acceso al servidor

Runner alojado por GitHub → se une al tailnet como **nodo efímero** con la acción oficial de
Tailscale → SSH al servidor → despliega → el nodo desaparece al terminar el job.

**Ningún puerto se abre y ninguna IP se publica.** El principio 1 de `home-server` queda
intacto.

El acceso usa una **clave SSH dedicada**, creada solo para esto, que da acceso a un usuario
restringido al stack de staging y **sin sudo**. La `SERVER_PASSWORD` existente no se usa aquí
en ninguna forma; rotarla sigue siendo deuda de aquel repositorio.

### 3. Contra la deriva de la configuración — tres capas, y ninguna finge ser un candado

**GitHub no permite bloquear los ajustes de un repositorio personal.** El dueño siempre puede
cambiarlos, y un agente con su credencial también. Esto se escribe aquí tal cual porque es el
documento que se va a leer cuando algo falle, y prometer un candado que no existe sería peor
que no tener ninguno.

Lo que sí se hace, en orden de costo:

1. **`enforce_admins: true`** (ya activo) más un **ruleset con lista de bypass vacía**, como
   segunda cerradura independiente de la protección clásica.
2. **La credencial que usan los agentes no tiene permiso de administración del
   repositorio.** Es la única barrera real: un token de alcance fino sin el permiso
   *Administration* no puede tocar protección de ramas, entornos ni secretos, por mucho que
   quien lo use tenga la cuenta del dueño. **Esta es la capa que responde a la petición
   original.**
3. **Check de deriva.** La configuración esperada de protección y entornos vive versionada en
   `.github/` como archivo declarativo, y un job la compara contra lo que devuelve la API. Si
   no coincide, falla y nombra el campo que cambió. **No impide el cambio: lo delata**, en el
   siguiente PR, en rojo.

### 4. Escáneres de secretos

Se conserva lo que hay —escaneo con protección de push, más GitGuardian— y **no se añade un
tercero**. Sí se conserva el job `tesis-privacy-guard`, porque ningún escáner de secretos
cubre nombres de personas.

## Alternativas consideradas

- **Runner self-hosted en el servidor doméstico.** Es lo primero que uno piensa y es la razón
  de este ADR. Descartada por seguridad: el repositorio es público, y esa laptop hospeda
  además Firefly III (finanzas personales), Portainer y el nodo Tailscale del dueño. Un PR
  desde un fork ejecutando código ahí no es un riesgo teórico. GitHub lo desaconseja
  explícitamente para repos públicos.

- **Abrir un puerto SSH en el router con reenvío.** Descartada: rompe el principio 1 de
  `home-server` de forma directa y permanente, a cambio de evitar una acción de tres líneas.

- **Un túnel permanente montado por el servidor hacia un intermediario.** Añade un servicio
  más que mantener y un tercero más en la cadena de confianza, para resolver algo que la
  membresía efímera en el tailnet ya resuelve durante los segundos que dura el job.

- **Mover el repositorio a una organización gratuita** y separar la cuenta dueña de la cuenta
  de trabajo, para tener registro de auditoría y que la credencial diaria no sea admin. **Es
  el arreglo de fondo** y no se descarta: se difiere, porque cambia la identidad del
  repositorio y merece su propia decisión.

- **Secretos a nivel de repositorio, más simples de configurar.** Descartada por la regla 1
  de la sec. 1: el costo de un secreto sobre-expuesto se paga una sola vez y para siempre.

- **Añadir `gitleaks` al CI.** Sería el tercer escáner sobre la misma superficie. Se
  descarta hasta tener evidencia de que los dos existentes dejan pasar algo. (Nota: el repo
  tuvo un `.gitleaks.toml` que se borró al retirar el pipeline de GitLab el 2026-09-15.)

## Consecuencias

**Lo bueno.** El despliegue funciona sin exponer nada a internet. La credencial de los
agentes deja de poder desarmar las compuertas que este conjunto de ADR levanta. Producción
tiene un punto de parada humano. Y el `.env` de desarrollo sigue funcionando igual que ayer.

**Lo malo.** Hay una dependencia externa nueva —la acción de Tailscale— en la ruta crítica
del despliegue: si esa acción cambia o falla, el despliegue a staging se detiene. Y trabajar
con un token de alcance reducido significa que, cuando de verdad haga falta cambiar la
configuración del repositorio, hay que hacerlo a mano y a conciencia. Eso es intencional.

**Lo que hay que vigilar.**

1. **Que el check de deriva no se vuelva ruido.** Si falla por cambios legítimos que nadie
   recuerda haber hecho, se investiga; no se relaja el check.
2. **Que la clave de despliegue no herede privilegios con el tiempo.** El usuario de
   despliegue no debe ganar sudo "porque hacía falta para una cosa".
3. **Que el entorno `production` no se tome por funcional.** Existe vacío y sin workflow que
   lo consuma, a la espera de un destino real — ver
   [ADR-0031](./0031-servidor-domestico-es-staging.md).

**Condición de reversa.** Se reabre si: el repositorio deja de ser público (cambia por
completo el cálculo del runner self-hosted), o si la membresía efímera en el tailnet resulta
inviable en la práctica y hay que elegir entre otro mecanismo de acceso —que sería un ADR
nuevo, no un parche al workflow.
