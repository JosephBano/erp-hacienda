import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IconComponent, IconName } from '../icon/icon.component';

export interface CatalogColumn<T> {
  key: keyof T & string;
  label: string;
  /**
   * Optional custom render. When omitted, the value is shown as-is via T[column.key].
   */
  render?: (row: T) => string;
  /**
   * When true, the cell renders a check/cross icon for boolean values instead of
   * the raw true/false string. Keeps the responsive table honest about its content.
   */
  boolean?: boolean;
}

export interface CatalogAction<T> {
  label: string;
  iconName: IconName;
  /**
   * When provided, the action button only renders if the predicate returns true.
   * Used to hide "Desactivar" on rows that are already inactive, etc.
   */
  showWhen?: (row: T) => boolean;
}

export interface CatalogActionEvent<T> {
  action: CatalogAction<T>;
  row: T;
}

@Component({
  selector: 'app-catalog-table',
  standalone: true,
  imports: [CommonModule, IconComponent],
  templateUrl: './catalog-table.component.html',
  styleUrls: ['./catalog-table.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogTableComponent<T> {
  @Input({ required: true }) rows: T[] = [];
  @Input({ required: true }) columns: CatalogColumn<T>[] = [];
  @Input() actions: CatalogAction<T>[] = [];
  @Input() emptyMessage = 'Sin registros';
  @Input() rowInactive: (row: T) => boolean = () => false;

  @Output() action = new EventEmitter<CatalogActionEvent<T>>();

  isInactive(row: T): boolean {
    return this.rowInactive(row);
  }

  hasActions(): boolean {
    return this.actions.length > 0;
  }

  onActionClick(action: CatalogAction<T>, row: T): void {
    this.action.emit({ action, row });
  }

  isActionVisible(action: CatalogAction<T>, row: T): boolean {
    return action.showWhen ? action.showWhen(row) : true;
  }

  cellText(row: T, column: CatalogColumn<T>): string {
    if (column.render) {
      return column.render(row);
    }
    const value = (row as Record<string, unknown>)[column.key];
    if (value === null || value === undefined) {
      return '';
    }
    return String(value);
  }

  isBooleanColumn(column: CatalogColumn<T>): boolean {
    return column.boolean === true;
  }

  booleanValue(row: T, column: CatalogColumn<T>): boolean {
    const value = (row as Record<string, unknown>)[column.key];
    return value === true;
  }
}
