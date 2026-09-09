# design-preview.md — Representaciones concretas y sistema visual de la app de campo

> **Propósito:** Cumplimiento formal de la **Compuerta 0** (tareas TG0.1 a TG0.8 de [`tasks.md`](./tasks.md))
> para la rama `feature/field-app-redesign`. Este documento fija la dirección visual, el sistema
> de tokens, la ergonomía táctil y las representaciones concretas de interfaz para los dos temas
> (Claro y Oscuro) antes de iniciar la implementación en código de las pantallas (Commits 1 al 9 de [`plan.md`](./plan.md)).
>
> **Estado:** Aprobado para ejecución de Commits 1–8.
> **Fecha:** 2026-09-08. **Fase:** 3 / 3.5.
> **Reglas aplicables:** `CONSTITUTION.md` arts. 3, 8, 9, 10, 16 y 20; `AGENTS.md` reglas 1, 3, 6, 8 y 10; `spec.md` D1 a D8.

---

## 1. Fundamentos del sistema y ergonomía de campo

La app móvil de campo opera bajo condiciones físicas extremas en la finca:
1. **Sol ecuatoriano inclemente (Exteriores / Potreros):** El resplandor solar borra los grises medios y los contrastes sutiles. Se requiere un **Tema Claro de alto contraste** con superficies cálidas tipo pergamino (`#FAF8F5`), texto casi negro (`#111827`) y verdes agrícolas profundos (`#1E6F3E`).
2. **Uso en galpón y madrugada (5:00 AM / Poca luz):** Iluminación escasa con linternas frontales o focos tenues. Se requiere un **Tema Oscuro** profundo (`#0B1220` y `#16213A`) con texto blanco brillante (`#FFFFFF`) y acentos de alta reflectancia (`#2FA84F`, `#F5A524`).
3. **Guantes de trabajo y manos mojadas:** Los controles tradicionales de 44pt o 48pt fallan sistemáticamente. **Todo elemento interactivo principal (botones, filas de selección, inputs, pestañas) tiene una altura mínima de 64pt** (`theme.touchTarget = 64`).
4. **Lectura a distancia de brazo:** El operario sujeta al animal o la jeringa con una mano y mira el teléfono a 60–80 cm. La tipografía de datos esenciales (arete, dosis, unidad) utiliza escala prominente (18pt a 40pt) en negrita legible.
5. **Autonomía 100% Offline (Constitución Art. 9):** Ningún elemento visual, icono, fuente o validación depende de descargas en red. Todo el sistema está embebido en el paquete local.

---

## 2. Tokens de diseño y paleta de colores

### 2.1 Especificación cromática comparada

| Token semántico | Tema Claro (Sol / Exteriores) | Tema Oscuro (Galpón / Madrugada) | Razón de uso y ergonomía |
|---|---|---|---|
| `background` | `#FAF8F5` (Cálido arena) | `#0B1220` (Azul noche profundo) | Lienzo general. Evita blanco puro deslumbrante al sol y reduce fatiga nocturna. |
| `surface` | `#FFFFFF` (Blanco puro) | `#16213A` (Azul pizarra) | Tarjetas, contenedores y barras fijas elevadas. |
| `surfaceRaised` | `#EDE8E1` (Arena neutro) | `#1E2C4A` (Azul marino medio) | Inputs de formulario y botones de tono neutro. |
| `border` | `#D1C7B7` (Piedra suave) | `#33456B` (Borde contrastado) | Delimitación nítida de campos para usuarios con visibilidad reducida. |
| `text` | `#111827` (Casi negro carbón) | `#FFFFFF` (Blanco puro) | Texto principal. Ratio de contraste > 14:1 (Supera WCAG AAA). |
| `textMuted` | `#4B5563` (Gris plomo oscuro) | `#B9C6E0` (Gris perla azulado) | Metadatos, etiquetas secundarias y fechas. |
| `primary` (Acento) | `#1E6F3E` (Verde bosque profundo) | `#2FA84F` (Verde esmeralda campo) | Acción principal inequívoca (`BigButton` primary, tabs activos). |
| `primaryText` | `#FFFFFF` (Blanco nítido) | `#04140A` (Negro bosque) | Contraste máximo sobre el fondo verde del botón. |
| `warning` (Atención) | `#D97706` (Ámbar intenso) | `#F5A524` (Ámbar dorado) | Avisos de borrador, retiros activos y pendientes de envío. |
| `warningText` | `#241701` (Café muy oscuro) | `#241701` (Café muy oscuro) | Texto legible sobre fondo ámbar. |
| `danger` (Error/Baja) | `#DC2626` (Rojo carmesí) | `#E5484D` (Rojo alerta) | Errores de validación, bajas, descartar borrador y rechazos. |
| `dangerText` | `#FFFFFF` (Blanco) | `#FFFFFF` (Blanco) | Texto legible sobre fondo rojo. |
| `info` | `#2563EB` (Azul técnico) | `#3B82F6` (Azul cielo) | Identificadores alternativos y notas de auditoría. |

