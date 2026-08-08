import { Component, ChangeDetectionStrategy, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IconComponent, IconName } from '../icon/icon.component';

export interface CatalogColumn<T> {
  /** Property of T to render. Required so the {{ row[col.key] }} interpolation stays strict. */
  key: keyof T & string;
  /**
   * Header text in Spanish. The spec contract is that the same string must appear as
   * `data-label` on every dynamic `<td>` of the column, so the responsive table reads
   * naturally on narrow screens.
   */
  label: string;
  /** Custom cell renderer. Falls back to stringification of the row property. */
  render?: (row: T) => string;
  /** When true, the cell renders a check/cross icon and never prints true/false. */
  boolean?: boolean;
}

export interface CatalogAction<T> {
  /** Stable label used by `data-action` and emitted with the action. */
  label: string;
  iconName: IconName;
  /** Hide the action for some rows (e.g. only show 'Activar' when isActive is false). */
  showWhen?: (row: T) => boolean;
  variant?: 'primary' | 'danger' | 'neutral';
}

@Component({
  selector: 'app-catalog-table',
  standalone: true,
  imports: [CommonModule, IconComponent],
  templateUrl: './catalog-table.component.html',
  styleUrls: ['./catalog-table.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogTableComponent<T extends { id?: string; key?: string }> {
  readonly rows = input.required<T[]>();
  readonly columns = input.required<CatalogColumn<T>[]>();
  readonly actions = input<CatalogAction<T>[]>([]);
  readonly emptyMessage = input<string>('Sin registros');

  /**
   * Emits the action label and the row the user clicked. The screen decides what to do
   * with the pair (call a deactivate endpoint, open an edit modal, etc.), keeping the
   * table component free of business logic.
   */
  readonly action = output<{ actionLabel: string; row: T }>();

  trackById(_index: number, row: T): string | number {
    return (row.id ?? row.key ?? _index) as string | number;
  }

  visibleActions(row: T): CatalogAction<T>[] {
    return this.actions().filter((a) => !a.showWhen || a.showWhen(row));
  }

  cellValue(row: T, column: CatalogColumn<T>): string {
    if (column.render) return column.render(row);
    const value = (row as Record<string, unknown>)[column.key];
    if (value === null || value === undefined) return '';
    return String(value);
  }

  isInactive(row: T): boolean {
    const value = (row as Record<string, unknown>)['isActive'];
    return value === false;
  }

  onAction(action: CatalogAction<T>, row: T): void {
    this.action.emit({ actionLabel: action.label, row });
  }
}
