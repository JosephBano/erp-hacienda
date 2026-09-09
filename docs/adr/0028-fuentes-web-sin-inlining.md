# ADR-0028 — Las fuentes web no se incrustan en el build

- **Estado:** Aceptado
- **Fecha:** 2026-09-09
- **Fase del roadmap:** Configuración del CI institucional

## Contexto

`clients/admin-web/src/styles.css` abre con un `@import` a
`fonts.googleapis.com` para Outfit y Plus Jakarta Sans. Angular, en
`ng build --configuration production`, resuelve ese `@import` descargando las
hojas de estilo y los `.woff2` para incrustarlos en el bundle. Es una
optimización real —evita un salto de red en el primer render— pero convierte
la **compilación** en una operación que necesita internet.

En el pipeline institucional eso es un problema. El job `test:admin-web` corre
en un runner k3s del campus cuya salida a internet no está garantizada, y
cuando falla lo hace así:

```
✘ [ERROR] Failed to inline external stylesheet
  'https://fonts.googleapis.com/css2?family=Outfit...'
  Error: Inlining of fonts failed. An error has occurred while retrieving
  ... over the internet.
```

Reproduciéndolo en local con la imagen del CI (`node:20-alpine`) falló dos
veces seguidas y después pasó seis, con la red disponible y las URLs
respondiendo 200 a `curl` desde el mismo contenedor. Es decir: incluso **con**
salida a internet el paso es intermitente, probablemente por limitación de
tasa de Google. Sin salida, falla siempre.

Un dato que acota el problema: el `@import` pide nueve variantes
(Outfit 300/400/500/600/700 y Plus Jakarta Sans 400/500/600/700), pero el
código solo declara tres pesos —500, 600 y 700— más el 400 implícito del
cuerpo de texto.

## Decisión

**Desactivar el inlining de fuentes en la configuración de producción** y dejar
que sea el navegador quien las pida.

En `clients/admin-web/angular.json`, configuración `production`:

```json
"optimization": {
  "scripts": true,
  "styles": true,
  "fonts": { "inline": false }
}
```

`scripts` y `styles` se declaran explícitamente porque al pasar `optimization`
de booleano a objeto se pierden los valores por defecto de producción, y
perder la minificación por un descuido de sintaxis sería un coste muy superior
al que se está evitando.

El `@import` de `styles.css` se conserva, con un comentario que apunta a este
ADR.

## Alternativas consideradas

**Auto-hospedar las fuentes ahora.** Bajar los ~7 `.woff2` que realmente se
usan a `public/fonts/` —ya está mapeado como assets, no haría falta tocar la
configuración de build— y sustituir el `@import` por bloques `@font-face`.
Elimina la dependencia de Google en build *y* en runtime, y es la solución
técnicamente correcta. Se descarta **por momento, no por mérito**: las
familias y los pesos actuales no provienen del manual de marca del instituto,
así que auto-hospedarlas hoy significa congelar en el repositorio unos
archivos binarios que habrá que reemplazar en cuanto se rehagan los diseños.
Se hará entonces, y este ADR quedará reemplazado.

**Cachear las fuentes en el runner o replicarlas en Harbor.** Resuelve el CI
sin tocar el código de la aplicación, pero traslada el problema a
infraestructura que no controla este equipo y deja al desarrollador con un
build que sigue fallando en su portátil si está sin red.

**Reintentar el job.** El pipeline ya reintenta `runner_system_failure`, pero
esto no lo es: el pod arranca y el script falla. Reintentar un fallo de script
esconde el problema y triplica la espera.

## Consecuencias

**Lo bueno.** La compilación de producción deja de necesitar internet: se
puede construir en el runner institucional, en un contenedor sin red y en el
portátil de cualquiera. Desaparece un fallo intermitente que no dependía de
nada que el equipo pudiera controlar.

**Lo malo.** La dependencia no se elimina, se mueve al cliente. El navegador de
cada usuario pide las fuentes a `fonts.googleapis.com` en cada carga fría: hace
falta internet en el puesto —dudoso si la aplicación acaba en la intranet— y
las visitas quedan expuestas a un tercero. Además se paga un salto de red antes
del primer render, con el consiguiente parpadeo de fuente (`display=swap`).

**Lo que hay que vigilar.** Si la aplicación se despliega en una red sin salida
a internet, la tipografía degradará a `sans-serif` de sistema. No rompe nada,
pero el resultado no será el diseñado.

**Condición de reversa.** Esta decisión se reabre cuando se rehagan los diseños
alineados con el manual de marca del instituto. En ese momento se auto-hospedan
las familias definitivas en `public/fonts/` con `@font-face`, se elimina el
`@import`, y este ADR queda reemplazado. También se reabre antes si se confirma
que el despliegue será en intranet sin salida a internet, porque entonces el
coste de no auto-hospedar deja de ser teórico.