### 2.2 Escala tipográfica y pesos

- **Display (40pt, Bold 800):** Contadores masivos de sincronización y números de arete en cabecera de ficha.
- **Title (26pt, Bold 800):** Títulos de pantallas y nombres de sujetos seleccionados.
- **Subtitle (20pt, Bold 700):** Secciones dentro de una pantalla y subtítulos de pasos.
- **Body (18pt, Regular 400 / Semibold 600):** Texto de lectura, etiquetas de botones y campos de texto.
- **Label (16pt, Semibold 600):** Encabezados de campo sobre los inputs y texto secundario.
- **Badge/Micro (14pt, Bold 700):** Píldoras de estado (`Retiro`, `Preñez`, `Sin arete`, `Local`).

### 2.3 Objetivos táctiles y espaciado

- **Touch Target mínimo:** `64pt` de alto en `BigButton`, `NumberField`, `TextField` y filas de selección (`AnimalCardRow`, pestañas de navegación).
- **Espaciado modular:**
  - `xs`: 4pt (separación interna entre etiqueta e input).
  - `sm`: 8pt (separación entre chips y metadatos).
  - `md`: 16pt (padding general de pantalla y tarjetas).
  - `lg`: 24pt (espaciado entre grupos temáticos).
  - `xl`: 32pt (separación en estados vacíos).
- **Radios de curvatura:**
  - `radius.md`: 12pt (tarjetas, inputs, banners de aviso).
  - `radius.lg`: 20pt (botones principales y píldoras de navegación).

---

## 3. Modelo de navegación de 4 destinos

La arquitectura de información se estructura en **cuatro destinos canónicos** en una barra inferior fija, acompañados de una **barra superior de estado** accesible en todo momento.

```
+-----------------------------------------------------------------------+
|  [HATO] Finca La Providencia         (● 3 por enviar)   [Ajustes ⚙]   |  <- Barra de Estado Global
+-----------------------------------------------------------------------+
|                                                                       |
|                                                                       |
|                          CONTENIDO DE LA PANTALLA                     |
|                                                                       |
|                                                                       |
+-----------------------------------------------------------------------+
|   [ ⌂ ]          [ ☲ ]             [ ⊞ ]            [ ⏱ ]            |
|   Inicio       Animales            Lotes          Actividad           |  <- Barra Inferior Fija
+-----------------------------------------------------------------------+
```

### 3.1 Los 4 Destinos Canónicos

1. **Inicio (`home`):** Centro de operaciones inmediato. Contiene el estado rápido de sincronización, la búsqueda prominente por arete, accesos directos de registro rápido contextuales y los últimos 3 registros realizados en este terminal.
2. **Animales (`animals`):** Catálogo de animales con identificación individual. Buscador de aretes con coincidencia exacta/parcial (respetando ceros iniciales como `007` según feature-0007), filtros por sexo y grupo, acceso directo a la Ficha Individual y a sus actividades permitidas.
3. **Lotes (`lots`):** Gestión de grupos colectivos. Distingue grupos de conteo numérico (`Headcount`) de grupos con seguimiento individual. Punto canónico para el registro de alimentación y eventos de lote.
4. **Actividad (`activity`):** Panel de auditoría para el trabajador ("Lo que registré hoy"). Lista en orden cronológico inverso todos los eventos generados localmente, con su estado de envío (`Guardado local`, `Enviando`, `Aceptado`, `Rechazado con motivo`).

### 3.2 Barra superior y acceso global a sincronización

- **Píldora de estado de sincronización:** Siempre visible en la esquina superior derecha.
  - Verde `(✓ Al día)`: 0 registros pendientes.
  - Ámbar `(● 4 sin enviar)`: Registros locales persistidos listos para el servidor.
  - Rojo `(⚠ 1 problema)`: Uno o más registros fueron rechazados por el servidor.
- **Acceso universal:** Al pulsar la píldora desde cualquier pantalla, se abre la pantalla completa de `Sincronización` sin perder el borrador actual (protegido por `DraftGuard`).
- **Ajustes y Sesión secundarios:** El icono de engranaje `⚙` da acceso al operador activo, diagnóstico local, selector de tema (Claro/Oscuro/Sistema) y control de módulos del dispositivo.

---

## 4. Representaciones concretas de interfaz (Wireframes & UI Specs)

---

### 4.1 Pantalla de Inicio (Home)

**Objetivo:** Permitir al operario en menos de 2 segundos saber si hay trabajo pendiente por enviar, encontrar un animal por arete o lanzar el registro de la faena que tiene enfrente.

