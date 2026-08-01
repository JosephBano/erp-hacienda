import { Routes } from '@angular/router';
import { DashboardComponent } from './components/dashboard/dashboard.component';
import { AnimalListComponent } from './components/animal-list/animal-list.component';
import { AnimalDetailComponent } from './components/animal-detail/animal-detail.component';
import { QuickMilkingComponent } from './components/quick-milking/quick-milking.component';
import { QuickEventComponent } from './components/quick-event/quick-event.component';
import { BreedingDashboardComponent } from './components/breeding-dashboard/breeding-dashboard.component';

export const routes: Routes = [
  { path: '', component: DashboardComponent },
  { path: 'animals', component: AnimalListComponent },
  { path: 'animals/:id', component: AnimalDetailComponent },
  { path: 'milking', component: QuickMilkingComponent },
  { path: 'events', component: QuickEventComponent },
  { path: 'breeding', component: BreedingDashboardComponent },
  { path: '**', redirectTo: '' }
];
