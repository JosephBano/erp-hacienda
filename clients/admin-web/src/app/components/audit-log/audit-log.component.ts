import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService, AuditLogDto, UserDto } from '../../services/api.service';
import { IconComponent } from '../../shared/icon/icon.component';

/**
 * Who registered what, and when (LOPDP transparency clause + Art. 4). The backend has
 * carried this since `feature/people-audit-trail`; nothing in the panel ever showed it.
 */
@Component({
  selector: 'app-audit-log',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent],
  templateUrl: './audit-log.component.html',
  styleUrls: ['./audit-log.component.css']
})
export class AuditLogComponent implements OnInit {
  private api = inject(ApiService);

  logs: AuditLogDto[] = [];
  users: UserDto[] = [];

  userIdFilter = '';
  fromFilter = '';
  toFilter = '';

  page = 1;
  pageSize = 50;
  totalCount = 0;

  loadError = false;

  ngOnInit(): void {
    this.api.getUsers().subscribe({ next: (data) => (this.users = data), error: () => {} });
    this.search();
  }

  search(): void {
    this.loadError = false;
    this.api.getAuditLogs({
      userId: this.userIdFilter || undefined,
      from: this.fromFilter ? new Date(this.fromFilter).toISOString() : undefined,
      to: this.toFilter ? new Date(this.toFilter).toISOString() : undefined,
      page: this.page,
      pageSize: this.pageSize
    }).subscribe({
      next: (result) => {
        this.logs = result.items;
        this.totalCount = result.totalCount;
      },
      error: () => {
        this.loadError = true;
        this.logs = [];
      }
    });
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.totalCount / this.pageSize));
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages) return;
    this.page = page;
    this.search();
  }
}
