# 05 — Trámites, documentos, presupuesto y cronograma

> Se llena en `docs/tesis/privado/05-TRAMITES.md`.
> **Los trámites tienen tiempos que no dependen de ti.** Una firma, una validación de
> instrumento o un turno de antiplagio pueden costar semanas. Es la causa más tonta de
> retraso de una tesis y la más fácil de evitar: se empiezan temprano.

---

## 1. Documentos del instituto

| Documento | ¿Lo exige? | Estado | Fecha | Dónde está |
|---|---|---|---|---|
| Reglamento de titulación vigente | — | ☐ conseguido | | |
| Formulario de aprobación del tema | ☐ | ☐ | | |
| Designación de tutor | ☐ | ☐ | | |
| Plan / anteproyecto | ☐ | ☐ | | |
| Informes de avance | ☐ | ☐ | | |
| Certificado antiplagio (% máximo: ___) | ☐ | ☐ | | |
| Certificado de tutor (aval final) | ☐ | ☐ | | |
| Declaración de autoría / cesión de derechos | ☐ | ☐ | | |
| Solicitud de fecha de defensa | ☐ | ☐ | | |

> La primera fila desbloquea todas las demás. Ver `00-QUE-ES-UNA-TESIS` sec. 0.

## 2. Documentos con el cliente / la finca

**Ninguno de estos archivos entra al repositorio.** Aquí queda solo el índice.

| Documento | Para qué | Estado | Firmado el | Dónde está |
|---|---|---|---|---|
| **Carta de autorización de la finca** | Permiso para desarrollar y **publicar** resultados | ☐ | | |
| **Consentimiento informado** (uno por participante) | Tratamiento de datos personales (LOPDP) | ☐ | | |
| **Acuerdo de piloto** | Qué das, qué recibes, por cuánto tiempo | ☐ | | |
| Acta de entrega / capacitación | Evidencia de implantación | ☐ | | |
| Acta de validación del cliente | El cliente confirma que el sistema hace lo acordado | ☐ | | |

### 2.1 Qué debe decir la carta de autorización

Membrete o datos de la finca · fecha · nombre del titular y su cédula/RUC · autorización
expresa para desarrollar el proyecto en sus instalaciones · **autorización para usar los
datos con fines académicos y publicar los resultados de forma anonimizada** · firma.

Esa frase intermedia es la que casi todo el mundo olvida, y es la que necesitas: sin ella
tienes permiso para trabajar pero no para publicar.

### 2.2 Qué debe decir el consentimiento informado

Quién eres y qué investigas · qué datos se recogen sobre esa persona · para qué se usan ·
que serán anonimizados · que la participación es voluntaria y puede retirarse · a quién
reclamar · firma y fecha.

### 2.3 El acuerdo de piloto — no te saltes esto

Con el cliente 1 el intercambio no quedó claro y la relación terminó sin explicación. Aunque
sea una página y sin abogado, deja escrito:

- Qué entregas (software, instalación, capacitación, soporte) y **qué no**.
- Qué recibes: acceso a los datos anonimizados, permiso de observación y publicación.
- Duración del piloto y qué pasa al terminar.
- Que el sistema está **en desarrollo**, que puede fallar, y de quién es la responsabilidad
  del respaldo de los datos de la finca.
- Qué licencia aplica al software entregado.

> **Nota, no académica pero importante:** el repositorio es MIT. Eso significa que quien
> recibe el software puede quedárselo, modificarlo y usarlo sin deberte nada. Es una decisión
> legítima, pero tómala a conciencia y no por herencia. Esto no afecta a la tesis; afecta a
> tu trabajo.

## 3. Presupuesto

El tribunal espera cifras, no exactitud contable. Incluye lo que **realmente** costó o
costará, y valoriza tu tiempo aunque no lo cobres — un proyecto que aparenta costo cero
resta credibilidad.

| Rubro | Detalle | Cantidad | Costo unit. | Total |
|---|---|---|---|---|
| **Talento humano** | Horas de desarrollo × tarifa referencial | | | |
| | Horas de levantamiento y capacitación | | | |
| **Infraestructura** | Servidor / VPS (meses) | | | |
| | Dominio y certificado | | | |
| | Respaldo en la nube | | | |
| **Equipos** | Teléfono(s) de campo | | | |
| | Computador de desarrollo (amortizado) | | | |
| **Software y servicios** | Cuentas de desarrollo (Expo/EAS, Play Store) | | | |
| | Herramientas de IA usadas en el desarrollo | | | |
| **Movilización** | Viajes a la finca | | | |
| **Trámites** | Antiplagio, empastado, copias | | | |
| **Imprevistos** | 10% | | | |
| | | | **TOTAL** | |

Sepáralo en **costo de desarrollo** y **costo de despliegue/operación anual**: son preguntas
distintas y al tribunal le gusta la segunda.

## 4. Cronograma

Diagrama de Gantt o tabla de semanas. Debe cuadrar con `04-PLAN-DE-DATOS` y con los plazos
del instituto.

| # | Actividad | Sem. 1-2 | 3-4 | 5-6 | 7-8 | 9-10 | 11-12 |
|---|---|---|---|---|---|---|---|
| 1 | Reglamento, tema y tutor | ■ | | | | | |
| 2 | Marco teórico y fuentes | ■ | ■ | | | | |
| 3 | Carta de autorización | ■ | | | | | |
| 4 | **Línea base (F0)** — antes de instalar | | ■ | | | | |
| 5 | Levantamiento de requisitos (F1) | | ■ | | | | |
| 6 | Desarrollo del módulo | | | ■ | ■ | | |
| 7 | Implantación y uso (F2–F3) | | | | ■ | ■ | |
| 8 | Evaluación y cierre (F4) | | | | | ■ | |
| 9 | Redacción, antiplagio, defensa | | | | | | ■ |

> Pon las actividades que dependen de terceros (firmas, tutor, antiplagio) **lo más temprano
> que el proceso permita**. Son las que se atrasan.

## 5. Riesgos

| Riesgo | Probabilidad | Impacto | Mitigación |
|---|---|---|---|
| El cliente 2 se desvincula como el 1 | Media | Alto | Plan B de `04-PLAN-DE-DATOS` sec. 5; tema que no dependa de permanencia |
| No se toma la línea base a tiempo | Media | **Irreversible** | No instalar nada antes de F0 |
| El alcance del módulo crece sin control | Alta | Alto | Recorte justificado en el levantamiento (ISO 29148, priorización) |
| Trámites fuera de plazo | Media | Medio | Empezarlos en las semanas 1–3 |
| Pérdida del borrador de la tesis | Baja | Alto | Respaldo propio, probado una vez |
