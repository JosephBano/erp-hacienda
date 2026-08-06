# ADR-0019 — Módulos que se ocultan, nunca se borran

- **Estado:** Propuesto
- **Fecha:** 2026-08-06
- **Fase del roadmap:** Fase 3.5 (Adaptación porcina) — bloque 3.5a, **transversal**

## Contexto

El cliente del piloto es una granja porcina. **Ordeño no aplica y no va a funcionar durante
esta fase**, así que el dueño del producto pidió que se oculte de la vista del usuario —
explícitamente **sin borrarlo**, y hasta que él diga lo contrario.

El plan traía una solución distinta y peor: ocultar Ordeño **cuando ninguna especie tenga
`Species.IsMilkable`**, es decir, visibilidad *derivada* de los datos. Tiene un defecto que
sólo aparece meses después: el día que alguien registre una vaca para leche de la casa, o
cree una especie ordeñable por cualquier motivo, **Ordeño reaparece solo, sin que nadie haya
decidido que vuelva**. Una superficie de producto que aparece por efecto secundario de un
INSERT no es una decisión: es una sorpresa.

Son dos ejes distintos y hasta ahora estaban confundidos en uno:

| Eje | Qué responde | Quién lo decide |
|---|---|---|
| `Species.IsMilkable` | **¿Se le puede sacar leche a esta especie?** Un cerdo no, nunca. | La realidad biológica. Es verdad de dominio. |
| Visibilidad del módulo | **¿Esta parte del producto está encendida para esta finca, hoy?** | El dueño. Es decisión de producto. |

Se puede tener vacas ordeñables y aun así querer el módulo apagado porque todavía no se
va a usar. Eso es exactamente el caso actual.

## Decisión

**1. Un interruptor explícito por módulo, en datos.**

```
FarmModule { key, enabled, disabled_reason?, updated_at, updated_by }
```

Catálogo pequeño y editable desde el panel (Art. 8): apagar o encender un módulo **no
requiere un deploy**. `disabled_reason` existe para que dentro de un año se sepa por qué está
apagado y no haya que adivinar.

**2. La visibilidad es una conjunción, y el interruptor manda.**

```
se muestra  ⟺  módulo habilitado  ∧  capacidades de la finca  ∧  permisos del usuario
```

Si el interruptor está apagado, se oculta y no se evalúa nada más. Si está encendido, recién
ahí aplican los filtros que ya existían — entre ellos `IsMilkable`, que **no se elimina**:
sigue siendo verdad de dominio y sigue impidiendo que alguien registre un ordeño de una
cerda.

Es una entrada más del filtrado que el `PLAN-FASE-3-5-PORCINO.md` §2.3 ya define para el
árbol de actividades. No es un mecanismo paralelo.

**3. No se borra absolutamente nada.**

El módulo `Production`, `MilkingScreen`, `milkingService`, la pestaña de `App.tsx`, los
componentes `quick-milking` de `admin-web`, los endpoints y **todas sus pruebas** quedan en
el repositorio, compilando y en verde. Lo único que cambia es que la navegación no ofrece la
entrada.

**4. Se oculta la entrada, jamás el camino de los datos.**

Esta es la regla que evita una pérdida silenciosa. Un teléfono puede tener **ordeños sin
sincronizar en su outbox** en el momento en que el módulo se apaga. Esos registros **deben
poder seguir subiendo**: los endpoints siguen aceptándolos y el motor de sync los sigue
empujando. Apagar un módulo oculta la puerta de entrada; nunca clausura la salida de lo que
ya se registró.

Lo mismo para el pull: los datos históricos de leche se siguen sincronizando y consultando.
Art. 1 — los datos no se destruyen ni se vuelven inalcanzables.

**5. El interruptor se evalúa sin red.**

Como todo lo demás (Art. 9), la bandera baja en el pull y la navegación la resuelve
localmente. Un módulo cuya visibilidad dependiera de la conexión parpadearía en el potrero.

