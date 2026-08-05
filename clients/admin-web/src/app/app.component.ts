import { CommonModule } from '@angular/common';
import { Component, HostListener, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from './services/auth.service';
import { IconComponent } from './shared/icon/icon.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterModule, IconComponent],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css']
})
export class AppComponent {
  private router = inject(Router);
  auth = inject(AuthService);

  title = 'HATO ERP';
  isLoginPage = signal(this.router.url.startsWith('/login'));
  drawerOpen = signal(false);

  constructor() {
    this.router.events.pipe(filter((event) => event instanceof NavigationEnd)).subscribe((event) => {
      this.isLoginPage.set(event.urlAfterRedirects.startsWith('/login'));
      this.closeDrawer();
    });
  }

  toggleDrawer(): void {
    this.drawerOpen.update((isOpen) => !isOpen);
  }

  closeDrawer(): void {
    this.drawerOpen.set(false);
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closeDrawer();
  }

  logout(): void {
    this.closeDrawer();
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
