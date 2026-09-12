import { Routes } from '@angular/router';

import { ProductListComponent } from './product-list/product-list';
import { ProductFormComponent } from './product-form/product-form';
import { StatusListComponent } from './status-list/status-list';
import { PriorityListComponent } from './priority-list/priority-list';
import { CategoryListComponent } from './category-list/category-list';
import { SourceListComponent } from './source-list/source-list';
import { RoleManagementComponent } from './role-management/role-management';
import { UserHubComponent } from './user-hub/user-hub';

export const MASTERFORM_ROUTES: Routes = [
  // Unified User Management
  { path: 'users', component: UserHubComponent },

  // Other Master Forms
  { path: 'products', component: ProductListComponent },
  { path: 'product/new', component: ProductFormComponent },
  { path: 'product/edit/:id', component: ProductFormComponent },
  { path: 'statuses', component: StatusListComponent },
  { path: 'priorities', component: PriorityListComponent },
  { path: 'categories', component: CategoryListComponent },
  { path: 'sources', component: SourceListComponent },
  { path: 'roles', component: RoleManagementComponent }
];