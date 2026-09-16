# GUIA-CAMPO-ANDROID.md — Acceso, instalación y actualización de HATO

> Guía del flujo previsto. La sección de descargas y mensajes descritos deben implementarse
> y validarse antes de entregar esta guía a empleados. El administrador entrega por canal
> privado la dirección web de la finca y la invitación de acceso. Nunca comparte su cuenta.

## Primera instalación acompañada

1. Conecta el teléfono a internet. El administrador comprueba que no exista otra instalación
   HATO con registros pendientes antes de comenzar.
2. Instala Tailscale desde su ficha oficial en Google Play. Abre la aplicación y accede con
   la identidad que el administrador invitó; acepta el permiso VPN de Android. El
   administrador aprueba el dispositivo y limita su acceso a HATO.
3. Comprueba que Tailscale indique conectado. El permiso VPN permite llegar a la red
   privada de la finca; no es tu usuario de HATO. Si otra VPN está activa, pide ayuda:
   no desactives protecciones corporativas por tu cuenta.
4. Abre en el navegador la dirección privada entregada. Guarda un marcador «HATO finca».
   Si no carga, revisa internet y Tailscale; no aceptes avisos de certificado inválido.
5. Inicia sesión en HATO con tu cuenta individual. En «Aplicaciones Android» descarga la
   versión estable recomendada. STAGE es para pruebas acompañadas; no registres allí
   trabajo real ni instales archivos recibidos de desconocidos.
6. Al abrir el APK, Android puede solicitar «Permitir desde esta fuente» para el navegador
   o gestor que abrió el archivo. Autoriza esa fuente de forma puntual y revoca después.
   No desactives Play Protect globalmente. Si bloquea el APK, avisa al administrador.
7. Abre HATO Campo, inicia sesión con internet y realiza la sincronización inicial.
   Comprueba finca, animales/lotes y versión con el encargado antes de registrar trabajo.
8. Junto al encargado, prueba el modo avión con un registro de prueba autorizado y luego
   sincronízalo; usar entorno de prueba para no crear hechos ficticios en producción.

## Trabajo diario

HATO puede registrar sin señal después de su preparación inicial. Verifica que el registro
quede guardado localmente. Al volver a tener internet, comprueba Tailscale conectado,
abre HATO y revisa sincronización. «Pendiente» no significa que esté en el servidor.
Si figura rechazado, lee el motivo y avisa; no repitas registros suponiendo que se perdieron.

Si Android lo permite, el administrador puede activar VPN siempre activa para Tailscale
y revisar restricciones de batería específicas del teléfono. No activar «bloquear todas
las conexiones sin VPN» como requisito general: podría impedir otros servicios. Probar
reconexión después de reiniciar y cambiar WiFi/datos. No se requiere exit node.

## Actualizar la app

1. Hazlo con batería suficiente, internet y Tailscale conectado, fuera del registro urgente.
2. Abre HATO y sincroniza; verifica pendientes y rechazados. Si quedan pendientes o no
   puedes comprobarlos, contacta al encargado antes de continuar. No borres datos.
3. Abre el marcador privado → Aplicaciones Android → estable recomendada. Comprueba las
   notas y descarga. La descarga requiere sesión; no hay enlace público ni instalación
   automática silenciosa.
4. Abre el APK y pulsa actualizar sobre la aplicación existente. No desinstales la anterior.
5. Abre HATO, comprueba versión, animales/lotes y sincronización. Si solicita login, usa
   tu cuenta; un cambio de sesión no justifica limpiar almacenamiento.
6. Revoca permiso de instalación del navegador si lo habilitaste y avisa de cualquier
   diferencia. El administrador confirma que no faltan operaciones del dispositivo.

Una actualización probada preserva datos locales, pero la sincronización previa reduce
el riesgo. «Conflicto de paquete», «firma incompatible» o petición de desinstalar requieren
intervención del administrador. No pruebes APKs antiguos para arreglarlo.

## Problemas frecuentes

| Lo que ocurre | Qué hacer |
|---|---|
| La web no abre | Revisar internet y estado Tailscale; probar nuevamente y comunicar hora/error |
| Tailscale pide iniciar sesión | Reautenticar con tu identidad autorizada; pedir ayuda si no recuerdas cuál |
| HATO funciona pero no sincroniza | Conservar registros, revisar pendientes/rechazados y avisar al encargado |
| Se pide actualizar para sincronizar | Continuar captura local si está disponible y coordinar actualización; no borrar datos |
| Android rechaza actualización | No desinstalar; enviar versión actual y mensaje al administrador |
| Perdí el teléfono | Avisar inmediatamente para revocar acceso; no asumir respaldo de pendientes |
| Cambié de teléfono | Entregar el anterior para conciliar pendientes antes de preparar el nuevo |

No compartir contraseña/PIN, keystore, archivos de backup ni capturas con datos personales
en grupos públicos. Soporte solicita versión, hora y mensaje; nunca contraseña. Exportar
diagnóstico solo por el flujo deliberado de HATO y canal privado del encargado.

## Entrega del administrador

Registrar dispositivo y responsable en inventario privado; confirmar acceso HTTPS permitido
y SSH/staging denegados, reinicio, login, actualización sobre versión anterior y
sincronización offline. Revocar dispositivo extraviado y usuario de aplicación según caso;
revocar Tailscale no borra datos que ya estaban en el teléfono. Capacitar al reemplazo.

Referencias: [Tailscale Android](https://tailscale.com/docs/install/android),
[actualización Android](https://developer.android.com/google/play/app-updates).
