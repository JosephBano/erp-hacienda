# test-e2e.md — Verificación manual extremo a extremo

> Escenarios que se ejecutan **después** de que la suite automatizada esté en verde. Aquí no
> se prueba código de dominio: se prueba que las compuertas cierran, que los secretos no se
> filtran, que lo desplegado dice qué es y que nada quedó expuesto a internet.
>
> Un escenario que falla se reporta con el paso exacto donde falló, no como "no anda".
>
> Decisiones en [`spec.md`](./spec.md) · orden en [`plan.md`](./plan.md) · casillas en
> [`tasks.md`](./tasks.md).

**Antes de empezar:**

- Rama `feature/devops-delivery-pipeline` con los once commits y el PR abierto.
- Los cuatro ADR (0029–0032) mergeados en `develop`.
- Pasos operativos O1, O2 y O3 ejecutados. **O4 todavía no** — los escenarios E2E-1 y E2E-2 lo
  exigen sin activar, y E2E-3 lo verifica ya activado.
- `gh` autenticado, y un dispositivo dentro del tailnet más otro fuera de él.
- Un teléfono con la build de `field-app` de esta rama, para E2E-5.

---

## E2E-1 — La higiene de PR rechaza lo que debe rechazar

**Preparación:** ninguna más allá del PR abierto.

**Pasos:**
1. Cambiar el título del PR a `arreglos varios`.
2. Esperar a que `pr-hygiene` termine.
3. Devolver el título al original.

**Debe pasar:**
- Con el título inválido, `pr-hygiene` queda en **rojo** y su salida nombra el título como
  causa, no un fallo genérico.
- Con el título corregido, vuelve a **verde** sin necesidad de un commit nuevo.
- `pr-hygiene` es el job **más rápido** de la ejecución, por al menos un orden de magnitud
  respecto a los de build.

## E2E-2 — El formateo masivo no escondió un cambio funcional

**Preparación:** ninguna.

**Pasos:**
1. `git show --stat <hash del commit 5>`.
2. Salirse a `develop`, correr la suite de ambos clientes, volver a la rama y correrla otra vez.

**Debe pasar:**
- El commit 5 **no lista ningún archivo** fuera de `clients/`, salvo `.git-blame-ignore-revs`.
- El número de pruebas que pasan es **el mismo** antes y después. Si cambió, el commit llevaba
  algo más que formato y hay que partirlo.
- `git blame --ignore-revs-file .git-blame-ignore-revs` sobre un archivo reformateado atribuye
  las líneas al autor original, no al commit de formateo.

## E2E-3 — La compuerta bloquea de verdad

> Este es el escenario que verifica que el Artículo 12 dejó de ser una frase. Se ejecuta
> **después** de O4.

**Preparación:** rama nueva desde `develop`, con una prueba rota a propósito.

**Pasos:**
1. Abrir PR desde esa rama hacia `develop`.
2. Esperar a que el CI termine.
3. Intentar mergear.

**Debe pasar:**
- El merge **no se puede hacer**, y la interfaz dice explícitamente que faltan checks
  obligatorios.
- Intentarlo por API también falla:
  `gh pr merge --squash` devuelve error, no un aviso.
- **Intentarlo con la cuenta del dueño tampoco funciona.** `enforce_admins` está en `true`, y
  si el dueño puede saltarse la compuerta, un agente con su cuenta también puede — que es
  justo lo que este trabajo existe para impedir.
- Borrar la rama de prueba al terminar.

## E2E-4 — La credencial de los agentes no puede desarmar la compuerta

**Preparación:** tener a mano la credencial reducida de O4.3.

**Pasos:**
1. Con esa credencial, intentar leer la protección de `develop`.
2. Con esa credencial, intentar **modificarla** (por ejemplo, quitar un check obligatorio).
3. Con esa credencial, clonar, crear una rama, commitear y empujar.

**Debe pasar:**
- El paso 2 es **rechazado por permisos**.
- El paso 3 funciona sin fricción: la credencial reducida sigue sirviendo para trabajar. Si no
  sirve, el siguiente trabajo empieza bloqueado y hay que ajustar el alcance antes de retirar
  la credencial anterior.

