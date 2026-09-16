# spec.md — Monitorización autohospedada de HATO

> Aprobado por ADR-0036, 2026-09-16. [Plan](./plan.md), [tareas](./tasks.md),
> [pruebas E2E](./test-e2e.md). Transversal; no declara producción operativa.

## 1. Propósito y límites de responsabilidad

El propietario solicita monitorización open source sin dependencia del servicio alojado
Healthchecks.io. Ya utiliza Beszel en home-server y acepta la dependencia del servidor
doméstico, cuya batería declara con unas cuatro horas de autonomía; UPS es una opción
futura. La autonomía de batería no prueba autonomía del router/ONT ni entrega de alertas.

Este spec define el contrato de integración de HATO. Instalación, Compose, datos, TLS y
timers de los monitores se versionan en `/home/joeman/Documents/home-server`, después
de leer sus instrucciones y abrir una rama propia. No duplicar su configuración en este
repo. Ambos PRs se enlazan y ninguna parte se declara terminada solo por existir la otra.

## 2. Diseño seleccionado para implementar

Beszel existente supervisa VPS, CPU, memoria, disco y disponibilidad. Healthchecks open
source autohospedado en home-server detecta ejecuciones ausentes de backup/restore.
No se presupone que Beszel soporte esas señales como checks arbitrarios.
Servicios por Tailscale y HTTPS de Caddy con certificado del nodo, conforme al patrón
existente; sin Funnel ni puertos públicos nuevos. Elegir puerto libre tras inventario,
sin desplazar servicios actuales. Configuración de producción y claves TLS independientes.

Healthchecks requiere aplicación, proceso de comprobación de plazos y DB persistente
soportada por la versión elegida. Revisar versión/licencia/imagen oficial y compatibilidad
antes de fijar digest. Beszel tiene licencia MIT y Healthchecks BSD-3-Clause; el código
autohospedado no requiere suscripción al servicio alojado.

## 3. Contrato con backups

Feature-0013 decide cuándo una copia es válida. Emite éxito únicamente después de subida
cifrada y verificación; fallo explícito al abortar. El monitor no convierte un dump local
en éxito offsite. Dos checks: backup diario (24 horas más 2 horas de gracia) y restore
(35 días máximo desde último éxito). Reiniciar monitor conserva fechas; no reinicia
silenciosamente el reloj de éxito. Ausencia de ping, reloj incorrecto o check deshabilitado
deben poder identificarse. Ping URLs son secretos, sin cuerpos con datos de finca.

El contrato de señales permanece en el spec de backups; aquí se configura receptor,
persistencia y entrega de alertas. La VPS necesita solo acceso de red al endpoint de ping,
no al panel administrativo. Aplicar restricciones de proxy/ruta además de grants si
panel y ping comparten puerto: Tailscale por sí solo no distingue rutas HTTP.

## 4. Alertas, credenciales y operación

Email a la cuenta de backups como destino operativo, sin dirección en git. Configurar
SMTP desde credencial de alcance mínimo en archivos protegidos de home-server; no usar
contraseña principal de Google. Probar TLS y entrega real antes de abrir producción.
Disponibilidad de SMTP y sus condiciones se verifica: software libre no elimina la
dependencia de proveedor de correo/red. Credenciales de monitor separadas de OAuth Drive.

Operador ve checks y alertas; empleados no acceden a paneles ni Beszel. No montar socket
Docker en Healthchecks. Revisar privilegios del agente Beszel antes de instalarlo en VPS,
incluido cualquier acceso de estadísticas de contenedores; no presentarlo como inocuo.

Medir consumo antes/después y durante un día de operación. Presupuesto inicial de aceptación
incremental para Healthchecks y su DB: 512 MiB de RAM y CPU media inferior al 5% de un núcleo
en reposo; son objetivos de ensayo, no cifras garantizadas. Retención de eventos de ping
30 días, logs rotados, alertas de disco y revisión mensual de versiones. Si no cumple,
ajustar configuración o propuesta antes de instalar en producción; no ampliar recursos
ni contratar servicio automáticamente. Beszel existente se mide por separado.

Respaldar DB/configuración del monitor según política de home-server, con secretos en
custodia separada. Exportar definiciones y restaurarlas sin revelar ping URLs. Un monitor
reconstruido no declara checks saludables hasta recibir o recuperar evidencia válida.

## 5. Riesgos aceptados y aceptación

Caída de Oracle se detecta desde home-server. Caída simultánea del monitor, su internet
o energía impide alertar; batería reduce ese riesgo sin eliminarlo. No prometer monitoreo
independiente del hogar. La necesidad de detectar caída del propio home-server exigiría
otro observador externo y una decisión posterior.

Aceptar cuando se detectan fallo y silencio, email llega, estado persiste tras reinicio,
VPS no ve panel, no hay exposición pública, consumo se mide y configuración de home-server
está versionada. Backups y producción dependen de esta evidencia, no solo de un dashboard.

Fuentes: [Beszel](https://github.com/henrygd/beszel),
[Healthchecks](https://github.com/healthchecks/healthchecks).
