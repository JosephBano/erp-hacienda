# 04 — Plan de recolección de datos

> Se llena en `docs/tesis/privado/04-PLAN-DE-DATOS.md`.
> Es el documento **más urgente** de esta carpeta, por una sola razón:

---

## ⚠️ Lo irreversible

**La línea base solo se puede tomar antes de instalar el sistema.**

Si el cliente 2 empieza a usar el software y después decides medir el "antes", el "antes" ya
no existe. No hay forma de reconstruirlo, ni con entrevistas, ni con los cuadernos, ni con
buena voluntad. Es el error número uno que hunde tesis de implementación de software, y es
el único de esta lista que no admite arreglo posterior.

Con el cliente 1 esto se perdió. **Con el cliente 2 todavía no has instalado nada: estás
exactamente en el único momento en que se puede hacer bien.**

**Regla:** ni una línea de código nuevo para el cliente 2, ni una instalación, hasta que la
línea base esté tomada y archivada.

---

## 1. Fases de recolección

| Fase | Cuándo | Qué se recoge | Dónde queda |
|---|---|---|---|
| **F0 — Línea base** | Antes de instalar nada | Cómo registran hoy, cuánto tardan, qué se pierde, qué volumen manejan | Fichas + fotos de cuadernos + entrevista |
| **F1 — Levantamiento** | Misma visita o siguiente | Requisitos ISO/IEC/IEEE 29148 (→ `03-METODOLOGIA` sec. 5) | Guía de entrevista + especificación |
| **F2 — Implantación** | Instalación y capacitación | Incidencias, dudas, tiempo de capacitación | Bitácora de campo |
| **F3 — Uso** | N semanas de uso real | Datos del sistema (automático) + observación puntual | Consultas SQL versionadas |
| **F4 — Cierre** | Al final del período | Cuestionario ISO/IEC 25010 + entrevista de salida | Cuestionario + transcripción |

**Define N ahora, no después.** Un período corto (4–6 semanas) es más defendible que uno
indefinido que quizá se interrumpa. Si el cliente permanece más tiempo, mejor: amplías.

## 2. Qué medir en la línea base (F0) — lista de verificación

Marca cada ítem cuando esté recogido **y archivado**:

- ☐ ¿Qué se registra hoy y en qué soporte? (cuaderno, hoja suelta, Excel, memoria)
- ☐ ¿Quién registra y cuándo? ¿En el momento del hecho o al final del día?
- ☐ **Volumen:** ¿cuántos eventos por semana de cada tipo? (entradas y salidas de insumos,
  tratamientos, pesajes, compras, pagos)
- ☐ **Oportunidad:** tiempo entre el hecho y su anotación. Pregúntalo y **obsérvalo**; la
  respuesta y la realidad rara vez coinciden.
- ☐ **Esfuerzo:** cronometra el registro de N eventos reales.
- ☐ **Completitud:** de los hechos que ocurrieron en un período verificable, ¿cuántos están
  anotados? Necesitas una fuente de contraste (facturas, la memoria del dueño, el conteo
  físico).
- ☐ **Errores y correcciones:** ¿qué se tacha, qué se corrige, qué se descubre mal después?
- ☐ **Consecuencias del vacío:** momentos concretos en que faltó un dato y costó dinero o
  tiempo. **Estas anécdotas son oro para el planteamiento del problema.**
- ☐ **Fotos de los registros en papel** (con permiso, y anonimizadas después)
- ☐ Inventario físico actual: qué hay, cuánto, dónde. Es tu punto cero de existencias.
- ☐ Conectividad real: dónde hay señal, dónde no, con qué operador.
- ☐ Dispositivos disponibles: qué teléfonos tienen, qué versión de Android.

## 3. Datos que salen del sistema (F3)

Cada métrica de la tabla de `03-METODOLOGIA` sec. 3 necesita su **consulta SQL escrita,
probada y versionada antes de empezar a medir**. Improvisarlas al final es como se descubre
que faltaba guardar un campo.

| Métrica | Consulta | Frecuencia | Verificada el |
|---|---|---|---|
| Oportunidad del registro | `fecha_evento` vs `created_at` | semanal | |
| Errores de captura | eventos de corrección ÷ total | semanal | |
| | | | |

> **Revisa antes de instalar si el sistema realmente guarda lo que vas a necesitar.** Si una
> métrica depende de un campo que no existe, ese es un cambio de código que debe entrar
> *antes* del despliegue. Descubrirlo en la semana 5 significa perder las semanas 1 a 4.

## 4. Archivo y respaldo

- **Dónde vive lo crudo:** _(ruta privada fuera del repositorio; escríbela aquí)_
- **Respaldo:** _(segundo lugar, distinto del primero)_
- **Respaldo probado el:** ______ ← restaura de verdad una vez, no lo asumas
- **Anonimización:** "Finca A", "Usuario 1", "Cliente 2". La tabla que mapea el código al
  nombre real vive aparte y no entra nunca a la tesis ni al repositorio.

## 5. Plan B

Si el cliente 2 se desvincula como el 1, ¿qué tesis te queda con los datos que ya tengas
recogidos hasta ese punto?

_(Respóndelo ahora, por escrito. Si la respuesta es "ninguna", el tema elegido en `01-TEMA`
es demasiado frágil y hay que corregirlo mientras todavía se puede.)_
