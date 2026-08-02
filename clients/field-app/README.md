# ERP Hacienda — App Móvil de Campo (`field-app`)

Aplicación móvil offline-first construida con React Native (Expo SDK 51), TypeScript y WatermelonDB para permitir el registro de campo (ordeño, tratamientos, partos, movimientos) sin señal celular.

## Requisitos
- Node.js 18+
- npm 9+
- Expo CLI (`npx expo`)
- Android Studio / Xcode para ejecución en emulador o dispositivo real.

## Instalación y Arranque Local

1. Instalar dependencias:
   ```bash
   cd clients/field-app
   npm install
   ```

2. Iniciar el servidor de desarrollo Expo:
   ```bash
   npm start
   ```

3. Ejecutar en emulador Android o iOS:
   ```bash
   npm run android
   # o
   npm run ios
   ```

## Ejecución de Pruebas
```bash
npm test
npm run typecheck
```

## Arquitectura Offline-First
- **WatermelonDB**: SQLite nativo de alto rendimiento para persistencia local de miles de animales y eventos.
- **Sesión Offline**: Caché cifrada del JWT y Refresh Token con desbloqueo rápido por PIN local de 4 dígitos.
- **Motor de Sincronización**: Cola Outbox local (`sync_outbox`) que envía peticiones acumuladas cuando el dispositivo recupera señal contra `/api/v1/sync/push`.
