# spec.md — Compilación y descarga privada de APKs Android

> **Diseño aprobado:** [ADR-0035](../../adr/0035-distribucion-android-privada.md),
> aceptación del propietario registrada el 2026-09-16. Sustituye el estado propuesto
> de esta redacción; inventario de firma y ejecución pendientes. No hay builds publicadas.

> Paquete completo: [tareas](./tasks.md) y [E2E](./test-e2e.md).
> Aplican [políticas](../../POLITICAS-OPERACION.md) y [preflight](../../PREFLIGHT-PRODUCCION.md).

> Diseño aprobado 2026-09-16. [Plan](./plan.md). Implementación pendiente. Depende de [producción](../feature-0012-production-environment/spec.md)
> para el canal estable; su catálogo se respalda según feature-0013.

## 1. Objetivo y estado actual

Desde una sección administrativa de la web, solicitar builds Android, seguir estado y
descargar la variante correcta. El propietario pide dos APKs y un historial acotado.
`clients/field-app/eas.json` ya tiene preview y production, pero comparten API HTTP;
production produce AAB. `app.json` define un solo package Android. `src/App.tsx` obtiene
EXPO_PUBLIC_API_URL en build time. La app llama la API, nunca PostgreSQL directamente.

## 2. Canales y reglas de estabilidad

| Canal | Fuente permitida | Paquete propuesto | Nombre visible | Backend |
|---|---|---|---|---|
| stage | SHA validado de develop | com.joemandev.hatofieldapp.stage | HATO Campo STAGE | API staging |
| prod | release etiquetada aprobada de main | com.joemandev.hatofieldapp | HATO Campo | API producción |

Los packages distintos permiten instalación simultánea y separan SQLite, tokens y outbox.
Confirmar antes de fijar package prod cuál está instalado realmente en campo y su firma;
preservarlos para actualización sin pérdida. Icono y banner diferencian stage incluso
offline. API URL es configuración pública del binario, nunca secreto ni contraseña DB.
Proponer HTTPS ts.net; selección de URLs solo por configuración administrativa revisada,
no texto arbitrario enviado por navegador.

Un merge a main no certifica una app estable. Estados separados: solicitada, en cola,
compilando, verificando, candidata, publicada, fallida, retirada. Stable exige CI verde,
firma y contrato de API verificados, prueba Android física y aprobación humana. Main
con release genera candidata prod; develop genera candidata stage. Otras ramas ejecutan
CI de desarrollo sin firma ni acceso al canal prod. No publicar binarios de forks.

Botón «Generar ambas»: congela dos SHAs, develop para stage y última release elegible de
main para prod. Crea dos solicitudes independientes agrupadas y muestra resultado parcial
si una falla. No recompila develop apuntándolo a DB real. Si falta release/backend prod
compatible, muestra prod bloqueado con razón y permite stage. Repetición idempotente no
genera builds duplicadas; reconstrucción deliberada recibe ID/versionCode nuevos.

## 3. Web y contrato backend

Sección «Aplicaciones Android»: versión instalada sugerida por canal, estado, fecha,
commit, tamaño, SHA256, notas, compatibilidad y descarga; filtros por canal y estado.
Administrador puede solicitar stage/prod/ambas, consultar cola, publicar o retirar; usuario
autorizado descarga publicadas. Configuración limitada a canales habilitados y políticas
aprobadas; nunca permite editar YAML, código, comandos, tokens ni URLs de red arbitrarias.

Propuesta de endpoints versionados, Problem Details y paginación:

- `POST /api/v1/mobile-build-requests`: canal stage/prod/both y release elegible; idempotency key.
- `GET /api/v1/mobile-build-requests/{id}`: estado por canal, errores redactados.
- `GET /api/v1/mobile-releases`: catálogo autorizado.
- `POST /api/v1/mobile-releases/{id}/publish` y `/withdraw`: transición auditada.
- `GET /api/v1/mobile-releases/{id}/download`: streaming privado con autorización.

Agregar términos al GLOSSARY antes del código: solicitud de compilación (`MobileBuildRequest`),
release móvil (`MobileRelease`), canal móvil (`MobileReleaseChannel`). Módulo operativo
`Delivery` bajo `src/Modules/Delivery/`, con Domain/Application/Infrastructure/Api según
convención real verificada al implementar; ADR previo por nuevo límite modular. Migraciones
EF Core para solicitudes, intentos, transiciones auditadas, release/artifacts y configuración.
Campos mínimos: UUID, canal, SHA, tag, versión, versionCode, package, API objetivo identificada,
firma fingerprint, artifact SHA256/bytes, workflow run/attempt, creador, fechas UTC y estado.
Tokens, llaves y contraseñas no se guardan en estas tablas.

Permisos configurados en People: `delivery.builds.manage`, `delivery.releases.publish`,
`delivery.releases.download`; stage además requiere manage, evitando entregarlo a empleados.
Toda operación protegida también fuera de la UI. Publicar exige rol humano autorizado;
callback de CI nunca publica estable por sí solo. Sin download anónimo ni JWT en querystring.
Soportar descargas grandes con streaming/range y límites; prueba real desde navegador Android.

## 4. Orquestación y secretos

Propuesta: GitHub Actions alojado ejecuta EAS Build local para Android en runner efímero,
con versiones fijadas y dependencias cacheadas sin secretos. Justificar y aprobar EAS CLI,
GitHub App y módulo Delivery en ADR antes de incorporar dependencias. Compilar fuera de
VPS; probar límite de tiempo y memoria en build nativa WatermelonDB antes de cerrar elección.

