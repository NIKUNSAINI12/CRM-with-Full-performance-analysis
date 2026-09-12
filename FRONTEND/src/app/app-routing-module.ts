import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

// Your existing component imports
import { HomeComponent } from './customer/home/home';
import { TicketCreateComponent } from './customer/ticket-create/ticket-create';
import { TicketDetailComponent } from './customer/ticket-detail/ticket-detail';

// --- ADDED FOR LOGIN ---
import { LoginComponent } from './auth/login/login';
import { AuthGuard } from './Core/guards/auth-guard';

export const routes: Routes = [
  // CHANGED: The default path now redirects to the login page
  { path: '', redirectTo: '/login', pathMatch: 'full' },
  
  // ADDED: The new route for the login page
  { path: 'login', component: LoginComponent },
  
  // PROTECTED: Your existing routes are now protected by the AuthGuard
  { 
    path: 'customer/home', 
    component: HomeComponent,
    canActivate: [AuthGuard] // <-- Protected
  },
  { 
    path: 'customer/ticket-create', 
    component: TicketCreateComponent,
    canActivate: [AuthGuard] // <-- Protected
  },
  { 
    path: 'customer/ticket/:id',
    component: TicketDetailComponent,
    canActivate: [AuthGuard] // <-- Protected
  },
  { 
    path: 'customer/tickets', 
    component: HomeComponent,
    canActivate: [AuthGuard] // <-- Protected
  },
  { 
    path: 'pm/performance', 
    loadComponent: () => import('./performance-dashboard/performance-dashboard').then(c => c.PerformanceDashboardComponent),
    canActivate: [AuthGuard]
  },
  {
    path: 'notifications',
    loadComponent: () => import('./customer/notifications/notifications').then(c => c.NotificationsComponent),
    canActivate: [AuthGuard]
  },
  { path: 'login/:type', component: LoginComponent },
  // Your existing lazy-loaded routes remain untouched
  {
    path: 'pm',
    loadChildren: () => import('./project-manager/project-manager-module').then(m => m.ProjectManagerModule)
  },
  {
    path: 'developer',
    loadChildren: () => import('./assignee/assignees/Developer.routes').then(r => r.DEVELOPER_ROUTES)
  },
  {
    path: 'masterform',
    loadChildren: () => import('./masterform/masterform.route').then(r => r.MASTERFORM_ROUTES),
    canActivate: [AuthGuard]
  }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }