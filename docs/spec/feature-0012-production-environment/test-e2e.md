# test-e2e.md — Verificación operativa

> Escenarios futuros, no ejecutados. [Spec](./spec.md) · [Tareas](./tasks.md).
> Ejecutar tras suite automatizada verde, con identidades fixture y entornos aislados.
> Nunca inyectar corrupción, perder acceso o borrar datos en la VPS real para ensayar.
> Registrar fecha, SHA, precondiciones, pasos, resultado y evidencia sin secretos.

## E2E-1 — Acceso privado

**Preparación:** VPS de ensayo con cuenta recuperable y teléfono autorizado.

**Pasos:** Comprobar HTTPS y SSH administrativos por Tailscale; desconectar tailnet y probar IP pública; reiniciar y repetir.

**Debe pasar:** Solo puertos/autorizaciones previstos accesibles; empleados sin SSH ni staging; segunda sesión y recuperación funcionan antes del cierre público.

## E2E-2 — Usuario restringido

**Preparación:** Usuario hato-deploy y release fixture aprobada.

**Pasos:** Probar shell, SFTP, forwarding, sudo general y modificación de helper; enviar manifiesto con comando/ruta ajena; después deploy válido.

**Debe pasar:** Todos los accesos ajenos fallan sin cambio; deploy autorizado funciona; no acceso al socket Docker.

## E2E-3 — Aprobación verificable

**Preparación:** Dos runs con SHA/attempt distintos, uno sin approval.

**Pasos:** Enviar run sin approval, SHA cambiado, digest ajeno y replay; simular GitHub inaccesible.

**Debe pasar:** Rechazo antes de mutación y error redactado; solo manifiesto ligado a run aprobado se acepta.

## E2E-4 — Secrets y DB

**Preparación:** Secrets ficticios rastreables y stack aislado.

**Pasos:** Buscar valores ficticios en logs/imagen/argv; intentar DDL/escritura como backup y DDL como runtime; arrancar sin JWT.

**Debe pasar:** No filtración; permisos mínimos; falta de secreto detiene arranque; dump autorizado completo.

## E2E-5 — Migración fallida

**Preparación:** Clon aislado, release previa y snapshot verificable.

**Pasos:** Provocar fallo antes y después de migración; revisar versión, escrituras y volumen original.

**Debe pasar:** Sin restauración automática; anterior solo si compatible; mantenimiento y error explícito después de cambio incompatible.

## E2E-6 — Corte y continuidad

**Preparación:** Datos fixture con UUID/eventos y teléfono con pendientes.

**Pasos:** Simular congelación, dump final, restore, actualización API y sincronización duplicada.

**Debe pasar:** Sin pérdida/duplicación; origen preservado; app anterior soportada; registros rechazados conservan motivo.

## E2E-7 — Aceptación operativa

**Preparación:** Backups, monitor externo y guías completados.

**Pasos:** Simular indisponibilidad y recuperar en VPS limpia; registrar tiempo y responsables; entregar guía al operador.

**Debe pasar:** Alerta externa y recuperación medida; no declarar producción si supera objetivo sin revisión explícita.

## Cierre

## E2E-8 — Certificado servido y renovación

**Preparación:** proxy aislado con nombre propio y timer de renovación, acceso de consola.

**Pasos:** comprobar HTTPS sin `-k`, renovar par y recargar por socket Unix; inspeccionar
certificado servido; simular fallo de emisión y reinicio previo a disponibilidad Tailscale.

**Debe pasar:** nombre/cadena/expiración válidos; recarga usa nuevo par; fallo conserva
certificado anterior y alerta; no API admin TCP ni fallback público; deploy no lee clave.

## Resultado final

Cualquier incumplimiento mantiene abierta su tarea. Confirmar restauración de configuración
y plazos tras ensayo, revocar credenciales fixture y eliminar únicamente recursos de prueba
identificados. Evidencia y decisión del propietario previas a apertura con registros reales.
