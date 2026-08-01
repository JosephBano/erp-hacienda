# Proyecto HATO — Documentos fundacionales

> ERP hacienda para una finca en Ecuador. Un desarrollador, años de horizonte, cero prisa,
> máxima disciplina. Estos documentos son la memoria del proyecto: para mí en el año 3,
> y para cualquier agente de IA que trabaje en el repo.
>
> *(HATO es nombre de trabajo — "hato" = rebaño/conjunto de ganado. Renómbralo cuando
> encuentres el definitivo; solo actualiza estos docs al hacerlo.)*

## Orden de lectura

| # | Documento | Qué es | Cuándo releerlo |
|---|---|---|---|
| 1 | [`SOUL.md`](SOUL.md) | Por qué existe esto y qué es el éxito | Cuando dude o me canse |
| 2 | [`CONSTITUTION.md`](CONSTITUTION.md) | Las reglas no negociables (20 artículos) | Antes de toda decisión grande |
| 3 | [`AGENTS.md`](AGENTS.md) | Protocolo para agentes de IA (y para mí) | Al iniciar sesión de trabajo con IA |
| 4 | [`GLOSSARY.md`](GLOSSARY.md) | Lenguaje ubicuo ES ↔ EN del dominio | Al modelar cualquier cosa nueva |
| 5 | [`ARCHITECTURE.md`](ARCHITECTURE.md) | Módulos, stack y decisiones de modelado | Al empezar cada módulo |
| 6 | [`ROADMAP.md`](ROADMAP.md) | Fases 0–7 con criterios de salida | Cada semana (¿en qué fase estoy?) |
| 7 | [`LEGAL-ECUADOR.md`](LEGAL-ECUADOR.md) | Agrocalidad/SIFAE, ARCSA, SRI, IESS, LOPDP | Antes de cada fase que lo toque |
| 8 | [`adr/`](adr/) | Decisiones de arquitectura (6 fundacionales + plantilla) | Antes de contradecir una |

## Uso previsto en el repositorio

```
hato/
├─ README.md            ← este archivo (o uno técnico que enlace a docs/)
├─ AGENTS.md            ← en la raíz: los agentes de código lo leen automáticamente
└─ docs/
   ├─ SOUL.md · CONSTITUTION.md · GLOSSARY.md
   ├─ ARCHITECTURE.md · ROADMAP.md · LEGAL-ECUADOR.md
   └─ adr/
```

## El proyecto en tres frases

1. **Todo es un evento**: la vida de la finca es una secuencia inmutable de hechos
   fechados; perder datos es el único fallo imperdonable.
2. **Lo específico es dato**: especies, productos, recetas y roles se configuran, no se
   programan — así leche→queso o vacas→equinos no reescriben el core.
3. **Cada fase termina usándose en la finca**: el avance se mide en uso real; lo demás
   es entretenimiento.

## Próximo paso concreto

Fase 0 del [`ROADMAP.md`](ROADMAP.md): crear el repo, GitFlow, CI, esqueleto del
monolito y `docker-compose` con PostgreSQL. Nada de features hasta que un PR de juguete
cruce el pipeline completo.
