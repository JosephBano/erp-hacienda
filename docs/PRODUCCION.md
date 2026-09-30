# PRODUCCION.md — Corte, mantenimiento y recuperación de producción

> Este documento responde **¿cómo se despliega, corta y recupera producción?** Describe el
> procedimiento operativo definido en
> `docs/spec/feature-0012-production-environment/spec.md` §4-§7. No certifica que producción
> esté abierta ni lista para registros reales: la apertura real depende de compuertas
> externas (ver sec. 6) que a la fecha de este documento no están cumplidas.
>
> Complementa, sin repetir, a `docs/POLITICAS-OPERACION.md` (qué política se decidió y por
> qué) y `docs/PREFLIGHT-PRODUCCION.md` (cómo inventariar el origen real del piloto antes del
> corte). Este documento es el procedimiento de ejecución: qué se hace, en qué orden, y qué
> hacer cuando algo falla.

## 1. Custodia

Resumen operativo sin valores reales; la tabla completa de custodia por material está en
`docs/spec/feature-0012-production-environment/spec.md` §5 y las decisiones asociadas en
`docs/POLITICAS-OPERACION.md`.

| Recurso | Quién controla | Dónde vive |
|---|---|---|
| Cuenta OCI, MFA, recuperación, acceso administrativo | Propietario | Gestor de contraseñas del propietario, copia de recuperación separada |
| Tailscale (tailnet, ACLs, OAuth de CI) | Propietario | Consola Tailscale; credencial de CI en GitHub Environment `production` |
| Imágenes de contenedor (GHCR) | Propietario / CI aprobado | GHCR por digest, referenciadas desde `ops/production/` |
| Clave SSH de CI y aprobación de despliegue | Operador / GitHub | GitHub Environment `production`, clave pública instalada en `hato-deploy` |
| Secretos de runtime (JWT, contraseñas DB, credencial lectura GHCR si aplica) | Operador | Archivos 600 root-owned en `/etc/hato-production`, fuera del checkout |
| Huella SSH del host | Operador | Configuración del entorno de despliegue, verificada con `StrictHostKeyChecking=yes` |
| Tokens Drive y claves de cifrado de backup | Según feature-0013 | Recuperación independiente de la de producción |
| Firma Android y GitHub App de build | Según feature-0014 | Separadas del despliegue de producción |

Ningún secreto de esta tabla vive en el checkout de este repositorio, en variables
`EXPO_PUBLIC_*`, en logs de compose renderizado, en argumentos de proceso ni en backups sin
cifrar (spec §5).

## 2. Mantenimiento

- **Ventana mensual** de mantenimiento con backup previo verificado y validación posterior
  antes de cerrarla (spec línea 109-111).
- **Actualizaciones de seguridad automáticas** se aplican sin reinicio inesperado del
  servicio; un reinicio de sistema operativo, si es necesario, se coordina dentro de la
  ventana, no fuera de ella.
- **Versiones y digests** de imágenes se fijan explícitamente y se revisan en un proceso
  mensual de actualización — no se congelan indefinidamente ni se actualizan fuera de
  proceso.
- Cada ventana de mantenimiento deja registro de: versión previa, versión aplicada,
  resultado de la validación posterior, sin incluir secretos.

## 3. Migración desde el piloto (corte de datos)

> **Retirado como compuerta de apertura el 2026-09-23
> ([ADR-0040](adr/0040-reorientacion-por-cambio-de-cliente-piloto.md)).** El piloto
> anterior se desvinculó y producción arranca sin datos. Los datos de ese piloto no se
> migran ni se borran. El procedimiento se conserva como referencia para cortar datos de una
> finca real en el futuro.

Este es el procedimiento de spec §6 líneas 155-160. Es una compuerta que exige conciliación
real, no un cambio de endpoint. Ver `docs/PREFLIGHT-PRODUCCION.md` para el inventario que
debe completarse **antes** de iniciar este procedimiento — este documento no repite ese
inventario, lo asume hecho.

