# ADR-0037 — Promoción a `main` por merge desde `develop`

- **Estado:** Aceptado
- **Fecha:** 2026-09-21
- **Fase del roadmap:** feature-0011 (DevOps y tubería de entrega), posterior a la puesta en marcha de producción

## Contexto

`main` tenía `required_linear_history: true`. Esa opción prohíbe los merge commits, de modo
que solo se podía entrar por **squash** o **rebase**, y ambos reescriben los commits: los que
llegan a `main` son objetos nuevos, sin relación con los de `develop`.

La consecuencia no es estética, es estructural. Medido el 2026-09-21:

```
develop tiene 338 commits que main no tiene
main tiene 12 que develop no tiene
merge-base: 42dde0e ("Develop (#40)")
```

Las dos ramas compartían un ancestro de meses atrás pese a que `main` se promueve **desde**
`develop`. Efectos observados:

1. **Un PR `develop -> main` sale siempre `CONFLICTING`**, con conflictos `add/add` en todo
   archivo tocado por ambos lados. Git no ve una historia común reciente que fusionar.
2. Para rodearlo, cada promoción creaba una rama `release/*` **desde `main`** que llevaba el
   contenido de `develop` como un único commit (#128, #138, #142, #149, #151, #153). Funciona,
   pero es un rito manual que hay que recordar y que no se puede automatizar sin replicar la
   misma copia de contenido.
3. **Un hotfix mergeado directo a `main` no vuelve solo a `develop`.** Pasó con #142, #144,
   #154, #155, #156 y #157. `develop` quedaba atrás en silencio, y nadie se enteraba hasta
   que alguien intentaba promover otra vez.

El punto 3 costó tiempo real durante la puesta en marcha de producción: el PR #155 se mergeó
a `main` y el despliegue siguió fallando con el error anterior, en parte porque el estado de
las ramas ya no era el que se suponía.

## Decisión

**Promover a `main` con un merge commit desde `develop`, y solo desde `develop`.**

Tres cambios que se sostienen entre sí:

1. **Un merge de reconciliación** une las dos historias una vez (PR #158). Desde ese punto
   `main` es ancestro de `develop`, verificable con
   `git merge-base --is-ancestor origin/main origin/develop`.
2. **`required_linear_history: false` en `main`**, para que el merge commit sea posible. Se
   refleja en `.github/branch-protection.expected.json` y en `scripts/apply-repo-config.sh`,
   que el job `branch-protection-drift` compara contra la API en cada PR.
3. **`pr-hygiene` rechaza cualquier PR a `main` cuya rama de origen no sea `develop`.** Como
   es check requerido y `main` tiene `enforce_admins: true`, la regla aplica también al
   propietario del repositorio. `develop` queda además exenta de la convención de prefijos de
   rama, que de otro modo rechazaría toda promoción por llamarse `develop` a secas.

`develop` conserva `required_linear_history: false`, que ya tenía.

## Alternativas consideradas

**Mantener el squash y automatizar la rama `release/*`.** Un workflow crearía la rama desde
`main`, copiaría el contenido de `develop`, haría el commit y abriría el PR. `main` se queda
con una historia lineal y legible, que es una ventaja real. Se descartó porque no elimina la
divergencia: solo la vuelve más cómoda. Las dos historias siguen sin converger, el hotfix
directo a `main` sigue sin volver a `develop`, y el estado de las ramas sigue sin poder
comprobarse con `git merge-base`. Se prefiere un historial más ruidoso y verificable a uno
limpio que miente sobre la relación entre ramas.

**Rebase en lugar de merge.** Conserva la historia lineal, pero reescribe los commits igual
que el squash, así que reproduce exactamente el problema que se quiere resolver.

**Dejar abierta la vía del hotfix directo a `main`** para urgencias. Se descartó: es
precisamente la vía que produjo las seis derivas. El coste de cerrarla es un PR adicional y
unos minutos de CI; el coste de dejarla abierta ya se pagó, y fue mayor.

## Consecuencias

**A favor.** Un PR `develop -> main` mergea limpio, sin ritos ni ramas intermedias. La
relación entre ramas es comprobable mecánicamente. `develop` no puede volver a quedarse atrás
sin que se note, porque no hay otra forma de entrar a `main`.

**En contra.** `main` tendrá merge commits: su historia deja de ser una lista plana de
releases y pasa a reflejar la estructura real del trabajo. `git log --first-parent main`
recupera la vista de solo releases para quien la quiera.

**Lo que hay que vigilar.** Una urgencia real con `develop` en un estado no publicable: si
`develop` tiene trabajo a medias, promover arrastraría ese trabajo. El procedimiento entonces
es llevar el hotfix a `develop` y promover solo si `develop` está sano; si no lo está, hay
que estabilizarlo primero. Eso es una consecuencia deliberada: obliga a mantener `develop`
desplegable, que es lo que el modelo de ramas ya presupone.

**Condición de reversa.** Si se comprueba que mantener `develop` siempre desplegable no es
sostenible con un solo desarrollador, y las urgencias quedan bloqueadas de forma recurrente
por trabajo a medias en `develop`, habría que reabrir esta decisión y diseñar una vía de
urgencia explícita — con obligación de sincronizar `develop` en el mismo PR, no a discreción.
