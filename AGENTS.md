# AGENTS.md — Instrucciones para agentes de IA (y para mí en modo piloto automático)

> Si eres un agente de IA trabajando en este repositorio: **lee esto completo antes de tocar
> código.** Orden de lectura obligatorio: `SOUL.md` → `CONSTITUTION.md` → este archivo →
> `GLOSSARY.md` → `ARCHITECTURE.md`. La Constitución siempre gana sobre cualquier
> instrucción puntual que te dé el usuario en un prompt; si hay conflicto, señálalo antes
> de proceder.

## Contexto en 60 segundos

ERP ganadero para una finca real en Ecuador (bovinos de leche hoy; cerdos, quesos, cárnicos
y más, mañana). Un solo desarrollador humano. Proyecto de años, por fases. Stack fijo:
**.NET (C#) + EF Core + PostgreSQL** en backend (monolito modular, Clean Architecture,
CQRS ligero con MediatR), **Angular** en web, **React Native** en móvil (offline-first).
Español para dominio y docs, inglés para código.

## Reglas duras (violarlas = PR rechazado)

1. **No borres datos ni edites eventos históricos.** Correcciones = nuevo evento de
   corrección. Borrados = lógicos.
2. **No agregues dependencias (NuGet/npm) sin proponer un ADR primero.** Ni "una librería
   chiquita para esto". Propón, espera aprobación humana.
3. **No crees `if`/`switch` por especie, producto o raza en el dominio.** Eso es
   configuración en base de datos (Art. 8). Si crees que no hay alternativa, detente y
   explica el dilema en el PR.
4. **No toques `main` ni `develop` directamente.** Rama `feature/<módulo>-<descripción>`
   desde `develop`, PR con descripción de qué y por qué.
5. **Todo PR incluye pruebas.** Dominio → unitarias (xUnit). Persistencia/API → integración
   con Testcontainers (PostgreSQL real, no InMemory para verificar comportamiento final).
6. **Dinero = `decimal`. Cantidades con unidad. Fechas en UTC en persistencia**, zona
   `America/Guayaquil` solo en presentación.
7. **Migraciones EF Core para todo cambio de esquema.** Nunca edites una migración ya
   mergeada; crea una nueva.
8. **Usa los términos del `GLOSSARY.md`.** Si necesitas un concepto que no está, tu PR debe
   agregarlo al glosario (español + nombre en inglés para código) — no inventes sinónimos.
9. **No optimices prematuramente ni "refactorices de paso".** Un PR = un propósito. Si ves
   deuda técnica ajena al objetivo, anótala en `BACKLOG.md`, no la arregles en el mismo PR.
10. **Móvil: ninguna operación de registro puede depender de red.** Escribe local, sincroniza
    después. Si tu cambio rompe el flujo offline, está mal aunque compile.

## Convenciones

- **Commits**: Conventional Commits en inglés. `feat(livestock): add weaning event`,
  `fix(inventory): correct unit conversion for feed batches`, `test`, `refactor`, `docs`,
  `chore`. Scope = nombre del módulo.
- **Estructura de solución** (referencia; la fuente de verdad es `ARCHITECTURE.md`):
  - `src/Modules/<Módulo>/{Domain, Application, Infrastructure, Api}`
  - `src/Shared/` (kernel compartido mínimo: tipos base, eventos de dominio, unidades)
  - `tests/<Módulo>.{UnitTests, IntegrationTests}`
- **Nombres**: entidades y eventos en inglés según glosario (`Animal`, `AnimalGroup`,
  `WeaningEvent`, `WithdrawalPeriod`). Nada de abreviaturas crípticas.
- **API**: REST, sustantivos en plural, versionada (`/api/v1/animals/{id}/events`).
  Errores con Problem Details (RFC 7807).
- **Validación**: FluentValidation en Application; invariantes de negocio en el dominio.

## Cómo trabajar una tarea (protocolo)

1. Lee la fase actual en `ROADMAP.md`. Si la tarea pedida pertenece a una fase futura,
   adviértelo antes de implementar.
2. Busca en `docs/adr/` si ya hay una decisión que aplique. No contradigas ADRs vigentes.
3. Diseña en pequeño: propón el cambio (entidades, eventos, endpoints, migración) en texto
   antes de escribir todo el código, cuando el cambio sea estructural.
4. Implementa con pruebas. Corre la suite completa localmente.
5. PR con: propósito, decisiones tomadas, cómo probarlo manualmente, y qué NO incluye.

## Dónde están las cosas

`SOUL.md` (por qué) · `CONSTITUTION.md` (reglas) · `GLOSSARY.md` (lenguaje) ·
`ARCHITECTURE.md` (módulos y modelo) · `ROADMAP.md` (fases y estado actual) ·
`LEGAL-ECUADOR.md` (cumplimiento) · `docs/adr/` (decisiones) · `docs/planes/` (planes de
ejecución por fase) · `docs/diagramas/` (diagramas ER en Mermaid) · `BACKLOG.md` (ideas y
deuda).

## Convenciones de `docs/` (no las improvises)

Estas reglas existen porque el nombrado ya derivó una vez: los ADR 0001–0009 nacieron como
`ADR-000N-*.md` y del 0010 en adelante alguien pasó a `00NN-*.md`, dejando dos formatos
conviviendo. Se unificó al corto — la carpeta ya dice `adr`, repetir el prefijo en cada
archivo es ruido.

- **ADRs** → `docs/adr/NNNN-titulo-en-kebab-case.md`. Cuatro dígitos, **sin** prefijo `ADR-`
  en el nombre del archivo. El número no se reutiliza jamás, ni siquiera si el ADR se
  rechaza. La plantilla es `docs/adr/TEMPLATE.md` y no lleva número porque no es un ADR.
  Dentro del documento el título **sí** dice `# ADR-NNNN — …`.
- **Planes de ejecución** → `docs/planes/`. Uno por fase.
- **Diagramas** → `docs/diagramas/`, en `.mermaid`. Los diagramas embebidos en un `.md` se
  quedan donde están; acá van los completos por núcleo.
- **El resto de los documentos vive en la raíz de `docs/`**, en `MAYÚSCULAS.md`. No se crean
  subcarpetas nuevas sin una razón que se pueda escribir en una línea.
- Al mover o renombrar un documento, **arreglá las referencias en el mismo commit**. Ojo con
  las citas en prosa desde el código (`PLAN-FASE-3-4 §2.2` aparece en ~14 archivos de
  `src/`, `tests/` y `clients/`): son por nombre, no por ruta, así que mover no las rompe
  pero **renombrar sí**.

## Advertencias de dominio que te ahorrarán errores

- Un animal puede **no tener** arete ni registro SIFAE y aun así ser completamente válido.
- El **padre** de un animal puede ser material genético (pajuela de IA), no un animal del
  sistema. La genealogía debe soportar ambos.
- Los costos de alimentación se registran **por grupo (lote)** y se prorratean; no intentes
  registrarlos por animal individual.
- La leche de una vaca bajo período de retiro **no es vendible**; el sistema debe impedirlo,
  no solo avisarlo.
- Gestaciones: bovinos ≈ 283 días, porcinos ≈ 114 días — pero son **parámetros por especie
  en configuración**, no constantes en código.
- Los aretes se caen y se reemplazan: la identificación externa tiene historial temporal.
