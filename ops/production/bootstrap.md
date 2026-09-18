# Bootstrap de `hato-deploy` — runbook administrativo

> **Estado:** procedimiento futuro. Nadie ha ejecutado esto todavía. El spec
> ([spec.md §3](../../docs/spec/feature-0012-production-environment/spec.md#3-preparación-usuario-de-despliegue))
> dice explícitamente: *"no se ha creado el usuario en este trabajo"*. Este documento es la
> guía paso a paso para cuando un administrador humano lo haga contra el servidor real.
>
> Fuente de verdad: spec.md §3 y §2 D7 (privilegios mínimos del usuario de CI). Custodia de
> credenciales: spec.md §5. Decisión de diseño: [ADR-0033](../../docs/adr/0033-produccion-privada-oracle.md)
> y [ADR-0030](../../docs/adr/0030-secretos-y-acceso-de-despliegue.md).

Este runbook lo ejecuta un administrador humano con su propia identidad SSH, nunca un
workflow ni un agente. Ningún paso de este documento debe automatizarse sin revisión
administrativa explícita.

## 0. Antes de conectar

1. Verificar la huella del host por un **canal independiente** al que se va a usar para
   conectar (por ejemplo, la consola de la nube o un canal ya autenticado previamente), no
   solo confiando en lo que devuelva `ssh-keyscan` en el momento de la conexión.
2. Conectar con el cliente SSH configurado con `StrictHostKeyChecking=yes` y la huella ya
   fijada, con **tu propia identidad de administrador**, no con la clave de CI.
3. **No cerrar esta sesión SSH pública** hasta completar el paso 6 (segunda sesión por
   Tailscale) y el paso 7 (acceso de recuperación OCI). Si se cierra antes y algo falla en
   la configuración de red, puede perderse el único acceso al servidor.

## 1. Crear el usuario `hato-deploy`

Cuenta sin contraseña utilizable, home dedicado, sin privilegios adicionales. La shell
efectiva queda restringida más adelante por el comando forzado de sshd (sección 4), no por
la shell de login en sí.

```bash
sudo adduser --disabled-password --gecos '' hato-deploy
```

## 2. Crear directorios root-owned

Ninguno de estos directorios es escribible por `hato-deploy`. Son responsabilidad exclusiva
del administrador.

```bash
sudo install -d -o root -g root -m 755 /etc/ssh/authorized_keys
sudo install -d -o root -g root -m 755 /usr/local/libexec/hato
sudo install -d -o root -g root -m 700 /etc/hato-production
```

| Directorio | Dueño | Modo | Propósito |
|---|---|---|---|
| `/etc/ssh/authorized_keys` | root:root | 755 | Contiene `hato-deploy` (644) con la clave pública de CI. Es una clave pública, y `sshd` debe poder leerla al evaluar esta cuenta. |
| `/usr/local/libexec/hato` | root:root | 755 | Contiene `deploy-entry` y `deploy-root`, ambos root-owned. |
| `/etc/hato-production` | root:root | 700 | Secretos y configuración de producción fuera del checkout. |

## 3. Instalar la clave pública dedicada de CI

La clave privada correspondiente vive únicamente en el GitHub Environment `production`
(spec.md §5), nunca en este repositorio ni en disco fuera de ese secreto gestionado.

```bash
# TODO: reemplazar <CLAVE-PUBLICA-CI> con el contenido real de la clave pública
# dedicada generada para el job de despliegue (no la clave personal del administrador).
sudo install -o root -g root -m 644 /dev/stdin /etc/ssh/authorized_keys/hato-deploy <<'EOF'
restrict,command="/usr/local/libexec/hato/deploy-entry" ssh-ed25519 AAAA...<CLAVE-PUBLICA-CI>... hato-deploy-ci
EOF
```

Notas sobre la entrada:

- `restrict` deshabilita por defecto forwarding de puertos, agente, X11, PTY y SFTP para
  esta clave. Es la primera capa; la segunda es la configuración de `sshd` (sección 4).
- `command="/usr/local/libexec/hato/deploy-entry"` fuerza que, sin importar qué comando
  pida el cliente SSH, el servidor ejecute siempre este script. `deploy-entry` recibe la
  petición original solo como texto en `SSH_ORIGINAL_COMMAND` y **no la usa como código**
  (ver `scripts/production-deploy-entry.sh`); lee la petición estructurada por stdin.

### Firma independiente de la autorización de despliegue

Además de la clave SSH, generar una segunda clave ed25519 exclusiva para firmar el
manifiesto de despliegue. Su privada entra como `DEPLOY_AUTHORIZATION_KEY` en el Environment
`production`; **no** se instala en la VPS. La pública se instala como una lista de firmantes
OpenSSH, con el principal fijo `hato-production`:

```bash
sudo install -o root -g root -m 644 /dev/stdin \
    /etc/hato-production/deploy-authorization.allowed-signers <<'EOF'
hato-production ssh-ed25519 AAAA...<CLAVE-PUBLICA-DE-AUTORIZACION>... hato-production-authorization
EOF
```

`deploy-entry` verifica esa firma antes de llamar a `sudo`. El manifiesto firmado contiene
`run_id`, `run_attempt`, release, SHA y los dos digests; `deploy-root` consume el par
run/attempt una sola vez en `/var/lib/hato-production/consumed-deployments`. Un retry debe
crear un nuevo intento de GitHub Actions, no reenviar un manifiesto previo.

## 4. Configurar sshd para esta cuenta

Instalar el fragmento versionado en el repo:

```bash
sudo install -o root -g root -m 644 ops/production/sshd-hato.conf \
    /etc/ssh/sshd_config.d/50-hato-deploy.conf
```

Revisar que `AuthorizedKeysFile` global no siga aplicando para esta cuenta desde otra ruta
(por ejemplo `~/.ssh/authorized_keys` de `hato-deploy` no debe existir ni usarse).

**Validar la sintaxis antes de recargar:**

```bash
sudo sshd -t
```

Solo si `sshd -t` no reporta error, recargar:

```bash
sudo systemctl reload sshd
```

## 5. Instalar `deploy-entry` y `deploy-root`

**Prerrequisito:** ambos scripts parsean la petición JSON con `jq`. Instalarlo antes de
copiar los scripts; si falta, `deploy-entry`/`deploy-root` fallan de forma segura (no
despliegan nada), pero conviene no depender de eso:

```bash
sudo apt install -y jq
```

```bash
sudo install -o root -g root -m 755 scripts/production-deploy-entry.sh \
    /usr/local/libexec/hato/deploy-entry
sudo install -o root -g root -m 755 scripts/production-deploy-root.sh \
    /usr/local/libexec/hato/deploy-root
sudo install -o root -g root -m 755 scripts/production-backup-pre-release.sh \
    /usr/local/libexec/hato/production-backup-pre-release
sudo install -o root -g root -m 755 scripts/backup.sh /usr/local/libexec/hato/backup.sh
sudo install -o root -g root -m 755 scripts/backup-upload.sh /usr/local/libexec/hato/backup-upload.sh
sudo install -o root -g root -m 755 scripts/backup-manifest.sh /usr/local/libexec/hato/backup-manifest.sh
```

Ambos archivos y el directorio que los contiene son root-owned y **no escribibles por
`hato-deploy`**. Confirmar con `ls -ld /usr/local/libexec/hato` y
`ls -l /usr/local/libexec/hato/deploy-entry /usr/local/libexec/hato/deploy-root` que el
grupo/otros no tienen permiso de escritura.

## 6. Instalar la regla sudoers

```bash
sudo visudo -cf ops/production/sudoers-hato
# Solo si la validación anterior pasa sin error:
sudo install -o root -g root -m 440 ops/production/sudoers-hato \
    /etc/sudoers.d/hato-deploy
sudo visudo -c
```

La regla da a `hato-deploy` permiso para ejecutar **exactamente un comando**,
`/usr/local/libexec/hato/deploy-root`, sin contraseña. No hay wildcards ni `ALL`. No hay
acceso al grupo `docker` ni al socket Docker (spec.md D7).

## 6.1 Instalar rotación de logs y programar mantenimiento

```bash
sudo install -d -o root -g root -m 750 /var/log/hato-production
sudo install -o root -g root -m 644 ops/production/hato-production.logrotate \
    /etc/logrotate.d/hato-production
sudo logrotate --debug /etc/logrotate.d/hato-production
```

La ventana mensual de mantenimiento se agenda por el operador después de que feature-0013
habilite el backup pre-mantenimiento verificable. Antes de actualizar paquetes o imágenes:
comprobar backup offsite, registrar SHA, validar health y versión después del mantenimiento.
No habilitar reinicios automáticos inesperados ni ejecutar mantenimiento desde `hato-deploy`.

## 7. Probar acceso de recuperación de OCI

Antes de cerrar la sesión SSH pública abierta en el paso 0, probar el mecanismo de
recuperación de consola/OCI (por ejemplo, la consola serie de Oracle Cloud) para confirmar
que existe una vía de entrada si Tailscale o sshd quedan mal configurados. Documentar en la
bitácora del propietario (no en este repo) que la prueba se hizo y su resultado.

## 8. Verificar segunda sesión por Tailscale

Con la sesión SSH pública original todavía abierta, abrir una **segunda sesión** distinta
usando la IP Tailscale del servidor, con la identidad de administrador. Solo si esa segunda
sesión funciona y el acceso de recuperación OCI ya se probó (paso 7), se puede considerar
cerrar el SSH público más adelante (fuera del alcance de esta entrega: eso es parte del
endurecimiento de red de spec.md §4, no de este runbook de usuario).

## 9. Checklist de verificación de cierre

Ejecutar cada verificación desde una conexión que use **únicamente** la clave dedicada de CI
(`ssh -i <clave-ci> hato-deploy@<host>`), nunca la identidad de administrador:

- [ ] La conexión dedicada, con una petición JSON válida por stdin, despliega una fixture
      autorizada de prueba (release/sha/digests de prueba, no producción real) y termina
      en éxito.
- [ ] Pedir una shell interactiva (`ssh hato-deploy@<host>`) **falla** — no debe abrir shell.
- [ ] Port forwarding (`ssh -L`/`-R`/`-D`) **falla**.
- [ ] PTY (`ssh -t hato-deploy@<host> ...`) **falla**.
- [ ] SFTP (`sftp hato-deploy@<host>`) **falla**.
- [ ] Cualquier comando arbitrario distinto del forzado
      (`ssh hato-deploy@<host> "cat /etc/passwd"`) **falla** o es ignorado — el servidor
      solo ejecuta `deploy-entry` sin importar qué se pida.
- [ ] `ssh hato-deploy@<host> "sudo -n true"` **falla** (no tiene sudo general, solo el
      comando exacto vía `deploy-root`).
- [ ] `hato-deploy` no puede leer `/etc/hato-production` (secretos) ni escribir en
      `/usr/local/libexec/hato` (helper) ni en la ruta del compose de producción.
- [ ] Registrar en la bitácora del propietario (no en este repo): usuario `hato-deploy`,
      huella pública de la clave de CI instalada, y fecha de esta verificación. **Nunca
      registrar la clave privada.**

Si alguna verificación de "debe fallar" tiene éxito, revertir la instalación (deshabilitar
la entrada en `authorized_keys`) y corregir antes de continuar.
