# POLITICAS-OPERACION.md — Decisiones operativas para la primera producción

> **Aprobación registrada el 2026-09-16:** respaldo, recuperación, retención y custodia
> aceptados por el propietario en [ADR-0034](./adr/0034-backups-cifrados-drive.md).
> Posteriormente el propietario aprobó también ADR-0033 y ADR-0035, así como la selección
> de Healthchecks autohospedado mediante ADR-0036, que sustituye el servicio alojado.
> Los cuatro ADRs están aceptados; ninguna instalación
> se declara realizada. Cuenta de backups y nombre de carpeta confirmados; folder ID, acceso OAuth, custodia
> de firma y fuente de datos requieren verificación según PREFLIGHT-PRODUCCION.

> Diseño del 2026-09-16. Política seleccionada para implementar; no acredita instalación,
> compra, alta de servicios externos ni pruebas ejecutadas. Complementa specs
> 0012, 0013 y 0014. Las decisiones estructurales requieren ADR aprobado antes del código.

## Alojamiento y propiedad

Producción: VPS Oracle Ubuntu 24.04 ARM64 bajo cuenta del propietario. Staging: servidor
doméstico. GitHub Actions alojado construye imágenes/APKs; GHCR aloja imágenes por digest.
No se contrata dominio: HTTPS privado mediante Caddy, tailscale cert y nombre ts.net. La API
y web no se publican mediante Funnel. Revisar cuota/condiciones Oracle antes del arranque;
no asumir que los recursos observados garantizan gratuidad indefinida.

El propietario mantiene OCI, Google Drive, GitHub, Tailscale y firma Android. Empleados
tienen cuentas individuales de aplicación y acceso de red limitado, nunca cuenta del dueño.
Registrar inventario privado de titulares, recuperación y facturación, sin correos en git.
Verificar condiciones del plan Tailscale para uso de la finca y número de usuarios; no
suponer que un plan personal cubre uso empresarial. Si exige pago, elección del propietario
antes del alta, no sustituir identidades individuales por cuenta compartida.

## Credenciales

### HTTPS reutilizando el patrón de home-server

Referencia inspeccionada: `home-server/stacks/caddy/Caddyfile` y
`home-server/scripts/renovar-certificados.sh`. Oracle recibe su propio certificado y
clave; nunca copiar server.key de staging. Caddy publica 443 ligado solo a IP tailnet.
Un solo origen sirve web y /api; staging conserva su direccionamiento existente.

Instalar Tailscale con repositorio oficial para Ubuntu 24.04 y verificar paquete/firma;
autenticar interactivamente como administrador, sin registrar URL de autorización ni
auth key en git. Obtener nombre con tailscale status, emitir certificado para ese nombre,
validar pares y expiración antes de instalarlos. Timer semanal root-owned genera archivos
temporales, instala atómicamente con clave 640 al grupo mínimo del proxy, y valida Caddy.
Recargar mediante API administrativa sobre socket Unix protegido, sin exponerla a red;
el Caddyfile debe habilitar ese socket, no `admin off`. Comprobar certificado servido
después de recarga y alertar con menos de 21 días de validez. Fallo conserva par anterior.
Prueba `curl` sin `-k`; reinicio con Tailscale aún ausente debe reintentar de forma acotada
sin cambiar binding a 0.0.0.0. Probar renovación real y rotación en entorno aislado.

Antes de cerrar SSH público: dos sesiones privadas independientes y consola OCI probada.
Firewall del host y reglas Oracle no publican 443; binding Docker inspeccionado en ambas
familias IP. Instalar Tailscale en teléfonos según GUIA-CAMPO-ANDROID y verificar permisos.

Custodia primaria: gestor de contraseñas del propietario; se usa el existente, sin imponer
producto nuevo. Recuperación: exportación cifrada fuera de Oracle y Google Drive, disponible
offline en soporte físico bajo control del propietario. Probar apertura semestralmente.
MFA obligatorio en cuentas administrativas; códigos de recuperación fuera del teléfono.

