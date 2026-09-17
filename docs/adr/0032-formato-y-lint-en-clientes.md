# ADR-0032 — Formato y lint obligatorios en los clientes, con el formateo masivo separado del cambio funcional

- **Estado:** Aceptado
- **Fecha:** 2026-09-15
- **Fase del roadmap:** Transversal.

## Contexto

El dueño pidió un check de CI de formato (`prettier`) y otro de calidad de código. Al
verificar qué hay hoy, resulta que **eso no es configurar un check: es añadir dependencias**,
y `AGENTS.md` regla 2 dice textual: *«No agregues dependencias (NuGet/npm) sin proponer un
ADR primero. Ni "una librería chiquita para esto"»*.

Estado verificado el 2026-09-15:

```
$ python3 -c "import json; d=json.load(open('clients/admin-web/package.json')); print(d['scripts'])"
{'ng': 'ng', 'start': 'ng serve', 'build': 'ng build',
 'watch': 'ng build --watch --configuration development', 'test': 'ng test'}
```

| Cliente | `prettier` | `eslint` | Script que los ejecute |
|---|---|---|---|
| `admin-web` | `^3.8.1` declarado en `devDependencies` | ausente | **ninguno** |
| `field-app` | ausente | ausente | **ninguno** |

`prettier` está instalado en `admin-web` y **nada lo invoca**: es una dependencia que no hace
nada desde que se añadió.

En el backend la situación es la opuesta y **ya está resuelta**:

- `dotnet format --verify-no-changes` corre como paso del job `backend-build-and-test`
  (`.github/workflows/ci.yml`).
- `Directory.Build.props` fija `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` con
  `<Nullable>enable</Nullable>`, que es una compuerta de calidad más severa que la de la
  mayoría de proyectos.

Es decir: el backend tiene formato y análisis obligatorios desde siempre, y los dos clientes
no tienen ninguno de los dos. La asimetría no fue una decisión; es una omisión.

## Decisión

**Se añaden `prettier` a `field-app`, `eslint` a ambos clientes, y sus pasos de verificación
dentro de los jobs de CI que ya existen — no como jobs nuevos. El primer formateo masivo va
en su propio commit, separado de cualquier cambio funcional.**

### 1. Qué se añade

| Cliente | Añade | Configuración |
|---|---|---|
| `admin-web` | `eslint` + config de Angular | `prettier` ya está; se le añade script |
| `field-app` | `prettier` + `eslint` + config de React Native / Expo | — |

La configuración de `prettier` es **una sola, compartida en la raíz de `clients/`**: dos
formatos distintos en un mismo repositorio son dos formatos que alguien va a confundir.

### 2. Dónde corre

Pasos nuevos dentro de `field-app-ci` y `admin-web-ci`, que ya existen. **No se crean jobs
nuevos.** Un check por herramienta multiplicaría los nombres que hay que registrar como
obligatorios sin verificar nada que estos pasos no verifiquen, y el catálogo del spec 0011
(sec. 5.5) ya decidió que el número de jobs es el costo real, no el número de verificaciones.

Ambos pasos **bloquean el merge**, igual que `dotnet format` bloquea hoy en el backend.

### 3. El primer formateo

Aplicar `prettier` por primera vez sobre dos bases de código que nunca lo tuvieron reescribe
cientos de archivos. Ese diff:

- va en **su propio commit**, con mensaje que lo declare como reformateo mecánico;
- **no comparte PR con ningún cambio funcional** — `AGENTS.md` regla 9, *«un PR = un
  propósito»*, y además un cambio de lógica escondido en 800 archivos reformateados es
  irrevisable;
- se ejecuta **antes** de volver el paso obligatorio, o el primer PR de cualquiera fallará
  por archivos que él no tocó.

### 4. Qué NO decide este ADR

El conjunto de reglas de `eslint` más allá de la configuración recomendada del framework.
Empezar con la base recomendada y endurecer con evidencia es más barato que discutir reglas
sin código que las motive. Cada regla adicional se justifica con un caso real, en el PR que
la añade.

## Alternativas consideradas

- **Dejar los clientes sin formato ni lint, como hoy.** Descartada: es la asimetría que
  motiva el ADR. El backend lleva la compuerta desde el inicio y los clientes acumulan
  estilo inconsistente que ningún revisor —humano o agente— puede distinguir de un cambio
  intencional.

- **Formato sí, lint no.** Tentadora, porque `prettier` es objetivo y `eslint` opina. Pero el
  lint es lo que atrapa la clase de defecto que de verdad duele aquí: variables no usadas,
  promesas sin `await`, dependencias faltantes en hooks. Tres de los defectos de
  sincronización de la Fase 3 eran de esa familia.

- **Jobs de CI separados por herramienta**, como pidió el dueño al enumerar ocho checks.
  Descartada por lo dicho en la sec. 2: más nombres que registrar y mantener, cero
  verificación adicional.

- **Formatear en un hook de pre-commit en vez de en CI.** Ayuda, pero no es una compuerta:
  un hook local no corre en el CI y se puede saltar con `--no-verify`. Puede añadirse
  después como comodidad; no sustituye al check.

- **Aplicar el formateo masivo dentro del mismo PR que añade las herramientas.** Descartada:
  ese PR ya es grande y mezclar la configuración con el reformateo hace imposible revisar
  ninguna de las dos cosas.

## Consecuencias

**Lo bueno.** Los tres artefactos del proyecto pasan a tener la misma exigencia de estilo, y
el diff de un PR vuelve a significar "esto cambió" en vez de "esto cambió más el estilo de
quien lo tocó". Para un proyecto donde buena parte del código lo escriben agentes, un formato
canónico es lo que hace que sus diffs sean legibles.

**Lo malo.** Un commit de reformateo masivo ensucia `git blame` en los dos clientes. Es un
costo que se paga una vez, y se puede mitigar registrando el hash de ese commit en
`.git-blame-ignore-revs`. Además, cada PR de cliente pasa a poder fallar por una coma.

**Lo que hay que vigilar.**

1. **Que el paso se active después del formateo masivo**, no antes. Al revés deja a cualquiera
   bloqueado por archivos ajenos.
2. **Que las reglas de `eslint` no crezcan por gusto.** Cada regla nueva, con su caso real.
3. **Que `field-app` no se vuelva lento de verificar.** Si el paso añade minutos notables al
   job, se acota el alcance de archivos antes que relajar la compuerta.

**Condición de reversa.** Se reabre si el paso de lint produce sistemáticamente falsos
positivos que obligan a suprimirlos caso por caso —señal de que la configuración elegida no
encaja con Expo o con Angular— y la salida sería cambiar de configuración, no retirar la
compuerta.