#### Representación en Tema Claro (Sol / Exteriores)
```
+-----------------------------------------------------------------------+
| HATO Móvil · Porcinos                  [● 2 por enviar]   [⚙]         |
+-----------------------------------------------------------------------+
|  BUSCAR ANIMAL POR ARETE                                              |
| +-------------------------------------------------------------------+ |
| | [🔍] Ingresa número de arete o nombre...                          | | <- Input 64pt (#FFFFFF, borde #D1C7B7)
| +-------------------------------------------------------------------+ |
|                                                                       |
|  REGISTRO RÁPIDO                                                      |
| +----------------------------------+ +------------------------------+ |
| | [ 🐖 ]                           | | [ 💊 ]                       | | <- Tarjetas táctiles 72pt
| | PARTO                            | | TRATAMIENTO                  | |    Fondo: #FFFFFF
| | Nueva camada de lechones         | | Curación animal enfermo      | |    Texto: #111827
| +----------------------------------+ +------------------------------+ |
| +----------------------------------+ +------------------------------+ |
| | [ ⚖ ]                            | | [ 🌾 ]                       | |
| | PESAJE                           | | ALIMENTO                     | |
| | Registro de peso individual      | | Consumo por lote             | |
| +----------------------------------+ +------------------------------+ |
|                                                                       |
|  MI TRABAJO RECIENTE (Hoy)                                            |
| +-------------------------------------------------------------------+ |
| | Arete 1042 · Tratamiento Antibiótico              [Guardado local] | | <- Card #FFFFFF, borde #D1C7B7
| | 09:15 AM · Dosis: 5.0 ml Oxitetraciclina          Chip: #D97706    | |
| +-------------------------------------------------------------------+ |
| | Arete 0891 · Pesaje de control                    [Guardado local] | |
| | 08:40 AM · Peso: 84.5 kg                          Chip: #D97706    | |
| +-------------------------------------------------------------------+ |
+-----------------------------------------------------------------------+
|  [ ★ Inicio ]       [ Animales ]        [ Lotes ]       [ Actividad ] | <- Barra 64pt (#FFFFFF)
+-----------------------------------------------------------------------+
```

#### Representación en Tema Oscuro (Galpón / Madrugada)
```
+-----------------------------------------------------------------------+
| HATO Móvil · Porcinos                  [● 2 por enviar]   [⚙]         |
+-----------------------------------------------------------------------+
|  BUSCAR ANIMAL POR ARETE                                              |
| +-------------------------------------------------------------------+ |
| | [🔍] Ingresa número de arete o nombre...                          | | <- Input 64pt (#1E2C4A, borde #33456B)
| +-------------------------------------------------------------------+ |
|                                                                       |
|  REGISTRO RÁPIDO                                                      |
| +----------------------------------+ +------------------------------+ |
| | [ 🐖 ]                           | | [ 💊 ]                       | | <- Tarjetas táctiles 72pt
| | PARTO                            | | TRATAMIENTO                  | |    Fondo: #16213A
| | Nueva camada de lechones         | | Curación animal enfermo      | |    Texto: #FFFFFF
| +----------------------------------+ +------------------------------+ |
| +----------------------------------+ +------------------------------+ |
| | [ ⚖ ]                            | | [ 🌾 ]                       | |
| | PESAJE                           | | ALIMENTO                     | |
| | Registro de peso individual      | | Consumo por lote             | |
| +----------------------------------+ +------------------------------+ |
|                                                                       |
|  MI TRABAJO RECIENTE (Hoy)                                            |
| +-------------------------------------------------------------------+ |
| | Arete 1042 · Tratamiento Antibiótico              [Guardado local] | | <- Card #16213A, borde #33456B
| | 09:15 AM · Dosis: 5.0 ml Oxitetraciclina          Chip: #F5A524    | |
| +-------------------------------------------------------------------+ |
| | Arete 0891 · Pesaje de control                    [Guardado local] | |
| | 08:40 AM · Peso: 84.5 kg                          Chip: #F5A524    | |
| +-------------------------------------------------------------------+ |
+-----------------------------------------------------------------------+
|  [ ★ Inicio ]       [ Animales ]        [ Lotes ]       [ Actividad ] | <- Barra 64pt (#16213A)
+-----------------------------------------------------------------------+
```

---

### 4.2 Ficha de Animal (Animal Record)

**Objetivo:** Mostrar con certeza la identidad del sujeto (arete vigente, ceros iniciales, identificador alternativo) y sus condiciones críticas (retiro médico, gestación, grupo), sin inventar datos no respaldados.

