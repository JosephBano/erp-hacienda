# BACKUPS.md — Estrategia de respaldos

> Cumple el **Art. 2** de la Constitución: *respaldos probados o nada*. Un backup que
> nunca se restauró no existe. Ninguna fase se declara "en producción" sin que lo de
> aquí abajo esté funcionando y verificado.
>
> Estado: **definido, aún no operativo** (no hay producción todavía — Fase 0).

## Qué se respalda

| Activo | Medio | Por qué |
|---|---|---|
| Base de datos PostgreSQL | `pg_dump` comprimido, formato `custom` (`-Fc`) | Es *la* historia de la finca. Irreemplazable. |
| Adjuntos (fotos de guías, facturas, certificados) | Copia del directorio/bucket de archivos | Los eventos los referencian; sin ellos el expediente queda cojo. |
| Variables de entorno y secretos | Gestor de secretos + copia cifrada offline | Sin ellos la restauración no arranca. |
| Código y migraciones | El propio repositorio Git (remoto) | Permite reconstruir el esquema desde cero. |

Lo que **no** se respalda: contenedores, artefactos de build, `node_modules`, logs de
aplicación. Todo eso es reproducible.

## Frecuencia y retención

- **Diario**, automatizado, a las 03:00 `America/Guayaquil` (baja actividad en la finca).
- Retención **3-2-1**: 3 copias, en 2 medios distintos, 1 de ellas **offsite**.
- Escalera de retención: 7 diarios · 4 semanales · 12 mensuales · 3 anuales.
- Cada respaldo se cifra antes de salir del servidor y se verifica su suma de control.

## Restauración probada (lo innegociable)

**Una vez al mes**, sin excepciones:

1. Levantar un entorno limpio y aislado (contenedor efímero, jamás producción).
2. Restaurar el backup más reciente con `pg_restore`.
3. Verificar tres cosas concretas, no solo que "el restore no dio error":
   - el conteo de `animal_events` coincide con el del día del respaldo;
   - el expediente de un animal conocido se lee completo y con sus adjuntos;
   - las migraciones EF Core están al día (`dotnet ef migrations list` sin pendientes).
4. Anotar la fecha, la duración y el resultado en la bitácora de restauraciones.
5. Destruir el entorno de prueba.

Si una restauración falla, **eso es la máxima prioridad del proyecto** hasta resolverse;
por encima de cualquier feature de cualquier fase.

## Objetivos declarados

- **RPO** (pérdida máxima aceptable): 24 h en Fase 1. Al llegar a Fase 3 —cuando los
  empleados registren desde el campo— baja a ≤ 1 h mediante archivado de WAL continuo.
- **RTO** (tiempo máximo de recuperación): 4 h. Se mide de verdad en el simulacro mensual.

## Prueba local hoy (sin producción)

Con `docker-compose` levantado, el ciclo completo se ensaya así:

```bash
# Respaldar
docker exec hato-postgres pg_dump -U hato -Fc hato > hato-$(date +%F).dump

# Restaurar en una base limpia y comparar
docker exec -i hato-postgres createdb -U hato hato_restore_test
docker exec -i hato-postgres pg_restore -U hato -d hato_restore_test < hato-$(date +%F).dump
```

## Pendiente al abrir producción (Fase 1)

- [ ] Cron o timer de systemd que ejecute el respaldo diario y notifique fallos.
- [ ] Destino offsite elegido y con credenciales rotables.
- [ ] Cifrado en reposo del backup y custodia de la clave fuera del servidor.
- [ ] Recordatorio mensual de simulacro de restauración (motor de alertas, Fase 2).
- [ ] Bitácora de restauraciones creada (`docs/restore-log.md`).
- [ ] Archivado WAL continuo antes de cerrar Fase 3.
