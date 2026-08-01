# SOUL.md — El alma del proyecto

> **Proyecto HATO** *(nombre de trabajo — cámbialo cuando encuentres el definitivo)*
> ERP ganadero para una finca real en Ecuador. Hecho por una persona, con años por delante, y con cariño.

---

## Por qué existe esto

Este proyecto no existe para venderse, para impresionar en un portafolio ni para cumplir un plazo.
Existe porque hay una finca real, con animales reales, y su historia hoy vive en cuadernos,
en la memoria del mayordomo y en la de su dueño. Cuando una vaca enferma, nadie recuerda con
certeza qué medicamento recibió hace dos años. Cuando una cerda pare, nadie sabe con datos si
es mejor madre que su hermana. Este sistema existe para que **nada de esa historia se pierda
nunca más**, y para que las decisiones de la finca se tomen con datos y no solo con intuición.

También existe por una segunda razón, igual de válida: es **mi** proyecto. Un lugar donde
aprender, hacer las cosas bien sin presión externa, y construir algo que dentro de diez años
siga funcionando y me siga dando orgullo.

## Lo que este proyecto ES

- Un **sistema de registro fiel** de la vida de la finca: cada animal, cada evento, cada litro,
  cada dólar. La verdad primero; los reportes bonitos después.
- Un **proyecto de largo aliento**: se construye por fases, cada fase termina en algo que
  se usa de verdad en la finca. El avance se mide en uso real, no en líneas de código.
- Un **laboratorio personal de excelencia**: clean code, arquitectura pensada, pruebas,
  decisiones documentadas. Sin fecha límite, pero con disciplina.
- Un sistema **extensible por diseño**: hoy vacas y leche; mañana queso, cerdos, cárnicos,
  equinos, turismo. El core no debe reescribirse para crecer — debe configurarse.

## Lo que este proyecto NO ES

- **No es una startup.** No hay inversores, no hay competencia, no hay prisa. Cuando sienta
  la tentación de "lanzar rápido y romper cosas", recordar: aquí lo que se rompe son datos
  de animales vivos y contabilidad real.
- **No es un parque de diversiones tecnológico.** Cada tecnología entra por una necesidad de
  la finca, no por curiosidad. La curiosidad se paga en ramas experimentales, nunca en `main`.
- **No es un reemplazo del contador, del veterinario ni de Agrocalidad.** Es la herramienta
  que les da datos limpios a todos ellos.
- **No es infinito.** Es grande, pero tiene mapa. Todo lo que no está en el roadmap es,
  por definición, "todavía no".

## Valores (en orden de prioridad)

1. **Los datos son sagrados.** Perder historial es el único fallo imperdonable. Backups
   probados > cualquier feature nueva.
2. **La finca manda.** Si el mayordomo no puede registrar el ordeño a las 5 AM sin señal,
   el sistema falló, aunque el código sea perfecto.
3. **Terminar fases, no perseguir perfección.** Hecho-y-usándose vence a perfecto-y-pendiente.
   La perfección se alcanza iterando sobre algo vivo.
4. **Simplicidad reversible.** Ante la duda, la opción más simple cuya decisión pueda
   revertirse después. La complejidad hay que ganársela con evidencia.
5. **Honestidad con uno mismo.** Si algo se estanca 3 semanas, se dice en voz alta (en el
   diario del proyecto) y se recorta alcance. Estancarse en silencio es como muere esto.

## La imagen del éxito

Es una mañana cualquiera dentro de unos años. El empleado registró el ordeño desde el potrero
sin señal y sincronizó al llegar a la casa. El sistema avisó que a la Pinta le termina hoy el
retiro de leche por el antibiótico. El panel muestra qué vacas son las mejores madres de los
últimos tres años, con su árbol genealógico. La contabilidad del mes cuadra y el contador
recibió su reporte. Y yo abro el código, después de meses sin tocar un módulo, y lo entiendo
en cinco minutos porque está limpio y documentado.

Ese día ya gané. Todo lo demás es propina.

---

*Si estás leyendo esto en un momento de duda o cansancio: el proyecto está bien.
Es grande porque tú lo quisiste grande. Vuelve al roadmap, elige la tarea más pequeña
de la fase actual, y termínala. Así se construye una catedral: una piedra a la vez.*