1. **Inventariar el servidor real de datos del piloto** y los outboxes de los teléfonos en
   campo (no asumir que el staging actual es descartable si contiene registros reales — ver
   `docs/PREFLIGHT-PRODUCCION.md` sec. 2).
2. **Congelar la fuente**: detener escrituras contra el servidor origen antes de tomar el
   dump final. Sin fuente congelada, cualquier corte posterior es inválido.
3. **Dump final** del origen congelado.
4. **Restauración aislada** en el entorno de producción nuevo, sin sobrescribir ni tocar el
   origen.
5. **Validación** sobre la restauración aislada:
   - UUIDs coinciden entre origen y destino.
   - Eventos e historial se preservan íntegros.
   - Permisos y roles quedan correctamente asignados.
   - Sincronización funciona extremo a extremo con al menos un dispositivo de prueba.
6. **Preservar la fuente y los dispositivos** — no se destruyen ni se reciclan hasta que la
   migración esté validada y aceptada.
7. **Prohibiciones explícitas durante el corte:**
   - No resetear SQLite en los teléfonos.
   - No reinstalar la app para cambiar de URL mientras existan operaciones pendientes sin
     sincronizar.
   - No tratar el respaldo del servidor como cobertura de lo pendiente local: un backup del
     servidor no incluye lo que aún vive solo en el teléfono.
8. Solo tras restauración de ensayo y conciliación exitosa se autoriza el corte final con
   escrituras detenidas (criterio ya establecido en `docs/PREFLIGHT-PRODUCCION.md` sec. 2).

## 4. Rotación de credenciales

Procedimiento a seguir tras una exposición sospechada o confirmada de cualquier credencial
de la tabla de la sec. 1 (spec línea 134):

1. Identificar exactamente qué credencial se expuso y qué consumidor la usa (tabla de
   custodia, sec. 1 de este documento).
2. Revocar la credencial expuesta en su origen (Tailscale, GitHub Environment, base de
   datos, etc.) antes de emitir la nueva.
3. Emitir credencial nueva y desplegarla al consumidor exacto — nunca ampliar el alcance ni
   los consumidores como atajo de la rotación.
4. Confirmar que el consumidor antiguo ya no puede autenticar con el valor revocado.
5. Documentar la rotación, la revocación y la restauración de servicio, sin registrar
   valores secretos — solo qué credencial, cuándo y quién.
6. Para JWT signing key: la rotación puede invalidar sesiones activas, pero no debe
   eliminar el outbox local de ningún dispositivo (`docs/POLITICAS-OPERACION.md`).
7. Rotación de contraseñas de DB, JWT y OAuth se ensaya primero en staging, nunca se
   estrena directamente en producción (`docs/POLITICAS-OPERACION.md`).
8. La firma Android **no** se rota rutinariamente — se preserva para permitir
   actualizaciones (`docs/POLITICAS-OPERACION.md`); su procedimiento de recuperación ante
   pérdida es un caso distinto, cubierto por feature-0014, no por esta rotación.

## 5. Diagnóstico

Qué observar cuando algo falla, y a través de qué canal:

- **Logs**: rotados; revisar primero los del servicio afectado (API, migrador, proxy,
  PostgreSQL) sin imprimir secretos ni compose renderizado.
- **Salud (health)**: endpoint de salud del API y verificación de versión/SHA desplegado.
- **Alertas de disco**: aviso a 70% de uso, alerta urgente a 85%.
- **Memoria / OOM**: vigilar eventos de out-of-memory del host y de los contenedores.
- **Salud externa**: verificación desde fuera de la VPS (no basta con que el propio host se
  reporte sano — spec línea 107-108 y criterio de aceptación de spec §7).

**Acceso por rol:**

- El **operador humano** (propietario) tiene acceso administrativo completo: OCI, SSH
  operador, consola, gestor de secretos.
- **CI aprobado** solo puede invocar el comando de despliegue restringido a través de
  `hato-deploy`; no tiene shell, no lee secretos, no modifica el helper ni el compose.
