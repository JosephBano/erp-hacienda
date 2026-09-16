# PRODUCCION.md — Corte, mantenimiento y recuperación de producción

> Este documento responde **¿cómo se despliega, corta y recupera producción?** Describe el
> procedimiento operativo definido en
> `docs/spec/feature-0012-production-environment/spec.md` §4-§7. No certifica que producción
> esté abierta ni lista para registros reales: la apertura real depende de compuertas
> externas (ver sec. 6) que a la fecha de este documento no están cumplidas.
>
> Complementa, sin repetir, a `docs/POLITICAS-OPERACION.md` (qué política se decidió y por
> qué) y `docs/PREFLIGHT-PRODUCCION.md` (cómo inventariar el origen real del piloto antes del
> corte). Este documento es el procedimiento de ejecución: qué se hace, en qué orden, y qué
> hacer cuando algo falla.

## 1. Custodia

Resumen operativo sin valores reales; la tabla completa de custodia por material está en
`docs/spec/feature-0012-production-environment/spec.md` §5 y las decisiones asociadas en
`docs/POLITICAS-OPERACION.md`.

| Recurso | Quién controla | Dónde vive |
|---|---|---|
| Cuenta OCI, MFA, recuperación, acceso administrativo | Propietario | Gestor de contraseñas del propietario, copia de recuperación separada |
| Tailscale (tailnet, ACLs, OAuth de CI) | Propietario | Consola Tailscale; credencial de CI en GitHub Environment `production` |
| Imágenes de contenedor (GHCR) | Propietario / CI aprobado | GHCR por digest, referenciadas desde `ops/production/` |
| Clave SSH de CI y aprobación de despliegue | Operador / GitHub | GitHub Environment `production`, clave pública instalada en `hato-deploy` |
| Secretos de runtime (JWT, contraseñas DB, credencial lectura GHCR si aplica) | Operador | Archivos 600 root-owned en `/etc/hato-production`, fuera del checkout |
| Huella SSH del host | Operador | Configuración del entorno de despliegue, verificada con `StrictHostKeyChecking=yes` |
| Tokens Drive y claves de cifrado de backup | Según feature-0013 | Recuperación independiente de la de producción |
| Firma Android y GitHub App de build | Según feature-0014 | Separadas del despliegue de producción |

Ningún secreto de esta tabla vive en el checkout de este repositorio, en variables
`EXPO_PUBLIC_*`, en logs de compose renderizado, en argumentos de proceso ni en backups sin
cifrar (spec §5).

## 2. Mantenimiento

- **Ventana mensual** de mantenimiento con backup previo verificado y validación posterior
  antes de cerrarla (spec línea 109-111).
- **Actualizaciones de seguridad automáticas** se aplican sin reinicio inesperado del
  servicio; un reinicio de sistema operativo, si es necesario, se coordina dentro de la
  ventana, no fuera de ella.
- **Versiones y digests** de imágenes se fijan explícitamente y se revisan en un proceso
  mensual de actualización — no se congelan indefinidamente ni se actualizan fuera de
  proceso.
- Cada ventana de mantenimiento deja registro de: versión previa, versión aplicada,
  resultado de la validación posterior, sin incluir secretos.

## 3. Migración desde el piloto (corte de datos)

Este es el procedimiento de spec §6 líneas 155-160. Es una compuerta que exige conciliación
real, no un cambio de endpoint. Ver `docs/PREFLIGHT-PRODUCCION.md` para el inventario que
debe completarse **antes** de iniciar este procedimiento — este documento no repite ese
inventario, lo asume hecho.

1. **Inventariar el servidor real de datos del piloto** y los outboxes de los teléfonos en
   campo (no asumir que el staging actual es descartable si contiene registros reales — ver
   `docs/PREFLIGHT-PRODUCCION.md` sec. 2).
2. **Congelar la fuente**: detener escrituras contra el servidor origen antes de tomar el
   dump final. Sin fuente congelada, cualquier corte posterior es inválido.
3. **Dump final** del origen congelado.
4. **Restauración aislada** en el entorno de producción nuevo, sin sobrescribir ni tocar el
   origen.
5. **Validación** sobre la restauración aislada:
   - UUIDs coinciden entre origen y destino.
   - Eventos e historial se preservan íntegros.
   - Permisos y roles quedan correctamente asignados.
   - Sincronización funciona extremo a extremo con al menos un dispositivo de prueba.
6. **Preservar la fuente y los dispositivos** — no se destruyen ni se reciclan hasta que la
   migración esté validada y aceptada.
7. **Prohibiciones explícitas durante el corte:**
   - No resetear SQLite en los teléfonos.
   - No reinstalar la app para cambiar de URL mientras existan operaciones pendientes sin
     sincronizar.
   - No tratar el respaldo del servidor como cobertura de lo pendiente local: un backup del
     servidor no incluye lo que aún vive solo en el teléfono.
8. Solo tras restauración de ensayo y conciliación exitosa se autoriza el corte final con
   escrituras detenidas (criterio ya establecido en `docs/PREFLIGHT-PRODUCCION.md` sec. 2).

## 4. Rotación de credenciales

Procedimiento a seguir tras una exposición sospechada o confirmada de cualquier credencial
de la tabla de la sec. 1 (spec línea 134):

1. Identificar exactamente qué credencial se expuso y qué consumidor la usa (tabla de
   custodia, sec. 1 de este documento).
2. Revocar la credencial expuesta en su origen (Tailscale, GitHub Environment, base de
   datos, etc.) antes de emitir la nueva.
3. Emitir credencial nueva y desplegarla al consumidor exacto — nunca ampliar el alcance ni
   los consumidores como atajo de la rotación.
