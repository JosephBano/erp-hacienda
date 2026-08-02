# ADR-0007 — Modelo de roles y permisos en base de datos (RBAC granular)

- **Estado:** Aceptado
- **Fecha:** 2026-08-02
- **Fase del roadmap:** Fase 3 (Suelo en backend / Seguridad)

## Contexto

Hasta la Fase 2, la autorización en la API se manejaba mediante roles hardcodeados (`UserRole` como enum compilado con `{ Admin, Registrar, Veterinarian }`). La Constitución (Art. 8) prohíbe explícitamente mantener conceptos de dominio o roles como `enum` o código hardcodeado cuando deben ser configuración en base de datos.

Adicionalmente, el avance hacia la app móvil (Fase 3) y el módulo financiero (Fase 4) exige un control de acceso fino por empleado (ej. un operador de ordeño debe poder registrar leche sin tener acceso a reportes financieros o administración de usuarios; un veterinario requiere permisos sobre salud y recetas pero no sobre facturación).

## Decisión

1. **Esquema RBAC Granular en Base de Datos**: Migrar de `UserRole` enum a entidades de base de datos dentro del módulo `People`:
   - `roles`: `id` (Guid), `code` (string unique, p.ej. `"admin"`, `"registrar"`, `"veterinarian"`), `name`, `description`, `is_system` (bool para roles base no eliminables), `created_at`, `updated_at`.
   - `permissions`: `id` (Guid), `code` (string unique, p.ej. `"livestock.animals.read"`, `"production.milking.record"`, `"people.users.manage"`), `name`, `module`, `description`, `created_at`.
   - `role_permissions`: relación N:N entre `roles` y `permissions` (`role_id`, `permission_id`).
   - `user_roles`: relación N:N entre `users` y `roles` (`user_id`, `role_id`).

2. **Diseño extensible para ámbito por Grupo/Lote**:
   En esta primera versión, los permisos aplican a nivel global del sistema (`group_id` es `null`). Las tablas de asignación mantendrán una columna opcional `group_id` (nullable) para permitir en fases posteriores acotar permisos a lotes o grupos específicos sin requerir cambios de esquema destructivos.

3. **Autorización basada en Permisos en ASP.NET Core**:
   Sustituir `RequireRole` / `UserRole` por una política de autorización basada en permisos (`PermissionRequirement` + `PermissionAuthorizationHandler`). Un usuario tendrá acceso a una acción si posee al menos un rol que contenga el permiso requerido (`code`).

4. **Optimización de resolución de permisos**:
   Los permisos del usuario autenticado se resolverán y almacenarán en memoria por ciclo de vida de la petición HTTP (`Scoped` service / `IHttpContextAccessor`) para evitar consultas redundantes a la base de datos durante la ejecución de los handlers de autorización.

5. **Semilla e Invariante de Administración**:
   - Se ejecutará una migración con datos semilla que cree los permisos granulares del sistema y los tres roles base (`admin`, `registrar`, `veterinarian`), asignando el rol `admin` a los usuarios existentes.
   - Invariante de dominio: El sistema impedirá eliminar o deshabilitar el último usuario con rol de administrador activo.

## Alternativas consideradas

- **Mantener Enum `UserRole` en código**: Descartado por violar el Art. 8 de la Constitución y limitar la creación de roles personalizados en la finca.
- **ABAC (Attribute-Based Access Control) con evaluación de reglas complejas**: Descartado por complejidad prematura (Art. 17). RBAC granular satisface el 100% de los casos de uso presentes y previstos para la hacienda.

## Consecuencias

- **Positivas**:
  + Cumplimiento estricto del Art. 8 de la Constitución.
  + Administración dinámica de roles y permisos desde el panel Angular.
  + Control de acceso preciso para la app móvil offline (Fase 3) y contabilidad (Fase 4).
- **Negativas / Riesgos**:
  − Requiere migración de datos para convertir los usuarios y roles existentes al nuevo modelo.
  − Mayor número de tablas e indizaciones en el módulo `People`.
- **Condición de reversa**: Evidencia fundada de que la resolución de permisos agregue latencia inaceptable a las peticiones HTTP que no pueda solucionarse con índices adecuados y almacenamiento en caché scoped.