- El **monitor** (feature-0015, autohospedado en home-server) recibe únicamente pings de
  estado — nunca claves de despliegue ni acceso a datos de la aplicación.

### 5.1 Verificar el despliegue capa por capa

El despliegue atraviesa siete capas y cada una falla con su propio mensaje. Cuando algo se
rompe, identificar **la capa** antes de tocar nada ahorra el grueso del tiempo: un fallo en
la capa 3 se manifiesta con frecuencia como un síntoma que parece de la capa 4.

| # | Capa | Cómo comprobarla de forma aislada |
|---|---|---|
| 1 | Nodo efímero de CI en el tailnet | paso `Assert tailnet membership` (exige `BackendState == Running`) |
| 2 | Ruta y ACL hacia el host | `ssh-keyscan` obtiene la clave de host; requiere grant `tag:ci` → destino |
| 3 | Identidad del host y clave de CI | huella comparada contra `PRODUCTION_SSH_HOST_FINGERPRINT` |
| 4 | Autorización del manifiesto | `deploy-entry` valida formato, firmantes y firma |
| 5 | Compose y variables | `docker compose pull/up` con `--env-file` root-owned |
| 6 | Base de datos y migraciones | contenedor `migrate` debe salir con código 0 |
| 7 | Proxy y publicación TLS | Caddy escuchando en 443 y `/health` respondiendo |

**Dos formas de verificar que mienten.** Ambas costaron tiempo real y conviene tenerlas
presentes:

- **Verificar con `sudo` no reproduce nada.** `deploy-entry` corre como `hato-deploy`, sin
  sudo (spec D7). Comprobar un permiso con `sudo` pasa por encima justo de las barreras que
  fallan. Usar siempre `sudo -u hato-deploy ...` para reproducir el camino real.
- **Verificar contra el repositorio no reproduce nada.** `deploy-entry`,
  `deploy-root` y `compose.production.yml` viven en la VPS y **no se sincronizan solos** al
  desplegar. Comparar siempre el `sha256sum` de lo instalado contra el del repositorio antes
  de dar por aplicado un cambio.

### 5.2 Catálogo de fallos conocidos del despliegue

Registrados durante la puesta en marcha de producción (2026-09-21). Todos eran bloqueos
absolutos en código que nunca se había ejecutado de extremo a extremo, no regresiones.
Cada uno tiene hoy una prueba de regresión en `tests/ops/production/`.

**`backend error: invalid key: unable to validate API key`** (capa 1)
Las credenciales OAuth de Tailscale del Environment correspondiente no son válidas
(revocadas, mal pegadas o de otro tailnet). Regenerar el cliente OAuth con scope
**Auth Keys: Write** y el tag `tag:ci`, y recargar `TS_OAUTH_CLIENT_ID` /
`TS_OAUTH_SECRET`. Atención: la acción de Tailscale cierra con `outcome=success` aunque
todos sus reintentos fallen; el paso `Assert tailnet membership` existe precisamente para
que eso no se enmascare.

**`ssh-keyscan` sin respuesta pese a que el host está vivo** (capa 2)
Falta un grant en la ACL de Tailscale hacia el destino. El nodo de CI lleva `tag:ci`;
comprobar que existe un grant `tag:ci` → `tag:<destino>` para `tcp:22`. Staging apunta a
`joemanserver` (`tag:home-server`), que es un destino **distinto** al de producción y
necesita su propio grant.

**`[deploy-entry] rechazado: authorization con formato inválido`** (capa 4)
Con una firma legítima, indica que la comprobación de formato no puede evaluarse. Causa
original: un cuantificador `{32,65536}` en la regex superaba el `RE_DUP_MAX` de glibc
(32767), bash no llegaba a compilar la expresión y `[[ =~ ]]` devolvía error, que el `!`
convertía en rechazo. No usar intervalos grandes en regex de bash; comprobar la longitud
aparte.

