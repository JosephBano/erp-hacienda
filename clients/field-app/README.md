# HATO — App de campo (`field-app`)

Aplicación móvil offline-first (Fase 3). Registra ordeño, tratamientos, pesajes,
movimientos y partos **sin señal**, y sincroniza contra `/api/v1/sync` cuando la hay.

## Requisitos

- Node.js 18+ y npm 9+
- Android Studio / Xcode para emulador o dispositivo real

WatermelonDB usa un módulo nativo, así que **no corre en Expo Go**: hace falta un
development build (`npx expo run:android`).

## Cómo se levanta contra el backend local

```bash
# 1. Backend + base de datos, desde la raíz del repo
docker compose up -d
dotnet run --project src/Hato.Api

# 2. App
cd clients/field-app
npm install
EXPO_PUBLIC_API_URL=http://10.0.2.2:5000 npx expo run:android
```

`10.0.2.2` es la dirección con la que el emulador de Android alcanza el `localhost` de la
máquina anfitriona. En un teléfono físico, use la IP de su computador en la red local.

## Cómo está organizada

```
src/
├── database/     esquema local, migraciones versionadas y modelos WatermelonDB
├── services/     outbox, motor de sincronización y los servicios de cada flujo
├── screens/      pantallas de campo
└── ui/           tokens de diseño y componentes (botones grandes, alto contraste)
```

Las decisiones que no son obvias leyendo el código:

- **Todo se escribe primero en el outbox local.** Ninguna pantalla llama a la red. Lo que
  el empleado registra queda en SQLite con su `clientOperationId`, y el motor lo envía
  después. Una operación rechazada por el servidor **nunca se borra**: pasa a la bandeja de
  problemas con el motivo, porque un registro que desaparece sin rastro es el único fallo
  que esta fase no puede permitirse.
- **Los UUID se generan en el teléfono** con `expo-crypto` (Art. 3). Eso permite registrar
  un animal en el potrero y pesarlo en la misma sesión sin señal.
- **El bloqueo por retiro (Art. 19) se resuelve localmente** contra la tabla
  `withdrawal_periods` que baja el pull, más el retiro que la propia app calcula al
  registrar un tratamiento. Un bloqueo que sólo funcionara con señal no sería un bloqueo.
- **El parto viaja como una sola operación** `recordBirth`. Registrar cada cría como un
  alta de animal suelta pierde la genealogía sin dar ningún error.
- **La sesión vive en el llavero del dispositivo** (`expo-secure-store`) y el PIN se guarda
  como digest SHA-256 con sal.

## Pruebas

```bash
npm test          # lógica (jsdom) + pantallas (React Native Testing Library)
npm run typecheck
```

Las pruebas de lógica corren contra una base WatermelonDB real (adaptador LokiJS en
memoria), no contra un doble: el esquema, las migraciones y las consultas se ejercitan de
verdad. La durabilidad entre reinicios de proceso es responsabilidad del adaptador SQLite y
se verifica en hardware, no aquí.

## Lo que todavía no hace

Para no dar por hecho lo que no está:

- **Fotos**: se guarda la referencia local en el evento, pero no se suben. Falta el módulo
  de adjuntos (`feature/shared-attachments`, Fase 4).
- **Piloto**: la app no ha pasado todavía una semana real de registros en la finca, que es
  el criterio de salida de la Fase 3.