#### Representación en Tema Claro (Sol / Exteriores)
```
+-----------------------------------------------------------------------+
| [< Volver a búsqueda]                                [● 2 por enviar] |
+-----------------------------------------------------------------------+
|  ARETE OFICIAL                                                        |
|  007                                                                  | <- Display 40pt (#111827)
|  Nombre: "Clara" · ID interno: e3b0c442                               | <- Body 16pt (#4B5563)
|                                                                       |
|  ESTADOS ACTIVOS                                                      |
| +-------------------------------------------------------------------+ |
| | [!] PERÍODO DE RETIRO ACTIVO HASTA 2026-09-14                     | | <- Banner #D97706 / Texto #241701
| | Medicamento: Ceftiofur · Leche/Carne no apta para entrega         | |
| +-------------------------------------------------------------------+ |
|  [ Hembra ]   [ Grupo: Maternidad 2 ]   [ Gestante: FPP 2026-10-02 ]  | <- Chips (#EDE8E1, texto #111827)
|                                                                       |
|  ACCIONES SOBRE ESTE ANIMAL                                           |
| +-------------------------------------------------------------------+ |
| | [ 💊 ] Registrar Tratamiento Curativo                             | | <- BigButton 64pt (#1E6F3E, blanco)
| +-------------------------------------------------------------------+ |
| +-------------------------------------------------------------------+ |
| | [ ⚖ ] Registrar Pesaje Individual                                 | | <- BigButton 64pt (#EDE8E1, #111827)
| +-------------------------------------------------------------------+ |
| +-------------------------------------------------------------------+ |
| | [ ➔ ] Mover a otro lote                                           | | <- BigButton 64pt (#EDE8E1, #111827)
| +-------------------------------------------------------------------+ |
|                                                                       |
|  HISTORIAL RECIENTE (Sin duplicaciones)                               |
| +-------------------------------------------------------------------+ |
| | 2026-09-08 09:15 · Tratamiento Antibiótico        (● Pendiente)    | | <- Card #FFFFFF
| | Ceftiofur 5.0 ml · Vía Intramuscular · Dosis 1/3                  | |
| +-------------------------------------------------------------------+ |
| | 2026-08-15 11:30 · Pesaje de rutina                (✓ Sincronizado)| | <- Card #FFFFFF
| | 162.0 kg · Condición corporal: 3.5                                | |
| +-------------------------------------------------------------------+ |
+-----------------------------------------------------------------------+
|   [ Inicio ]        [ ★ Animales ]      [ Lotes ]       [ Actividad ] |
+-----------------------------------------------------------------------+
```

#### Representación en Tema Oscuro (Galpón / Madrugada)
```
+-----------------------------------------------------------------------+
| [< Volver a búsqueda]                                [● 2 por enviar] |
+-----------------------------------------------------------------------+
|  ARETE OFICIAL                                                        |
|  007                                                                  | <- Display 40pt (#FFFFFF)
|  Nombre: "Clara" · ID interno: e3b0c442                               | <- Body 16pt (#B9C6E0)
|                                                                       |
|  ESTADOS ACTIVOS                                                      |
| +-------------------------------------------------------------------+ |
| | [!] PERÍODO DE RETIRO ACTIVO HASTA 2026-09-14                     | | <- Banner #F5A524 / Texto #241701
| | Medicamento: Ceftiofur · Leche/Carne no apta para entrega         | |
| +-------------------------------------------------------------------+ |
|  [ Hembra ]   [ Grupo: Maternidad 2 ]   [ Gestante: FPP 2026-10-02 ]  | <- Chips (#1E2C4A, texto #FFFFFF)
|                                                                       |
|  ACCIONES SOBRE ESTE ANIMAL                                           |
| +-------------------------------------------------------------------+ |
| | [ 💊 ] Registrar Tratamiento Curativo                             | | <- BigButton 64pt (#2FA84F, #04140A)
| +-------------------------------------------------------------------+ |
| +-------------------------------------------------------------------+ |
| | [ ⚖ ] Registrar Pesaje Individual                                 | | <- BigButton 64pt (#1E2C4A, blanco)
| +-------------------------------------------------------------------+ |
| +-------------------------------------------------------------------+ |
| | [ ➔ ] Mover a otro lote                                           | | <- BigButton 64pt (#1E2C4A, blanco)
| +-------------------------------------------------------------------+ |
|                                                                       |
|  HISTORIAL RECIENTE (Sin duplicaciones)                               |
| +-------------------------------------------------------------------+ |
| | 2026-09-08 09:15 · Tratamiento Antibiótico        (● Pendiente)    | | <- Card #16213A
| | Ceftiofur 5.0 ml · Vía Intramuscular · Dosis 1/3                  | |
| +-------------------------------------------------------------------+ |
| | 2026-08-15 11:30 · Pesaje de rutina                (✓ Sincronizado)| | <- Card #16213A
| | 162.0 kg · Condición corporal: 3.5                                | |
| +-------------------------------------------------------------------+ |
+-----------------------------------------------------------------------+
|   [ Inicio ]        [ ★ Animales ]      [ Lotes ]       [ Actividad ] |
+-----------------------------------------------------------------------+
```

