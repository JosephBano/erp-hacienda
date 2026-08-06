# PLAN-FASE-3-5-PORCINO-3.5b.5-C.md — Advertencias visibles y versionado de definiciones

> **Sub-plan extraído de `PLAN-FASE-3-5-PORCINO.md` §3.5b.5.**
> Este archivo **es ejecutable de forma independiente** del macro plan y de sus
> pares 3.5b.5-A (núcleo de AnimalTrait) y 3.5b.5-B (absorción de SelectionCriterion).
> Depende de A mergeada. La dependencia con B es opcional pero útil: si B ya
> drenó, las primeras definiciones con `is_visible_as_alert = true` vienen
> seeded y se pueden mostrar de inmediato.

- **Rama Git:** `feature/livestock-trait-alerts-and-versioning`
- **ADR que la respalda:** [ADR-0018](../adr/0018-caracteristicas-observables-del-animal.md) §9 (versionado) + §6 (advertencias de campo).
- **Pares del split:**
  - [`3.5b.5-A`](./PLAN-FASE-3-5-PORCINO-3.5b.5-C.md) (rama `feature/livestock-animal-traits-core`): núcleo de AnimalTrait y TraitObservation — **requerido**.
  - [`3.5b.5-B`](./PLAN-FASE-3-5-PORCINO-3.5b.5-B.md) (rama `feature/livestock-selection-criterion-deprecation`): absorción de `SelectionCriterion` — **recomendada** (las semillas con alertas visibles vienen de B).
- **Fuente original:** [`PLAN-FASE-3-5-PORCINO.md` §3.5b.5](../PLAN-FASE-3-5-PORCINO.md#45--featurelivestock-animal-traits--estructural-adr-0018)

---

## Por qué existe esta sub-rama

Esta sub-rama resuelve los dos puntos del macro plan §3.5b.5 que más **se
notan en el campo** y que menos se pueden hacer sin el núcleo:

1. **Advertencias visibles.** Una característica marcada con `is_visible_as_alert
   = true` aparece en la ficha del animal en el móvil **antes** de que alguien
   lo toque. *"PATEA"* al empleado que llegó el lunes es — en palabras del
   macro plan — **lo de mayor valor por línea de código de toda la fase**.
   No sirve de nada definir `tetas funcionales` si esa información vive sólo
   en el sistema del veterinario y nunca aparece donde se necesita.
2. **Versionado de definiciones usadas.** Si una escala `EscalaOrdinal`
   `[manso, normal, nervioso]` pasa a `[muy_manso, manso, normal, nervioso,
   muy_nervioso, agresivo]` mañana, y el sistema las trata como el mismo
   concepto, **un 3 viejo y un 3 nuevo dejan de significar lo mismo en
   silencio**. El Art. 1 (historial inmutable) más el ADR-0018 §9 juntos
   dicen: una definición usada se **versiona**, no se edita.

Estos dos puntos comparten un mismo blast radius (la **integridad** de las
observaciones viejas) y un mismo frente de UI (la ficha del animal). Lo que
**no** comparten es el modelo de cambios: las advertencias son configuración
de display; el versionado es invariante estructural. Por eso viven juntos: el
reviewer encuentra el contrato completo en un solo PR.

## Decisiones tomadas en el macro plan y que aplican a esta sub-rama

- §3.5b.5 punto 7: "Una definición usada se versiona, no se edita (ADR-0018
  §9): si una escala 1–5 pasa a 1–10 con observaciones ya registradas, un 3
  viejo y un 3 nuevo dejan de significar lo mismo, en silencio."
- §3.5b.5 punto 6: "**Advertencias de campo**: `visible_como_advertencia`
  muestra la característica en la ficha del animal en el móvil, antes de que
  alguien lo toque."
- §3.5b.5 punto final de pruebas: "observación conserva su interpretación
  tras versionarse la definición".