**`[deploy-entry] rechazado: no existe el archivo root-owned de firmantes autorizados`** (capa 4)
El archivo puede existir con permisos correctos y aun así ser inalcanzable: `hato-deploy`
no puede **atravesar** un directorio 700. Por eso la lista de firmantes vive en
`/etc/hato-production-public` (755) y no junto a los secretos. Comprobar con
`sudo -u hato-deploy test -f ...`, nunca con `sudo` a secas.

**`[deploy-entry] rechazado: la firma de autorización no es válida`** (capa 4)
Si la clave es la correcta, sospechar de los **bytes**, no de la clave. El servidor no
verifica lo que recibe: reconstruye el payload con
`jq -cS '{release, sha, run_id, run_attempt, digests}'` y verifica eso. El workflow tiene
que firmar exactamente esa forma canónica. Firmar la salida por defecto de `jq` producía
110 bytes contra 79 verificados.

**`dependency failed to start: container hato-production-postgres is unhealthy`** (capa 6)
Revisar los logs del contenedor buscando el hook de inicialización. Un
`syntax error at or near ":"` con un `:'nombre'` sin interpolar significa que
`\getenv` no encontró la variable de entorno **dentro del contenedor**: `compose.production.yml`
debe pasar cada variable que el hook lee, con su nombre original. Importante: el hook solo
corre con `PGDATA` vacío. Si ya falló una vez, la imagen lo salta
(`Skipping initialization`) y los roles no se crearán aunque se corrija la causa — hay que
vaciar el directorio de datos, decisión que exige confirmación humana explícita (§6).

**`open /data/tls/server.crt: no such file or directory`** (capa 7)
Caddy en bucle de reinicio con el certificado sano en otro volumen. Compose antepone el
nombre del proyecto a los volúmenes declarados, mientras que `production-tls-renew.sh`
escribe en el nombre sin prefijo. Los volúmenes llevan `name:` explícito para desactivar
ese prefijado; si vuelve a divergir, comparar el nombre en ambos archivos.

**`[deploy-root] fallo: no existe el helper root-owned de backup pre-release`** (capa 5)
El bootstrap instala **seis** helpers (`ops/production/bootstrap.md` §5); si solo se
ejecutó parte del bloque, faltan. Este en concreto no se nota hasta el **segundo**
despliegue: el primero no tiene release previa registrada y se salta el backup por
completo, así que ese camino no se recorre. Comprobar que existen y son ejecutables
`production-backup-pre-release`, `backup.sh`, `backup-upload.sh` y `backup-manifest.sh`,
además de `deploy-entry` y `deploy-root`.

**El paso `Verify release` queda `cancelled` y el job agota `timeout-minutes`** (capa 7)
Síntoma de que el smoke test no alcanza el endpoint, no de que el release esté mal.
Comprobar **antes que nada** si el despliegue en sí funcionó: contenedores arriba,
`STATE_FILE` actualizado y `/health` respondiendo desde una máquina del tailnet con
permisos. La causa observada fue que la ACL concedía a `tag:ci` únicamente `tcp:22` hacia
`tag:hato-production`, mientras que el smoke test necesita **`tcp:443`**; el nodo de CI
podía desplegar pero no verificar lo que acababa de desplegar.

### 5.3 Estado alcanzado el 2026-09-21

Primer despliegue completo de producción, verificado de extremo a extremo:

```
release       8167a4eb33d861f566d0fc2c0f186231bc108a76
/health       {"status":"ok"}
/version      commit 8167a4eb... (coincide exacto con el release)
roles         hato_owner, hato_runtime, hato_backup
contenedores  postgres (healthy), migrate (exit 0), api, proxy
publicacion   100.78.135.16:443 via Caddy con certificado de Tailscale
```

**Producción expone únicamente la API.** `compose.production.yml` declara `postgres`,
`migrate`, `api` y `proxy`; no incluye el servicio `web` que sí tiene
`compose.staging.yml`, y `Caddyfile.production` envía todo `:443` a `api:8080`. La interfaz
administrativa (`clients/admin-web`) está desplegada en staging, no en producción; añadirla
es alcance nuevo, no un arreglo pendiente.

