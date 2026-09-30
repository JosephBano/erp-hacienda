import { TestBed, ComponentFixture } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { FarmModulesComponent } from './farm-modules.component';
import { ApiService, type FarmModuleDto } from '../../services/api.service';
import { ModuleVisibilityService } from '../../services/module-visibility.service';

describe('FarmModulesComponent', () => {
  let fixture: ComponentFixture<FarmModulesComponent>;
  let component: FarmModulesComponent;
  let mockApiService: {
    getFarmModules: ReturnType<typeof vi.fn>;
    setFarmModuleEnabled: ReturnType<typeof vi.fn>;
  };
  let mockVisibilityService: {
    loadModules: ReturnType<typeof vi.fn>;
  };

  const sampleModules: FarmModuleDto[] = [
    { key: 'inventory', enabled: true, disabledReason: null, parentKey: null },
    {
      key: 'inventory.transformations',
      enabled: true,
      disabledReason: null,
      parentKey: 'inventory',
    },
    { key: 'breeding', enabled: false, disabledReason: 'no aplica', parentKey: null },
  ];

  beforeEach(async () => {
    mockApiService = {
      getFarmModules: vi.fn().mockReturnValue(of(sampleModules)),
      setFarmModuleEnabled: vi.fn().mockReturnValue(
        of({
          key: 'inventory',
          enabled: false,
          disabledReason: 'motivo de prueba',
          parentKey: null,
        }),
      ),
    };

    mockVisibilityService = {
      loadModules: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [FarmModulesComponent],
      providers: [
        { provide: ApiService, useValue: mockApiService },
        { provide: ModuleVisibilityService, useValue: mockVisibilityService },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(FarmModulesComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('renders modules tree with children shown under their parent', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const parentNode = compiled.querySelector('[data-module="inventory"]');
    expect(parentNode).toBeTruthy();

    const childNode = parentNode?.querySelector('[data-module="inventory.transformations"]');
    expect(childNode).toBeTruthy();
  });

  it('requires a reason when disabling a module', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const disableButton = compiled.querySelector('[data-toggle="inventory"]') as HTMLButtonElement;
    expect(disableButton).toBeTruthy();

    disableButton.click();
    fixture.detectChanges();

    // Reason input must be visible
    const reasonInput = compiled.querySelector(
      '[data-reason-input="inventory"]',
    ) as HTMLInputElement;
    expect(reasonInput).toBeTruthy();

    const confirmBtn = compiled.querySelector(
      '[data-confirm-disable="inventory"]',
    ) as HTMLButtonElement;
    expect(confirmBtn).toBeTruthy();

    // Confirming without reason should NOT call API
    confirmBtn.click();
    fixture.detectChanges();
    expect(mockApiService.setFarmModuleEnabled).not.toHaveBeenCalled();

    // Type a reason and confirm
    component.disableReason = 'no se usa en esta finca';
    fixture.detectChanges();
    confirmBtn.click();
    fixture.detectChanges();

    expect(mockApiService.setFarmModuleEnabled).toHaveBeenCalledWith(
      'inventory',
      false,
      'no se usa en esta finca',
    );
  });

  it('displays "apagado por su padre" when child is on but parent is off', () => {
    const modulesWithParentOff: FarmModuleDto[] = [
      { key: 'inventory', enabled: false, disabledReason: 'inventario apagado', parentKey: null },
      {
        key: 'inventory.transformations',
        enabled: true,
        disabledReason: null,
        parentKey: 'inventory',
      },
    ];
    mockApiService.getFarmModules.mockReturnValue(of(modulesWithParentOff));

    component.loadModules();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const childNode = compiled.querySelector('[data-module="inventory.transformations"]');
    expect(childNode).toBeTruthy();
    expect(childNode?.textContent?.toLowerCase()).toContain('apagado por su padre');
  });
});
