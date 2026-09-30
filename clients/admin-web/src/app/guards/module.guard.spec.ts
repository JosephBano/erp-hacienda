import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { moduleGuard } from './module.guard';
import { ModuleVisibilityService } from '../services/module-visibility.service';

describe('moduleGuard', () => {
  let mockVisibility: { isVisible: ReturnType<typeof vi.fn> };
  let router: Router;

  beforeEach(() => {
    mockVisibility = { isVisible: vi.fn() };

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: ModuleVisibilityService, useValue: mockVisibility },
      ],
    });

    router = TestBed.inject(Router);
  });

  it('allows navigation when module is visible', () => {
    mockVisibility.isVisible.mockReturnValue(true);

    const guard = moduleGuard('breeding');
    const result = TestBed.runInInjectionContext(() =>
      guard(
        {} as unknown as import('@angular/router').ActivatedRouteSnapshot,
        {} as unknown as import('@angular/router').RouterStateSnapshot,
      ),
    );

    expect(result).toBe(true);
    expect(mockVisibility.isVisible).toHaveBeenCalledWith('breeding');
  });

  it('redirects to / when navigating to a switched-off module', () => {
    mockVisibility.isVisible.mockReturnValue(false);

    const guard = moduleGuard('breeding');
    const result = TestBed.runInInjectionContext(() =>
      guard(
        {} as unknown as import('@angular/router').ActivatedRouteSnapshot,
        {} as unknown as import('@angular/router').RouterStateSnapshot,
      ),
    );

    const expectedTree = router.parseUrl('/');
    expect(result).toEqual(expectedTree);
    expect(mockVisibility.isVisible).toHaveBeenCalledWith('breeding');
  });
});
