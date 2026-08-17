# test-e2e.md — Verificación manual extremo a extremo

> **Documento archivado.** Estos escenarios se ejecutaron durante los PRs #80–#83, cerrados el
> 2026-08-08. Se conservan como guion reproducible: son la forma de comprobar que la pantalla
> de lotes sigue funcionando después de cualquier cambio que la toque.
>
> Se ejecutan **después** de que la suite automatizada esté en verde. Las pruebas unitarias y
> de integración prueban las piezas; esto prueba que el flujo completo funciona tal como lo va
> a usar un administrador de la finca. Un escenario que falla se reporta con el paso exacto
> donde falló, no como "no anda".

**Antes de empezar:**

- Backend corriendo en `localhost:5000` con la base migrada.
- Dos usuarios: `admin` (tiene `livestock.animals.write`) y `veterinarian` (no lo tiene). El
  segundo es imprescindible: la mitad de lo que hay que verificar es lo que **no** debe poder
  hacer.
- `E2E-1` sólo necesita el backend. `E2E-2` y `E2E-3` necesitan además el panel servido.

---

## E2E-1 — Los cuatro verbos responden por HTTP

**Por qué:** es el escenario que se podía correr apenas mergeado el PR2, antes de que
existiera un solo píxel de UI. Sigue siendo la forma más rápida de aislar si un fallo es del
backend o del panel.

**Preparación:** ninguna; el escenario crea y destruye su propio grupo.

**Pasos:**

```bash
# 1. Login como admin
curl -c cookies.txt -X POST localhost:5000/api/v1/people/auth/login \
  -H 'Content-Type: application/json' -d '{"username":"admin","password":"..."}'

# 2. Crear (el POST ahora exige el permiso; admin lo tiene)
curl -b cookies.txt -X POST localhost:5000/api/v1/animal-groups \
  -H 'Content-Type: application/json' \
  -d '{"name":"E2E Manual","trackingMode":"Individual"}'

# 3. Actualizar
curl -b cookies.txt -X PUT localhost:5000/api/v1/animal-groups/<id> \
  -H 'Content-Type: application/json' -d '{"name":"E2E Manual v2"}'

# 4. Ver la lista con los campos derivados
curl -s -b cookies.txt localhost:5000/api/v1/animal-groups \
  | jq '.[] | {id, name, liveHeadCount, speciesName}'

# 5. Desactivar, y repetir para comprobar idempotencia
curl -b cookies.txt -X DELETE localhost:5000/api/v1/animal-groups/<id>
curl -b cookies.txt -X DELETE localhost:5000/api/v1/animal-groups/<id>

# 6. Reactivar
curl -b cookies.txt -X POST localhost:5000/api/v1/animal-groups/<id>/activate

# 7. Cambiar el modo (grupo vacío: debe funcionar)
curl -b cookies.txt -X PATCH localhost:5000/api/v1/animal-groups/<id>/tracking-mode \
  -H 'Content-Type: application/json' -d '{"trackingMode":"Headcount"}'

# 8. Agregar un miembro y volver a intentar el cambio de modo
curl -b cookies.txt -X POST localhost:5000/api/v1/animal-groups/<id>/members \
  -H 'Content-Type: application/json' -d '{"animalId":"<animal-id>"}'
curl -i -b cookies.txt -X PATCH localhost:5000/api/v1/animal-groups/<id>/tracking-mode \
  -H 'Content-Type: application/json' -d '{"trackingMode":"Individual"}'
```

**Debe pasar:**

- Pasos 2–3: 201 y 204.
- Paso 4: cada fila trae `liveHeadCount` numérico y `speciesName` resuelto (o `null` si el
  grupo no tiene especie). **Ninguna fila pide una petición extra** para tener esos datos.
- Paso 5: 204 las dos veces. La segunda no falla — el desactivado es idempotente.
- Paso 6: 204, y el grupo vuelve a aparecer en la lista por defecto.
- Paso 7: 204.
- Paso 8: **409 con `ProblemDetails`**. Este es el paso que importa: con un miembro activo, el
  modo deja de ser mutable.