Web → API autenticada → solicitud persistida → worker de entrega → GitHub workflow.
Worker emite token de instalación de GitHub App restringida al repo, Actions write y
Contents read; clave privada montada solo en worker, nunca API web ni navegador. Permiso
Actions write es amplio dentro del repo: wrapper permite únicamente workflow/ref/input
allowlist; no afirmar que GitHub restringe el token a un único YAML. Sin PAT personal.
API no tiene socket Docker ni claves de deploy. Cola en PostgreSQL y worker recuperable,
leases y unique constraints evitan duplicados tras reinicio. GitHub Environments separan
secretos stage/prod; approval prod sigue vigente aunque la solicitud venga de la web.

Sin webhook público. Worker consulta estado de runs y descarga artifacts autenticados
usando run ID ligado a solicitud, SHA, workflow y attempt; no acepta URLs suministradas
por clientes. Verifica manifest y APK antes de importar a staging temporal y renombrar
atómicamente. Reintentos y timeout terminan en error visible; nunca presentan éxito por
el simple dispatch. Cuotas iniciales: una build por canal simultánea, diez solicitudes
diarias por administrador y cola máxima veinte; ajustables con auditoría.

Keystores stage/prod diferentes, persistentes, custodiados por propietario con backup
seguro externo. Production debe reutilizar firma ya instalada si existe. Secretos en
entornos GitHub correspondientes: archivo keystore codificado (codificar no cifra), alias,
passwords y credenciales EAS únicamente si el camino elegido las requiere. Materializar
en directorio temporal protegido, nunca cachear ni subir como artifact; limpiar al terminar
incluso con fallo. Logs sin secretos. Tener copia de keystore y passwords recuperable antes
de primera publicación; perder firma puede impedir actualizaciones del mismo package.

## 5. Versionado, historial y almacenamiento

Nombre: `hato-1.2.3-stage-b1042-a1b2c3d.apk` o
`hato-1.2.3-prod-b1043-d4e5f6a.apk`. VERSION es fuente semántica; SHA es identidad de código.
versionCode entero monotónico por package, reservado transaccionalmente antes de construir,
no fecha ambigua ni run_number de workflows diferentes. Fallos dejan huecos, nunca reutilizan
códigos. Rebuild genera nuevo código. Verificar package, versionName, versionCode,
certificado y URL esperada inspeccionando APK/manifiesto antes de publicar.

La APK publicada es inmutable por hash. «Actual estable» es un puntero auditado, no un
archivo sobrescrito. Propuesta: stage conserva últimas 10 APKs exitosas con máximo 30 días;
prod últimas 5 estables con máximo 180 días. Excepciones obligatorias: actual estable,
última buena compatible y releases marcadas para investigación. Conservar metadatos y
auditoría aunque se pode binario; ausencia visible. Retención configurable limitada y
dry-run antes de poda. No almacenar APKs en git ni Releases públicas.

Artifacts de CI son transporte temporal durante 7 días; biblioteca privada en
`/srv/hato-production/mobile-artifacts`, fuera del directorio web, con presupuesto inicial
5 GiB y alertas. Para recuperación, conservar copia cifrada de binarios retenidos en
namespace Drive independiente con su propia política, implementada en esta feature usando
el transporte de feature-0013; no mezclar retención APK con dumps. Si falta ese archivo,
no prometer reproducción byte a byte: reconstrucción es nueva build. No eliminar los
protegidos para liberar espacio; bloquear importación y alertar si se alcanza presupuesto.

## 6. Android y transición de dispositivos

APKs release firmadas, sin Metro, dev client ni debug. Perfil production Android debe
usar buildType apk durante piloto; Play Store/AAB e iOS fuera de alcance. Instalar requiere
autorizar la fuente del navegador en Android; indicar origen y hash en la web, sin pedir
desactivar protecciones generales. Actualización normal conserva datos si package/firma
coinciden y migraciones SQLite son compatibles. No ofrecer downgrade mediante reinstalación.
Rollback funcional es nueva build con código mayor y migraciones compatibles, no instalar
APK antigua borrando la aplicación. Outbox pendiente nunca se sacrifica por cambiar canal.

Antes del primer cambio de backend: inventariar package/firma/versionCode y pendientes de
teléfonos del piloto; sincronizar y conciliar fuente según feature-0012. Si una build anterior
apuntaba a datos reales aunque se llamara preview, no considerarla descartable. Bloquear
apertura de canal nuevo hasta decidir migración preservando UUID e historia.

## 7. Aceptación y referencias

La copia externa de APKs comparte el máximo de **300 GB decimales** de la carpeta Drive
con backups y adjuntos (POLITICAS-OPERACION.md). No dispone de 300 GB adicionales.
Su upload utiliza el mismo control serializado de admisión y alertas a 210/255 GB;
el presupuesto local de 5 GiB permanece independiente.

Diseño se implementa cuando: dos APKs coexisten con DB local separada; cada URL y firma
son correctas; build prod usa release de main aprobada; peticiones duplicadas no duplican
trabajo; ninguna credencial termina en bundle/log; fallo de un canal muestra estado parcial;
descarga no autorizada falla; metadatos sobreviven poda; actualización conserva outbox;
actual estable protegida y restauración del catálogo/artefacto comprobada.

[Expo variantes](https://docs.expo.dev/build-reference/variants/),
[APK](https://docs.expo.dev/build-reference/apk/),
[versionado](https://docs.expo.dev/build-reference/app-versions/).
