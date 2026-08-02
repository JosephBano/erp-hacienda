import { Routes } from '@angular/router';
import { DashboardComponent } from './components/dashboard/dashboard.component';
import { AnimalListComponent } from './components/animal-list/animal-list.component';
import { AnimalDetailComponent } from './components/animal-detail/animal-detail.component';
import { QuickMilkingComponent } from './components/quick-milking/quick-milking.component';
import { QuickEventComponent } from './components/quick-event/quick-event.component';
import { BreedingDashboardComponent } from './components/breeding-dashboard/breeding-dashboard.component';
import { LoginComponent } from './components/login/login.component';
import { authGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: '', component: DashboardComponent, canActivate: [authGuard] },
  { path: 'animals', component: AnimalListComponent, canActivate: [authGuard] },
  { path: 'animals/:id', component: AnimalDetailComponent, canActivate: [authGuard] },
  { path: 'milking', component: QuickMilkingComponent, canActivate: [authGuard] },
  { path: 'events', component: QuickEventComponent, canActivate: [authGuard] },
  { path: 'breeding', component: BreedingDashboardComponent, canActivate: [authGuard] },
  { path: '**', redirectTo: '' }
];
