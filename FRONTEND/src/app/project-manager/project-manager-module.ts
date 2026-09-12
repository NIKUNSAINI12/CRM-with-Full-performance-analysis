import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';

import { ProjectManagerRoutingModule } from './project-manager-routing-module';
import { PmDashboardComponent } from './pm-dashboard/pm-dashboard';
import { TatDashboardComponent } from './tat-dashboard/tat-dashboard';
import { HierarchyChartComponent } from './hierarchy-chart/hierarchy-chart';



@NgModule({
  declarations: [
      
  

  
    
  ],
  imports: [
    CommonModule,
    ProjectManagerRoutingModule,
    PmDashboardComponent,
    TatDashboardComponent,
    HierarchyChartComponent
    
  
  ]
})
export class ProjectManagerModule { }