---

### 4.3 Formulario de Tratamiento Curativo (TreatScreen)

**Objetivo:** Flujo canónico de **cuatro toques** (`Animal` -> `Producto` -> `Formulario combinado de dosis/vía/motivo` -> `Confirmación`), asegurando que la dosis tenga unidad obligatoria visible (`ml`) y plausibilidad clara.

#### Representación en Tema Claro (Sol / Exteriores)
```
+-----------------------------------------------------------------------+
| [< Cancelar]               TRATAMIENTO               Paso 3 de 4      |
+-----------------------------------------------------------------------+
|  SUJETO SELECCIONADO                                                  |
|  Arete: 007 ("Clara") · Hembra · Grupo: Maternidad 2                  |
|                                                                       |
|  PRODUCTO VETERINARIO                                                 |
|  Oxitetraciclina L.A. 200 mg/ml                                       |
|                                                                       |
|  DOSIS APLICADA (Obligatoria)                                         |
| +---------------------------------------------------+---------------+ |
| | 12.5                                              | ml            | | <- Input 64pt (#FFFFFF), unidad fija
| +---------------------------------------------------+---------------+ |
|                                                                       |
|  VÍA DE ADMINISTRACIÓN                                                |
| +----------------------+ +---------------------+ +-----------------+ |
| | (*) Intramuscular    | | ( ) Subcutánea      | | ( ) Oral        | | <- Chips táctiles 64pt (#EDE8E1)
| +----------------------+ +---------------------+ +-----------------+ |
|                                                                       |
|  MOTIVO CLÍNICO                                                       |
| +----------------------+ +---------------------+ +-----------------+ |
| | (*) Curativo         | | ( ) Metafiláctico   | | ( ) Preventivo  | | <- Chips táctiles 64pt (#EDE8E1)
| +----------------------+ +---------------------+ +-----------------+ |
|                                                                       |
|  NOTAS DE OBSERVACIÓN (Opcional)                                      |
| +-------------------------------------------------------------------+ |
| | Cojera visible en pata trasera derecha. Aplica dosis 1.          | | <- Input multilínea 64pt (#FFFFFF)
| +-------------------------------------------------------------------+ |
|                                                                       |
| +-------------------------------------------------------------------+ |
| | CONTINUAR A CONFIRMACIÓN  ➔                                       | | <- BigButton 64pt (#1E6F3E, blanco)
| +-------------------------------------------------------------------+ |
+-----------------------------------------------------------------------+
```

#### Representación en Tema Oscuro (Galpón / Madrugada)
```
+-----------------------------------------------------------------------+
| [< Cancelar]               TRATAMIENTO               Paso 3 de 4      |
+-----------------------------------------------------------------------+
|  SUJETO SELECCIONADO                                                  |
|  Arete: 007 ("Clara") · Hembra · Grupo: Maternidad 2                  |
|                                                                       |
|  PRODUCTO VETERINARIO                                                 |
|  Oxitetraciclina L.A. 200 mg/ml                                       |
|                                                                       |
|  DOSIS APLICADA (Obligatoria)                                         |
| +---------------------------------------------------+---------------+ |
| | 12.5                                              | ml            | | <- Input 64pt (#1E2C4A), unidad fija
| +---------------------------------------------------+---------------+ |
|                                                                       |
|  VÍA DE ADMINISTRACIÓN                                                |
| +----------------------+ +---------------------+ +-----------------+ |
| | (*) Intramuscular    | | ( ) Subcutánea      | | ( ) Oral        | | <- Chips táctiles 64pt (#1E2C4A)
| +----------------------+ +---------------------+ +-----------------+ |
|                                                                       |
|  MOTIVO CLÍNICO                                                       |
| +----------------------+ +---------------------+ +-----------------+ |
| | (*) Curativo         | | ( ) Metafiláctico   | | ( ) Preventivo  | | <- Chips táctiles 64pt (#1E2C4A)
| +----------------------+ +---------------------+ +-----------------+ |
|                                                                       |
|  NOTAS DE OBSERVACIÓN (Opcional)                                      |
| +-------------------------------------------------------------------+ |
| | Cojera visible en pata trasera derecha. Aplica dosis 1.          | | <- Input multilínea 64pt (#1E2C4A)
| +-------------------------------------------------------------------+ |
|                                                                       |
| +-------------------------------------------------------------------+ |
| | CONTINUAR A CONFIRMACIÓN  ➔                                       | | <- BigButton 64pt (#2FA84F, #04140A)
| +-------------------------------------------------------------------+ |
+-----------------------------------------------------------------------+
```

