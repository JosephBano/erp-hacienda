# CONSTITUTION.md — Constitución del Proyecto HATO

> Reglas **no negociables**. Ni yo en un mal día, ni un agente de IA con una "gran idea",
> pueden violarlas. Cambiar un artículo exige un ADR aprobado conscientemente (dormir una
> noche sobre la decisión) — nunca un cambio silencioso en un commit.

---

## Título I — De los datos

**Art. 1 — Los datos jamás se destruyen.**
Nada se borra físicamente. Todo borrado es lógico (`deleted_at` / estado). Los eventos del
historial de un animal son **inmutables**: un error se corrige con un evento de corrección
que referencia al original, nunca editando el pasado.

**Art. 2 — Respaldos probados o nada.**
PostgreSQL tiene backup automático diario, con copia fuera del servidor (offsite). Una vez
al mes, **se restaura un backup en un entorno limpio y se verifica**. Un backup no probado
no existe. Ninguna fase se declara "en producción" sin esto funcionando.

**Art. 3 — Identidad interna soberana.**
Todo animal (y toda entidad) nace con un **UUID interno generado por el sistema**, desde el
día uno, exista o no arete, registro SIFAE o nombre. Los identificadores externos (código de
manejo, arete oficial SIFAE, RFID) son *identificaciones adjuntas* con historial de
asignación. **Prohibido** usar un identificador externo como clave primaria o asumir que
existe.

**Art. 4 — Todo cambio relevante es un evento con fecha, autor y costo (si aplica).**
El historial de la finca es una secuencia de eventos tipados. Si una acción no quedó
registrada como evento, para el sistema no ocurrió.

## Título II — De la arquitectura

**Art. 5 — Monolito modular. No microservicios.**
Un solo deploy, módulos desacoplados por interfaces y eventos de dominio internos. Extraer
un módulo a servicio independiente exige evidencia real de necesidad + ADR. (Ver ADR-0001.)

**Art. 6 — Los módulos no se conocen por dentro.**
Un módulo solo habla con otro mediante sus contratos públicos (interfaces/eventos de
dominio). Prohibido que `Ventas` consulte tablas de `Ganadería` directamente. La
contabilidad es el módulo central receptor: todos le emiten asientos; ella no conoce a nadie.

**Art. 7 — REST primero.**
La API es REST (o Minimal APIs) versionada. GraphQL solo entraría por ADR cuando existan
múltiples clientes con necesidades de datos demostradamente distintas. (Ver ADR-0002.)

**Art. 8 — Lo específico es dato, no código.**
Especies, razas, tipos de evento, categorías, productos, recetas de transformación (BOM),
roles: **configuración en base de datos**. Agregar "equinos" o "queso maduro" no puede
requerir compilar. Si un requerimiento nuevo pide un `if (especie == "cerdo")` en el core,
la respuesta es rediseñar el dato, no escribir el `if`.

**Art. 9 — La app móvil funciona sin internet. Siempre.**
Offline-first no es una feature: es un requisito constitucional. Registro local (SQLite),
sincronización posterior, IDs generados en cliente (UUID), resolución de conflictos
definida. Una pantalla móvil que requiera conexión para *registrar* está inconstitucional.
(Ver ADR-0005.)

**Art. 10 — El dinero se calcula con `decimal`, jamás con `float`/`double`.**
Y toda cantidad física lleva su unidad explícita (litros, kg, unidades). Los redondeos
contables se definen una vez, en un solo lugar, y se testean.

## Título III — Del proceso

**Art. 11 — Cada fase termina en producción real.**
Una fase se cierra cuando **alguien en la finca la usa de verdad**, no cuando el código está
"listo". Si una fase supera ~3 meses sin uso real, se recorta alcance hasta que lo tenga.

**Art. 12 — Sin pruebas no hay merge.**
Lógica de dominio → pruebas unitarias. Persistencia y API → pruebas de integración
(Testcontainers contra PostgreSQL real). El CI corre todo en cada PR; CI en rojo bloquea
el merge a `develop` y `main`. Sin excepciones "porque es un cambio chiquito".

**Art. 13 — GitFlow disciplinado.**
`main` (producción, solo releases etiquetadas) ← `develop` ← `feature/*`, `release/*`,
`hotfix/*`. Commits con formato Conventional Commits. Nadie — humano o agente — commitea
directo a `main` o `develop`.

**Art. 14 — Las decisiones de arquitectura se escriben (ADR).**
Toda decisión estructural (dependencia nueva, cambio de patrón, esquema transversal) queda
en `docs/adr/`. Una decisión no escrita no existe y puede revertirse sin culpa.

**Art. 15 — La base de datos solo cambia por migraciones.**
Migraciones versionadas (EF Core), reproducibles desde cero, jamás editadas después de
mergeadas. El esquema en producción nunca se toca a mano.

## Título IV — De la tecnología y el alcance

**Art. 16 — El stack es el que domino.**
Backend: **.NET + EF Core + PostgreSQL**. Web: **Angular**. Móvil: **React Native**.
Cambiar cualquiera exige ADR con justificación de finca, no de moda. (Ver ADR-0003.)

**Art. 17 — Nada de tecnología por curiosidad en `main`.**
IA, blockchain, colas, caches distribuidos: entran cuando una necesidad real de la finca lo
pida y su fase del roadmap llegue. La curiosidad vive en ramas `experiment/*` que pueden
morir sin pena. En particular: **blockchain queda diferido** a un anclaje de hashes para
certificación de trazabilidad, fase 6+, y solo si aparece un tercero que lo exija.

**Art. 18 — No reconstruir lo que el Estado o terceros ya resuelven.**
Facturación electrónica SRI: primero mediante proveedor autorizado con API; integración
directa solo en fase tardía y con ADR. Contabilidad tributaria formal: el sistema genera
datos limpios para el contador; no pretende reemplazarlo en fases tempranas.

**Art. 19 — Cumplimiento legal ecuatoriano por diseño.**
El sistema debe poder responder lo que Agrocalidad/SIFAE, ARCSA, SRI, IESS y la LOPDP
pidan (ver `LEGAL-ECUADOR.md`). Los períodos de retiro de medicamentos generan alertas y
bloqueos de venta de leche/carne: esto es requisito de producto, no un "nice to have".

**Art. 20 — Idiomas.**
El dominio se piensa y documenta en **español** (glosario obligatorio: `GLOSSARY.md`).
El código (clases, métodos, variables) se escribe en **inglés**, usando las traducciones
canónicas del glosario. Un término nuevo entra primero al glosario, luego al código.

---

*Firmado en la fundación del proyecto. Que estas reglas me protejan de mi peor enemigo:
yo mismo con una idea nueva a las 2 de la mañana.*
