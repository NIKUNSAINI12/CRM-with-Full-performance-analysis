import { Routes } from '@angular/router';
import { AssigneesComponent } from './assignees';
import { DeveloperTicketDetailComponent } from '../developer-ticket-detail-component/developer-ticket-detail-component';


export const DEVELOPER_ROUTES: Routes = [
  {
    path: 'dashboard',
    component: AssigneesComponent 
  },
  {
    // The path now just takes ticket id, developerId comes from Auth
    path: 'ticket/:id', 
    component: DeveloperTicketDetailComponent 
  },
  {
    path: '', 
    redirectTo: 'dashboard', 
    pathMatch: 'full' 
  }
];