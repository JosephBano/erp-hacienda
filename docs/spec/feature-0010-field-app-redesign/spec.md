# spec.md — Rediseño integral de la app móvil de campo

> **Estado:** alcance ampliado por el dueño, **sin implementación**. La carpeta tiene los
> cuatro documentos de la convención: `spec.md` (qué se decidió), [`plan.md`](./plan.md) (en
> qué orden y en qué commits), [`tasks.md`](./tasks.md) (el desglose con casillas) y
> [`test-e2e.md`](./test-e2e.md) (la verificación manual).
>
> **Origen:** este documento nació dentro de
> [feature-0006](../feature-0006-field-app-interaction-reliability/spec.md) y se separó de
> él el 2026-09-07. El 0006 mezclaba dos entregables de tamaño incompatible: la corrección
> puntual de scroll, teclado y gestos que los empleados reportaron —angosta, localizada y
> entregable de inmediato— y un rediseño completo de la aplicación que depende de que
> aterricen 0004, 0005, 0007, 0008 y 0009. Dejarlos juntos ponía la queja real de los
> empleados detrás de cinco features. El 0006 conserva la corrección (su carpeta ya se
> llamaba `interaction-reliability`, que la describe con exactitud) y este 0010 recibe el
> rediseño.
>
> **Posición en la serie:** es el spec final de integración de la experiencia móvil,
> posterior a los resultados funcionales de 0004, 0005, 0007, 0008 y 0009 que afecten a la
> app. El número identifica el trabajo, no fija su orden de implementación; el orden real
> está en [el índice de la serie](../README.md).

- **Rama documental:** `feature/field-app-production-specs`, base `0faf483`.
- **Fecha:** 2026-09-07. **Fase:** 3 / 3.5, estabilización en producción.
- **Reglas:** AGENTS.md 1, 2, 3, 5, 7, 9 y 10; Constitución arts. 3, 8, 9 y 11.
- **Referencias:** [ADR-0005](../../adr/0005-offline-first-movil.md),
  [asistente de parto existente](../feature-0002-field-app-parto-redesign/spec.md).

## 1. Encargo y límites de lo conocido

El dueño amplió el encargo original de interacción: «que este sea el spec final» y «un
rediseño general de la app». El resultado buscado es una experiencia completa y coherente:
entender dónde estoy, encontrar al animal por arete, registrar una actividad con confianza
y saber qué quedó guardado en el teléfono y qué recibió el servidor.

El rediseño puede reorganizar navegación, composición de pantallas, componentes, tipografía,
colores y transiciones. El trabajo de sincronización, validaciones y permisos aporta los
estados reales que esta interfaz debe mostrar. **Sus correcciones urgentes no esperan al
rediseño para llegar a producción**, incluidas las de [0006](../feature-0006-field-app-interaction-reliability/spec.md).

