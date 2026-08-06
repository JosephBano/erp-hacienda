import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService, SyncConflictDto, SyncOperationDto } from '../../services/api.service';
import { IconComponent } from '../../shared/icon/icon.component';

/**
 * Two related trays a device's sync push feeds. Rejected operations (PLAN-FASE-3-4 §3.C:
 * "bandeja de conflictos y rechazos") are the invariant of zero silent loss made visible —
 * a push the server refused is never deleted, it lands here with its reason. LWW conflicts
 * (ADR-0008) are the narrower case of two devices editing the very same field.
 */
@Component({
  selector: 'app-sync-tray',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent],
  templateUrl: './sync-tray.component.html',
  styleUrls: ['./sync-tray.component.css']
})
export class SyncTrayComponent implements OnInit {
  private api = inject(ApiService);

  activeTab: 'rejected' | 'conflicts' = 'rejected';

  operations: SyncOperationDto[] = [];
  statusFilter = 'Rejected';

  conflicts: SyncConflictDto[] = [];

  loadError = false;

  ngOnInit(): void {
    this.loadOperations();
    this.loadConflicts();
  }

  loadOperations(): void {
    this.loadError = false;
    this.api.getSyncOperations(this.statusFilter || undefined).subscribe({
      next: (data) => (this.operations = data),
      error: () => (this.loadError = true)
    });
  }

  loadConflicts(): void {
    this.api.getSyncConflicts().subscribe({
      next: (data) => (this.conflicts = data),
      error: () => (this.loadError = true)
    });
  }
}
