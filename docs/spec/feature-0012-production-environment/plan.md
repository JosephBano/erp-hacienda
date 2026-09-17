# plan.md — Configuración de producción

> Paquete completo: [tareas](./tasks.md) y [E2E](./test-e2e.md).
> Aplican [políticas](../../POLITICAS-OPERACION.md) y [preflight](../../PREFLIGHT-PRODUCCION.md).

> Plan de [spec.md](./spec.md), aprobado 2026-09-16. La base documental y los
> artefactos locales se implementan en esta rama; ninguna de esas entregas acredita una
> instalación en Oracle, custodia de secretos ni apertura a datos reales. La guía
> writing-plans orienta secuencia y dependencias.

## Cierre de implementación local — 2026-09-16

**Objetivo:** dejar artefactos versionados que puedan desplegar exclusivamente imágenes
ARM64 aprobadas por digest, sin exponer conexiones en argumentos de proceso, y que sean
comprobables antes del bootstrap humano.

**Orden ejecutable:**

1. Hacer que cada fábrica de DbContext acepte `ConnectionStrings__HatoDb` del entorno y
   retirar `--connection` del migrador; probar el contrato de configuración.
2. Sustituir los `build:` de API/migrador en producción por referencias obligatorias a
   imágenes GHCR por digest. El helper valida el manifiesto y escribe solo metadatos no
   secretos en una configuración root-owned antes de llamar a Compose.
3. Añadir Caddyfile, renovación de `tailscale cert` y comprobación de expiración, sin
   publicar un endpoint de administración ni conceder la clave al usuario de despliegue.
4. Incorporar una autorización de despliegue verificable por el host, ligada a
   run/attempt/SHA/digests, y rechazar replays antes de mutar el stack.
5. Ejecutar checks estáticos de YAML y shell, pruebas unitarias de los contratos nuevos y
   `dotnet build/test -c Release`. Las tareas que requieren Oracle, Tailscale, GitHub
   Environments, Drive o teléfonos conservan su estado pendiente hasta tener evidencia
   privada reproducible.

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
