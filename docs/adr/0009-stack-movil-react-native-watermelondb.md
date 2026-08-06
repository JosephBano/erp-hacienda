# ADR-0009 — Stack Móvil React Native, Base Local WatermelonDB y Dependencias Iniciales

- **Estado:** Aceptado
- **Fecha:** 2026-08-02

## Contexto
La aplicación móvil (`field-app`) debe ejecutarse en teléfonos Android y iOS de gama media en zonas rurales sin señal celular. Necesita persistir miles de registros (catálogo de animales, ordeño, eventos) localmente en el dispositivo, responder instantáneamente (≤3 toques por registro), soportar sincronización incremental por outbox y permitir autenticación offline mediante PIN local tras la primera sesión activa.

## Decisión

### 1. Framework y Herramientas Móviles
- **Expo Dev Client (SDK 51+):** Se adopta Expo con `expo-dev-client` (prebuild / dev-client) en lugar de React Native bare. Permite incluir módulos nativos C++/Java como SQLite de WatermelonDB manteniendo la agilidad de desarrollo y builds automatizadas con Expo CLI.

### 2. Base de Datos Local y Reactividad
- **WatermelonDB (`@nozbe/watermelondb`):** Base de datos reactiva basada en SQLite embebido y compilación nativa en C++. Elegida por su arquitectura multihilo (la UI no se congela en consultas pesadas), consultas perezosas (lazy loading) y adaptadores de sincronización nativos.
- **WatermelonDB Schema:** Tablas locales mapeadas (`animals`, `animal_identifiers`, `animal_groups`, `group_memberships`, `inventory_items`, `sync_outbox`, `sync_conflicts`).

### 3. Almacenamiento Seguro y Sesión Offline
- **`expo-secure-store`:** Para cachear de forma cifrada el JWT, el Refresh Token y la clave/hash del PIN local de 4-6 dígitos para desbloqueo sin red.

### 4. Lista Aprobada de Dependencias NPM Iniciales
Se aprueban formalmente las siguientes dependencias para `clients/field-app/`:
- **Core Móvil:** `react`, `react-native`, `expo`, `expo-dev-client`, `expo-status-bar`, `expo-crypto`, `expo-secure-store`
- **Persistencia Local:** `@nozbe/watermelondb`, `@nozbe/with-observables`
- **Navegación & UI:** `@react-navigation/native`, `@react-navigation/stack`, `@react-navigation/bottom-tabs`, `react-native-screens`, `react-native-safe-area-context`, `react-native-gesture-handler`, `react-native-reanimated`, `@expo/vector-icons`
- **Testing & Tooling:** `typescript`, `jest`, `@testing-library/react-native`, `@types/react`

## Alternativas Descartadas
- **SQLite Raw / AsyncStore:** Descartado por falta de reactividad, lentitud en consultas relacionales grandes y bloqueo del hilo principal de UI.
- **PWA (Progressive Web App):** Descartada por limitaciones de almacenamiento en iOS y peor rendimiento nativo en Android gama baja.

## Consecuencias
+ **Rendimiento Nativo Extremo:** La interfaz de campo responde sin lag y no congela la UI al procesar catálogos extensos.
+ **Seguridad Offline:** Tokens e identidades protegidas por almacenamiento cifrado del OS (Keystore/Keychain).
− **Builds Nativas:** Requiere `npx expo run:android` / dev-client debido a las librerías C++ nativas de WatermelonDB.