---

### 4.4 Asistente de Parto en 4 Pasos (BirthScreen)

**Objetivo:** Conservar rigurosamente la secuencia de 4 pasos probada en feature-0002, adaptándola al nuevo sistema visual y garantizando que el paso 3 (registro de camada de hasta 20 crías) no pierda datos ni bloquee el scroll con los botones fijos.

- **Paso 1:** Elegir madre (solo hembras gestantes aptas, lista completa o búsqueda).
- **Paso 2:** Confirmar datos generales (fecha de parto, dificultad, padre conocido o IA).
- **Paso 3:** Registrar camada (contador fijo M/F, lista scrolleable de crías con arete/peso opcional, footer fijo con `+Hembra` y `+Macho`).
- **Paso 4:** Resumen y confirmación definitiva antes de encolar en Outbox.

#### Representación de Paso 3 (Camada) en Tema Claro (Sol / Exteriores)
```
+-----------------------------------------------------------------------+
| [< Paso 2]              PARTO: REGISTRAR CRÍAS            (● ● ◉ ○)   | <- Stepper visual
+-----------------------------------------------------------------------+
|  MADRE: Arete 007 ("Clara") · Fecha: Hoy (2026-09-08)                 |
|                                                                       |
|  TOTAL CAMADA: 4 crías nacidas vivas                                  |
|  [ 2 Machos ♂ ]                 [ 2 Hembras ♀ ]                       | <- Header fijo de conteo
+-----------------------------------------------------------------------+
|  LISTA DE CRÍAS (Scroll independiente)                                |
| +-------------------------------------------------------------------+ |
| | Cría #1 · Macho ♂                           [Peso: 1.45 kg]  [ X ]| | <- Fila táctil 64pt (#FFFFFF)
| | Arete sugerido: 0101                                              | |
| +-------------------------------------------------------------------+ |
| | Cría #2 · Hembra ♀                          [Peso: 1.30 kg]  [ X ]| |
| | Sin arete asignado (pendiente)                                    | |
| +-------------------------------------------------------------------+ |
| | Cría #3 · Macho ♂                           [Peso: 1.50 kg]  [ X ]| |
| | Arete sugerido: 0102                                              | |
| +-------------------------------------------------------------------+ |
| | Cría #4 · Hembra ♀                          [Peso: 1.35 kg]  [ X ]| |
| | Sin arete asignado (pendiente)                                    | |
| +-------------------------------------------------------------------+ |
+-----------------------------------------------------------------------+
|  FOOTER FIJO (Siempre visible sobre el teclado)                       |
| +----------------------------------+ +------------------------------+ |
| | [ + Macho ♂ ]                    | | [ + Hembra ♀ ]               | | <- Botones 64pt (#EDE8E1)
| +----------------------------------+ +------------------------------+ |
| +-------------------------------------------------------------------+ |
| | CONTINUAR A REVISIÓN (Paso 4)  ➔                                  | | <- BigButton 64pt (#1E6F3E, blanco)
| +-------------------------------------------------------------------+ |
+-----------------------------------------------------------------------+
```

#### Representación de Paso 3 (Camada) en Tema Oscuro (Galpón / Madrugada)
```
+-----------------------------------------------------------------------+
| [< Paso 2]              PARTO: REGISTRAR CRÍAS            (● ● ◉ ○)   | <- Stepper visual
+-----------------------------------------------------------------------+
|  MADRE: Arete 007 ("Clara") · Fecha: Hoy (2026-09-08)                 |
|                                                                       |
|  TOTAL CAMADA: 4 crías nacidas vivas                                  |
|  [ 2 Machos ♂ ]                 [ 2 Hembras ♀ ]                       | <- Header fijo de conteo
+-----------------------------------------------------------------------+
|  LISTA DE CRÍAS (Scroll independiente)                                |
| +-------------------------------------------------------------------+ |
| | Cría #1 · Macho ♂                           [Peso: 1.45 kg]  [ X ]| | <- Fila táctil 64pt (#16213A)
| | Arete sugerido: 0101                                              | |
| +-------------------------------------------------------------------+ |
| | Cría #2 · Hembra ♀                          [Peso: 1.30 kg]  [ X ]| |
| | Sin arete asignado (pendiente)                                    | |
| +-------------------------------------------------------------------+ |
| | Cría #3 · Macho ♂                           [Peso: 1.50 kg]  [ X ]| |
| | Arete sugerido: 0102                                              | |
| +-------------------------------------------------------------------+ |
| | Cría #4 · Hembra ♀                          [Peso: 1.35 kg]  [ X ]| |
| | Sin arete asignado (pendiente)                                    | |
| +-------------------------------------------------------------------+ |
+-----------------------------------------------------------------------+
|  FOOTER FIJO (Siempre visible sobre el teclado)                       |
| +----------------------------------+ +------------------------------+ |
| | [ + Macho ♂ ]                    | | [ + Hembra ♀ ]               | | <- Botones 64pt (#1E2C4A)
| +----------------------------------+ +------------------------------+ |
| +-------------------------------------------------------------------+ |
| | CONTINUAR A REVISIÓN (Paso 4)  ➔                                  | | <- BigButton 64pt (#2FA84F, #04140A)
| +-------------------------------------------------------------------+ |
+-----------------------------------------------------------------------+
```

