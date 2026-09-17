# tasks.md — Monitorización autohospedada

> [Spec](./spec.md) · [Plan](./plan.md) · [E2E](./test-e2e.md). Pendiente de ejecución.

- [x] **T1** Aprobar ADR-0036. Evidencia: aceptación explícita del propietario registrada el 2026-09-16.
- [ ] **T2** Leer instrucciones de home-server e inventariar versiones, puertos, DB y
  recursos; abrir rama y enlazar PR. Terminado: rutas de implementación concretas en su plan.
- [x] **T2.DOC** Completar investigación documental y crear paquete en
  home-server/docs/spec/hato-monitoring, rama feature/hato-monitoring-spec.
  Evidencia: Compose/Caddy/Beszel revisados, archivos y puertos propuestos concretos.
  T2 permanece pendiente por medición viva y PR, no por ausencia de diseño.
- [ ] **T3** Configurar Healthchecks y persistencia con Caddy privado. Terminado: E2E-1/3.
- [ ] **T4** Restringir panel y ping y custodiar URLs/SMTP. Terminado: E2E-1 sin secretos en git.
- [ ] **T5** Configurar correo de la cuenta de backups privadamente. Terminado: E2E-2 entrega real.
- [ ] **T6** Conectar éxitos/fallos de feature-0013. Terminado: E2E-2 no acepta dump solo local.
- [ ] **T7** Medir Beszel y Healthchecks/DB durante 24 h. Terminado: informe de RAM/CPU/disco
  cumple presupuesto o se revisa diseño antes de apertura.
- [ ] **T8** Respaldar y recuperar configuración/DB del monitor. Terminado: E2E-3/4.
- [ ] **T9** Actualizar runbooks y ambos repos; ejecutar checks/CI y registrar pruebas.
  Terminado: PRs enlazados y compuerta operativa de feature-0012 verificable.
