import { Injectable, effect, inject, signal } from '@angular/core';
import { ApiService } from './api.service';
import { AuthService } from './auth.service';
import { isVisible, type ModuleRow } from './module-visibility';

@Injectable({
  providedIn: 'root',
})
export class ModuleVisibilityService {
  private api = inject(ApiService);
  private auth = inject(AuthService);

  readonly modules = signal<ModuleRow[]>([]);
  readonly loaded = signal<boolean>(false);

  constructor() {
    effect(() => {
      const user = this.auth.currentUser();
      if (user) {
        this.loadModules();
      } else {
        this.modules.set([]);
        this.loaded.set(false);
      }
    });
  }

  /**
   * Fetches module switches from the server and updates the reactive signal.
   * Can be called on login and after changes made in the modules management view.
   */
  loadModules(): void {
    this.api.getFarmModules().subscribe({
      next: (data) => {
        this.modules.set(data);
        this.loaded.set(true);
      },
      error: () => {
        // Fail-open policy (S2): on failure, don't block navigation
        this.loaded.set(true);
      },
    });
  }

  /**
   * Evaluates visibility along the key chain reactively against the modules signal.
   * When called inside an Angular reactive context (computed, template, effect),
   * changes to modules() automatically re-trigger evaluation.
   */
  isVisible(key: string): boolean {
    return isVisible(key, this.modules());
  }
}