- Glosario: `TraitAlert`, `CurrentDisposition` (que ya viene de A como derivado).

## Tareas

1. **Versionado por clonado.** Editar una `animal_traits` que tenga
   observaciones crea una **nueva fila** con `version = version_anterior + 1`,
   `is_active = true`, y la fila anterior se marca `is_active = false`. Las
   observaciones viejas quedan con su `trait_version_at_observation` apuntando
   a la versión que estaba vigente cuando se hicieron (esto lo setea A; acá se
   respeta).
2. **Operación atómica de cambio de definición.** Una transacción que:
   - Marca `is_active = false` en la versión vieja.
   - Inserta la versión nueva copiando todos los campos excepto `version`,
     `is_active`, `created_at`, `created_by_user_id`.
   Si falla cualquier paso, todo se revierte. **Nunca** hay un instante donde
   "no existe la versión vigente de una característica".
3. **`CurrentDisposition` lee de la versión vigente.** La función de A se
   extiende: cuando se pide el resumen derivado para una característica, las
   observaciones se interpretan con la versión que tenían al momento de
   observarse, no con la vigente. Esto **no** requiere cambiar la firma; es un
   cambio en la implementación de la consulta.
4. **UI de alertas en la ficha del animal** en `field-app`: bloque
   `TraitAlertList` arriba del bloque de identificación, con cada
   característica `is_visible_as_alert = true` con su última observación
   formateada. Si no hay observaciones: no aparece. La presencia de la alerta
   **se sincroniza con el pull** — sin red la ficha se ve igual.
5. **Sin red para alertas (Art. 9):** el conjunto de alertas vigentes y los
   valores actuales son cacheados localmente; se evalúan localmente. No
   consulta la red.
6. **Re-evaluación offline después de versionado.** Cambiar la definición
   globalmente con un cliente offline: el cliente cachea la versión vieja; al
   recuperar señal, hace pull y vuelve a cachear. La ficha local no se rompe
   en medio de la transición.
7. **UI de gestión de advertencias en `admin-web`:** pantalla que lista
   características con `is_visible_as_alert = true`, permite toggle y
   justificación de la alerta. La justificación es inmutable una vez hay
   observaciones referenciándola (es de display, no de contenido: si la
   razón cambia, se crea una nueva versión de display, no se toca la vieja).
   Para esta sub-rama basta con persistir la justificación; el versionado
   fino de display queda documentado como evolución futura.
8. **Re-evaluación de la prueba más importante del ADR-0018** — la
   "observación conserva su interpretación tras versionarse la definición":
   test específico que clona una `EscalaOrdinal`, cambia la escala, y verifica
   que la observación vieja se sigue interpretando con la escala vieja al
   consultarla con `CurrentDisposition` para esa versión.
9. **Pruebas críticas** (detalladas más abajo).

## Pruebas

Las pruebas mínimas obligatorias:

1. **Versionado por clonado funciona.** Sembrar `EscalaOrdinal { levels:
   [manso, normal, nervioso] }` con tres observaciones. Editarla a
   `{ levels: [muy_manso, manso, normal, nervioso, muy_nervioso] }`. Verificar:
   - La versión vieja queda `is_active = false` con sus tres observaciones
     intactas.
   - La versión nueva queda `is_active = true`.
   - Las tres observaciones viejas mantienen
     `trait_version_at_observation` apuntando a la versión vieja.
   - Una observación nueva usa la versión nueva.
2. **Atomicidad del versionado.** Forzar un fallo en mitad de la transacción
   (constraint invalid). El estado queda exactamente como antes: la versión
   vieja sigue activa, no se creó la nueva. Test concurrente: dos clientes
   editan al mismo tiempo, **uno** gana y el otro ve "definición actualizada,
   revisa tu UI".
3. **Interpretación preservada.** Una observación "3" sobre la escala vieja
   `[manso, normal, nervioso]` se sigue leyendo como `normal` aunque la
   escala vigente hoy tenga 5 niveles. `CurrentDisposition` para esa versión
   devuelve `normal`, no `?`.
