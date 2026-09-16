# spec.md — Backups de PostgreSQL cifrados en Google Drive

> **Monitorización separada:** [feature-0015](../feature-0015-self-hosted-monitoring/spec.md)
> define alojamiento y alertas autohospedadas. Este spec conserva el contrato de señales
> y plazos. La contratación del monitor SaaS queda suspendida; ADR-0036 aceptado sustituye
> esa parte de ADR-0034. No se modifica la retención aprobada.

> **Política aprobada el 2026-09-16:** [ADR-0034](../../adr/0034-backups-cifrados-drive.md).
> La aprobación sustituye el carácter propuesto de la retención y decisiones de backup
> en este documento. Implementación y comprobación operativa siguen pendientes.

> Paquete completo: [tareas](./tasks.md) y [E2E](./test-e2e.md).
> Aplican [políticas](../../POLITICAS-OPERACION.md) y [preflight](../../PREFLIGHT-PRODUCCION.md).

> Aprobado 2026-09-16, transversal. [Plan](./plan.md). Implementación pendiente. Complementa [producción](../feature-0012-production-environment/spec.md).

## 1. Evidencia y objetivo

Constitución Art. 2 exige respaldo diario offsite y restauración mensual. `scripts/backup.sh`
genera SQL gzip y SHA256 con retención local de 30 días, sin cifrado ni offsite.
`scripts/restore.sh` permite omitir checksum, usa dump con CREATE/CONNECT a la base original
y psql sin ON_ERROR_STOP; el conteo puede convertirse en cero y presentarse como éxito.
Estos caminos deben corregirse antes de restaurar cerca de datos reales.

Objetivo: recuperar producción tras pérdida completa de la VPS usando una copia descargada
de Drive y credenciales custodiadas fuera de Oracle. Google Drive es una copia externa,
no almacenamiento WORM. Un atacante con token de escritura puede borrar copias: el cifrado
y los hashes no impiden ese borrado ni prueban autenticidad si también controla las claves.

## 2. Decisiones propuestas

- D1. Backup completo PostgreSQL diario a 07:00 UTC (02:00 Ecuador), timer systemd
  persistente, y adicional antes de migraciones. RPO objetivo 24 horas si la última copia
  es válida; una falla puede ampliarlo y debe alertarse. RTO objetivo inicial 4 horas,
  medido en restauración completa, no prometido antes del simulacro.
- D2. `pg_dump -Fc --no-owner --no-acl` de toda la DB con cliente PostgreSQL 16 compatible.
  Sin copiar pgdata en caliente. Roles/permisos se reconstruyen desde configuración
  versionada y secretos recuperados; inventariar extensiones y todas las bases necesarias.
- D3. rclone con OAuth de usuario hacia carpeta dedicada de Drive y remote crypt que cifra
  contenido y nombres. Dependencia operativa nueva: ADR aprobado antes de instalar.
  Token con menor scope viable; `drive.file` debe probar creación/listado/restauración
  entre equipos antes de adoptarlo. Root folder ID limita destino operativo, no convierte
  por sí solo un token de scope amplio en acceso restringido.
- D4. Retención aprobada: local 7 días, Drive diarios 30 días y una copia mensual 12
  meses, conforme aprobación registrada en ADR-0034. Nunca borrar la última copia
  verificada por edad. Historial de eventos de negocio no se elimina al rotar backups.
- D5. Subida unidireccional con copy, sin sync, mirror ni purge sobre Drive. Rotación por
  manifiesto de IDs propios y prefijo exclusivo; simulación inicial y eliminación acotada
  con papelera cuando sea viable. Cuota de papelera también cuenta en la planificación.
- D6. Descargar y restaurar mensualmente en PostgreSQL aislado. Prueba inicial obligatoria
  antes de registrar datos reales; hash o `pg_restore --list` solos no son restauración.

## 3. Cuenta, claves y recuperación

Drive y OAuth de la cuenta de backups, bajo control del propietario con MFA y recuperación. Carpeta sin enlaces
públicos. Autorizar interactivamente desde equipo confiable; no compartir contraseña
Google con el servidor. Revisar estado de consentimiento OAuth: tokens de aplicaciones
en modo Testing pueden caducar; validar operación desatendida sostenida antes de apertura.

Configuración rclone, refresh token y claves crypt en archivo 600 accesible únicamente a
`hato-backup`; ningún secreto en argumentos, GitHub logs, git, Docker layers o EXPO_PUBLIC.
Obscure de rclone no equivale a cifrado fuerte del archivo de configuración. Custodiar
clave/salt y procedimiento de reautorización en gestor seguro y copia de recuperación
separada. No guardar la única clave de descifrado dentro del backup cifrado.

Crear usuario de servicio `hato-backup`, sin sudo ni socket Docker, con credencial DB
dedicada de lectura. Acceso a PostgreSQL por conexión local protegida y pgpass 600; la
infraestructura expone, si es imprescindible, un binding de loopback específico, nunca
0.0.0.0, y lo documenta en producción. Alternativamente helper de dump root-owned con
salida únicamente; elegir y probar una ruta en ADR antes de implementar.
No entregar tokens Drive al usuario hato-deploy ni a la API. Rotación de claves mantiene
las anteriores hasta que todas las copias dependientes caduquen y la nueva restaure.

## 4. Contrato de ejecución

### Convención de nombres y namespaces

Raíz Drive lógica (vista descifrada): `backups-hato-erp/`. Subcarpetas `database/prod/daily/`,
`database/prod/monthly/`, `database/prod/pre-release/`, `mobile/stage/`, `mobile/prod/` y
`monitoring/daily/`. Todos consumen los mismos 300 GB mediante un uploader serializado.
Los nombres físicos en Drive serán opacos por crypt; no buscar nombres lógicos en Drive web.