La investigación de código que sustenta este documento —estructuras de pantalla, tema
central, navegación por claves de pestaña— está en
[0006 sec. 2](../feature-0006-field-app-interaction-reliability/spec.md#2-hallazgos-por-lectura-del-código),
donde sigue siendo la evidencia de la corrección puntual. No se repite aquí. La ejecución
de pruebas y sus límites se registran en
[0004 sec. 2.1](../feature-0004-field-app-sync-reliability/spec.md#21-verificación-ejecutada-y-sus-límites).

Los hallazgos relevantes para el rediseño, y no para la corrección, son dos:

| Evidencia en el repositorio (`0faf483`) | Hallazgo |
|---|---|
| `clients/field-app/src/screens/ActivitiesHub.tsx`, secciones «Sujetos» y «Más opciones». | Coexisten recorridos por animal con accesos separados a tratamiento y eventos; se repite el acceso a acciones con diferente contexto. La pantalla dedica espacio a explicar categorías internas. |
| `clients/field-app/src/ui/theme.ts`, `color`, `font`, `space` y `touchTarget`. | Ya existe un tema central con fondo azul oscuro, acento verde y controles grandes. Se puede evolucionar como sistema común; no es necesario definir estilos independientes por pantalla. |

## 2. Decisiones fijadas para la propuesta

- **D1 — Rediseñar la experiencia completa.** Incluir arquitectura de información,
  navegación, inicio operativo, fichas, formularios, sistema visual y estados. El
  asistente de parto conserva su secuencia funcional de cuatro pasos mientras recibe
  el nuevo lenguaje visual y de interacción.
- **D2 — Respetar captura offline y lo ya escrito.** Retroceder dentro de un flujo
  conserva sus valores. Abandonarlo con cambios advierte antes de descartarlos.
- **D3 — No añadir una librería de animaciones por anticipación.** Se ajusta con
  componentes disponibles; una dependencia nueva requiere necesidad medida y ADR.
- **D4 — El animal identificado es central en el manejo actual.** Arete, sexo, grupo y
  estado relevante permiten reconocer al sujeto antes de actuar. Alimentación y
  actividades grupales conservan su recorrido propio; la interfaz respeta ambos modos.
- **D5 — El estado del registro forma parte de la interfaz principal.** El empleado
  puede distinguir guardado local, envío pendiente y rechazo sin abrir una herramienta
  técnica. «Sin señal» no se presenta como fracaso de un registro guardado localmente.
- **D6 — Un sistema visual compartido.** Pantallas, componentes y mensajes usan las
  mismas jerarquías, vocabulario y estados; no se entrega una mezcla permanente de
  pantallas nuevas y formularios antiguos con convenciones diferentes.
- **D7 — Cierre visual sobre capacidades comprobadas.** Los otros specs gobiernan
  identidad, sincronización, validación, permisos y atribución de consumo. Este spec
  gobierna cómo encontrarlas, entenderlas y operarlas. No declara resueltos sus defectos
  por cambiar el mensaje o esconder el control.
- **D8 — La fiabilidad de interacción es requisito previo, no parte del rediseño.** Las
  reglas de scroll, teclado y gestos las fija 0006 y este spec las hereda: rediseñar
  sobre pantallas que atrapan el scroll reproduciría el defecto con otra apariencia.

## 3. Alcance y comportamiento esperado

Incluye acceso y desbloqueo, Inicio, selección de animal/lote, fichas, acciones del
sujeto, tratamiento, vacunación, pesaje, movimientos, bajas, consumo, ordeño, parto,
edición, historial disponible, actividad del empleado, sincronización y ajustes móviles.
En todas ellas se revisan distribución, jerarquía, colores, tipografía, componentes,
mensajes, desplazamiento, teclado, foco, regreso y pulsaciones.

Una lista larga conserva selección y posición razonable al volver del detalle. Un
refresco de datos no salta al inicio ni cierra el formulario. Si invalida el sujeto,
aplica [0005](../feature-0005-field-app-activity-validation/spec.md).

El nuevo inicio es operativo: accesos, trabajo reciente y estado de envío con datos
disponibles. No añade analítica productiva, métricas financieras ni funciones de fases
futuras. Tampoco incluye rediseño del panel Angular, lectores QR/RFID, nueva autenticación
o modificación de reglas de dominio. La priorización fina de accesos por frecuencia se
valida en campo; no se presume que el ordeño sea central para esta operación porcina.

### 3.1 Navegación y arquitectura de información

La propuesta organiza cuatro destinos principales, siempre reconocibles por texto y
símbolo: **Inicio, Animales, Lotes y Actividad**. El estado de sincronización es accesible
desde cualquier destino principal; ajustes y sesión son secundarios. El detalle de un
animal y los pasos de registro conservan un regreso claro a su origen.

| Destino | Contenido principal | Acción esperada |
|---|---|---|
| Inicio | Estado de trabajo local/envío, acceso destacado a buscar arete, accesos de registro y registros recientes propios. | Retomar el trabajo o comenzar una captura. |
| Animales | Búsqueda por identificadores/nombre, filtros por grupo y sexo, selección y ficha. | Encontrar al animal correcto y registrar sobre él. |
| Lotes | Grupos de animales y modo de seguimiento; resumen disponible y miembros cuando se conocen. | Registrar alimentación u otra actividad grupal. |
| Actividad | Lo registrado por el empleado, ordenado por fecha, con estado y detalle. | Comprobar guardado, entender un rechazo y corregir cuando esté permitido. |

Los accesos rápidos conducen al mismo flujo canónico que la ficha; no duplican
formularios ni reglas. Una acción iniciada en la ficha conserva el sujeto elegido.
Una acción iniciada en Inicio solicita un sujeto apto para esa actividad. El registro
de parto ofrece madres elegibles, sin pedir recorrer antes todo el hato.

Los módulos desactivados y acciones no autorizadas no ocupan accesos operativos. Una
ruta antigua, selección obsoleta o dato recibido posteriormente no evita la validación
del servicio y del servidor. La consulta de historia permitida se mantiene aunque una
actividad deje de estar disponible.

### 3.2 Fichas, búsqueda y continuidad de contexto

La ficha del animal destaca arete vigente, nombre si existe, sexo, grupo y los estados
conocidos relevantes para actuar. Retiro, baja y preñez solo se muestran cuando hay
datos que los respaldan; ausencia de información no se traduce en «sano» o «disponible».
El historial separa hechos confirmados de registros locales pendientes sin duplicarlos
al recibir la confirmación del servidor.

La ficha del grupo de animales distingue identificación individual y conteo. Muestra
únicamente resúmenes calculables y declara fecha o limitación de actualización. Si un
resumen requiere conexión, su indisponibilidad no bloquea la captura local. No se fabrican
pesos promedio, existencias, dosis o indicadores que el backend aún no proporciona.

La búsqueda mantiene texto, filtros y posición al regresar de la ficha. La coincidencia
por arete es reconocible y no pierde ceros iniciales; una ambigüedad exige selección
consciente, según 0007. Animales sin arete conservan una identificación alternativa legible.

### 3.3 Formularios y recorrido de registro

Los formularios comparten encabezado de actividad y sujeto, campos con unidad visible,
ayuda breve, validación cerca del campo y acción principal inequívoca. Campos opcionales
se distinguen y los catálogos largos permiten búsqueda o selección progresiva en lugar
de ocupar toda la pantalla con botones. Ningún valor esencial depende solo del color.

Cada flujo muestra únicamente los campos relevantes para su sujeto y actividad. La
confirmación final resume lo que importa revisar: animal o grupo, fecha, cantidad/unidad
y producto o causa cuando correspondan. Las acciones frecuentes no reciben pasos
decorativos; parto y operaciones complejas conservan pasos que reduzcan errores.

Después del guardado local, la respuesta indica el hecho registrado y permite consultar
su detalle o continuar con otro sujeto sin duplicarlo. Los errores mantienen los valores
editables y ofrecen una acción concreta. No se promete corregir desde el teléfono un
tipo de registro que el dominio todavía no permite corregir.

### 3.4 Dirección visual

La dirección propuesta es una **herramienta de campo sobria, clara y reconocible por
sus aretes y registros**. Superficies claras cálidas, texto oscuro y verde profundo como
acento principal; ámbar para atención y rojo para errores o acciones irreversibles.
Se usa un tema oscuro coherente para uso con poca luz. La preferencia puede seguir al
sistema o fijarse localmente; ninguna variante requiere conexión.

Tipografía con lectura cómoda a distancia, números y unidades nítidos, títulos breves
y etiquetas de arete prominentes. Se mantiene una escala central de espaciado y tamaño;
objetivos táctiles de al menos 64 unidades lógicas en controles de campo. Se priorizan
familias ya disponibles; incorporar otra fuente exige resolver su distribución offline
y licencia sin introducir descargas durante el uso.

Listas para colecciones y tarjetas para resúmenes o agrupaciones con significado.
Evitar que cada campo y cada acción sean una tarjeta con igual peso. Una acción primaria
por etapa, secundarios discretos y advertencias próximas al dato que afectan. Iconos
consistentes acompañados de texto en navegación y operaciones de campo.

Esta dirección queda especificada como propuesta de diseño; no se presenta como una
preferencia estética ya validada por empleados. Antes de desarrollar todas las pantallas,
debe poder revisarse una representación concreta de Inicio, ficha de animal, tratamiento,
parto y rechazo de sincronización, en ambos temas. Esa representación es parte del
alcance futuro del rediseño, no un artefacto generado en esta entrega documental.

### 3.5 Movimiento, accesibilidad y rendimiento

Transiciones breves y coherentes hacen visible avanzar, regresar, desplegar y confirmar.
Como objetivo de diseño, las transiciones ordinarias duran entre 150 y 250 ms; no retrasan
el guardado ni exigen esperar para interactuar. Con reducción de movimiento, se elimina
el desplazamiento animado no esencial y se conserva la comunicación del estado.

Botones ofrecen respuesta inmediata al toque. Listas extensas deben renderizarse de
forma adecuada al volumen, conservar posición y no reconstruir el formulario al llegar
un delta. Se verifican ambos temas al sol y con poca luz; el contraste, las etiquetas
accesibles, el orden de foco y el texto ampliado forman parte del diseño común.

### 3.6 Estados y mensajes compartidos

| Situación | Lo que debe comprender el empleado |
|---|---|
| Registro persistido localmente, sin red | «Guardado en este teléfono. Pendiente de enviar». Puede seguir trabajando. |
| Envío o descarga en curso | Qué parte está en curso; la captura local sigue disponible. |
| Registro aceptado, descarga pendiente | Ese registro llegó; todavía faltan cambios por recibir. |
| Operación rechazada | Qué registro fue rechazado, motivo legible y acción disponible; el contenido sigue conservado. |
| Catálogo vacío o incompleto | Diferencia entre ausencia real, filtro sin coincidencias y datos aún no descargados. |
| Sesión expirada | Cómo recuperar envío sin perder lo registrado; respetar desbloqueo y autorización offline vigentes. |
| Error de almacenamiento local | No afirmar «guardado»; conservar lo posible e indicar recuperación sin sugerir borrar la app. |

Los detalles técnicos de soporte se separan del mensaje operativo. El acceso a diagnóstico
persistente de 0004 es alcanzable, pero el empleado no necesita entender cursors, UUID
o nombres de tablas para registrar una actividad.

### 3.7 Integración final y actualización de la app existente

Este spec integra la recepción y el estado de 0004, la aptitud por actividad de 0005,
la identidad por arete de 0007, las acciones permitidas de 0008 y los textos de consumo
de 0009 cuando aparezcan en móvil. No depende de llevar a la app pantallas de oficina
que esos specs no incluyen. El diseño puede revisarse antes, pero el cierre se prueba
sobre esas capacidades funcionales, no sobre respuestas simuladas únicamente.

Actualizar desde la versión de campo conserva sesión según la política vigente,
outbox, datos locales e identificadores. No exige reinstalar ni «empezar limpio».
Un borrador persistido tiene recuperación explícita; si el flujo viejo no guardaba
borradores, el rediseño no promete recuperar información que nunca se almacenó.
Una migración local necesaria para el nuevo comportamiento se define y prueba antes
de distribuirla, sin cambiar el protocolo de sync o el modelo de permisos por motivos
puramente visuales.

## 4. Matriz de aceptación propuesta

Las dimensiones siguientes son condiciones de prueba propuestas, no mediciones de los
teléfonos actuales: Android con área útil aproximada de 360 × 640 y 412 × 915 unidades
lógicas, texto al 100 % y 150 %, teclado abierto/cerrado, modo avión/conectado. Incluir
al menos un teléfono real usado por los empleados y anotar modelo, SO y build.

Usar 200 animales, 20 grupos, nombres de 100 caracteres, un parto de 20 crías y un
catálogo de tratamiento con 8 vías y 6 motivos. Son fixtures de estrés de UI, no cifras
afirmadas sobre la finca. Medir fluidez en compilación equivalente a producción; no
atribuir al producto la sobrecarga de herramientas de desarrollo.

## 5. Criterios de aceptación

1. Dos usuarios de campo completan los flujos que usan habitualmente sin ayuda para
   encontrar la acción final. Se documentan dificultades restantes antes de aceptar.
2. Inicio, Animales, Lotes y Actividad permiten completar la búsqueda por arete, captura
   y consulta de resultado con navegación consistente. Los accesos rápidos y los de
   ficha terminan en el mismo formulario con el contexto correcto.
3. Todas las pantallas incluidas adoptan el sistema visual común, con ambos temas,
   tamaños táctiles y estados definidos. Se revisan las representaciones de diseño
   indicadas en 3.4 antes de extenderlas a todos los flujos.
4. El usuario distingue una escritura guardada localmente de una enviada, una rechazada
   y una descarga incompleta. Ningún estado de éxito oculta los defectos de 0004.
5. Las fichas conservan historia, muestran identidad y estados respaldados por datos,
   y ofrecen actividades según aptitud y permisos. Un módulo desactivado no domina Inicio.
6. El recorrido parto → cría con arete → registro individual → sincronización → consulta
   de resultado funciona con las capacidades de 0007 y preserva identidad y contenido.
7. Refresco automático no cambia arbitrariamente la selección, posición de lectura ni
   texto escrito. Un sujeto invalidado se trata según 0005.
8. Volver de un paso conserva sexo, arete, peso, dosis y demás valores ya introducidos.
   Abandonar un formulario modificado tiene salida explícita y comprensible.
9. Actualizar una instalación con registros pendientes conserva esos registros y sus
   referencias. Un reinicio durante captura o envío no presenta como guardado un dato
   no persistido ni obliga a eliminar la aplicación.
10. Los criterios de fiabilidad de interacción de 0006 siguen cumpliéndose sobre las
    pantallas rediseñadas; el rediseño no puede reintroducir un scroll atrapado ni un
    control tapado por el teclado.
11. La aceptación final incluye la aplicación completa sobre datos persistidos y servidor
    real, además de tests de componentes. Se registra build, teléfono y observaciones
    de campo; los defectos de scroll y sincronización no se cierran solo con un prototipo.

## 6. Dependencias, límites y aspectos por validar

- Requisito previo: [fiabilidad de interacción 0006](../feature-0006-field-app-interaction-reliability/spec.md).
- Specs funcionales relacionados: [sincronización 0004](../feature-0004-field-app-sync-reliability/spec.md),
  [validaciones 0005](../feature-0005-field-app-activity-validation/spec.md),
  [aretes 0007](../feature-0007-field-app-individual-tagged-livestock/spec.md),
  [permisos 0008](../feature-0008-people-permission-enforcement/spec.md) y
  [consumo 0009](../feature-0009-inventory-consumption-batch-attribution/spec.md).
- La prioridad final de accesos y vocabulario se valida con los empleados. No se
  atribuyen frecuencias ni preferencias a conversaciones que no ocurrieron.
- Rediseñar navegación puede requerir un ADR si cambia su infraestructura o agrega
  dependencias. Este spec no autoriza instalar librerías ni sustituir React Native.
- La apariencia de cada estado puede cambiar; los contratos de historial inmutable,
  identidad interna, captura offline y visibilidad de rechazos deben conservarse.
- «Spec final» significa cierre de la experiencia de esta serie. No declara terminada
  Fase 3/3.5 ni satisfechos sus criterios de campo mientras falte evidencia.
