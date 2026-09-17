# ADR-0031 — El servidor doméstico es el entorno de staging, y nunca producción

- **Estado:** Aceptado
- **Fecha:** 2026-09-15
- **Fase del roadmap:** Transversal. Condiciona el criterio "en producción real" (Art. 11).

## Contexto

El proyecto no tiene ningún entorno donde probar antes de la finca. Hasta hoy, todo cambio se
valida en el portátil del desarrollador y después en animales reales. El dueño dispone de un
servidor doméstico (`joemanserver`, repositorio `home-server`) y preguntó si sirve, mencionando
que su RAM sube de 8 a 12 GB en unos días.

Hechos verificados en `home-server/docs/SISTEMA.md` (ficha del 2026-09-01) y en aquel README:

| Componente | Valor | Lectura |
|---|---|---|
| Equipo | Laptop reutilizada | El propio repo dice que eso define sus prioridades |
| CPU | Intel i5-7200U, 2 núcleos / 4 hilos, 2017 | Suficiente para una API y un PostgreSQL de staging |
| RAM | 7.5 GB, **6.8 GB libres** con los servicios actuales | La ficha lo dice textual: *«No es un cuello de botella»* |
| Disco | SSD 931 GB, **uno solo, sin redundancia** | *«Un fallo del SSD se lleva todo»*, dice la propia ficha |
| Red | **WiFi**; el ethernet `enp2s0` está caído | Declarado como cuello de botella principal |
| Acceso | **SSH sobre Tailscale, únicamente** | Sin IP pública, sin port forwarding |
| SO | Ubuntu 24.04.4 LTS, Docker, LVM con `/srv` en volumen propio | Listo para recibir un stack de compose |

Servicios ya activos: Firefly III (finanzas personales del dueño), Portainer, Beszel, Caddy y
un servidor de Minecraft que arranca apagado.

Dos artículos de la Constitución pesan sobre esto:

- **Art. 1 — Los datos jamás se destruyen.** *«Perder historial es el único fallo
  imperdonable.»*
- **Art. 2 — Respaldos probados o nada.** *«Ninguna fase se declara "en producción" sin
  esto funcionando.»*

Y un hecho que el dueño no había considerado al preguntar: **la app de campo no puede
alcanzar un servicio que solo vive en el tailnet.** Los empleados en el potrero no están en
esa red.

## Decisión

**El servidor doméstico es el entorno de `staging` del proyecto, y no se usará como
producción.** Producción espera a un VPS, y esa será su propia decisión.

### Por qué no es producción

1. **Un solo disco sin redundancia.** Incompatible con el Art. 1 y el Art. 2 para los datos
   de una finca real. No lo arregla un respaldo —el respaldo mitiga la pérdida, no evita la
   caída— y el equipo no admite un segundo disco sin sacrificar la unidad óptica, que la
   propia ficha lista como pendiente.
2. **El usuario de producción no llega.** Tailscale-only significa que la app de campo, que es
   el cliente de producción, no alcanza el servicio. Un servidor de producción al que el
   usuario de producción no llega no es producción.
3. **Red WiFi en un enlace doméstico.** Aceptable para un entorno que nadie usa para trabajar;
   no para el registro del ordeño a las 5 AM.

### Por qué sí es un buen staging

Las tres objeciones se desvanecen cuando lo que corre ahí no importa: en staging no hay datos
que perder, el WiFi da igual, y que solo se alcance por Tailscale es una **ventaja** de
seguridad. Además el equipo ya tiene Docker, Caddy como capa de entrada, `/srv` en su propio
volumen lógico y monitorización con Beszel.

### Cómo vive el stack allí

- `ASPNETCORE_ENVIRONMENT: Staging`, no `Development`.
- **Sin publicar puertos al host.** El Caddy que ya corre expone el servicio dentro del
  tailnet, como hace con Firefly y Beszel.
- Volumen de datos bajo `/srv`, según el principio 4 de `home-server` (*«Datos fuera de la
  raíz»*).