## E2E-5 — El despliegue ocurre solo y lo desplegado dice qué es

**Preparación:** anotar el hash corto de `develop` antes de empezar.

**Pasos:**
1. Mergear el PR a `develop`.
2. Observar `deploy-staging` sin intervenir.
3. Desde un dispositivo **dentro** del tailnet, pedir el endpoint de versión.
4. Abrir la pantalla de diagnóstico en el teléfono con la build de esta rama.

**Debe pasar:**
- El despliegue arranca **sin que nadie lo lance a mano**.
- El job termina en verde **solo después** de que el smoke test responda. Si el smoke test se
  omite o se ignora, el escenario falla aunque la API esté viva.
- El endpoint devuelve la misma cadena que `cat VERSION`, más el hash anotado en la
  preparación y la fecha de build.
- El teléfono muestra esa misma versión.
- Ningún valor de secreto aparece en el log del job: se ven **nombres** de secreto, nunca
  contenidos.

## E2E-6 — Nada quedó expuesto fuera del tailnet

> El principio 1 de `home-server` dice: *«Ningún servicio escucha en la IP pública ni en el
> router»*. Este escenario lo verifica después del despliegue, no lo asume.

**Pasos:**
1. Desde un dispositivo **fuera** del tailnet, intentar resolver y alcanzar el nombre del
   servidor.
2. En el servidor, listar los puertos publicados por el stack de staging.
3. Revisar la configuración del router.
4. `gh api repos/:owner/:repo/actions/runners`.

**Debe pasar:**
- Desde fuera **no resuelve**. No es que responda 403: no llega.
- El stack de staging **no publica ningún puerto al host**; lo expone el Caddy que ya corría.
- El router no ganó ninguna regla de reenvío.
- La lista de runners está **vacía**: ningún self-hosted registrado (D5).

## E2E-7 — La deriva de configuración se delata

**Pasos:**
1. Cambiar a mano un campo de la protección de `develop` — por ejemplo, desactivar
   `required_conversation_resolution`.
2. Abrir un PR cualquiera y esperar al job de deriva.
3. Revertir el cambio y volver a lanzar el job.

**Debe pasar:**
- El job falla y **nombra el campo exacto** que cambió, no "la configuración no coincide".
- Al revertir, vuelve a verde.
- El mensaje de fallo **no afirma** que el cambio esté impedido. Detecta; no bloquea
  (ADR-0030 sec. 3). Si el texto promete un candado, hay que corregirlo: es el documento que
  alguien va a leer cuando esto salte de verdad.

## E2E-8 — Un despliegue fallido no tumba staging

**Preparación:** staging desplegado y respondiendo.

**Pasos:**
1. Provocar un fallo de arranque en el commit siguiente (por ejemplo, una variable de entorno
   obligatoria ausente).
2. Empujar a `develop` y observar el despliegue.
3. Pedir el endpoint de versión desde el tailnet.
4. Revertir el fallo.

**Debe pasar:**
- El job de despliegue queda en **rojo**.
- La API **sigue respondiendo**, con la versión **anterior**. No hay rollback automático y no
  hace falta: `docker compose up -d` no reemplaza un contenedor sano por uno que no arranca
  (ADR-0031).
- Tras revertir, el siguiente despliegue deja la versión nueva.

---

## Cierre de la verificación

Los ocho escenarios en verde **no cierran la rama por sí solos**. Falta:

- La suite automatizada completa en verde: `dotnet test -c Release`, más `npm test` y
  `npm run typecheck` en ambos clientes (`tasks.md` TC.2).
- Los **19 criterios de aceptación** de [`spec.md` sec. 11](./spec.md), repasados uno por uno
  (`tasks.md` TC.4). Varios no los cubre ningún escenario de aquí — el alcance de
  `dependabot.yml`, la anotación fechada de CodeQL en `docs/BACKLOG.md`, las filas de
  `docs/DOCUMENTACION.md`.
- El commit **en el repositorio `home-server`** que declara el stack de staging
  (`tasks.md` T10.7). Vive fuera de este repositorio y por eso es lo más fácil de olvidar.