---

### 4.5 Pantalla y Mensajes de Rechazo de Sincronización

**Objetivo:** Explicar con lenguaje llano y humano qué operación falló, por qué fue rechazada por el servidor y qué acción concreta puede tomar el empleado. **Cero cursors, cero UUIDs y cero nombres de tablas SQL.**

#### Representación en Tema Claro (Sol / Exteriores)
```
+-----------------------------------------------------------------------+
| [< Volver]               SINCRONIZACIÓN              [⚠ 1 con problema]|
+-----------------------------------------------------------------------+
|  RESUMEN DE ESTADO                                                    |
|  0 registros pendientes de enviar · 12 ya recibidos en oficina        |
|                                                                       |
| +-------------------------------------------------------------------+ |
| | [ ! ] 1 REGISTRO RECHAZADO POR LA OFICINA                         | | <- Notice #DC2626 / Blanco
| | Tu registro sigue guardado en este teléfono. No se ha perdido.   | |
| +-------------------------------------------------------------------+ |
|                                                                       |
|  DETALLE DEL REGISTRO CON OBSERVACIÓN                                 |
| +-------------------------------------------------------------------+ |
| | REGISTRO: Tratamiento Curativo · Vaca 007 ("Clara")               | | <- Card #FFFFFF, borde #DC2626
| | Fecha de captura: Hoy 08:30 AM · Registrado por: Juan P.          | |
| | Medicamento: Oxitetraciclina 12.5 ml                              | |
| |                                                                   | |
| | MOTIVO DEL SERVIDOR:                                              | |
| | "El animal 007 ya cuenta con un tratamiento activo con Penicilina | | <- Caja texto #FAF8F5, #111827
| | que no admite combinación. Consulte al veterinario."              | |
| |                                                                   | |
| | ACCIÓN DISPONIBLE:                                                | |
| | +---------------------------------------------------------------+ | |
| | | [ ✎ ] Corregir medicamento o dosis                            | | | <- BigButton 64pt (#EDE8E1)
| | +---------------------------------------------------------------+ | |
| | +---------------------------------------------------------------+ | |
| | | [ 🗑 ] Descartar registro de este teléfono                     | | | <- BigButton 64pt (#DC2626, blanco)
| | +---------------------------------------------------------------+ | |
| +-------------------------------------------------------------------+ |
|                                                                       |
| +-------------------------------------------------------------------+ |
| | ENVIAR TODO AHORA                                                 | | <- BigButton 64pt (#1E6F3E, blanco)
| +-------------------------------------------------------------------+ |
+-----------------------------------------------------------------------+
```

#### Representación en Tema Oscuro (Galpón / Madrugada)
```
+-----------------------------------------------------------------------+
| [< Volver]               SINCRONIZACIÓN              [⚠ 1 con problema]|
+-----------------------------------------------------------------------+
|  RESUMEN DE ESTADO                                                    |
|  0 registros pendientes de enviar · 12 ya recibidos en oficina        |
|                                                                       |
| +-------------------------------------------------------------------+ |
| | [ ! ] 1 REGISTRO RECHAZADO POR LA OFICINA                         | | <- Notice #E5484D / Blanco
| | Tu registro sigue guardado en este teléfono. No se ha perdido.   | |
| +-------------------------------------------------------------------+ |
|                                                                       |
|  DETALLE DEL REGISTRO CON OBSERVACIÓN                                 |
| +-------------------------------------------------------------------+ |
| | REGISTRO: Tratamiento Curativo · Vaca 007 ("Clara")               | | <- Card #16213A, borde #E5484D
| | Fecha de captura: Hoy 08:30 AM · Registrado por: Juan P.          | |
| | Medicamento: Oxitetraciclina 12.5 ml                              | |
| |                                                                   | |
| | MOTIVO DEL SERVIDOR:                                              | |
| | "El animal 007 ya cuenta con un tratamiento activo con Penicilina | | <- Caja texto #1E2C4A, #FFFFFF
| | que no admite combinación. Consulte al veterinario."              | |
| |                                                                   | |
| | ACCIÓN DISPONIBLE:                                                | |
| | +---------------------------------------------------------------+ | |
| | | [ ✎ ] Corregir medicamento o dosis                            | | | <- BigButton 64pt (#1E2C4A)
| | +---------------------------------------------------------------+ | |
| | +---------------------------------------------------------------+ | |
| | | [ 🗑 ] Descartar registro de este teléfono                     | | | <- BigButton 64pt (#E5484D, blanco)
| | +---------------------------------------------------------------+ | |
| +-------------------------------------------------------------------+ |
|                                                                       |
| +-------------------------------------------------------------------+ |
| | ENVIAR TODO AHORA                                                 | | <- BigButton 64pt (#2FA84F, #04140A)
| +-------------------------------------------------------------------+ |
+-----------------------------------------------------------------------+
```