GitHub Environments contienen SSH CI/OAuth Tailscale y firma Android por canal. Claves DB y
JWT en /etc/hato-production (root, 600); token Drive/crypt accesible solo por hato-backup;
GitHub App privada solo por worker Delivery. Keystore y passwords Android tienen copia
de recuperación independiente. No rotar firma Android rutinariamente: preservarla para
actualizaciones. Revisar accesos trimestralmente y revocar de inmediato ante pérdida,
salida de empleado o exposición. Rotación DB/JWT/OAuth ensayada en staging; JWT puede
invalidar sesiones pero no debe eliminar outbox. Registrar cambios sin valores secretos.

## Privilegios del despliegue

El operador crea hato-deploy conforme al spec 0012; no está creado aún. Sin grupo docker,
sudo general ni shell libre. Helper root-owned invocable por SSH forzado. La aprobación
no se demuestra con un booleano enviado por CI: el helper consulta GitHub por run ID y
comprueba workflow/ref/SHA/entorno y registro de aprobación usando credencial propia de
lectura. GitHub inaccesible o evidencia incompleta: rechazar. Manifiesto debe coincidir
con artefacto de ese run, tener nombres/repositorios allowlist y digests exactos.
Vincular run/attempt al despliegue para impedir replay; rollback autorizado crea run nuevo.
Si la API disponible no permite comprobar aprobación de forma inequívoca, detener el
desarrollo de ese camino y definir atestación firmada en ADR; no degradar a confiar en stdin.

## Backups y alertas

Monitorización registrada como [feature-0015](./spec/feature-0015-self-hosted-monitoring/spec.md),
con [ADR-0036](./adr/0036-monitorizacion-autohospedada.md) aceptado. Home-server aloja
los monitores; HATO mantiene contrato de pings y compuerta de respaldo comprobado.

**Destino de backups:** carpeta Drive `backups-hato-erp`, en la **cuenta de backups**,
que también será la identidad de contacto para esta configuración. Esta denominación
identifica su función y no implica crear otra cuenta. El correo no se copia al repositorio
público. Falta resolver el folder
ID por OAuth y verificar propiedad/espacio; no se ha creado ni conectado la carpeta.

**Monitor seleccionado:** Beszel existente y Healthchecks autohospedado en home-server,
conforme ADR-0036 aceptado. No se contrata Healthchecks.io. Plan concreto en
`home-server/docs/spec/hato-monitoring/`; consumo y envío de email se prueban antes de apertura.

### Límite de almacenamiento aprobado

El propietario aprobó un máximo de **300 GB decimales (300 000 000 000 bytes)** para la
carpeta dedicada de Drive y sus subcarpetas, incluidos dumps, manifiestos cifrados, copias
mensuales, APKs y adjuntos respaldados. Es un límite operativo de la automatización,
no una cuota nativa de carpeta ni una reserva garantizada en la cuenta Google.
Aviso preventivo a **210 GB (70 %)** y alerta urgente a **255 GB (85 %)**; los 45 GB
restantes son margen operativo, no almacenamiento adicional al límite.

Antes de cada subida comprobar consumo, tamaño cifrado estimado con margen y espacio real
disponible en la cuenta. Considerar objetos de ejecuciones incompletas y papelera imputable
al respaldo al estimar uso; vigilar también cuota global, que puede agotarse por otros
archivos. Serializar admisión de uploads de backups, monitorización y APKs para evitar sobrepasar el límite
por concurrencia. Si no cabe, fallar con alerta, conservar copia local y no avanzar éxito.

Un único uploader bajo hato-backup en Oracle serializa todos los namespaces, incluido
monitoring/. Home-server entrega dump de su monitor por un canal de ingreso restringido,
sin token de escritura Drive independiente. Caída de Oracle deja cola local conservada.
No reducir retención automáticamente ni borrar la última copia válida o APKs protegidas.
El límite se revisa con tamaños medidos; aumentarlo requiere decisión del propietario.

Se selecciona retención: 7 días locales, 30 días diarios offsite y 12 copias mensuales.
Mantener siempre última copia válida; no podar ante upload fallido. RPO objetivo 24 horas,
RTO objetivo 4 horas verificado en simulacro. Timer diario 07:00 UTC y restore mensual.

