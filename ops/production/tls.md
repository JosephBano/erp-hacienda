# TLS privado de producción

Implementa TLS.1/TLS.2 de feature-0012. Solo un administrador del host ejecuta
este procedimiento: `hato-deploy` no puede leer el volumen de Caddy ni estos secretos.

## Instalación

Después de crear Tailscale y antes de levantar por primera vez el proxy, crear el volumen
que usa `compose.production.yml`, instalar los artefactos root-owned y crear la
configuración local:

```bash
sudo docker volume create hato-production-caddy-data
sudo install -o root -g root -m 750 scripts/production-tls-renew.sh /usr/local/libexec/hato/production-tls-renew
sudo install -o root -g root -m 750 scripts/production-tls-monitor.sh /usr/local/libexec/hato/production-tls-monitor
sudo install -o root -g root -m 644 ops/production/hato-tls-*.service ops/production/hato-tls-*.timer /etc/systemd/system/
sudo install -o root -g root -m 600 /dev/stdin /etc/hato-production/tls.env
```

El contenido de `tls.env` contiene únicamente el hostname Tailscale real, por ejemplo
`HATO_TLS_HOSTNAME=nombre.oracle.ts.net`. No se registra ese valor en este repositorio.
Crear también `/etc/hato-production/tls-alert-url`, root:root modo 600, con la URL de ping
secreta de Healthchecks. La alerta no incluye datos de la finca.

Emitir el primer par y validar la configuración antes de arrancar el proxy:

```bash
sudo systemctl daemon-reload
sudo systemctl start hato-tls-renew.service
sudo systemctl enable --now hato-tls-renew.timer hato-tls-monitor.timer
```

El servicio crea el par temporalmente en `/run`, verifica hostname, vigencia y correspondencia
con la clave, y solo entonces lo instala root:root con permisos 640 dentro del
volumen. La imagen `caddy:2-alpine` ejecuta Caddy como root y no define un grupo Unix
`caddy`; si se cambia esa imagen, revisar este contrato antes de cambiar permisos. Si
`tailscale cert` falla no modifica el par existente. Durante una renovación el
par previo se conserva y se restaura si la recarga falla. Caddy se recarga por
`unix//data/caddy-admin.sock`; no se habilita API administrativa TCP.

## Verificación y recuperación

Después de levantar el stack, ejecutar `sudo systemctl start hato-tls-renew.service` y
verificar desde una identidad autorizada del tailnet `curl https://<hostname>/health` sin
`-k`. Inspeccionar el certificado servido con `openssl s_client`, sin copiar la clave.

Para probar TLS.2 en un entorno aislado, sustituir temporalmente el certificado en el
volumen por un fixture con menos de 21 días, ejecutar `hato-tls-monitor.service` y confirmar
la alerta Healthchecks. Restaurar inmediatamente el par de prueba. Un error de emisión o
recarga debe dejar disponible el certificado anterior; investigar `journalctl -u
hato-tls-renew.service` y no reiniciar el proxy hasta tener un par válido.
