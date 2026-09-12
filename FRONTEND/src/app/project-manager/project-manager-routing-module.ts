import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { PmDashboardComponent } from './pm-dashboard/pm-dashboard'; 
import { TicketDetailComponent } from '../customer/ticket-detail/ticket-detail';
import { TatDashboardComponent } from './tat-dashboard/tat-dashboard';
import { HierarchyChartComponent } from './hierarchy-chart/hierarchy-chart';

const routes: Routes = [
  {
    path: 'dashboard',
    component: PmDashboardComponent
  },
  {
    path: 'tat',
    component: TatDashboardComponent
  },
  {
    path: 'hierarchy',
    component: HierarchyChartComponent
  },
  {
    // UPDATED: The path now just takes ticket id, pmId comes from Auth
    path: 'ticket/:id',
    component: TicketDetailComponent 
  },
  {
    path: '',
    redirectTo: 'dashboard', // Default PM ID
    pathMatch: 'full'
  },
  
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class ProjectManagerRoutingModule { }