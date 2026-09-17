# spec.md — Producción privada en Oracle Cloud

> **Diseño aprobado:** [ADR-0033](../../adr/0033-produccion-privada-oracle.md),
> aceptación del propietario registrada el 2026-09-16. Sustituye el estado propuesto
> de esta redacción; ejecución y usuarios pendientes. Las compuertas de pruebas permanecen.

> Paquete completo: [tareas](./tasks.md) y [E2E](./test-e2e.md).
> Aplican [políticas](../../POLITICAS-OPERACION.md) y [preflight](../../PREFLIGHT-PRODUCCION.md).

> Diseño aprobado, 2026-09-16. Trabajo transversal: habilita producción sin cerrar ninguna
> fase de ROADMAP. Los artefactos locales están implementados; instalación, evidencias E2E,
> backups y apertura siguen pendientes. Ejecución en [plan.md](./plan.md).
> Rama documental: `feature/production-environment-spec`, base `aed5461` de develop.

## 1. Propósito y evidencia

Separar producción de staging y convertir la VPS en un destino recuperable, privado y
operable. El propietario confirmó que la cuenta Oracle y el tailnet estarán bajo su
control; correo, MFA, recuperación y facturación se verifican antes de instalar.
No se publican identidades personales, claves, OCID ni contraseñas en estos documentos.

La comprobación SSH de esta conversación devolvió Ubuntu 24.04, aarch64, dos CPU visibles,
5.8 GiB de RAM, filesystem raíz de 58 GB con 56 GB libres y SSH activo. Son datos de una
observación, no reserva de capacidad ni garantía del free tier. No se verificó el shape
mediante API de Oracle. La IP y la clave administrativa viven en la configuración privada
del operador; no son valores que deba copiar un workflow desde un documento público.

`compose.staging.yml` define staging; no existe compose ni workflow de producción.
`src/Hato.Api/Dockerfile` ejecuta la API como UID 1001.
`src/Hato.Api/migrate.Dockerfile` aplica migraciones por módulo y actualmente pasa la
conexión como argumento: debe eliminarse esa exposición en producción.
`clients/field-app/eas.json` asigna hoy la misma API HTTP a preview y production.
`.github/branch-protection.expected.json` restringe production a main y exige revisor.

## 2. Decisiones y compuertas

- D1. Oracle aloja producción; el servidor doméstico continúa como staging. Sin HA ni
  promesa de disponibilidad continua. Cuenta y recuperación pertenecen al propietario.
- D2. Acceso privado por Tailscale, incluidos los teléfonos de campo. Sin dominio comprado.
  HTTPS con el nombre ts.net y Caddy usando certificado emitido por `tailscale cert`.
  Funnel permanece deshabilitado. El nombre de certificado puede aparecer en registros
  públicos de transparencia; privado significa acceso restringido, no nombre secreto.
- D3. Aplicar ADR-0033 aceptado para producción, acceso y privilegios. Respeta
  ADR-0029/0030 y mantiene el destino doméstico de ADR-0031. Reemplaza expresamente la
  premisa de que los teléfonos no pueden incorporarse al tailnet y revisa ADR-0010 sobre
  direccionamiento. No reescribir decisiones históricas como si nunca hubieran existido.
- D4. Releases etiquetadas de main, CI exitoso para el SHA exacto, aprobación del entorno
  production y despliegue serializado. Un push a main por sí solo no publica producción.
- D5. Imágenes ARM64 construidas en runner alojado, publicadas en GHCR y desplegadas por
  digest. Aprobar GHCR y la estrategia de artefactos en el ADR. Nunca compilar en la VPS
  que sirve a la finca ni instalar runner self-hosted allí.
- D6. Producción no abre registros reales hasta cumplir el
  [spec de backups](../feature-0013-drive-backups/spec.md): copia offsite descargada y
  restaurada, alertas operativas y objetivos de recuperación aceptados.
- D7. El usuario de CI solo invoca un despliegue validado. No tiene sudo general, shell
  remoto libre, acceso al socket Docker ni pertenencia al grupo docker.

