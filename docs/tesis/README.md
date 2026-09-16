# `docs/tesis/` — El andamiaje de la tesis

> Este directorio responde **¿cómo se construye la tesis sobre este proyecto y dónde vive
> cada parte?** No contiene la tesis. Contiene el método, las plantillas y las reglas de
> privacidad que hacen posible escribirla sin filtrar datos de nadie.

## 1. La regla de oro de este directorio

**Aquí no entra ni un dato real.** Ni un nombre de cliente, ni el de la finca, ni una carta
firmada, ni una tabla de producción. Este repositorio tiene licencia MIT: cualquiera puede
clonarlo, y el historial de git no olvida.

```
docs/tesis/
├─ README.md            ← este archivo. Versionado.
├─ plantillas/          ← semillas vacías. Versionado.
└─ privado/             ← tu tesis. .gitignore lo excluye entero. Git no lo ve.
```

## 2. Cómo se trabaja

```bash
./scripts/tesis-init.sh     # copia plantillas/ -> privado/, sin sobrescribir nada
```

Escribes en `docs/tesis/privado/`. Nunca en `plantillas/`, salvo que quieras mejorar la
plantilla *para la próxima vez* — y entonces la mejora no puede llevar contenido tuyo.

**Respaldo:** `docs/BACKUPS.md` cubre la base de datos de la finca, **no cubre esto**. Tu
tesis necesita su propio respaldo (nube personal, disco externo, lo que sea) y necesita que
lo pruebes una vez. Perder la tesis la semana antes de entregarla es un final tonto para
años de trabajo.

**Reconstrucción:** si pierdes `privado/`, `tesis-init.sh` te devuelve el esqueleto. El
contenido no: eso es lo que protege tu respaldo.

## 3. Dónde vive lo que no puede estar aquí

| Qué | Dónde va | Por qué |
|---|---|---|
| Tesis en redacción | `docs/tesis/privado/` (local, ignorado) | Contiene nombres y datos reales |
| Cartas firmadas, consentimientos | Fuera del repo, en tu almacenamiento privado | Firmas y cédulas de personas |
| Datos crudos del piloto | Fuera del repo | Datos de finca de un tercero |
| Datos **anonimizados** para análisis | Fuera del repo mientras la tesis esté abierta | Anonimizar mal es fácil; revisarlo, caro |
| Plantillas, método, instrumentos | Aquí, versionado | No identifican a nadie |

`05-TRAMITES` lleva el **índice** de qué documentos firmados existen y dónde están. El
índice sí puede vivir en privado/; los documentos, no en el repo en ninguna forma.

## 4. La red de seguridad

- `.gitignore` excluye `docs/tesis/privado/` **como directorio completo**, no por patrón de
  archivo.
- El job `tesis-privacy-guard` de CI **falla el build** si algo bajo esa ruta aparece
  rastreado por git. Existe para el `git add -A` distraído, no porque se desconfíe del
  `.gitignore`.
- GitGuardian ya cubre credenciales. No cubre nombres de personas. Por eso este job.

## 5. Orden de lectura

| # | Documento | Qué responde |
|---|---|---|
| 0 | `plantillas/00-QUE-ES-UNA-TESIS.md` | ¿Qué es esto, de qué partes consta y qué me van a pedir? |
| 1 | `01-TEMA` | ¿Sobre qué es mi tesis y qué me propongo demostrar? |
| 2 | `02-MARCO-TEORICO` | ¿Sobre qué literatura y normas me apoyo? |
| 3 | `03-METODOLOGIA` | ¿Cómo lo voy a investigar y con qué instrumentos? |
| 4 | `04-PLAN-DE-DATOS` | ¿Qué mido, cuándo, a quién y cómo lo registro? |
| 5 | `05-TRAMITES` | ¿Qué papeles, permisos y presupuesto necesito? |
| 6 | `06-FUENTES` | ¿De dónde saco las fuentes y cómo las gestiono? |
| — | `BITACORA` | ¿Qué hice y qué decidí, con fecha? |

El `00` es guía de lectura pura: se lee, no se llena. Los demás son plantillas que se
llenan en `privado/`.

## 6. Entrega final

El markdown es el borrador de trabajo. La entrega al instituto será Word o PDF con **su**
formato obligatorio. La conversión se hace al final con Pandoc:

```bash
pandoc docs/tesis/privado/01-TEMA.md -o /ruta/privada/tesis.docx
```

No construyas esa tubería ahora. Primero hay que tener qué convertir.

## 7. Al terminar

Cuando la tesis esté defendida y aprobada: se borra `docs/tesis/` del árbol de trabajo en
un commit normal. El historial de git queda intacto y **no hay nada que purgar**, porque
las plantillas nunca tuvieron datos reales. Esa es exactamente la razón de la separación.
