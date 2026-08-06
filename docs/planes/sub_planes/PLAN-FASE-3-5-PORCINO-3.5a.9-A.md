# PLAN-FASE-3-5-PORCINO-3.5a.9-A.md — Visibilidad de módulos por interruptor explícito

> **Sub-plan extraído de `PLAN-FASE-3-5-PORCINO.md` §3.5a.9.**
> Este archivo **es ejecutable de forma independiente** del macro plan y de su par
> 3.5a.9-B. El macro plan sigue siendo la fuente de verdad para el resto del proyecto:
> toda decisión que aplique a varias ramas vive allá. Acá viven sólo las decisiones y
> el alcance de esta sub-rama.

- **Rama Git:** `feature/field-app-module-visibility`
- **ADR que la respalda:** [ADR-0019](../adr/0019-visibilidad-de-modulos.md)
- **Par de split:** [PLAN-FASE-3-5-PORCINO-3.5a.9-B.md](./PLAN-FASE-3-5-PORCINO-3.5a.9-B.md) (navegación por árbol de actividades)
- **Fuente original:** [`PLAN-FASE-3-5-PORCINO.md` §3.5a.9](../PLAN-FASE-3-5-PORCINO.md#35a9--featurefield-app-herd-navigation)
- **Compuerta:** ninguna (no depende del árbol de actividades cerrado con el cliente).
  Esto es deliberado: el interruptor es un cambio de producto pequeño, transversal y
  testeable en aislamiento; el árbol de actividades es UX-estructural y bloqueado por
  la conversación con el cliente (§2.3 y §7-C del macro plan).

---

## Por qué existe esta sub-rama

El cliente del piloto es una granja porcina. **Ordeño no aplica** y el dueño pidió que
se oculte de la app — explícitamente **sin borrarlo**, hasta que él diga lo contrario.
El cambio no es trivial porque tiene tres consecuencias que el código debe sostener:

1. El interruptor lo decide el dueño del producto, no el catálogo de datos.
2. Apagar el módulo **no puede impedir** que los ordeños ya encolados en el outbox
   del móvil sigan subiendo: esa es la única ruta de pérdida silenciosa del plan.
3. El código del módulo apagado **sigue testeándose en CI**: esconder no es permiso
   para dejar de mantener, porque nadie va a ejercitarlo a mano mientras esté oculto.

Estos tres puntos vienen del ADR-0019 (mergeado en `develop`), que se referencia y no
se reabre acá.

## Decisiones tomadas en el macro plan que aplican a esta sub-rama

- Sección §2.3 del macro plan: el árbol de actividades de campo se filtra por
  **interruptor de módulo ∧ capacidades de la finca ∧ permisos del usuario**. Esta
  sub-rama introduce el primer término de la conjunción. El término "capacidades" lo
  evalúa [3.5a.9-B](./PLAN-FASE-3-5-PORCINO-3.5a.9-B.md); "permisos" ya existe vía
  ADR-0007.
- Sección §7 "Lo que sólo el cliente puede responder" del macro plan: el dueño decide
  cuándo apagar y encender cada módulo. El sistema no pregunta.

## Tareas

1. **Tabla `farm_modules`** con la forma exacta del ADR-0019 §1:
   `{ key, enabled, disabled_reason?, updated_at, updated_by }`. Catálogo semilla con
   todos los módulos actuales (`Production`, `Livestock`, `Inventory`, `Breeding`,
   `Tasks`, `People`).
2. **Sincronización al móvil vía pull**, igual que el resto de configuración (Art. 9):
   `farm_modules` entra a una colección del pull, el cliente la cachea y la evalúa
   localmente. La navegación no consulta la red para decidir qué mostrar.
3. **Conjunción de visibilidad** centralizada en una función de dominio del módulo
   que arranca la app móvil (`CanShowModule(key)`), evaluada **sin red**. La firma
   exacta se fija en la implementación; el contrato es:
   ```
   CanShowModule(key) := moduleEnabled[key] ∧ capabilities(key) ∧ userHasPermission(key)
   ```
   Donde `capabilities` lo provee 3.5a.9-B cuando exista, y hoy evalúa `true` para
   todos los módulos excepto `Production`, que mira `Species.IsMilkable` si el módulo
   está encendido. **Si el módulo está apagado, los otros términos no se evalúan.**
4. **Ocultar la entrada de Ordeño en `field-app`:** la pestaña en `App.tsx`, los botones
   en `EventsScreen`, el `BigButton` del menú principal. La pantalla `MilkingScreen`,
   el `milkingService.ts`, los endpoints `POST /api/v1/milking/...` y **todas sus
   pruebas** quedan intactos y deben seguir compilando y en verde.
5. **Pantalla de administración de módulos en el panel web** (`admin-web`/`quick-*`):
   lista los módulos con su `enabled` y `disabled_reason` editable; cambia el
   interruptor; propaga al próximo pull. La pantalla **nunca puede ocultarse a sí
   misma** aunque su propio módulo (admin-web) esté apagado (es el camino único para
   volver a encenderlo).
6. **Pruebas críticas** (detalladas más abajo).

## Pruebas

Las pruebas mínimas obligatorias son cinco; cada una protege una de las cinco
promesas del ADR-0019 que este plan hereda:

1. **Outbox no se interrumpe.** Sembrar un teléfono con un ordeño pendiente en el
   outbox y `farm_modules.production.enabled = false`. Verificar que el próximo push
   (con módulo apagado) sube el ordeño sin error. Esta es la pérdida silenciosa que
   el ADR-0019 §4 prohíbe.
2. **Pantalla no alcanzable.** Con el módulo apagado, intentar llegar a
   `MilkingScreen` por cada ruta existente en `field-app` (menú, deep-link,
   selector post-escaneo). Todas rechazan sin red.
3. **Encender devuelve sin tocar código.** Setear `farm_modules.production.enabled
   = true`, forzar un pull, abrir la app, verificar que la entrada y la pantalla
   vuelven a estar disponibles.
4. **CI no se rompe.** Después del cambio, la suite de Ordeño (lo que existe hoy)
   sigue corriendo y verde. Si se rompiera, el guardarraíl del ADR-0019 §7 falló.
5. **Autoadministración imposible.** Intentar apagar `admin-web` desde su propia
   UI. La pantalla debe rechazar o no permitirlo.

Adicional, no obligatorio pero recomendado:

- 200 animales sembrados, módulo apagado, intentar entrar al buscador de animales:
  el camino de ordeño no aparece, los animales sí.
- Sin red, cambiar el flag a mano en el pull del cliente (simulando un toggle
  recibido) y verificar que la UI reacciona sin pedir señal.

## Lo que NO incluye (queda para 3.5a.9-B o para Fase 4)

- El árbol de actividades completo, con sujeto como primer nivel y filtrado por
  especie-capacidades. Eso es [3.5a.9-B](./PLAN-FASE-3-5-PORCINO-3.5a.9-B.md).
- Búsqueda por identificador, "Recientes", filtro por lote en el selector de
  animales. Esas son mejoras del selector que se justifican cuando el sujeto
  "lote" es un primer nivel del árbol.
- El switch de los demás módulos (`Inventory`, `Breeding`, etc.). Sólo se
  implementa para `Production` como piloteo; el resto se enciende en una rama
  posterior si la finca los pide.
- La integración con `WithdrawalTarget.Meat` para alertas de bloqueo en carne (eso
  es 3.5b.7 del macro plan).

## Cómo probarlo

```bash
git fetch origin
git switch feature/field-app-module-visibility
dotnet test --configuration Release
cd clients/field-app && npm run typecheck && npm test
```

Para el flujo manual con la app:

```bash
# Sembrar el estado inicial
./scripts/seed-modules.sh
# Bajar el módulo
psql -d hato -c "UPDATE farm_modules SET enabled=false, disabled_reason='no aplica al piloto' WHERE key='production';"
# Reconstruir + instalar la app, intentar entrar a Ordeño: debe estar oculto.
# Reactivar, abrir la app con pull: la entrada vuelve.
```

## Riesgos específicos de esta sub-rama

| Riesgo | Mitigación |
|---|---|
| La navegación consulta red para el flag | El pull baja el flag, se evalúa local (Art. 9). |
| Alguien borra el módulo Production para "limpiar" | ADR-0019 §3 (no se borra) + CI existente que cubre el camino oculto bloquea el merge si falla. |
| Pantalla admin-web se oculta a sí misma en un descuido | Test específico obligatorio (punto 5 de Pruebas). |
| Pérdida de ordeños en outbox al apagar | Test específico obligatorio (punto 1 de Pruebas). |
