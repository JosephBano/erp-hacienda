import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AppComponent } from './app.component';
import { AuthService } from './services/auth.service';
import { ModuleVisibilityService } from './services/module-visibility.service';

describe('AppComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [provideHttpClient(), provideRouter([])],
    }).compileComponents();
  });

  function createShell() {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    return fixture;
  }

  function menuButton(root: HTMLElement): HTMLButtonElement {
    const button = root.querySelector<HTMLButtonElement>(
      'button[aria-controls="primary-navigation"]',
    );

    if (!button) {
      throw new Error('Expected an accessible primary navigation menu button');
    }

    return button;
  }

  it('creates the application shell with the product name', () => {
    const fixture = createShell();

    expect(fixture.componentInstance).toBeTruthy();
    expect(fixture.componentInstance.title).toBe('HATO ERP');
  });

  it('renders a closed accessible navigation drawer by default', () => {
    const fixture = createShell();
    const root = fixture.nativeElement as HTMLElement;
    const button = menuButton(root);

    expect(button.getAttribute('aria-expanded')).toBe('false');
    expect(root.querySelector('#primary-navigation')).not.toBeNull();
    expect(root.querySelector('.sidebar-overlay')).toBeNull();
  });

  it('opens the drawer from the menu button and closes it from the overlay', () => {
    const fixture = createShell();
    const root = fixture.nativeElement as HTMLElement;
    const button = menuButton(root);

    button.click();
    fixture.detectChanges();

    expect(button.getAttribute('aria-expanded')).toBe('true');
    expect(root.querySelector('.sidebar')?.classList.contains('is-open')).toBe(true);

    const overlay = root.querySelector<HTMLButtonElement>('.sidebar-overlay');
    expect(overlay).not.toBeNull();
    overlay?.click();
    fixture.detectChanges();

    expect(button.getAttribute('aria-expanded')).toBe('false');
    expect(root.querySelector('.sidebar-overlay')).toBeNull();
  });

  it('closes the drawer when Escape is pressed', () => {
    const fixture = createShell();
    const root = fixture.nativeElement as HTMLElement;
    const button = menuButton(root);

    button.click();
    fixture.detectChanges();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();

    expect(button.getAttribute('aria-expanded')).toBe('false');
    expect(root.querySelector('.sidebar-overlay')).toBeNull();
  });

  it('closes the drawer after selecting a navigation destination', () => {
    const fixture = createShell();
    const root = fixture.nativeElement as HTMLElement;
    const button = menuButton(root);

    button.click();
    fixture.detectChanges();
    root.querySelector<HTMLAnchorElement>('#primary-navigation a')?.click();
    fixture.detectChanges();

    expect(button.getAttribute('aria-expanded')).toBe('false');
  });

  it('uses local vector icons instead of emoji glyphs in the shell', () => {
    const fixture = createShell();
    const root = fixture.nativeElement as HTMLElement;
    const emojiPattern = /[\u{1F000}-\u{1FAFF}\u{2600}-\u{27BF}]/u;

    expect(root.querySelectorAll('app-icon').length).toBeGreaterThan(0);
    expect(root.textContent ?? '').not.toMatch(emojiPattern);
  });

  it('hides breeding nav item when breeding is off, but keeps modules, roles, audit, sync', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const auth = TestBed.inject(AuthService);
    const modVis = TestBed.inject(ModuleVisibilityService);

    auth.currentUser.set({
      token: 'jwt-test',
      expiresAt: new Date(Date.now() + 3600000).toISOString(),
      userId: 'user-1',
      fullName: 'Test Admin',
      roles: ['Admin'],
      permissions: [
        'people.roles.manage',
        'people.users.manage',
        'settings.farm-modules.manage',
        'livestock.animals.write',
        'livestock.species.manage',
      ],
    });

    modVis.modules.set([
      { key: 'breeding', enabled: false },
      { key: 'livestock', enabled: true },
      { key: 'production', enabled: true },
    ]);
    modVis.loaded.set(true);

    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;

    // Breeding link must NOT exist in the DOM
    expect(root.querySelector('a[routerLink="/breeding"]')).toBeNull();
    expect(root.textContent).not.toContain('Reproducción y Alertas');

    // Modules, Roles, Audit, Sync, and Dashboard must remain present
    expect(root.querySelector('a[routerLink="/modules"]')).not.toBeNull();
    expect(root.querySelector('a[routerLink="/roles"]')).not.toBeNull();
    expect(root.querySelector('a[routerLink="/audit"]')).not.toBeNull();
    expect(root.querySelector('a[routerLink="/sync"]')).not.toBeNull();
    expect(root.querySelector('a[routerLink="/"]')).not.toBeNull();
  });
});