## 3. Preparación: usuario de despliegue

Esta sección es un procedimiento futuro: **no se ha creado el usuario** en este trabajo.
El administrador entra con su identidad SSH propia y verifica la huella del host por un
canal independiente. Mantiene esa sesión abierta hasta comprobar una segunda sesión por
Tailscale. Antes de cerrar SSH público prueba también el acceso de recuperación de OCI.

Crear `hato-deploy` como cuenta sin contraseña utilizable, home dedicado y shell capaz de
ejecutar únicamente el comando forzado SSH. Ejemplo de preparación administrativa:

```bash
sudo adduser --disabled-password --gecos '' hato-deploy
sudo install -d -o root -g root -m 755 /etc/ssh/authorized_keys
sudo install -d -o root -g root -m 755 /usr/local/libexec/hato
sudo install -d -o root -g root -m 700 /etc/hato-production
```

La clave pública dedicada de CI se instala en `/etc/ssh/authorized_keys/hato-deploy`,
propiedad root, modo 600. sshd se configura para consultar esa ruta para esta cuenta.
Entrada autorizada: `restrict,command="/usr/local/libexec/hato/deploy-entry"` seguida de
la clave pública aprobada. `deploy-entry` y todos sus directorios son root-owned y no
escribibles por hato-deploy. Antes de recargar sshd ejecutar `sudo sshd -t`.

El entrypoint valida una petición pequeña por stdin con release, SHA y digests; rechaza
campos extra, comandos, rutas, registros e imágenes fuera de la allowlist. Invoca un único
helper root-owned mediante una regla sudoers exacta, instalada y validada con `visudo`.
El helper no ejecuta shell recibido, no acepta compose arbitrario y verifica que el
manifiesto corresponde a un release aprobado. No usa SSH_ORIGINAL_COMMAND como código.
La automatización privilegiada solo ejecuta recetas instaladas por el administrador.
Cambiar esas recetas exige revisión administrativa; desplegar imágenes sigue implicando
confianza en código aprobado que puede leer los datos de la aplicación.

Verificación de cierre: conexión dedicada despliega fixture autorizada; shell, forwarding,
PTY, SFTP, comandos arbitrarios y `sudo -n true` fallan; no puede leer secretos ni modificar
helper/compose. Registrar usuario, huella pública y fecha, nunca clave privada.

## 4. Host, red y datos

Servicios: PostgreSQL 16, migrador one-shot, API y web/proxy. Proyecto Compose
`hato-production`; datos en `/srv/hato-production/pgdata`, releases fuera de pgdata,
secretos en `/etc/hato-production`, backups según feature-0013. La infraestructura
de producción se versiona en este repo bajo `ops/production/`; home-server solo conserva
su responsabilidad de staging. Ningún volumen de producción se monta en staging.

PostgreSQL sin puerto publicado; API accesible solo desde proxy; único binding de entrada
web en IP Tailscale concreta puerto 443, consumido por Caddy. Reglas tailnet explícitas: operador a SSH y
HTTPS, empleado solo HTTPS, CI solo comando de despliegue. No heredar acceso general del
tailnet. Verificar IPv4 e IPv6 y puertos publicados por Docker; UFW solo no es prueba.
Cerrar SSH público únicamente después de las pruebas de segunda sesión y recuperación.

UTC y sincronización de reloj; logs rotados, alertas de disco a 70/85%, memoria/OOM y
salud externa. Swap de 2 GiB como propuesta sujeta a medición, no requisito de capacidad.
Actualizaciones de seguridad automáticas sin reinicio inesperado; ventana mensual de
mantenimiento con backup previo y validación posterior. Fijar versiones/digests y proceso
mensual de actualización; no congelarlas indefinidamente.

