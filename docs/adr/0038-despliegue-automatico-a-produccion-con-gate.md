# ADR-0038 — Despliegue automático a producción con gate de aprobación

- **Estado:** Aceptado
- **Fecha:** 2026-09-21
- **Fase del roadmap:** feature-0011 (DevOps y tubería de entrega), posterior a la puesta en marcha de producción
- **Reemplaza parcialmente:** la decisión D4 en su punto *«un push a main por sí solo no publica producción»*

## Contexto

Hasta ahora `deploy-production.yml` solo se disparaba con `workflow_dispatch`, y había que
indicar a mano el SHA a desplegar. La spec lo justificaba así:

- línea 46-47 (D4): *«Un push a main por sí solo no publica producción»*
- línea 141: *«preferencia inicial por workflow_dispatch sobre main con tag validado»*

Esa preferencia se declaró **inicial**, antes de que existiera producción. Tras la puesta en
marcha del 2026-09-21 hay evidencia que no había entonces:

1. La cadena completa —autorización, compose, migraciones, TLS— funciona de extremo a
   extremo, con siete bloqueos encontrados y cada uno cubierto por una prueba de regresión.
2. El disparo manual obliga a recordar el SHA exacto y a lanzarlo, un paso que se olvida
   justo cuando más prisa hay.
3. El gate de aprobación del Environment `production` —revisor requerido, rama permitida
   `main`— ya existe y es lo que de verdad protege producción. El disparador manual no
   añadía seguridad sobre ese gate: solo añadía fricción.

Lo que D4 protegía era **que nadie tocara producción sin decisión humana**. Eso lo garantiza
el gate del entorno, no el disparador.

## Decisión

**Desplegar automáticamente cada commit que entre en `main`, conservando intacto el gate de
aprobación del Environment `production`.**

- `deploy-production.yml` añade el disparador `push` sobre `main`. `workflow_dispatch` se
  mantiene para redesplegar un SHA concreto a mano.
- El job `deploy` conserva `environment: production`, con revisor requerido y
  `allowed_branches: ["main"]`. **El despliegue se lanza solo, pero no toca la VPS hasta que
  un humano lo aprueba.**
- El paso *Require green CI for this exact SHA* pasa a **esperar** a que `ci.yml` termine,
  hasta 10 minutos. Con `push`, ambos workflows arrancan a la vez, así que exigir una
  ejecución ya completada fallaría siempre. La garantía no se debilita: sigue exigiendo
  `conclusion == "success"` para ese SHA exacto, y un CI en rojo aborta de inmediato en vez
  de seguir esperando.

## Alternativas consideradas

**`workflow_run` colgado del final de `ci.yml`.** Es la forma natural de encadenar «CI verde
⟶ desplegar» y evitaría la espera activa. Se descartó por un motivo concreto: GitHub ejecuta
`workflow_run` **en el contexto de la rama por defecto**, que en este repositorio es
`develop`. El Environment `production` declara `allowed_branches: ["main"]`, de modo que el
job quedaría rechazado por la política del entorno. Abrir esa política a `develop` para
rodearlo debilitaría justo la barrera que impide desplegar desde cualquier rama, que vale
mucho más que ahorrarse una espera de tres minutos.

**Filtrar por rutas (`paths-ignore: docs/**`)** para no redesplegar por cambios de
documentación. No se adopta de momento: un despliegue de producción debe corresponder a un
commit concreto de `main`, y excluir rutas hace que el SHA desplegado y el de `main` diverjan
de formas difíciles de razonar. Si el coste de los despliegues de documentación resulta
molesto en la práctica, se reabre.

**Desplegar sin gate, de verdad automático.** Se descartó explícitamente. Ese job ejecuta
migraciones sobre la base de datos real, y el contrato de fallos vigente dice que un error
posterior a la migración **no** dispara restauración automática y exige decisión humana. Un
despliegue sin intervención sería incoherente con ese contrato.

## Consecuencias

**A favor.** Lo que está en `main` llega a producción sin depender de que alguien se acuerde
de lanzarlo. El SHA desplegado es siempre un commit de `main` con CI verde. La decisión
humana sigue existiendo, pero en el punto donde importa —aprobar el acceso a producción— en
vez de en el de arrancar el pipeline.

**En contra.** Cada merge a `main` construye dos imágenes ARM64 bajo emulación QEMU y queda
esperando aprobación, incluso si el cambio es solo documentación. Son minutos de runner y una
notificación más. Combinado con ADR-0037, que exige que todo entre a `main` desde `develop`,
el volumen es acotado: una promoción por tanda de trabajo, no una por commit.

**Lo que hay que vigilar.** Que los despliegues pendientes de aprobación no se acumulen sin
atender. `concurrency.group: deploy-production` con `cancel-in-progress: false` los serializa,
así que varios sin aprobar forman cola en vez de pisarse; conviene aprobarlos o cancelarlos
en orden y no dejarlos abiertos.

**Condición de reversa.** Si los despliegues por cambios que no afectan al runtime resultan
ser mayoría y el ruido supera al beneficio, el primer paso es filtrar por rutas —no volver al
disparo manual—. Volver a `workflow_dispatch` solo tendría sentido si se comprueba que el
gate de aprobación deja de ser suficiente para proteger producción, que es la premisa sobre
la que descansa esta decisión.
