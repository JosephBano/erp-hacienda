# test-e2e.md — Monitorización autohospedada

> Pruebas futuras, en entorno aislado. Registrar SHA/versiones, fecha y resultados;
> nunca adjuntar ping URLs, contraseñas, correos personales ni dumps a artifacts públicos.

## E2E-1 — Acceso privado

Preparar operador, VPS emisora y empleado. Probar panel, ping y Beszel desde cada identidad
y fuera de tailnet. Esperado: operador autorizado; VPS solo señal; empleado y acceso público
denegados. HTTPS válido sin -k. No socket Docker en Healthchecks.

## E2E-2 — Fallo y ejecución ausente

Crear checks fixture con plazos breves; simular upload fallido, éxito verificado y silencio
del emisor apagado. Esperado: correo real de fallo/ausencia y recuperación tras éxito;
dump local sin verificación remota no marca éxito. Restablecer plazos definitivos de
26 horas y 35 días y comprobar configuración guardada.

## E2E-3 — Reinicio y recursos

Reiniciar stack de ensayo, verificar fechas previas y proceso que detecta vencimientos.
Medir 24 horas RAM/CPU/disco y rotación de logs. Esperado: persistencia intacta, alertas
siguen funcionando y presupuesto del spec cumplido o diseño revisado antes de apertura.

## E2E-4 — Restauración y límite del monitor

Restaurar DB/configuración del monitor en aislamiento, desde custodia recuperable.
Esperado: checks y estado recuperados sin secretos publicados ni falsos éxitos.
Simular caída del propio monitor: registrar ausencia esperada de alertas y procedimiento
manual de recuperación, sin afirmar que puede detectarse a sí mismo apagado.