Roles PostgreSQL separados: propietario/migrador, runtime con DML mínimo y backup de
lectura. Runtime sin superusuario ni DDL. Mantener invariantes de eventos y probar
restricciones de modificación de historial donde existan; backup no garantiza inmutabilidad.
Admin inicial mediante comando de aplicación explícito de un solo uso, secreto por stdin
o archivo protegido; sin contraseña default, seed de staging ni reseteo al reiniciar.
JWT signing key, issuer y audience propios; Production debe fallar si faltan secretos.

## 5. Custodia de credenciales

| Material | Custodia | Consumidor |
|---|---|---|
| OCI, MFA, recuperación y acceso administrativo | Gestor seguro del propietario, copia de recuperación separada | Humano |
| SSH CI y OAuth Tailscale de CI con tags restringidos | GitHub Environment production | Job aprobado |
| Huella host SSH verificada | Configuración del entorno | Cliente SSH con StrictHostKeyChecking=yes |
| JWT, contraseñas DB, credencial lectura GHCR si necesaria | Archivos 600 root-owned fuera del checkout | Servicios concretos |
| Tokens de Google y claves de cifrado | Según feature-0013; recuperación independiente | Backup |
| Firma Android y GitHub App para compilación | Según feature-0014, separadas del deploy | Build/orquestador |

No poner secretos en EXPO_PUBLIC, bundles, Docker ARG/layers, compose renderizado en logs,
argumentos de proceso o backups sin cifrar. Configurar archivo montado y carga segura
cuando la aplicación no soporte *_FILE. Enmascarado de GitHub no sustituye evitar imprimir.
Rotar tras exposición; documentar rotación, revocación y restauración de cada credencial.

## 6. Release, migraciones y recuperación

El workflow recibe referencia main y etiqueta inmutable, resuelve SHA, exige CI de ese SHA,
construye artefactos y solicita aprobación antes de acceder a production. Mantener la
política main del entorno: si el disparador es un tag, actualizar explícitamente la
política declarativa y probarla; preferencia inicial por workflow_dispatch sobre main
con tag validado. SSH compara huella fijada; ssh-keyscan solo no autentica al servidor.

Descargar imágenes antes de detener escrituras. Backup pre-release verificado offsite,
modo mantenimiento para escrituras y sincronización push, migración sobre DB real y
arranque candidato. Publicar solo tras health, versión SHA y smoke autenticado. Para
migraciones que requieran exclusión, detener todos los escritores antes del backup final.
Registrar versión previa y resultado sin secretos.

Compose no garantiza rollback. Si falla antes de migrar conservar release anterior; si
migró, volver a imágenes previas solo si son compatibles con ese esquema. En caso contrario
mantener mantenimiento y aplicar corrección hacia adelante. Restaurar DB exige decisión
humana sobre datos posteriores, nunca rollback automático ni borrado del volumen anterior.

Antes de primera carga, inventariar el servidor donde realmente están los datos del piloto
y los outboxes de teléfonos. No asumir staging descartable si tiene registros reales.
Corte con fuente congelada, dump final, restauración aislada y validación de UUID, eventos,
permisos y sincronización. Preservar fuente y dispositivos. No resetear SQLite ni reinstalar
para cambiar URL con operaciones pendientes. Respaldo del servidor no incluye pendientes
locales. Esta compuerta requiere conciliación real, no solo cambiar endpoints.

## 7. Aceptación y límites

Se exige evidencia de: cuenta bajo control; acceso privado tras reinicio; usuario CI
restringido; secrets ausentes en artefactos/logs; roles DB; release y smoke del mismo SHA;
fallo de migración sin destrucción; recuperación medida; teléfono offline que sincroniza
sin duplicación; alertas desde fuera de la VPS. Free tier puede perderse: verificar sus
condiciones vigentes y permitir reconstrucción en otro proveedor. No incluye HA,
Kubernetes, dominio comprado, funcionalidades de negocio ni publicación en tiendas.

Fuentes técnicas para implementación: [Tailscale Serve](https://tailscale.com/docs/features/tailscale-serve),
[Docker y privilegios](https://docs.docker.com/engine/install/linux-postinstall/).
