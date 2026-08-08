import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CatalogTableComponent, CatalogColumn, CatalogAction } from './catalog-table.component';
import { ICON_NAMES } from '../icon/icon.component';

interface Row {
  id: string;
  name: string;
  isActive: boolean;
}

const columns: CatalogColumn<Row>[] = [
  { key: 'name', label: 'Nombre' },
  { key: 'isActive', label: 'Activa', boolean: true },
];

const actions: CatalogAction<Row>[] = [
  { label: 'Desactivar', iconName: 'close', variant: 'danger' },
];

const rows: Row[] = [
  { id: 'r1', name: 'Bovino', isActive: true },
  { id: 'r2', name: 'Porcino', isActive: false },
];

@Component({
  standalone: true,
  imports: [CommonModule, CatalogTableComponent],
  template: `
    <app-catalog-table
      [rows]="rows()"
      [columns]="columns"
      [actions]="actions"
      [emptyMessage]="empty"
      (action)="onAction($event)"
    />
  `,
})
class HostComponent {
  rows = signal<Row[]>(rows);
  columns = columns;
  actions = actions;
  empty = 'Sin registros';
  lastAction: { actionLabel: string; row: Row } | null = null;

  onAction(event: { actionLabel: string; row: Row }): void {
    this.lastAction = event;
  }
}

describe('CatalogTableComponent', () => {
  function render(): ComponentFixture<HostComponent> {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HostComponent],
    }).compileComponents();
  });

  it('renders one column header per declared column plus an Acciones column when actions are provided', () => {
    const root = render().nativeElement as HTMLElement;
    const headers = [...root.querySelectorAll<HTMLTableCellElement>('thead th')];

    expect(headers).toHaveLength(columns.length + 1);
    expect(headers.map((h) => h.textContent?.trim().trim())).toEqual(['Nombre', 'Activa', 'Acciones']);
  });

  it('shows the empty message when no rows are provided', () => {
    const fixture = render();
    fixture.componentInstance.rows.set([]);
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    const emptyRow = root.querySelector('.empty-row');

    expect(emptyRow?.textContent?.trim()).toContain('Sin registros');
  });

  it('emits action with the row when a row action button is clicked', () => {
    const fixture = render();
    const root = fixture.nativeElement as HTMLElement;
    const row = root.querySelector<HTMLTableRowElement>('tbody tr');
    const actionButton = row?.querySelector<HTMLButtonElement>('button[data-action="Desactivar"]');

    expect(actionButton).not.toBeNull();
    actionButton!.click();
    fixture.detectChanges();

    expect(fixture.componentInstance.lastAction).not.toBeNull();
    expect(fixture.componentInstance.lastAction?.actionLabel).toBe('Desactivar');
    expect(fixture.componentInstance.lastAction?.row.id).toBe('r1');
  });

  it('renders boolean columns with check/cross icons rather than literal text', () => {
    const root = render().nativeElement as HTMLElement;
    const booleanCells = [
      ...root.querySelectorAll<HTMLTableCellElement>('tbody tr td[data-label="Activa"]'),
    ];

    expect(booleanCells).toHaveLength(2);
    const [activeCell, inactiveCell] = booleanCells as unknown as [HTMLElement, HTMLElement];

    expect(activeCell.textContent).not.toMatch(/\b(true|false)\b/);
    expect(inactiveCell.textContent).not.toMatch(/\b(true|false)\b/);

    const activeIcon = activeCell.querySelector('app-icon');
    const inactiveIcon = inactiveCell.querySelector('app-icon');
    expect(activeIcon).not.toBeNull();
    expect(inactiveIcon).not.toBeNull();
    expect((activeIcon as HTMLElement).getAttribute('name')).toBe('check');
    expect((inactiveIcon as HTMLElement).getAttribute('name')).toBe('close');

    ICON_NAMES.forEach((name) => {
      expect(name).toBeTruthy();
    });
  });

  it('flags inactive rows with a row-inactive class so they dim via CSS', () => {
    const root = render().nativeElement as HTMLElement;
    const rows = [...root.querySelectorAll<HTMLTableRowElement>('tbody tr')];
    const activeRow = rows.find((r) => r.querySelector('td[data-label="Nombre"]')?.textContent?.trim() === 'Bovino');
    const inactiveRow = rows.find((r) => r.querySelector('td[data-label="Nombre"]')?.textContent?.trim() === 'Porcino');

    expect(activeRow?.classList.contains('row-inactive')).toBe(false);
    expect(inactiveRow?.classList.contains('row-inactive')).toBe(true);
  });
});
