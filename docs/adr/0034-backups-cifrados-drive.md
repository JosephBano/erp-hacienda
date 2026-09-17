# ADR-0034 — Backups cifrados Drive con verificación externa

- **Estado:** Aceptado.
- **Fecha:** 2026-09-16.
- **Fase:** Transversal.

## Registro de aprobación

El propietario aprobó la política presentada en esta conversación el 2026-09-16:
«correcto apruebo esto que falta», y solicitó registrar esa aprobación:
«registra mi aprobacion entonces, documenta todo lo adr».
La aprobación corresponde a respaldo, recuperación, retención y custodia descritos aquí.
No acredita instalación, creación de cuentas/usuarios, contratación ni restauración ejecutada.
No enmienda la Constitución: cumple su Art. 2.

## Contexto

Art. 2 exige backup diario offsite y restore mensual. Scripts actuales no suben fuera
del host ni prueban recuperación con errores cerrados. El propietario dispone de Drive.

## Decisión

pg_dump custom de DB completa; rclone crypt y OAuth de la cuenta de backups, carpeta dedicada.
Usar un cliente OAuth propio de rclone bajo un proyecto Google controlado por la cuenta de
backups, para aislar cuota y revocación del cliente compartido. La aplicación es privada de
uso operativo, sin usuarios públicos; antes de automatizar se verifica que el consentimiento
no permanezca en un estado de prueba que haga caducar prematuramente el refresh token.
Timer diario UTC; siete días locales, treinta diarios externos y doce mensuales con
última válida protegida. Restore mensual aislado; RPO 24 h y RTO 4 h sujetos a medición.
Beszel y Healthchecks open source autohospedado en home-server, conforme ADR-0036,
con email para ausencia de backup/restore y URLs de ping secretas.
Helper de dump limitado evita acceso Docker al servicio backup. Claves recuperables
fuera de Oracle y fuera del conjunto cifrado. Ver
[spec 0013](../spec/feature-0013-drive-backups/spec.md),
[plan](../spec/feature-0013-drive-backups/plan.md),
[tareas](../spec/feature-0013-drive-backups/tasks.md),
[verificación E2E](../spec/feature-0013-drive-backups/test-e2e.md) y
[políticas](../POLITICAS-OPERACION.md).

### Cobertura y retención aprobadas

| Activo | Frecuencia | Conservación |
|---|---|---|
| PostgreSQL completo, incluidos usuarios, permisos, eventos y catálogo de releases | Diario 07:00 UTC (02:00 Ecuador), adicional antes de migraciones | 7 días locales, 30 días diarios Drive y 12 copias mensuales |
| APKs stage publicadas | Cada publicación | Últimas 10 con máximo 30 días |
| APKs prod publicadas | Cada publicación | Últimas 5 con máximo 180 días |
| Configuración sin secretos | Cada cambio revisado | Historial Git |
| Credenciales y firma Android | Al crear/cambiar | Gestor del propietario y copia cifrada offline fuera de Oracle y Drive |

La política APK conserva excepciones: actual estable, última buena compatible e investigación.
La copia externa de binarios sigue la misma retención que la biblioteca; el historial de
metadatos permanece. Su implementación pertenece a feature-0014: aprobar esta retención
no aprueba automáticamente el diseño completo de su pipeline.

Dump custom comprimido; contenido y nombres cifrados con rclone crypt. Nunca sync
bidireccional. Verificación de integridad por ejecución y restore mensual desde descarga
externa en PostgreSQL limpio. Preservar última copia válida y no podar remoto ante fallo
de subida/verificación. Poda limitada a IDs propios; no destruir datos históricos de negocio.

Alertar al fallar y tras 26 horas sin backup válido o 35 días sin restore comprobado.
RPO se refiere a datos sincronizados; outbox de teléfonos queda fuera. Inventariar adjuntos
y respaldarlos con sus referencias antes de declarar cobertura completa. Drive no es WORM;
el token de escritura puede permitir borrado. RTO de 4 horas requiere medición real.

### Estado de ejecución

**Precisión aprobada por el propietario:** máximo operativo de 300 GB decimales para
toda la carpeta Drive dedicada (backups y APKs), aviso a 210 GB y alerta a 255 GB.
La solicitud «REgistralo como limite entonces» confirma este límite. No equivale a cuota
de carpeta impuesta por Google. Aplicar control de admisión y cuota de cuenta según
[políticas operativas](../POLITICAS-OPERACION.md); no modificar retención para hacerlo caber.

Pendientes: carpeta/OAuth de Drive, custodia recuperable de claves, usuario hato-backup,
helper, scripts, timers, monitor/email y simulacro. No hay evidencia de backup de producción
operativo. La aceptación de este ADR permite avanzar al proceso de implementación y PR;
no sustituye autorizaciones de cuentas externas ni permite borrar fuentes reales.

## Alternativas

Sync bidireccional propaga borrados; snapshot solo Oracle no resuelve pérdida de cuenta.
WORM/PITR ofrece protección distinta y se difiere, no se afirma que Drive la proporcione.

## Consecuencias

Nuevas dependencias rclone y monitor externo, revisión de OAuth/cuotas y contratación
por propietario. Token de escritura comprometido puede borrar backups. Reabrir ante
RPO inaceptable, cuota insuficiente o necesidad de almacenamiento inmutable.