Dump: `hato-prod-db-20260916T070000Z-<uuid>.dump`. Compañeros con el mismo stem:
`.manifest.json`, `.sha256` y `.complete`. UUID generado por ejecución, UTC sin espacios
ni caracteres de shell; sin personas, animales, correos o contraseñas en nombres.
Backup monitor: `hato-monitor-db-20260916T070000Z-<uuid>.dump`. La copia mensual conserva
ID del backup diario seleccionado pero vive en monthly; contabilizar bytes duplicados.
Pre-release: misma convención bajo pre-release, tag/SHA en manifiesto. Retención 30 días
para pre-release, salvo última copia válida o incidente retenido; no acumulación ilimitada.
APKs conservan nombres de feature-0014, con versión/canal/build/SHA.

`.partial` nunca se publica como completo; `.complete` solo tras verificación. Retención
consulta manifiesto validado e ID, no decide por nombre ni por fecha modificable en Drive.
No adoptar los `<uuid>` de ejemplos literalmente: son campos generados, no config pendiente.

Cada ejecución toma lock exclusivo, verifica destino y espacio (mínimo dos veces el dump
anterior más margen operativo de 2 GiB), conexión, versiones y credenciales. Genera un ID
UTC más UUID, dump en archivo .partial modo 600, y verifica finalización de pg_dump antes
de renombrar. Nunca presentar un parcial como válido. Primer dimensionamiento con dump
medido; límites y alertas no se basan solo en un porcentaje de disco.

Manifiesto versionado: ID, entorno, UTC, DB lógica, versión PostgreSQL, release/SHA, bytes,
SHA256 con nombre relativo, estado de verificación y versión de clave sin material secreto.
Se cifra junto al dump. Subir objetos bajo namespace exclusivo y publicar marcador complete
solo después de verificar remoto mediante cryptcheck y/o descarga descifrada + SHA256.
Archivo existente con mismo ID y hash distinto produce error; nunca sobreescribir.

Reintentos acotados con backoff, recuperación de fallos de red y exclusión entre backup,
rotación y mantenimiento. Upload fallido conserva copia local, no avanza last-success y
no permite rotación remota. Rotar solo conjuntos completos comprobados y mantener al menos
una copia mensual restaurada mientras su política siga vigente. Dump fallido, corrupción,
token vencido, cuota llena y falta de espacio terminan con código no cero.

Timer `hato-backup.timer`: diario, Persistent=true y jitter acotado; timeout del servicio
evita procesos infinitos. `hato-restore-check.timer`: mensual en host de verificación
separado; si usa servidor doméstico, recursos aislados de staging y sin acceso de apps.
El host de restauración tiene acceso a datos sensibles y requiere la misma custodia.
Si está apagado, una alerta externa detecta verificación vencida.

## 5. Restauración y monitorización

Descargar desde Drive en entorno limpio con configuración recuperada del gestor, verificar
hash obligatorio y descifrar. Restaurar con `pg_restore --exit-on-error --no-owner --no-acl`
en base nueva y servidor aislado. Reconstruir roles, aplicar grants previstos, verificar
migraciones por módulo, integridad referencial, UUID/eventos, usuarios, inventario y sync;
comparar conteos contra manifiesto obtenido de snapshot consistente o invariantes conocidas,
no contra conteos de otra hora en producción. Ejecutar smoke de API sobre esa restauración.
Nunca permitir que el comando acepte por defecto el host/base de producción; guardas de
destino y confirmación explícita para una recuperación real. No ejecutar DROP DATABASE
ni borrar volúmenes del entorno original como parte de la verificación.

Reporte: ID restaurado, bytes, tiempos descarga/restore/smoke, RPO/RTO observados, resultado
y versión. Excluir filas personales y credenciales. Retención propuesta de reportes 12 meses.
Avisar al propietario en cada fallo y si no hay copia offsite válida en 26 horas o restore
en 35 días. Canal: Healthchecks autohospedado en home-server y email a la cuenta de backups,
según ADR-0036. Destinatario y credencial se configuran privadamente antes de activar. Probar caída completa de VPS: un timer local no puede avisar
cuando su host dejó de existir. Alertar cuota Drive y disco antes de agotarse.

## 6. Aceptación, alcance y fuentes

Límite aprobado de carpeta Drive: **300 GB decimales**, compartido con APKs/adjuntos,
alertas a 210/255 GB. Aplicar la política de almacenamiento de POLITICAS-OPERACION.md.
La admisión de uploads contempla concurrencia, parciales, papelera y cuota global; probar
rechazo de una subida que exceda límite sin alterar última copia válida ni last-success.

Exigir restauración real descargada, tokens renovables, recuperación sin VPS original,
alerta de backup ausente, upload fallido sin poda, rechazo de .partial y hash inválido,
retención probada con tiempo simulado y protección del último backup. Estimar almacenamiento
con tamaño medido de copias, crecimiento, papelera y cupo disponible de cuenta Google.

Incluye DB completa, metadatos de release y receta para reconstruir roles; inventariar
archivos subidos por usuarios y añadirlos a la unidad recuperable si existen antes de
declarar cobertura completa. Excluye APKs, outbox no sincronizado y backup de sistema
operativo. PITR/WAL y WORM quedan para ADR posterior si 24 horas de pérdida son inaceptables.

[PostgreSQL backup](https://www.postgresql.org/docs/16/backup-dump.html),
[rclone Drive](https://rclone.org/drive/), [crypt](https://rclone.org/crypt/),
[cryptcheck](https://rclone.org/commands/rclone_cryptcheck/).