**6. El interruptor siempre es alcanzable.**

La pantalla que administra los módulos no puede ocultarse a sí misma. Vive en el panel,
bajo permiso de administrador, y es el único camino para volver a encender lo apagado.

**7. Un módulo oculto se sigue probando.**

Es la trampa de esta decisión. Un módulo que nadie ejecuta y que nadie mira **se pudre en
silencio**: cuando el dueño pida encenderlo dentro de un año, estará roto por acumulación de
cambios que nunca lo tocaron a propósito. Su suite sigue corriendo en CI **exactamente igual
que antes**, y CI en rojo por Ordeño bloquea el merge como cualquier otra cosa (Art. 12).
Ocultar es una decisión de producto, no un permiso para dejar de mantener.

## Alternativas consideradas

- **Visibilidad derivada de `IsMilkable`** (lo que decía el plan). Descartada: confunde
  verdad de dominio con decisión de producto, y hace que el módulo reaparezca por efecto
  secundario de un INSERT que alguien hizo por otro motivo.

- **Borrar el módulo y recuperarlo del historial de git cuando haga falta.** Descartada. El
  código volvería desde un commit viejo a un repositorio que avanzó meses: dependencias
  distintas, esquema distinto, contratos distintos. "Está en el historial" es una forma
  elegante de decir que hay que reescribirlo. Y contradice el espíritu del Art. 1.

- **Bandera en configuración o variable de entorno.** Descartada por Art. 8: apagar o
  encender exigiría un deploy, y el dueño pidió decidirlo él. Además no bajaría al móvil por
  el mismo canal que todo lo demás.

- **Bloquear también los endpoints del módulo apagado.** Descartada **por ahora**: dejaría
  huérfanos los ordeños que ya estén en el outbox de un teléfono, que es precisamente la
  pérdida silenciosa que el punto 4 evita. Si algún día hace falta bloquear escrituras
  nuevas, se decide aparte y con una ruta de drenaje para lo pendiente.

- **Comentar el código o dejarlo tras un `if (false)`.** Descartada: es la versión que se
  pudre más rápido, deja de compilar sin que nadie se entere y no tiene interruptor.

## Consecuencias

- **Positivas**:
  + El dueño enciende y apaga módulos sin pedir un deploy, que es lo que significa "software
    a medida" cuando el mismo producto sirve a fincas distintas.
  + La app deja de mostrarle al empleado una función que no aplica a su trabajo. Menos ruido
    en la pantalla y menos formas de registrar algo sin sentido.
  + El mecanismo es reutilizable: la próxima finca que no críe, o que no lleve inventario,
    apaga el módulo correspondiente sin una rama de código.
  + Separar verdad de dominio (`IsMilkable`) de decisión de producto (visibilidad) deja los
    dos conceptos más claros de lo que estaban.

- **Negativas / costos**:
  − **Un módulo oculto se pudre.** Es el costo real y el punto 7 lo acota, pero no lo elimina:
    nadie ejercita Ordeño manualmente, así que un defecto que las pruebas no cubran vivirá
    ahí hasta que se encienda. **Mitigación**: al reencenderlo, tratarlo como una feature que
    vuelve a producción —revisión y prueba manual— y no como un interruptor inocuo.
  − Una conjunción más en cada decisión de visibilidad. **Mitigación**: se resuelve en el
    mismo lugar donde §2.3 ya filtra el árbol de actividades; no se reparte por los
    llamadores.
  − El código de un módulo apagado sigue pesando en el bundle del móvil. Se acepta: es
    pequeño frente al costo de borrarlo y tener que reescribirlo.

- **Condición de reversa**: cuando el dueño pida encender Ordeño, se enciende — ese es el
  funcionamiento normal, no una reversa. Este ADR se revisaría si al cabo de varias fincas
  ningún módulo se apagara nunca, en cuyo caso el mecanismo sobra y se retira dejando los
  filtros por capacidad y permiso que ya existían.
