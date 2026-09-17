# test-e2e.md — Verificación operativa

> Escenarios futuros, no ejecutados. [Spec](./spec.md) · [Tareas](./tasks.md).
> Ejecutar tras suite automatizada verde, con identidades fixture y entornos aislados.
> Nunca inyectar corrupción, perder acceso o borrar datos en la VPS real para ensayar.
> Registrar fecha, SHA, precondiciones, pasos, resultado y evidencia sin secretos.

## E2E-1 — Dos aplicaciones

**Preparación:** Teléfono de prueba y dos APKs firmadas.

**Pasos:** Instalar stage/prod, iniciar sesión y registrar fixtures offline separados; reconectar.

**Debe pasar:** Packages/SQLite/outbox separados; cada una sincroniza solo con su API; banner stage inequívoco.

## E2E-2 — Generar ambas

**Preparación:** Administrador con develop y release main elegibles.

**Pasos:** Solicitar ambas dos veces con igual idempotency key y provocar fallo de una build.

**Debe pasar:** Una solicitud por canal, SHAs correctos, estado parcial honesto; prod no usa develop.

## E2E-3 — Canal estable

**Preparación:** Candidata main sin aprobación y usuario sin permiso publicar.

**Pasos:** Intentar publicar/descargar candidata, luego aprobar y publicar con humano autorizado.

**Debe pasar:** Bloqueo backend independiente de UI; stable solo después de evidencias y autorización.

## E2E-4 — Secrets y artefacto

**Preparación:** Credenciales ficticias identificables y keystore de ensayo.

**Pasos:** Buscar secretos en logs/cache/APK, alterar SHA/package/firma del artifact e importar.

**Debe pasar:** Sin secretos; artifact alterado rechazado antes del catálogo; temporales de firma no archivados.

## E2E-5 — Actualización con pendientes

**Preparación:** App estable anterior con pendientes offline y misma firma de actualización.

**Pasos:** Actualizar sobre app existente sin desinstalar en ensayo; abrir y sincronizar dos veces.

**Debe pasar:** Pendientes sobreviven y se aplican una vez; migración SQLite compatible; guía cotidiana recomienda sync previo.

## E2E-6 — Compatibilidad y bloqueo

**Preparación:** App anterior soportada y fixture incompatible.

**Pasos:** Probar sync contra API nueva; presentar contrato incompatible y luego actualizar.

**Debe pasar:** Soportada funciona; incompatible conserva captura/outbox y explica actualización, sin falso éxito.

## E2E-7 — Privacidad y descargas

**Preparación:** Sesiones admin, empleado y anónima.

**Pasos:** Descargar desde Chrome Android, probar URL sin sesión/range y pedir stage como empleado.

**Debe pasar:** Streaming válido autorizado; resto 401/403; sin token en URL ni artifacts públicos.

## E2E-8 — Historial y recuperación

**Preparación:** Biblioteca con objetos antiguos, actual y última buena.

**Pasos:** Simular poda/cuota, retirar release y restaurar catálogo/binario desde backup.

**Debe pasar:** Metadatos persisten, protegidas no se borran, cuota bloquea importación antes de perder estable.

## E2E-9 — Alta y pérdida de teléfono

**Preparación:** Operador siguiendo guía y dispositivo fixture.

**Pasos:** Instalar Tailscale, reiniciar, actualizar, reportar pérdida y revocar.

**Debe pasar:** Acceso conforme permisos; app actualiza sin desinstalar; revocación bloquea online sin prometer borrado remoto.

## Cierre

Cualquier incumplimiento mantiene abierta su tarea. Confirmar restauración de configuración
y plazos tras ensayo, revocar credenciales fixture y eliminar únicamente recursos de prueba
identificados. Evidencia y decisión del propietario previas a apertura con registros reales.
