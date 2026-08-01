# ADR-0006 — Identidad interna soberana + identificaciones externas adjuntas

- **Estado:** Aceptado
- **Fecha:** (fundación)

## Contexto
Los animales no siempre se registran ante SIFAE el día uno; algunos se venden antes de
registrarse; los aretes se caen y se reemplazan. Atar la identidad del sistema a un ID
externo pierde historial (riesgo inaceptable, Art. 1).

## Decisión
`animals.id` = UUID interno generado por el sistema (incluso offline en el móvil).
Tabla `animal_identifiers` con {tipo: FarmTag | OfficialTag(SIFAE) | RFID | Nombre,
valor, válido_desde, válido_hasta}. Búsqueda por cualquier identificador vigente.

## Alternativas
Arete oficial como clave (descartado: puede no existir, llegar tarde o cambiar),
código de finca como clave (descartado: se reutilizan y se repintan).

## Consecuencias
+ Cero pérdida de historia; animales informales y oficiales conviven; el historial de
re-identificación es en sí mismo trazabilidad.
− Toda búsqueda de usuario pasa por la tabla de identificadores (resolver con índice y
una vista/consulta canónica).
