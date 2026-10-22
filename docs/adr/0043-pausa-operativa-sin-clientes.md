# ADR-0043 — Pausa operativa de HATO por ausencia de clientes

- **Estado:** Aceptado (2026-10-22, decisión del propietario)
- **Fecha:** 2026-10-22
- **Fase del roadmap:** Transversal; pausa el trabajo de producto y la operación de staging
- **Relacionado:** ADR-0040 (cambio de cliente piloto), ADR-0031 (staging doméstico), ADR-0036 (monitorización autohospedada), arts. 1, 2 y 11 de la Constitución

## Contexto

El cliente del piloto anterior se desvinculó, como registró ADR-0040. La segunda
persona que se esperaba incorporar desistió antes de adoptar HATO. Al 2026-10-22
el proyecto no tiene ningún cliente activo ni una finca comprometida para validar
los criterios de salida de sus fases. El recorte de la Fase 4 acordado en ADR-0040
dependía de ese segundo prospecto; ya no representa una necesidad confirmada.

El servidor doméstico aloja staging y la monitorización HATO. Mantenerlos activos
consume recursos y exige mantenimiento sin aportar validación de uso real. El
repositorio, el historial y los datos existentes siguen sujetos a los arts. 1
y 2 de la Constitución. La VPS de producción y cualquier dato que custodie son
un ámbito distinto del servidor doméstico.

## Decisión

**Pausar el desarrollo orientado a clientes y retirar la infraestructura de HATO
del servidor doméstico, conservando el código, las decisiones y los datos.**

1. La Fase 3 sigue abierta y la Fase 3.5 sigue pausada. La Fase 4 deja de ser
   el siguiente trabajo comprometido para un cliente. Ningún criterio de salida
   se declara cumplido por avance técnico sin uso real (art. 11).
2. Se da de baja el staging de `joemanserver`: contenedores, imágenes, rutas
   8448/8449, acceso SSH de despliegue y el workflow que lo recreaba en cada
   push a `develop`. ADR-0031 queda como registro histórico de la decisión que
   ahora se revoca para este servidor.
3. Se da de baja la monitorización HATO alojada allí: Healthchecks, rutas
   8450/8451, temporizadores y receptor de señales desde la VPS. Los accesos
   exclusivos de HATO en la política de Tailscale deben retirarse sin afectar
   los demás servicios domésticos ni el acceso administrativo a la VPS.
4. Antes de retirar datos locales se conserva una copia privada verificable.
   No se borran eventos ni datos de negocio. Los archivos de configuración con
   credenciales no se incorporan al repositorio ni al ADR.
5. Esta decisión no ordena borrar datos ni apagar la VPS de producción. Su
   retención, respaldos, credenciales y eventual baja requieren un inventario
   separado. Mientras no haya cliente activo, HATO no se presenta como un
   servicio en producción. Antes de volver a servir una finca, se debe
   restablecer y probar la monitorización y la restauración mensual de backups
   que exige el art. 2.

## Alternativas consideradas

**Mantener staging y Healthchecks encendidos hasta encontrar otro cliente.**
Conserva una prueba continua, pero no resuelve la falta de validación en finca
y obliga a sostener infraestructura que ahora no se usa.

**Borrar el proyecto y todos sus datos.** Libera espacio, pero destruye
historial y decisiones que podrían necesitarse de nuevo; contradice el art. 1.

**Mover staging a otra máquina de inmediato.** Cambia el gasto y el lugar de
operación sin una finca que defina qué debe validarse. Se reevaluará cuando
exista una necesidad concreta.

## Consecuencias

El servidor doméstico queda disponible para otros servicios y dejan de
producirse despliegues de staging sin propósito. El código, las pruebas y los
documentos permanecen para una posible reanudación. No habrá un entorno HATO
permanente donde ejecutar pruebas de extremo a extremo mientras dure la pausa.

La retirada de Healthchecks elimina las alertas que llegaban al hogar desde la
VPS. Eso debe constar como una brecha operativa durante la pausa: si la VPS
continúa ejecutando tareas de backup, su estado se revisa directamente allí
hasta decidir su baja o instalar monitorización alternativa. No se atribuye
a este ADR una garantía de protección que ya no existe.

**Condición de reversa.** Reabrir cuando haya una finca que acepte usar HATO y
se levanten sus requisitos; entonces se redefine la fase prioritaria, se
verifica la custodia de los datos previos y se elige un staging y una
monitorización acordes con el nuevo despliegue.
