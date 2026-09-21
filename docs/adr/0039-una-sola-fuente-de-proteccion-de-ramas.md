# ADR-0039 — Una sola fuente de verdad para la protección de ramas

- **Estado:** Aceptado
- **Fecha:** 2026-09-21
- **Fase del roadmap:** feature-0011 (DevOps y tubería de entrega)
- **Relacionado:** ADR-0030 (secretos y configuración declarada), ADR-0037 (promoción a `main` por merge desde `develop`)

## Contexto

GitHub ofrece **dos mecanismos distintos** para proteger ramas, que conviven y se aplican a la
vez:

- **Protección clásica de rama** (`/repos/{owner}/{repo}/branches/{branch}/protection`)
- **Rulesets** (`/repos/{owner}/{repo}/rulesets`), más recientes

**Son aditivos: gana siempre la regla más restrictiva.** Nada en la interfaz advierte de que
el otro mecanismo existe ni de que puede estar contradiciendo lo que se acaba de configurar.

Este repositorio tenía ambos activos a la vez. La protección clásica estaba declarada y
versionada:

```
.github/branch-protection.expected.json   la configuración esperada
scripts/apply-repo-config.sh              la aplica
scripts/check-config-drift.sh             detecta deriva; es check requerido en CI
```

Además existían dos rulesets (`main` y `develop`), creados por la interfaz web, que nadie
había declarado en ninguna parte.

El 2026-09-21, al implementar ADR-0037, se desactivó `required_linear_history` en `main` por
la API clásica para permitir los merge commits que esa decisión necesita. `check-config-drift.sh`
confirmó:

```
OK [main]: required_linear_history.enabled = False
OK: Configuración de ramas y entornos sin deriva
```

Pero el PR de promoción #161 seguía sin poder mergearse. El motivo: el ruleset `main` también
declaraba `required_linear_history`, y seguía imponiéndolo.

**El problema no fue la regla duplicada, sino el punto ciego.** `check-config-drift.sh`
consulta únicamente la API clásica; los rulesets le son invisibles. Dio un `OK` verde mientras
la configuración efectiva era otra. Un detector de deriva que no ve la mitad de la
configuración es peor que no tenerlo: produce confianza falsa.

## Decisión

**Mantener la protección clásica de rama como única fuente de verdad, y no usar rulesets.**

- Los rulesets `main` y `develop` se eliminan.
- Toda la protección se declara en `.github/branch-protection.expected.json`, se aplica con
  `scripts/apply-repo-config.sh` y se vigila con `scripts/check-config-drift.sh`.
- No se crean rulesets por la interfaz web. Si en el futuro se adoptan, se migra **entero**,
  con la herramienta actualizada para leer su API — nunca a medias.

Configuración efectiva resultante, verificada tras el cambio:

```
main     linear=false  enforce_admins=true  force_push=false  deletions=false  [5 checks]
develop  linear=false  enforce_admins=true                                     [5 checks]
```

La restricción de que a `main` solo se promueva desde `develop` **no se implementa aquí**:
ni los rulesets ni la protección clásica tienen una regla que mire la rama de origen de un
PR. Esa comprobación vive en el job `pr-hygiene` (ADR-0037), que al ser check requerido se
aplica con la misma fuerza.

## Alternativas consideradas

**Migrar todo a rulesets.** Son el mecanismo más moderno de GitHub y ofrecen cosas que la
protección clásica no tiene: listas de excepción (bypass actors), una misma regla aplicada a
varias ramas por patrón, y un modo *evaluate* que permite probar una política sin aplicarla.
Se descartó por coste y por momento: obligaría a reescribir `apply-repo-config.sh`,
`check-config-drift.sh` y el JSON esperado, y ninguna de esas ventajas se está usando hoy —
con un solo desarrollador y `enforce_admins: true`, las bypass lists no aportan nada. Queda
como migración posible, no como pendiente.

**Conservar ambos mecanismos, quitando solo `required_linear_history` del ruleset.** Habría
desbloqueado el PR de inmediato con un cambio mínimo. Se descartó porque no arregla la causa:
seguirían existiendo dos fuentes de verdad y el detector de deriva seguiría ciego a una de
ellas. El síntoma se iba; la trampa se quedaba, esperando a la siguiente regla que alguien
marcara en la interfaz.

## Consecuencias

**A favor.** Un solo sitio donde mirar y un solo sitio donde cambiar. `check-config-drift.sh`
vuelve a ser fiable: lo que reporta es lo que hay. La configuración de protección queda
versionada y revisable en PR, como cualquier otro cambio.

**En contra.** Se renuncia a las bypass lists, al targeting por patrón y al modo *evaluate*.
Si en algún momento hacen falta —por ejemplo, si el equipo crece y conviene eximir a un bot
de CI— habrá que migrar a rulesets de verdad.

**Lo que hay que vigilar.** Que nadie cree un ruleset desde la interfaz web «para probar».
Sería invisible para el detector de deriva y volvería a producir el mismo fallo silencioso.
La interfaz de GitHub empuja hacia los rulesets, así que la tentación es real.

**Condición de reversa.** Si se necesitan bypass actors, targeting por patrón o el modo
*evaluate*, se migra a rulesets **por completo**, actualizando primero
`check-config-drift.sh` para que lea su API y falle si encuentra reglas no declaradas. La
migración debe dejar el repositorio otra vez con una sola fuente de verdad, nunca con las dos.