Acceso DB seleccionado: helper root-owned que ejecuta dump en contenedor PostgreSQL con
rol de lectura y entrega solo bytes por stdout. hato-backup no tiene Docker ni password
de superusuario; helper sin argumentos de contenedor/base arbitrarios, sin eco de secretos.
Esto evita publicar un puerto adicional de PostgreSQL. Probar lectura de todas las tablas
y secuencias y ausencia de permisos de escritura/DDL.

Healthchecks autohospedado envía notificaciones por email a la cuenta de backups. Dos checks externos: respaldo diario (24 horas + 2 de gracia) y
restore mensual (35 días máximo). Ping éxito solo tras verificación; /fail ante fallo,
sin datos de finca en payload. URL de ping es secreto. El sitio conoce tiempos y estado,
no dumps ni nombres de personas. Probar email entregado y silencio del host antes de abrir.
Alta y entrega SMTP se verifican operativamente; ADR-0036 ya aceptado, servicio aún no instalado.

## Releases y compatibilidad

Conservar APK stage últimas 10 y hasta 30 días; prod últimas 5 y hasta 180 días, con
excepciones actual, última buena e investigación. Metadatos/auditoría no se borran.
Aplicación actual y anterior estable soportadas al menos 90 días desde publicación de
su sucesora; ampliar soporte si quedan dispositivos activos sin conciliar. Inventario
de versión reportada al sincronizar, sin usar falta de conexión como prueba de abandono.

API conserva comandos/payloads y semántica idempotente de versiones soportadas. Cambios
aditivos primero; migraciones expand-contract, retirar campos solo tras conciliación.
Header de versión informativo no autentica dispositivo. Negociación de capacidades antes
de sync: cliente incompatible conserva registros y muestra actualización necesaria, sin
descartar operaciones ni marcar éxitos falsos. Seguir permitiendo captura local.
Incidente de seguridad puede cortar acceso online, con recuperación manual de pendientes
por operador; no forzar desinstalación. Probar app anterior contra API nueva y viceversa
para el intervalo explícitamente soportado. Ningún rollback SQL automático.

## Valores pendientes de inventario, no decisiones de arquitectura

Registrar privadamente: hostname ts.net efectivo, responsable y destinatario de alertas,
folder ID Google, cuenta/plan contratado, huellas SSH, package/firma/versionCode del piloto,
servidor fuente de datos y teléfonos pendientes. Las tareas de preflight obtienen estos
valores; nunca reemplazarlos por credenciales ficticias en un despliegue real.

## Estado documental y compuertas

| Frente | Decisión | Estado documental | Evidencia que falta antes de operar |
|---|---|---|---|
| Producción Oracle privada | ADR-0033 | Aceptada | `hato-deploy`, Tailscale/Caddy, stack y release probados |
| Backups Drive | ADR-0034 | Aceptada | OAuth/carpeta verificados, `hato-backup`, primer dump y restore real |
| APKs stage/prod | ADR-0035 | Aceptada | Custodia de firma, pipeline, descarga y actualización Android probada |
| Monitor autohospedado | ADR-0036 | Aceptada | Stack home-server, email, ausencia de heartbeat y consumo medidos |

Los valores del inventario no reabren estas decisiones salvo que la evidencia contradiga
una premisa esencial. Ejemplos: no localizar la firma instalada obliga a diseñar migración,
pero no autoriza a desinstalar; descubrir varias bases reales obliga a conciliarlas, no a
elegir por fecha. Cambiar proveedor, retención, exposición pública o autoridad de usuarios
sí exige revisar el ADR correspondiente.

El diseño documental se considera listo para implementar por paquetes en este orden:
0012 bootstrap y acceso restringido; 0013 backup/restore junto con 0015 monitorización;
corte de producción 0012; 0014 distribución móvil. Cada paquete cierra sus propias tareas
y E2E. Ninguna aprobación documental equivale a completar una tarea operativa.

Fuentes: [Healthchecks](https://healthchecks.io/docs/),
[actualizaciones Android](https://developer.android.com/google/play/app-updates).