---

## E2E-2 — El flujo completo del administrador

**Por qué:** es el recorrido que hace un administrador real la primera vez que configura un
lote, incluida la parte que más se equivoca — elegir mal el modo y tener que corregirlo.

**Preparación:** al menos una especie cargada, para que el desplegable no esté vacío.

**Pasos:**

1. Entrar como `admin`.
2. Click en "Lotes y Grupos" en el sidebar, bajo Administración.
3. Click en "Nuevo grupo". Completar nombre, elegir una especie del desplegable y marcar el
   modo **Por conteo**. Crear.
4. En el detalle, editar el nombre inline y guardar.
5. Volver a la lista.
6. Abrir el detalle otra vez y usar "Cambiar modo" → **Individual**. Guardar.
7. Agregar un miembro al grupo (por `curl`, paso 8 de E2E-1, o desde la pantalla de animales).
8. Intentar cambiar el modo de nuevo.
9. Desactivar el grupo: primer click, después el de confirmación.
10. Reactivarlo.

**Debe pasar:**

- Paso 2: la entrada existe en el sidebar y lleva a `/animal-groups`, con la tabla de grupos.
- Paso 3: al crear, redirige al detalle del grupo nuevo, que muestra **cabezas vivas = 0**.
- Paso 4: el cambio se guarda sin recargar la página.
- Paso 5: la lista refleja el nombre nuevo.
- Paso 6: el cambio de modo se aplica — el grupo está vacío, así que la guarda no dispara.
- Paso 8: la interfaz **avisa antes de intentarlo**, y si se confirma, muestra el error que
  llega del 409 en vez de tragárselo. El modo no cambia.
- Paso 9: hacen falta **dos clicks**; con uno solo no pasa nada. Después, el grupo desaparece
  de la lista por defecto.
- Paso 10: aparece "Reactivar", y al usarlo el grupo vuelve a la lista activa.

---

## E2E-3 — Quien no tiene el permiso no ve ni entra

**Por qué:** ocultar el link no es autorización. Hay que comprobar las dos capas por separado,
porque son dos mecanismos distintos y cualquiera de los dos puede romperse solo.

**Pasos:**

1. Cerrar sesión y entrar como `veterinarian`.
2. Mirar el sidebar.
3. Escribir `/animal-groups` directamente en la barra de direcciones.
4. Con la sesión de `veterinarian`, intentar por HTTP:
   ```bash
   curl -i -b cookies-vet.txt -X PUT localhost:5000/api/v1/animal-groups/<id> \
     -H 'Content-Type: application/json' -d '{"name":"no debería"}'
   curl -i -b cookies-vet.txt -X POST localhost:5000/api/v1/animal-groups \
     -H 'Content-Type: application/json' -d '{"name":"tampoco"}'
   ```
5. Con la misma sesión, hacer una lectura:
   ```bash
   curl -i -b cookies-vet.txt localhost:5000/api/v1/animal-groups
   ```

**Debe pasar:**

- Paso 2: "Lotes y Grupos" **no aparece**.
- Paso 3: redirige al dashboard. El guard de ruta actúa aunque el link esté oculto.
- Paso 4: **403 las dos veces**. La segunda es la que cubre el hueco preexistente de la
  creación sin permiso.
- Paso 5: **200**. Las lecturas siguen abiertas para un autenticado; el endurecimiento sólo
  alcanzó a la escritura, y pasarse de ahí rompería pantallas que hoy funcionan.

---

## Cierre de la verificación

Los tres escenarios en verde no cierran el trabajo por sí solos. Faltan:

- Los nueve criterios de aceptación de [`spec.md`](./spec.md) sec. 9.
- `dotnet test`, `ng test --watch=false` y `ng build` en exit 0, sin warnings de compilación
  nuevos.
