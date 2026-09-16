# plan.md — Configuración de producción

> Paquete completo: [tareas](./tasks.md) y [E2E](./test-e2e.md).
> Aplican [políticas](../../POLITICAS-OPERACION.md) y [preflight](../../PREFLIGHT-PRODUCCION.md).

> Plan de [spec.md](./spec.md), aprobado 2026-09-16. Implementación pendiente. La guía writing-plans orienta secuencia y dependencias.

## Secuencia de entregas

1. `docs(ops): define private production and deployment trust boundaries`.
   Aplicar ADR-0033 ya aceptado; nuevas decisiones fuera de él requieren ADR adicional.
   Actualizar `docs/SEGURIDAD.md`, `docs/ARCHITECTURE.md` y README con destino, autoridad
   y custodia; preservar ADR-0031 histórico y describir qué premisa reemplaza el nuevo.
2. `feat(ops): add restricted production bootstrap`.
   Crear `ops/production/bootstrap.md`, `ops/production/sshd-hato.conf`,
   `ops/production/sudoers-hato`, `scripts/production-deploy-entry.sh` y
   `scripts/production-deploy-root.sh`. Aplicación administrativa separada y explícita:
   cuenta, directorios, huellas y segunda sesión Tailscale antes de cerrar SSH público.
   Verificar `sshd -t`, `visudo -cf` y rechazo de shell, forwarding y entradas maliciosas.
3. `feat(ops): define isolated production stack`.
   Crear `compose.production.yml`, `ops/production/production.env.example` sin valores
   secretos y configuración del proxy. Ajustar Dockerfiles para imágenes ARM64 por digest
   y migrador que no exponga conexión en argv. Roles DB y bootstrap admin de un solo uso
   deben usar aplicación/migraciones, no cambios manuales de esquema. Verificar compose
   en entorno aislado, arranque sin secrets rechazado y DB inaccesible externamente.
4. `ci(ops): deploy approved production releases`.
   Crear `.github/workflows/deploy-production.yml` y
   `scripts/production-release-verify.sh`. Construir en runners alojados, GHCR, checks
   ligados a SHA, aprobación, host key pinning, lock y manifiesto allowlist. Incorporar
   pruebas de entradas inválidas y rechazo de referencias ajenas a main. Confirmar que
   un error pre-migración conserva servicio y uno post-migración no restaura datos solo.
5. `docs(ops): define production cutover and recovery`.
   Crear `docs/PRODUCCION.md` y registrar su pregunta operativa en DOCUMENTACION.md.
   Incluir custodia, mantenimiento, migración desde piloto, rotación, diagnóstico y
   retorno seguro. Conectar feature-0013 y feature-0014 como compuertas externas.

## Dependencias y verificación de implementación

ADR → bootstrap → stack → release → backup restaurado (feature-0013) → corte de datos →
APK configurada (feature-0014) → validación de campo → apertura de producción.
Las entregas son separables por PR, todas derivadas de develop actualizado. No saltar
aprobaciones operativas usando la cuenta del propietario. El usuario hato-deploy queda
creado y probado al terminar la entrega 2, no al aprobar este documento.

Pruebas automatizadas futuras en `tests/ops/production/`: validación de manifiestos,
inyección por stdin, paths/registros rechazados, bloqueo concurrente, ausencia de secretos
y fallo de migración. Integración con PostgreSQL real. Ejecutar `dotnet build -c Release`
y `dotnet test -c Release`, más checks de clientes cuando se modifiquen, antes del PR.
En VPS medir reinicio, acceso público denegado, lectura privada autorizada y restore
aislado; registrar resultado, fecha, SHA y operador, sin datos personales.

El punto delicado es el primer corte de escrituras reales: exige respaldo final y
conciliación de móviles. No tiene como reversa automática restaurar una base antigua.

PR: propósito producción privada recuperable; decisiones del spec y ADR; prueba de acceso,
release y recuperación; exclusiones HA/negocio; dependencias pendientes claramente visibles.
