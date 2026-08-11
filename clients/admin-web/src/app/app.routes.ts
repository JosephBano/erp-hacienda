import { Routes } from '@angular/router';
import { DashboardComponent } from './components/dashboard/dashboard.component';
import { AnimalListComponent } from './components/animal-list/animal-list.component';
import { AnimalDetailComponent } from './components/animal-detail/animal-detail.component';
import { AnimalRegisterComponent } from './components/animal-register/animal-register.component';
import { QuickMilkingComponent } from './components/quick-milking/quick-milking.component';
import { QuickEventComponent } from './components/quick-event/quick-event.component';
import { BreedingDashboardComponent } from './components/breeding-dashboard/breeding-dashboard.component';
import { RolesManagementComponent } from './components/roles-management/roles-management.component';
import { AuditLogComponent } from './components/audit-log/audit-log.component';
import { SyncTrayComponent } from './components/sync-tray/sync-tray.component';
import { CatalogsComponent } from './components/catalogs/catalogs.component';
import { LoginComponent } from './components/login/login.component';
import { AnimalGroupsListComponent } from './components/animal-groups-list/animal-groups-list.component';
import { AnimalGroupCreateComponent } from './components/animal-group-create/animal-group-create.component';
import { AnimalGroupDetailComponent } from './components/animal-group-detail/animal-group-detail.component';
import { InventoryItemDetailComponent } from './components/inventory-item-detail/inventory-item-detail.component';
import { authGuard } from './guards/auth.guard';
import { permissionGuard } from './guards/permission.guard';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: '', component: DashboardComponent, canActivate: [authGuard] },
  { path: 'animals', component: AnimalListComponent, canActivate: [authGuard] },
  { path: 'animals/new', component: AnimalRegisterComponent, canActivate: [authGuard] },
  { path: 'animals/:id', component: AnimalDetailComponent, canActivate: [authGuard] },
  { path: 'milking', component: QuickMilkingComponent, canActivate: [authGuard] },
  { path: 'events', component: QuickEventComponent, canActivate: [authGuard] },
  { path: 'breeding', component: BreedingDashboardComponent, canActivate: [authGuard] },
  {
    path: 'animal-groups',
    component: AnimalGroupsListComponent,
    canActivate: [authGuard, permissionGuard('livestock.animals.write')]
  },
  {
    path: 'animal-groups/new',
    component: AnimalGroupCreateComponent,
    canActivate: [authGuard, permissionGuard('livestock.animals.write')]
  },
  {
    path: 'animal-groups/:id',
    component: AnimalGroupDetailComponent,
    canActivate: [authGuard, permissionGuard('livestock.animals.write')]
  },
  {
    path: 'inventory/items/:id',
    component: InventoryItemDetailComponent,
    canActivate: [authGuard, permissionGuard('inventory.items.write')]
  },

    component: RolesManagementComponent,
    canActivate: [authGuard, permissionGuard('people.roles.manage')]
  },
  {
    path: 'audit',
    component: AuditLogComponent,
    canActivate: [authGuard, permissionGuard('people.users.manage')]
  },
  {
    path: 'sync',
    component: SyncTrayComponent,
    canActivate: [authGuard, permissionGuard('people.users.manage')]
  },
  {
    path: 'catalogs',
    component: CatalogsComponent,
    canActivate: [authGuard, permissionGuard('livestock.species.manage')]
  },
  { path: '**', redirectTo: '' }
];
