# test-e2e.md — Monitorización autohospedada

> Pruebas futuras, en entorno aislado. Registrar SHA/versiones, fecha y resultados;
> nunca adjuntar ping URLs, contraseñas, correos personales ni dumps a artifacts públicos.

## Evidencia parcial — 2026-09-29 UTC

- Healthchecks/PostgreSQL están activos en home-server; Caddy publica panel privado en 8450
  y receptor de ping en 8451. Oracle alcanza el receptor y no el panel.
- Los checks `backup-prod` y `restore-prod` quedaron `up` después de señales reales de
  backup y restore. La reconciliación preserva el estado.
- Las pruebas locales de Healthchecks/Caddy/sync-env pasan y no imprimen secretos.
- El 2026-09-29 `home-server/scripts/test-healthchecks-email.py` pasó los casos `/fail` y
  silencio con notificaciones registradas sin error SMTP; ambos checks temporales se
  eliminaron. El worker `sendalerts` está `running`, sin reinicios y fuera de la red proxy.
  El propietario confirmó recepción de ambos correos en el buzón; los vencimientos productivos
  26 h/35 días todavía no se han observado.
- Los 590 tests .NET pasaron contra PostgreSQL 16 aislado el 2026-09-29. La suite operativa
  volvió a pasar sin skips el 2026-09-29, incluidas las restauraciones reales de fixture.

## Evidencia de ejecución — 2026-09-30 UTC

### E2E-1 — Acceso privado y matriz de identidades
- **Panel 8450 desde tailnet:** Responde `HTTP/2 302 -> 200` en `/accounts/login/` con certificado válido Let's Encrypt (emitido por Tailscale, validado sin `-k`), HSTS `Strict-Transport-Security: max-age=31536000` y cookies `Secure`.
- **Panel 8450 desde internet público:** Inaccesible. DNS público retorna `NXDOMAIN`; la IP `100.120.245.109` es CGNAT privada de Tailscale y el router doméstico no tiene reenvío de puertos.
- **Ping 8451 desde Oracle VPS:** Conecta con TLS válido verificado; ruta raíz `/` responde `HTTP/2 404 Not found`. Solo endpoints con regex de UUID (`^/([0-9a-fA-F-]{36})(/start|/fail)?$`) son aceptados.
- **Panel 8450 desde Oracle VPS:** Conexión descartada/rechazada (`Timeout was reached`), bloqueada en capa de red por las Tailscale ACLs (`tag:hato-production` solo tiene permitido el puerto 8451 de `tag:home-server`).
- **Rutas no autorizadas en 8451:** Solicitudes a `/`, `/accounts/login/`, `/api/v3/checks/` retornan `404 Not found`.
- **Aislamiento de Docker:** Ningún contenedor de Healthchecks (`healthchecks-web`, `healthchecks-alerts`, `healthchecks-db`) monta el socket de Docker (`/var/run/docker.sock`).

### E2E-2 — Fallo, ausencia y contrato de backups
- **Prueba automatizada de contrato:** `tests/ops/backups/test-schedule-and-status.sh` (Test 4) ejecutado con éxito:
  - Respaldo solo local (`backup.sh`) genera dump pero no emite ningún ping hacia Healthchecks.
  - Fallo en subida remota (`backup-daily.sh` con subida abortada) emite señal explícita `/fail` y **jamás** emite ping de éxito.
- **Validación de alertas por correo:** `scripts/test-healthchecks-email.py` ejecutó fallos y silencios inducidos sobre checks efímeros:
  - Notificaciones enviadas por SMTP sin errores (`email_errors=0`).
  - Propietario confirmó recepción real en su buzón el 2026-09-29.
- **Checks productivos:** `backup-prod` (plazo 26 horas) y `restore-prod` (plazo 35 días) configurados y en estado `up`.

### E2E-3 — Reinicio, recarga TLS y recursos
- **Recarga TLS Caddy:** Ejecutada mediante API Unix `docker exec caddy caddy reload --config /etc/caddy/Caddyfile --adapter caddyfile --address unix//run/caddy-admin.sock`. Cero interrupciones, socket admin protegido sin exposición a TCP.
- **Reinicio del stack:** `docker compose restart` ejecutado en `home-server`:
  - Contenedores `healthchecks-web`, `healthchecks-alerts`, `healthchecks-db` reiniciaron limpiamente.
  - Los checks `backup-prod` y `restore-prod` mantuvieron sus estados `up` y sus marcas `last_ping` previas intactas en PostgreSQL persistente (`/srv/healthchecks/postgres`).
- **Medición de recursos (24 h):**
  - Muestreo activo cada 15m vía `medir-recursos-24h.timer` hacia `/srv/healthchecks/metricas-24h.tsv`.
  - Evaluación histórica de 365 muestras en Beszel (2026-09-17 a 2026-09-30) con `scripts/analizar-metricas-24h.py`:
    - RAM Media: 319.87 MiB (Presupuesto: 512.0 MiB) -> **CUMPLE**
    - RAM Pico: 367.42 MiB (Límite: 512.0 MiB) -> **CUMPLE**
    - CPU Media: 0.50% (Presupuesto: < 5.0%) -> **CUMPLE**
    - CPU Pico: 1.39%
    - Disco PostgreSQL: 49 MB (Presupuesto: 2048 MB) -> **CUMPLE**
  - La ventana formal de 24 horas concluye el **2026-10-01 02:42 UTC**.

### E2E-4 — Restauración y límite del monitor
- **Respaldo automatizado:** `scripts/backup-healthchecks.sh` ejecutado exitosamente; generó volcado binario custom comprimido (`healthchecks-db-...dump`, 79 KB), hash SHA256 y manifiesto JSON en `/srv/healthchecks/backups/` con permisos `600`.
- **Restauración en aislamiento:** Volcado restaurado en contenedor limpio y aislado `test-hc-restore-isolated` (`postgres:16-alpine`):
  - Los 4 checks (`My First Check`, `backup-prod`, `tls-cert-prod`, `restore-prod`) y sus estados se recuperaron íntegramente.
  - El contenedor de prueba se destruyó inmediatamente tras verificar la consulta SQL, sin alterar la base productiva.
- **Límite arquitectónico documentado:** Si el servidor doméstico sufre una caída total (corte eléctrico prolongado o corte de fibra/ISP), las alertas no se emitirán. Un monitor autohospedado en un único host físico no puede alertar sobre su propia caída completa; supervisión externa redundante requeriría un segundo proveedor independiente (ADR futuro).

