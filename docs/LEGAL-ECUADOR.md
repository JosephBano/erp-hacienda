# LEGAL-ECUADOR.md — Mapa de cumplimiento

> ⚠️ **Este documento es una guía de trabajo, no asesoría legal.** Las resoluciones cambian.
> Antes de cada fase que toque uno de estos frentes: verificar con las fuentes oficiales
> (agrocalidad.gob.ec, sri.gob.ec, controlsanitario.gob.ec, iess.gob.ec) y con el contador.
> Registrar aquí la fecha de última verificación de cada sección.

## 1. Sanidad y trazabilidad animal — Agrocalidad / SIFAE
*Aplica desde: Fase 1 (datos) · Última verificación: __________*

- **Registro del predio** ante Agrocalidad (requisito base para operar formalmente).
- **Identificación animal oficial** (aretes) para bovinos y otras especies según normativa
  vigente; el SIFAE mantiene el historial y movimientos para trazabilidad.
- **Guías de movilización** zoosanitarias para todo transporte de animales (compra, venta,
  feria, faenamiento). → El sistema debe poder adjuntarlas a movimientos/ventas.
- **Campañas obligatorias** (p. ej., vacunación contra fiebre aftosa por ciclos) →
  calendario sanitario del sistema debe reflejarlas.
- Registro de tratamientos veterinarios y **períodos de retiro** (buenas prácticas
  pecuarias; además condición de compra de las procesadoras de leche).

**Diseño del sistema:** doble ID (interno + SIFAE opcional y tardío), eventos sanitarios
completos, adjuntos documentales, reportes exportables por si la autoridad los solicita.

## 2. Alimentos procesados — ARCSA (cuando llegue queso/cárnicos, Fase 5)
*Última verificación: __________*

- **Notificación/registro sanitario** por producto procesado antes de comercializar.
- Buenas Prácticas de Manufactura para el área de proceso.
- Etiquetado conforme a normativa ecuatoriana (incluido el sistema de etiquetado nutricional).
- Para faenamiento de cerdos/bovinos: usar centros de faenamiento autorizados; venta
  informal de carne tiene alto riesgo sanitario y legal.

## 3. Tributario — SRI
*Aplica desde: Fase 4 (ventas formales) · Última verificación: __________*

- RUC activo con actividad agropecuaria; régimen tributario definido con el contador
  (el agro tiene tratamientos particulares — p. ej., regímenes simplificados/RIMPE según
  el caso).
- **Facturación electrónica** obligatoria para la generalidad de contribuyentes:
  comprobantes XML firmados y autorizados. → Estrategia del proyecto: **proveedor
  autorizado con API primero** (Art. 18); integración directa solo en fase tardía.
- Retenciones, IVA (mucho producto agropecuario en estado natural tiene tarifa 0% — 
  confirmar por producto con el contador), anexos e impuesto a la renta.

## 4. Laboral — IESS y Código del Trabajo
*Aplica desde: que exista el primer empleado formal · Última verificación: __________*

- **Afiliación al IESS obligatoria desde el primer día** de trabajo.
- Contratos registrados (SUT), salario ≥ mínimo sectorial agropecuario, décimo tercero,
  décimo cuarto, vacaciones, fondos de reserva, utilidades si aplica.
- Jornadas y sobretiempos: el ordeño de madrugada y fines de semana deben encajar en un
  esquema de turnos legal — diseñarlo con asesoría, no improvisarlo.
- El módulo People debe guardar los datos que estos trámites piden (cédula, fechas,
  cargos, remuneraciones) aunque la nómina completa sea de fase tardía.

## 5. Datos personales — LOPDP
*Aplica desde: Fase 3 (app de empleados) · Última verificación: __________*

- La Ley Orgánica de Protección de Datos Personales aplica a datos de **empleados y
  clientes**: base de licitud, información al titular, seguridad, derechos ARCO.
- La app móvil registra actividad de empleados (quién registró qué y cuándo): informar
  esto por escrito al contratar; no recolectar más de lo necesario (p. ej., geolocalización
  solo si hay una razón operativa real y declarada).
- Medidas del sistema: cifrado en tránsito, contraseñas con hash, control de acceso por
  roles, auditoría — ya exigidas por la Constitución del proyecto.

### 5.1 Deber de Información al Empleado (Bitácora de Auditoría LOPDP - Art. 12/15)
Al registrar empleados en el sistema (Módulo People / App Móvil), se debe entregar por escrito o incluir en la cláusula de uso de herramientas de trabajo la siguiente notificación:

> **Cláusula Modelo de Transparencia de Auditoría Operativa:**
> *"El Administrado/Empleado conoce y acepta que el sistema de gestión ganadera (HATO) registra automáticamente la trazabilidad de sus operaciones (eventos de animales, registros de ordeño, consumo de alimento y cambios en el sistema) asociando su identificador de usuario, nombre, fecha y hora UTC. Esta información se recolecta exclusivamente para fines de auditoría operativa, control de calidad pecuario y cumplimiento de estándares zoosanitarios. Los registros de auditoría son inalterables e impiden su edición o eliminación."*

## 6. Otros frentes según crezca el proyecto

- **Turismo en la hacienda (Fase 7+):** registro turístico, LUAE/permisos municipales,
  bomberos, y tratamiento tributario distinto → tratarlo como negocio aparte con su
  centro de costo.
- **Bienestar animal y ambiente:** normas de manejo de desechos pecuarios (purines de
  cerdos son un tema serio), uso de agua; anticiparlo si la porcicultura escala.
- **Seguros:** no obligatorio, pero un seguro pecuario/agrícola es la contraparte "real"
  de los backups.

## Cómo el sistema convierte esto en ventaja

La mayoría de fincas sufre las inspecciones porque su información está dispersa. HATO
debe poder generar en minutos: lista de animales con identificación oficial y sin ella,
historial sanitario por animal, tratamientos con retiros respetados, movimientos con guías
adjuntas, y ventas facturadas. **Cumplir barato es una feature.**
