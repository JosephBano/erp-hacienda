import { TestBed } from '@angular/core/testing';
import { computed, signal } from '@angular/core';
import { of } from 'rxjs';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { ModuleVisibilityService } from './module-visibility.service';
import { ApiService, type FarmModuleDto } from './api.service';
import { AuthService, type LoginResult } from './auth.service';

describe('ModuleVisibilityService', () => {
  let service: ModuleVisibilityService;
  let mockApiService: {
    getFarmModules: ReturnType<typeof vi.fn>;
  };
  let mockAuthService: {
    currentUser: ReturnType<typeof signal<LoginResult | null>>;
  };

  const sampleModules: FarmModuleDto[] = [
    { key: 'inventory', enabled: true, disabledReason: null, parentKey: null },
    {
      key: 'inventory.transformations',
      enabled: false,
      disabledReason: 'disabled',
      parentKey: 'inventory',
    },
    { key: 'breeding', enabled: false, disabledReason: 'not used', parentKey: null },
  ];

  beforeEach(() => {
    mockApiService = {
      getFarmModules: vi.fn().mockReturnValue(of(sampleModules)),
    };

    mockAuthService = {
      currentUser: signal<LoginResult | null>(null),
    };

    TestBed.configureTestingModule({
      providers: [
        ModuleVisibilityService,
        { provide: ApiService, useValue: mockApiService },
        { provide: AuthService, useValue: mockAuthService },
      ],
    });

    service = TestBed.inject(ModuleVisibilityService);
  });

  it('loads module rows when user is authenticated / logs in', () => {
    expect(service.loaded()).toBe(false);
    expect(service.modules()).toEqual([]);

    // Simulate login
    mockAuthService.currentUser.set({
      token: 'jwt-123',
      expiresAt: '2026-10-01',
      userId: 'user-1',
      fullName: 'Admin User',
      roles: ['admin'],
      permissions: ['settings.farm-modules.read'],
    });
    TestBed.flushEffects();

    expect(mockApiService.getFarmModules).toHaveBeenCalled();
    expect(service.loaded()).toBe(true);
    expect(service.modules().length).toBe(3);
    expect(service.isVisible('inventory')).toBe(true);
    expect(service.isVisible('inventory.transformations')).toBe(false);
    expect(service.isVisible('breeding')).toBe(false);
  });

  it('reloads module rows after a change', () => {
    const updatedModules: FarmModuleDto[] = [
      { key: 'inventory', enabled: true, disabledReason: null, parentKey: null },
      {
        key: 'inventory.transformations',
        enabled: true,
        disabledReason: null,
        parentKey: 'inventory',
      },
    ];
    mockApiService.getFarmModules.mockReturnValue(of(updatedModules));

    service.loadModules();

    expect(service.modules().length).toBe(2);
    expect(service.isVisible('inventory.transformations')).toBe(true);
  });

  it('is reactive via computed when module rows change', () => {
    service.modules.set([
      { key: 'inventory', enabled: true },
      { key: 'inventory.transformations', enabled: false },
    ]);

    const canSeeTransformations = computed(() => service.isVisible('inventory.transformations'));
    const canSeeInventory = computed(() => service.isVisible('inventory'));

    expect(canSeeInventory()).toBe(true);
    expect(canSeeTransformations()).toBe(false);

    // Turn on inventory.transformations
    service.modules.set([
      { key: 'inventory', enabled: true },
      { key: 'inventory.transformations', enabled: true },
    ]);

    expect(canSeeTransformations()).toBe(true);

    // Turn off parent inventory -> both become hidden
    service.modules.set([
      { key: 'inventory', enabled: false },
      { key: 'inventory.transformations', enabled: true },
    ]);

    expect(canSeeInventory()).toBe(false);
    expect(canSeeTransformations()).toBe(false);
  });
});