URLs vigentes, todas solo dentro del tailnet:

| Entorno | API | Interfaz web |
|---|---|---|
| Producción | `https://hato-production.tail833a02.ts.net` | — no desplegada |
| Staging | `https://joemanserver.tail833a02.ts.net:8448` | `https://joemanserver.tail833a02.ts.net:8449` |

## 6. Retorno seguro / rollback

`docker compose` **no garantiza rollback** (spec línea 150-153). El procedimiento de
recuperación depende de en qué punto del release falló:

- **Falla antes de migrar**: conservar el release anterior en ejecución; el despliegue
  fallido no reemplaza nada todavía.
- **Falla después de migrar**:
  - Si las imágenes previas son **compatibles** con el esquema ya migrado, volver a esas
    imágenes previas.
  - Si **no son compatibles**, no hay vuelta atrás automática: se mantiene modo
    mantenimiento y se aplica **corrección hacia adelante** (fix forward), nunca un
    downgrade de esquema improvisado.
- **Restaurar la base de datos** desde backup exige una **decisión humana explícita** sobre
  qué hacer con los datos posteriores al backup restaurado. Nunca es automática, nunca
  implica borrar el volumen de datos anterior sin esa decisión.
- Cada intento de recuperación deja registro de: versión previa, acción tomada, resultado,
  sin secretos.

## 7. Compuertas externas

Este documento describe el procedimiento; **no certifica que se haya ejecutado ni que
producción esté abierta a registros reales.** Las siguientes compuertas, externas a este
documento, deben cumplirse antes de esa apertura:

- **`docs/spec/feature-0013-drive-backups/spec.md`** — D6: producción no abre registros
  reales hasta que exista una restauración mensual real probada en PostgreSQL aislado,
  ejecutada antes de registrar datos reales (D6 de esa spec). El estado de esa compuerta a
  la fecha de este documento es: ADR-0034 aceptado, implementación e instalación
  pendientes (ver `docs/POLITICAS-OPERACION.md` sec. "Estado documental y compuertas").
- **`docs/spec/feature-0014-android-release-distribution/spec.md`** — distribución Android:
  depende de que el canal estable de producción (esta spec, feature-0012) exista primero.
  ADR-0035 aceptado; inventario de firma y ejecución pendientes a la fecha de este
  documento.
- ~~**Validación de campo del corte de datos** (sec. 3 de este documento)~~ — retirada el
  2026-09-23 por [ADR-0040](adr/0040-reorientacion-por-cambio-de-cliente-piloto.md):
  producción arranca sin datos del piloto anterior.

El orden de paquetes declarado en `docs/PREFLIGHT-PRODUCCION.md` sec. 4 aplica: bootstrap de
0012 → backup 0013 y monitores 0015 → restore de DB → release y corte de 0012 (este
documento) → 0014 y ensayo de actualización del teléfono → apertura. Ninguna aprobación
documental (ADR aceptado, spec redactada) equivale a completar una tarea operativa.

## 8. Límites explícitos

Fuera de alcance para producción, conforme spec §7:

- Alta disponibilidad (HA).
- Kubernetes.
- Dominio comprado (el acceso es privado vía Tailscale, sin dominio público).
- Funcionalidades de negocio nuevas.
- Publicación en tiendas de aplicaciones.

Adicionalmente, spec §7 advierte: el free tier de OCI puede perderse — se debe verificar sus
condiciones vigentes y mantener la posibilidad de reconstrucción en otro proveedor como
salida, no como plan activo.

## 9. Ver también

- `docs/spec/feature-0012-production-environment/spec.md` — spec completa de producción.
- `docs/POLITICAS-OPERACION.md` — decisiones operativas y su estado de aprobación (ADRs).
- `docs/PREFLIGHT-PRODUCCION.md` — inventario y altas asistidas previas al corte.
- `docs/BACKUPS.md` — estrategia y protocolo de backups (documento previo; la implementación
  vigente de backup para producción según feature-0013 se referencia en
  `docs/POLITICAS-OPERACION.md`).