- Imágenes con versión fija, **nunca `latest`** (principio 7 de `home-server`).
- La contraseña llega a un archivo de entorno escrito con permisos restringidos, no a la
  línea de comandos ni a un `.env` versionado.

### Lo que staging no tiene, a propósito

**Backups.** El Art. 2 aplica a producción. Staging se reconstruye desde cero por definición,
y un respaldo que nadie va a restaurar es ceremonia. Si algún día staging guarda algo que
duele perder, es señal de que se está usando mal.

**Rollback automático.** Si un despliegue falla, `docker compose up -d` no reemplaza un
contenedor sano por uno que no arranca: staging se queda en la versión anterior y el job
falla. Un mecanismo de rollback que nunca se ejercita es peor que no tenerlo. Producción sí
tendrá que resolverlo, y será su ADR.

### Obligación hacia el otro repositorio

El README de `home-server` dice: *«si no está en este repo, no debería estar en el
servidor»*. El stack de staging de HATO **debe declararse allí**, en un commit de aquel
repositorio. Es una dependencia externa de este trabajo y no se considera terminado sin ella.

## Alternativas consideradas

- **Usarlo como producción y confiar en backups offsite.** Descartada. El Art. 2 exige
  respaldo probado *además* de un entorno digno, no en su lugar, y el bloqueante de
  alcanzabilidad (la app de campo no llega) no lo resuelve ningún backup.

- **Esperar a la ampliación de RAM a 12 GB y reevaluar.** Descartada porque parte de un
  diagnóstico equivocado: la propia ficha del servidor dice que la RAM **no** es el cuello de
  botella, con 6.8 GB libres. Los cuellos reales son el disco único, el WiFi y los 2 núcleos,
  y ninguno lo toca añadir memoria.

- **Exponer el servicio a internet con el mecanismo de publicación de Tailscale** para que la
  app de campo llegue. Técnicamente viable y no requiere abrir puertos, pero convertiría una
  laptop doméstica con disco único y WiFi en el destino de los datos de una finca real. Es
  exactamente la decisión que el Art. 1 existe para impedir.

- **No tener staging y seguir validando en el portátil.** Es el estado actual, y es la razón
  por la que tres defectos de pérdida silenciosa de datos llegaron a una fase declarada
  cerrada (`docs/ROADMAP.md`, "Fase 3 — cierre revertido").

- **Un VPS pequeño ya para staging.** Descartada por ahora: cuesta dinero mensual para
  resolver algo que el hardware existente resuelve bien. El dinero mensual se reserva para
  producción, que es donde sí hace falta.

## Consecuencias

**Lo bueno.** Existe por primera vez un lugar donde un cambio se ejecuta de verdad antes de
tocar una finca. El costo marginal es cero: el hardware ya está encendido. Y queda por
escrito, citable, que staging no es producción, para que nadie lo confunda dentro de seis
meses.

**Lo malo.** El proyecto sigue sin destino de producción, así que el criterio del Art. 11
("cada fase termina en producción real") sigue dependiendo de instalar en la máquina de un
cliente. Este ADR no lo resuelve; lo hace explícito.

**Lo que hay que vigilar.**

1. **Que nadie empiece a guardar datos reales en staging** "solo para probar con algo de
   verdad". Es la forma más probable de que esta decisión se rompa.
2. **Que el stack quede declarado en `home-server`**, o el servidor tendrá algo no versionado
   y aquel repositorio dejará de ser su fuente de verdad.
3. **El tiempo de build en 2 núcleos.** Si construir en el servidor resulta inaceptable, la
   salida es un registro de imágenes, y eso es un ADR nuevo — no un parche. (Harbor en
   particular ya fue probado y retirado el 2026-09-15: es historia, no una opción abierta.)

**Condición de reversa.** Se reabre si: el servidor gana redundancia real de almacenamiento
**y** aparece una forma de que la app de campo lo alcance que no dependa de meter cada
teléfono de la finca en el tailnet. Ambas cosas, no una.
