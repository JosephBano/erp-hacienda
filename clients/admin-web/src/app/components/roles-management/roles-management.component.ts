import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService, PermissionDto, RoleDto, UserDto } from '../../services/api.service';
import { IconComponent } from '../../shared/icon/icon.component';

/**
 * Roles, permissions and who has which role (ADR-0007). The backend and its API service
 * methods already existed — createRole, getRoles, getUsers, assignUserRole — but nothing
 * in the panel ever called them, so there was no way to grant or review access without
 * going straight to the database.
 */
@Component({
  selector: 'app-roles-management',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent],
  templateUrl: './roles-management.component.html',
  styleUrls: ['./roles-management.component.css']
})
export class RolesManagementComponent implements OnInit {
  private api = inject(ApiService);

  roles: RoleDto[] = [];
  permissions: PermissionDto[] = [];
  users: UserDto[] = [];

  permissionsByModule: { module: string; items: PermissionDto[] }[] = [];

  newRoleCode = '';
  newRoleName = '';
  newRoleDescription = '';
  newRolePermissionIds = new Set<string>();

  /** Non-null while a role's name/description/permissions are being edited in place. */
  editingRoleId: string | null = null;
  editRoleName = '';
  editRoleDescription = '';
  editRolePermissionIds = new Set<string>();

  assignUserId = '';
  assignRoleId = '';

  successMessage = '';
  errorMessage = '';
  loadError = false;

  ngOnInit(): void {
    this.loadAll();
  }

  loadAll(): void {
    this.loadError = false;
    this.api.getRoles().subscribe({
      next: (data) => (this.roles = data),
      error: () => (this.loadError = true)
    });
    this.api.getPermissions().subscribe({
      next: (data) => {
        this.permissions = data;
        this.permissionsByModule = this.groupByModule(data);
      },
      error: () => (this.loadError = true)
    });
    this.api.getUsers().subscribe({
      next: (data) => {
        this.users = data;
        if (data.length > 0) this.assignUserId = data[0].id;
      },
      error: () => (this.loadError = true)
    });
  }

  private groupByModule(permissions: PermissionDto[]): { module: string; items: PermissionDto[] }[] {
    const groups = new Map<string, PermissionDto[]>();
    for (const p of permissions) {
      const list = groups.get(p.module) ?? [];
      list.push(p);
      groups.set(p.module, list);
    }
    return Array.from(groups.entries()).map(([module, items]) => ({ module, items }));
  }

  togglePermission(id: string): void {
    if (this.newRolePermissionIds.has(id)) {
      this.newRolePermissionIds.delete(id);
    } else {
      this.newRolePermissionIds.add(id);
    }
  }

  /**
   * Editing is offered only for custom roles. The three system roles (admin, registrar,
   * veterinarian) are the RBAC seed's baseline — the API would technically allow editing
   * them too, but doing that casually from this screen is how an admin locks themselves
   * out by unchecking `people.roles.manage` from their own role.
   */
  startEditingRole(role: RoleDto): void {
    this.errorMessage = '';
    this.successMessage = '';
    this.editingRoleId = role.id;
    this.editRoleName = role.name;
    this.editRoleDescription = role.description;
    this.editRolePermissionIds = new Set(role.permissions.map((p) => p.id));
  }

  cancelEditingRole(): void {
    this.editingRoleId = null;
  }

  toggleEditPermission(id: string): void {
    if (this.editRolePermissionIds.has(id)) {
      this.editRolePermissionIds.delete(id);
    } else {
      this.editRolePermissionIds.add(id);
    }
  }

  saveRoleEdits(): void {
    if (!this.editingRoleId) return;

    this.errorMessage = '';
    this.successMessage = '';

    if (!this.editRoleName.trim()) {
      this.errorMessage = 'El nombre del rol no puede quedar vacío.';
      return;
    }

    this.api.updateRole(this.editingRoleId, {
      roleId: this.editingRoleId,
      name: this.editRoleName.trim(),
      description: this.editRoleDescription.trim(),
      permissionIds: Array.from(this.editRolePermissionIds)
    }).subscribe({
      next: () => {
        this.successMessage = `Rol "${this.editRoleName}" actualizado.`;
        this.editingRoleId = null;
        this.loadAll();
      },
      error: (err) => {
        this.errorMessage = err?.error?.detail || 'No se pudo actualizar el rol.';
      }
    });
  }

  createRole(): void {
    this.errorMessage = '';
    this.successMessage = '';

    if (!this.newRoleCode.trim() || !this.newRoleName.trim()) {
      this.errorMessage = 'El código y el nombre del rol son obligatorios.';
      return;
    }

    this.api.createRole({
      code: this.newRoleCode.trim(),
      name: this.newRoleName.trim(),
      description: this.newRoleDescription.trim(),
      permissionIds: Array.from(this.newRolePermissionIds)
    }).subscribe({
      next: () => {
        this.successMessage = `Rol "${this.newRoleName}" creado.`;
        this.newRoleCode = '';
        this.newRoleName = '';
        this.newRoleDescription = '';
        this.newRolePermissionIds = new Set<string>();
        this.loadAll();
      },
      error: (err) => {
        this.errorMessage = err?.error?.detail || 'No se pudo crear el rol.';
      }
    });
  }

  assignRole(): void {
    this.errorMessage = '';
    this.successMessage = '';

    if (!this.assignUserId || !this.assignRoleId) {
      this.errorMessage = 'Seleccione un usuario y un rol.';
      return;
    }

    this.api.assignUserRole(this.assignUserId, this.assignRoleId).subscribe({
      next: () => {
        this.successMessage = 'Rol asignado correctamente.';
        this.loadAll();
      },
      error: (err) => {
        this.errorMessage = err?.error?.detail || 'No se pudo asignar el rol.';
      }
    });
  }
}
