# PREFLIGHT-PRODUCCION.md — Inventario y altas asistidas

> Procedimiento de preparación. No publicar seriales, correos, tokens, dumps, filas reales
> ni APKs en git. Resultados sensibles en inventario privado del operador.

## 1. App Android instalada

Con teléfono desbloqueado, depuración USB habilitada y PC autorizada:

```bash
adb devices -l
adb shell pm list packages
adb shell dumpsys package com.joemandev.hatofieldapp
adb shell pm path com.joemandev.hatofieldapp
```

Filtrar resultados por versionName/versionCode y package, no publicar dumpsys completo.
Copiar únicamente base.apk desde ruta devuelta por pm path a directorio temporal 700,
archivo 600. Verificar con apksigner verify --print-certs y calcular sha256sum. No extraer
datos privados ni usar adb backup, run-as, uninstall, clear, downgrade o restore sobre el
teléfono del empleado. Leer URL del bundle no prueba qué servidor contiene datos válidos.

Observación de este turno: Android 8.1.0, HATO package com.joemandev.hatofieldapp,
versionName 1.0.0, versionCode 1. Certificado del APK con DN Android Debug; no basta para
inferir que el flag debuggable esté activo ni que se tenga su clave privada. El bundle
contiene API HTTP del piloto. Firma debug es compuerta: localizar custodia y comprobar
huella, luego diseñar migración a firma release sin borrar datos. No publicar el APK.

Huella SHA-256 del certificado y hash del APK fueron observados durante el diagnóstico,
pero se conservan en evidencia temporal privada y no son necesarios en este repositorio
público. Repetir la medición desde el dispositivo al ejecutar la tarea INV.1 y guardar el
acta privada; no tratar una huella copiada de documentación como prueba actual.

La huella se obtiene del certificado, pero la clave privada no se recupera del APK.
Registrar inventario privado de cada teléfono, responsable, package, certificado, hash,
versionCode, URL y fecha. En UI del teléfono revisar pendientes y rechazados y versión
mostrada; UI puede aportar otra información que el APK. Confirmar con operador que esa
instalación registra datos reales. No cambiar backend mientras haya pendientes sin plan.

## 2. Identificar fuente real a migrar

La URL observada indica un candidato, no una fuente autoritativa. Resolver nodo Tailscale
de esa dirección con el operador y consultar /version y /health de solo lectura.
En host confirmado, inspeccionar nombres de contenedores/procesos, mounts y nombre de DB
sin imprimir docker inspect/env completo ni cadenas con password. Leer configuración
redactada de conexión (host lógico, base, roles) para enlazar API con PostgreSQL exacto.

Con credencial de lectura, registrar tamaño DB, migraciones por módulo, conteos de
animales/eventos/usuarios y fechas extremas en informe privado. Comparar un pequeño conjunto
de UUID y registros reconocidos por el encargado entre teléfono, API y DB. No crear
registros ficticios para confirmar una base real. Si hay varias fuentes, conservar todas
y conciliar diferencias; fecha más nueva o etiqueta production no decide por sí sola.

Completar acta privada: API/SHA → host → DB → volumen → backup de prueba → responsable
que confirma origen. Registrar dispositivos pendientes, cuentas y ventanas de corte.
Solo tras restauración de ensayo y conciliación se autoriza corte final con escrituras
detenidas. Nueva VPS vacía no es fuente; staging con datos reales no es descartable.

## 3. Alta asistida Drive y credenciales

El agente acompaña al propietario: iniciar sesión en la cuenta de backups en navegador,
crear o seleccionar backups-hato-erp y verificar que no tenga compartición pública.
Resolver folder ID y titular mediante OAuth; guardarlos privadamente. Comprobar cuota
global y reservar presupuesto operativo de 300 GB, sin confundirlo con cuota nativa.

Configurar consentimiento OAuth apto para operación desatendida y menor scope viable;
no solicitar contraseña Google al chat. Autorizar desde navegador del propietario,
guardar refresh token y crypt en archivos protegidos y copia offline separada. Verificar
subida/descarga de archivo fixture cifrado a namespace de ensayo, hash y renovación del
token. Probar recuperación en otro entorno con copia de claves, no solo token residente.

Usar un cliente OAuth propio de rclone creado bajo la cuenta de backups, no el client ID
público compartido. Guardar client ID/secret y refresh token como secretos distintos.
Configurar la pantalla de consentimiento para uso operativo privado y comprobar que el
refresh token no expira por permanecer en modo Testing. Revisar las reglas vigentes de
Google durante el alta; no ampliar usuarios o scopes para ocultar un error de configuración.

Alta de SMTP/monitor por cuenta de backups y credencial limitada; prueba email real y
falta de heartbeat. El registro público guarda estado de cada paso y fecha, sin valores.
No marcar tareas de acceso completas por aprobar el spec. No destruir carpeta al terminar
ensayo: limpiar solo objetos fixture expresamente identificados.

## 4. Usuarios y orden de implementación

| Identidad | Host | Momento | Autoridad |
|---|---|---|---|
| ubuntu | Oracle | Existente, SSH comprobado | Administración y bootstrap |
| hato-deploy | Oracle | Feature-0012 bloque 2 | Comando de despliegue restringido |
| hato-backup | Oracle | Feature-0013 bloque 2 | Dump por helper, cifrado y uploader |
| joeman | home-server | Existente según repo | Administra monitores |

No crear un segundo usuario para cada spec con el mismo propósito. Roles SQL separados
del usuario Linux; identidad CI separada del operador. Monitor no recibe claves de deploy.
Un único uploader hato-backup administra el presupuesto Drive de todos los namespaces;
home-server entrega su backup de monitor por entrada restringida. No ejecutar shell ni
elegir rutas arbitrarias desde esa entrada. Serializar uploads y retención en el uploader.

Orden: bootstrap de 0012 → backup 0013 y monitores 0015/home-server → restore de DB →
release y corte de 0012 → 0014 y ensayo de actualización del teléfono → apertura.
Se puede desarrollar cada spec por separado pero las compuertas de apertura los enlazan.

## 5. Dos pruebas diferentes

Restauración real: descargar Drive, recuperar PostgreSQL aislado, roles/migraciones y
smoke API; no requiere teléfono y nunca sobrescribe DB fuente. Prueba Android: instalar
actualización compatible sobre fixture, conservar outbox y sincronizar con API de ensayo.
El teléfono conectado se puede inventariar ahora; si tiene datos reales, usar otro
dispositivo o variante independiente para ensayos destructivos. No se ha realizado
ninguna restauración ni actualización en este turno.
