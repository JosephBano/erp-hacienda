# test-e2e.md — Verificación operativa

> Escenarios futuros, no ejecutados. [Spec](./spec.md) · [Tareas](./tasks.md).
> Ejecutar tras suite automatizada verde, con identidades fixture y entornos aislados.
> Nunca inyectar corrupción, perder acceso o borrar datos en la VPS real para ensayar.
> Registrar fecha, SHA, precondiciones, pasos, resultado y evidencia sin secretos.

## E2E-1 — Dump completo

**Preparación:** DB PostgreSQL 16 fixture con todos los módulos y rol lector.

**Pasos:** Respaldar, descargar desde Drive y restaurar en DB nueva; verificar migraciones y smoke autenticado.

**Debe pasar:** UUID/eventos/permisos e inventario presentes; ninguna conexión a DB original durante restore.

## E2E-2 — Fallos cerrados

**Preparación:** Fixtures .partial, checksum ausente/incorrecto y SQL inválido.

**Pasos:** Ejecutar backup/restore con cada fixture y comparar exit code y last-success.

**Debe pasar:** Código no cero, sin mensaje falso de éxito, sin marcador complete ni poda.

## E2E-3 — Cifrado y recuperación

**Preparación:** Configuración recuperada en equipo limpio.

**Pasos:** Inspeccionar carpeta Drive y descargar mediante crypt; perder simuladamente configuración de VPS y usar copia offline.

**Debe pasar:** Nombres/contenido cifrados; claves fuera del dump; descarga y restore posible sin VPS original.

## E2E-4 — Cuota y OAuth

**Preparación:** Remote exclusivo de ensayo.

**Pasos:** Simular cuota llena/token revocado y corte de red durante upload; restaurar conectividad.

**Debe pasar:** Conserva local, reintenta acotadamente y alerta; no sobrescribe ID conflictivo ni rota sin éxito.

## E2E-5 — Retención segura

**Preparación:** IDs propios con fechas simuladas, último válido antiguo y archivos ajenos.

**Pasos:** Ejecutar dry-run y poda en carpeta fixture.

**Debe pasar:** Solo candidatos propios completos; última válida y mensuales protegidas; ajenos intactos; papelera computada.

## E2E-6 — Timers y alerta externa

**Preparación:** Checks de ensayo y buzón del propietario.

**Pasos:** Disparar fallo; omitir ping hasta plazo abreviado de ensayo; apagar host de ensayo; restaurar plazos de política.

**Debe pasar:** Email recibido en fallo y ausencia; éxito solo tras verificación; periodicidad final 26 horas/35 días.

## E2E-7 — Restore mensual y RTO

**Preparación:** Host limpio sin acceso al socket/volumen original.

**Pasos:** Recuperar secretos, descargar dump, reconstruir roles y API y medir tiempos.

**Debe pasar:** RTO medido; reporte sin datos personales; si supera 4 horas bloquea apertura hasta ajuste o decisión documentada.

## Cierre

## E2E-8 — Límite compartido de Drive

**Preparación:** inventario simulado en bytes, cuota global simulada y dos uploads
concurrentes de backup/APK. No llenar Drive real con 300 GB para probar límites.

**Pasos:** cruzar umbrales 210 y 255 GB; solicitar uploads cuya suma exceda 300 GB;
simular cuota global agotada antes del límite y parciales/papelera imputables.

**Debe pasar:** alertas correspondientes; admisión serializada rechaza exceso o cuota
insuficiente; conserva backup local, última copia válida y APKs protegidas; no avanza
last-success ni reduce retención. Presupuesto local APK no se confunde con cuota Drive.

## Resultado final

Cualquier incumplimiento mantiene abierta su tarea. Confirmar restauración de configuración
y plazos tras ensayo, revocar credenciales fixture y eliminar únicamente recursos de prueba
identificados. Evidencia y decisión del propietario previas a apertura con registros reales.
