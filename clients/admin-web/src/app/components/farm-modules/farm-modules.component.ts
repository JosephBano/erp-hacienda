import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService, type FarmModuleDto } from '../../services/api.service';
import { ModuleVisibilityService } from '../../services/module-visibility.service';
import { IconComponent } from '../../shared/icon/icon.component';

export interface ModuleTreeNode {
  module: FarmModuleDto;
  parentDisabled: boolean;
  disabledByParent: boolean;
  children: ModuleTreeNode[];
}

@Component({
  selector: 'app-farm-modules',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent],
  templateUrl: './farm-modules.component.html',
  styleUrls: ['./farm-modules.component.css'],
})
export class FarmModulesComponent implements OnInit {
  private api = inject(ApiService);
  private visibility = inject(ModuleVisibilityService);

  readonly moduleTree = signal<ModuleTreeNode[]>([]);
  readonly isLoading = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  pendingDisableKey: string | null = null;
  disableReason = '';
  validationError: string | null = null;

  ngOnInit(): void {
    this.loadModules();
  }

  loadModules(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.api.getFarmModules().subscribe({
      next: (modules) => {
        this.moduleTree.set(this.buildTree(modules));
        this.isLoading.set(false);
      },
      error: () => {
        this.errorMessage.set('Error al cargar la lista de módulos.');
        this.isLoading.set(false);
      },
    });
  }

  buildTree(modules: FarmModuleDto[]): ModuleTreeNode[] {
    const byParent = new Map<string | null, FarmModuleDto[]>();
    for (const m of modules) {
      const parent = m.parentKey ?? null;
      if (!byParent.has(parent)) {
        byParent.set(parent, []);
      }
      byParent.get(parent)!.push(m);
    }

    const buildNodes = (parentId: string | null, parentDisabled: boolean): ModuleTreeNode[] => {
      const list = byParent.get(parentId) ?? [];
      return list.map((m) => {
        const isSelfOrParentDisabled = parentDisabled || !m.enabled;
        const disabledByParent = parentDisabled && m.enabled;
        return {
          module: m,
          parentDisabled,
          disabledByParent,
          children: buildNodes(m.key, isSelfOrParentDisabled),
        };
      });
    };

    return buildNodes(null, false);
  }

  onToggle(module: FarmModuleDto): void {
    if (module.enabled) {
      this.pendingDisableKey = module.key;
      this.disableReason = '';
      this.validationError = null;
    } else {
      this.enableModule(module.key);
    }
  }

  cancelDisable(): void {
    this.pendingDisableKey = null;
    this.disableReason = '';
    this.validationError = null;
  }

  confirmDisable(key: string): void {
    const trimmed = this.disableReason.trim();
    if (!trimmed) {
      this.validationError = 'El motivo es obligatorio para desactivar el módulo.';
      return;
    }

    this.validationError = null;
    this.api.setFarmModuleEnabled(key, false, trimmed).subscribe({
      next: () => {
        this.pendingDisableKey = null;
        this.disableReason = '';
        this.successMessage.set(`Módulo "${key}" desactivado.`);
        this.visibility.loadModules();
        this.loadModules();
      },
      error: () => {
        this.errorMessage.set(`No se pudo desactivar el módulo "${key}".`);
      },
    });
  }

  private enableModule(key: string): void {
    this.api.setFarmModuleEnabled(key, true, undefined).subscribe({
      next: () => {
        this.successMessage.set(`Módulo "${key}" activado.`);
        this.visibility.loadModules();
        this.loadModules();
      },
      error: () => {
        this.errorMessage.set(`No se pudo activar el módulo "${key}".`);
      },
    });
  }
}