4. Confirmar que el consumidor antiguo ya no puede autenticar con el valor revocado.
5. Documentar la rotación, la revocación y la restauración de servicio, sin registrar
   valores secretos — solo qué credencial, cuándo y quién.
6. Para JWT signing key: la rotación puede invalidar sesiones activas, pero no debe
   eliminar el outbox local de ningún dispositivo (`docs/POLITICAS-OPERACION.md`).
7. Rotación de contraseñas de DB, JWT y OAuth se ensaya primero en staging, nunca se
   estrena directamente en producción (`docs/POLITICAS-OPERACION.md`).
8. La firma Android **no** se rota rutinariamente — se preserva para permitir
   actualizaciones (`docs/POLITICAS-OPERACION.md`); su procedimiento de recuperación ante
   pérdida es un caso distinto, cubierto por feature-0014, no por esta rotación.

## 5. Diagnóstico

Qué observar cuando algo falla, y a través de qué canal:

- **Logs**: rotados; revisar primero los del servicio afectado (API, migrador, proxy,
  PostgreSQL) sin imprimir secretos ni compose renderizado.
- **Salud (health)**: endpoint de salud del API y verificación de versión/SHA desplegado.
- **Alertas de disco**: aviso a 70% de uso, alerta urgente a 85%.
- **Memoria / OOM**: vigilar eventos de out-of-memory del host y de los contenedores.
- **Salud externa**: verificación desde fuera de la VPS (no basta con que el propio host se
  reporte sano — spec línea 107-108 y criterio de aceptación de spec §7).

**Acceso por rol:**

- El **operador humano** (propietario) tiene acceso administrativo completo: OCI, SSH
  operador, consola, gestor de secretos.
- **CI aprobado** solo puede invocar el comando de despliegue restringido a través de
  `hato-deploy`; no tiene shell, no lee secretos, no modifica el helper ni el compose.
- El **monitor** (feature-0015, autohospedado en home-server) recibe únicamente pings de
  estado — nunca claves de despliegue ni acceso a datos de la aplicación.

## 6. Retorno seguro / rollback

`docker compose` **no garantiza rollback** (spec línea 150-153). El procedimiento de
recuperación depende de en qué punto del release falló:

- **Falla antes de migrar**: conservar el release anterior en ejecución; el despliegue
  fallido no reemplaza nada todavía.
- **Falla después de migrar**:
  - Si las imágenes previas son **compatibles** con el esquema ya migrado, volver a esas
    imágenes previas.
  - Si **no son compatibles**, no hay vuelta atrás automática: se mantiene modo
    mantenimiento y se aplica **corrección hacia adelante** (fix forward), nunca un
    downgrade de esquema improvisado.
- **Restaurar la base de datos** desde backup exige una **decisión humana explícita** sobre
  qué hacer con los datos posteriores al backup restaurado. Nunca es automática, nunca
  implica borrar el volumen de datos anterior sin esa decisión.
- Cada intento de recuperación deja registro de: versión previa, acción tomada, resultado,
  sin secretos.

## 7. Compuertas externas

Este documento describe el procedimiento; **no certifica que se haya ejecutado ni que
producción esté abierta a registros reales.** Las siguientes compuertas, externas a este
documento, deben cumplirse antes de esa apertura:

- **`docs/spec/feature-0013-drive-backups/spec.md`** — D6: producción no abre registros
  reales hasta que exista una restauración mensual real probada en PostgreSQL aislado,
  ejecutada antes de registrar datos reales (D6 de esa spec). El estado de esa compuerta a
  la fecha de este documento es: ADR-0034 aceptado, implementación e instalación
  pendientes (ver `docs/POLITICAS-OPERACION.md` sec. "Estado documental y compuertas").
- **`docs/spec/feature-0014-android-release-distribution/spec.md`** — distribución Android:
  depende de que el canal estable de producción (esta spec, feature-0012) exista primero.
  ADR-0035 aceptado; inventario de firma y ejecución pendientes a la fecha de este
  documento.
- **Validación de campo del corte de datos** (sec. 3 de este documento): restauración de
  ensayo, conciliación de UUID/eventos/permisos/sincronización y confirmación del operador
  de que el origen migrado es el real.

El orden de paquetes declarado en `docs/PREFLIGHT-PRODUCCION.md` sec. 4 aplica: bootstrap de
0012 → backup 0013 y monitores 0015 → restore de DB → release y corte de 0012 (este
documento) → 0014 y ensayo de actualización del teléfono → apertura. Ninguna aprobación
documental (ADR aceptado, spec redactada) equivale a completar una tarea operativa.

## 8. Límites explícitos

Fuera de alcance para producción, conforme spec §7:

- Alta disponibilidad (HA).
- Kubernetes.
- Dominio comprado (el acceso es privado vía Tailscale, sin dominio público).
- Funcionalidades de negocio nuevas.
- Publicación en tiendas de aplicaciones.

Adicionalmente, spec §7 advierte: el free tier de OCI puede perderse — se debe verificar sus
condiciones vigentes y mantener la posibilidad de reconstrucción en otro proveedor como
salida, no como plan activo.

## 9. Ver también

- `docs/spec/feature-0012-production-environment/spec.md` — spec completa de producción.
- `docs/POLITICAS-OPERACION.md` — decisiones operativas y su estado de aprobación (ADRs).
- `docs/PREFLIGHT-PRODUCCION.md` — inventario y altas asistidas previas al corte.
- `docs/BACKUPS.md` — estrategia y protocolo de backups (documento previo; la implementación
  vigente de backup para producción según feature-0013 se referencia en
  `docs/POLITICAS-OPERACION.md`).