4. **Alertas visibles en la ficha.** Sembrar un animal con dos
   características `is_visible_as_alert = true`, una con observación, otra sin.
   La ficha móvil muestra la primera, no la segunda.
5. **Alertas sin red.** Activar modo avión. Abrir la ficha del animal. Las
   alertas se ven igual (datos de la cache local).
6. **Toggle de alerta en el panel.** Sembrar característica con
   `is_visible_as_alert = false`. Marcar como `true` desde el panel. Pull del
   móvil: la alerta aparece.
7. **Sin RED para alertas**: el conjunto de alertas vigentes y los valores
   actuales son cacheados localmente; se evalúan localmente.
8. **Sin alertas no aparece nada.** Si no hay ninguna característica
   `is_visible_as_alert = true` para el animal, la ficha no muestra el
   bloque `TraitAlertList` (no muestra "no hay alertas", simplemente no
   aparece).
9. **Concurrencia cliente-editor.** Un cliente edita la definición mientras
   otro cliente abre la ficha. El editor observa la alerta vieja hasta que su
   propio pull refresca. No se pisan.

Adicional recomendado:

- `[3.5b.5-B](./PLAN-FASE-3-5-PORCINO-3.5b.5-B.md)` ya drenó — `Temperamento`
  e `Hernia` son `is_visible_as_alert = true` por default; este test los
  verifica al instante.

## Lo que NO incluye (queda para otras ramas)

- El cálculo del `MaternalIndex` (§4.6) y sus pesos configurables. Esta sub-rama
  deja la materia prima (observaciones firmadas, alertas visibles, versiones
  preservadas); el cálculo es una vista derivada que vive aparte.
- Las alertas de comportamiento adicionales ("se escapa del corral",
  "abre el pestillo") — se siembran progresivamente con input del cliente.
- El versionado fino de display (justificaciones que se versionan). Documentado
  como evolución futura.
- Las alertas contextuales que desaparecen después de un evento (p. ej.
  "aplasta crías en este parto"). Las observaciones contextuales existen (A)
  y se pueden consultar; los "displays condicionales" son otra capa.

## Cómo probarlo

```bash
git fetch origin
git switch feature/livestock-trait-alerts-and-versioning
dotnet test --configuration Release
cd clients/field-app && npm run typecheck && npm test
```

Para el flujo manual:

1. Sembrar una característica `Temperamento` con `is_visible_as_alert = true`
   y `levels = [manso, normal, nervioso]`. Registrar una observación `nervioso`
   sobre un animal X.
2. Abrir la ficha de X en el móvil. La alerta `Temperamento: nervioso` aparece
   en el bloque superior.
3. Editar la escala a 5 niveles. Re-pull. La alerta sigue diciendo
   `nervioso` (su versión vieja), no se pierde.
4. Verificar en BD: `temperamento_v1 is_active = false`,
   `temperamento_v2 is_active = true`, observación con
   `trait_version_at_observation = 1`.

## Riesgos específicos de esta sub-rama

| Riesgo | Mitigación |
|---|---|
| El versionado por clonado duplica filas y crece la tabla | Las definiciones cambian poco en la práctica; el ahorro semántico pesa más que el storage. Índice por `species + key + is_active` mantiene las consultas eficientes. |
| Concurrencia cliente-editor pisa definición | Test #2 concurrente explícito + transacción + retry sugerido al cliente. |
| `is_visible_as_alert` se cambia y rompe el contrato de display | La justificación inmutable (tarea 7) protege contra cambios silenciosos. |
| El móvil ignora la alerta cuando se edita la definición | La alerta se cachea por versión; se invalida cuando llega la nueva versión en el pull. |
| Alguien edita la versión vieja "para corregir" | El dominio rechaza la edición si `is_active = false`. |