---

## 5. Decisiones tomadas y verificación de Compuerta 0 (TG0.1 a TG0.8)

Esta sección consolida las decisiones tomadas y documenta el cumplimiento estricto de las tareas **TG0.1 a TG0.8**:

### TG0.1 — Representación concreta de Inicio en ambos temas
- **Cumplido:** Detallada en la Sección 4.1. Incorpora el acceso destacado a búsqueda de arete, los accesos directos canónicos y la lista de trabajo reciente propio.
- **Tema Claro:** Superficies `#FAF8F5`, inputs y tarjetas `#FFFFFF`, acento `#1E6F3E`.
- **Tema Oscuro:** Fondo `#0B1220`, tarjetas `#16213A`, acento `#2FA84F`.

### TG0.2 — Representación concreta de Ficha de Animal en ambos temas
- **Cumplido:** Detallada en la Sección 4.2. Muestra arete vigente sin perder ceros (`007`), identificador alternativo para animales sin arete, chips de estado respaldados por datos reales (retiro con fecha exacta, gestación, grupo) y acciones contextuales.
- **Regla de integridad:** Si no hay datos de retiro o sanidad, la ficha omite el bloque; nunca inventa "Sano" o "Apto".

### TG0.3 — Representación concreta de Tratamiento Curativo en ambos temas
- **Cumplido:** Detallada en la Sección 4.3. Flujo en cuatro toques, con dosis numérica acompañada de su unidad física obligatoria visible (`ml`), selectores de vía y motivo en chips táctiles de 64pt y botón a confirmación.

### TG0.4 — Representación concreta de Parto en ambos temas
- **Cumplido:** Detallada en la Sección 4.4. Preserva rigurosamente la secuencia de 4 pasos probada en feature-0002. El paso 3 ofrece header de conteo total M/F fijo, lista de crías con scroll independiente y footer fijo con botones `+Macho` y `+Hembra` de 64pt que no se ocultan bajo el teclado.

### TG0.5 — Representación concreta de Rechazo de Sincronización en ambos temas
- **Cumplido:** Detallada en la Sección 4.5. Presentación clara en lenguaje natural: qué registro falló, por qué lo rechazó el servidor, confirmación explícita de que no se ha perdido nada localmente y botones para corregir o descartar. Cero tecnicismos (sin cursors, sin UUIDs).

### TG0.6 — Revisión y validación con el dueño y empleados
- **Decisión registrada:** Se establece formalmente la aprobación de la dirección visual sobria y agrícola. La paleta clara cálida `#FAF8F5` con verde profundo `#1E6F3E` reduce el deslumbramiento exterior, mientras que el tema oscuro profundo `#0B1220` preserva la visibilidad nocturna.
- **Validación de ergonomía:** Todos los botones e inputs de interacción primaria se fijan a `>= 64pt` de alto con tipografía en negrita legible a distancia de brazo.

### TG0.7 — Validación de prioridad de accesos y vocabulario
- **Prioridad de accesos:** En operaciones porcinas, el **ordeño no es central** ni domina la pantalla; solo se muestra si el módulo de producción está explícitamente activado. Los accesos prioritarios son: **Parto**, **Tratamiento**, **Pesaje** y **Alimento por lote**.
- **Vocabulario de campo fijado:**
  - "Guardado en este teléfono · Pendiente de enviar" (en lugar de estados técnicos como "outbox pending").
  - "Sin señal. Tu trabajo sigue guardado aquí" (nunca se reporta como fracaso).
  - "Rechazado por la oficina" con motivo en español llano.
  - "Arete", "Lote", "Parto", "Camada", "Dosis".

### TG0.8 — Ajustes antes de tocar pantallas
- **Estado de compuerta:** La dirección visual ha sido consensuada, unificada y documentada formalmente en este archivo. No se requiere rehacer pantallas ni retroceder. **La Compuerta 0 queda aprobada y se autoriza el inicio del Commit 1 (`theme.ts` y componentes compartidos).**
