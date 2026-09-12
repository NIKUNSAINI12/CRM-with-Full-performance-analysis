import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { API_BASE_URL } from '../../app';

interface Role {
  id: number;
  roleName: string;
  dashboardRoute?: string;
  defaultDashboardRoute?: string;
}

@Component({
  selector: 'app-role-management',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './role-management.html'
})
export class RoleManagementComponent implements OnInit {
  roles: Role[] = [];
  
  isModalOpen = false;
  isEditing = false;
  editingRoleId: number | null = null;
  
  newRoleName = '';
  newDashboardRoute = '/pm/dashboard';
  
  // Available default dashboards
  dashboards = [
    { label: 'Project Manager Dashboard', value: '/pm/dashboard' },
    { label: 'Developer Dashboard', value: '/developer/dashboard' },
    { label: 'Customer Home', value: '/customer/home' }
  ];

  activeTab: 'Roles' | 'StatusWorkflows' = 'Roles';

  // Status Workflows
  statusWorkflows: any[] = [];
  isStatusWorkflowModalOpen = false;
  newStatusWorkflow: any = { roleName: '', currentStatus: '', nextStatus: '', condition: '' };
  allStatuses: any[] = []; // to populate dropdowns

  constructor(private http: HttpClient) {}

  ngOnInit(): void {
    this.loadRoles();
    this.loadStatusWorkflows();
    this.loadAllStatuses();
  }

  setTab(tab: 'Roles' | 'StatusWorkflows'): void {
    this.activeTab = tab;
  }

  loadRoles(): void {
    this.http.get<Role[]>(`${API_BASE_URL}/Roles`, { withCredentials: true })
      .subscribe({
        next: (data) => this.roles = data,
        error: (err) => console.error('Error loading roles', err)
      });
  }

  loadStatusWorkflows(): void {
    this.http.get<any[]>(`${API_BASE_URL}/Roles/status-workflows`, { withCredentials: true })
      .subscribe({
        next: (data) => this.statusWorkflows = data,
        error: (err) => console.error('Error loading status workflows', err)
      });
  }

  loadAllStatuses(): void {
    this.http.get<any[]>(`${API_BASE_URL}/Manager/statuses`, { withCredentials: true })
      .subscribe({
        next: (data) => this.allStatuses = data,
        error: (err) => console.error('Error loading statuses', err)
      });
  }

  // Roles modal
  openCreateModal(): void {
    this.isEditing = false;
    this.editingRoleId = null;
    this.newRoleName = '';
    this.newDashboardRoute = '/pm/dashboard';
    this.isModalOpen = true;
  }

  openEditModal(role: Role): void {
    this.isEditing = true;
    this.editingRoleId = role.id;
    this.newRoleName = role.roleName;
    this.newDashboardRoute = role.defaultDashboardRoute || role.dashboardRoute || '/pm/dashboard';
    this.isModalOpen = true;
  }

  closeModal(): void {
    this.isModalOpen = false;
  }

  saveRole(): void {
    if (!this.newRoleName || !this.newDashboardRoute) {
      alert('Role Name and Dashboard Route are required.');
      return;
    }

    const payload = {
      roleName: this.newRoleName,
      defaultDashboardRoute: this.newDashboardRoute,
      isActive: true,
      permissionIds: []
    };

    if (this.isEditing && this.editingRoleId) {
      this.http.put(`${API_BASE_URL}/Roles/${this.editingRoleId}`, payload, { withCredentials: true })
        .subscribe({
          next: () => {
            this.loadRoles();
            this.closeModal();
          },
          error: (err) => {
            console.error('Error updating role', err);
            alert('Failed to update role.');
          }
        });
    } else {
      this.http.post(`${API_BASE_URL}/Roles`, payload, { withCredentials: true })
        .subscribe({
          next: () => {
            this.loadRoles();
            this.closeModal();
          },
          error: (err) => {
            console.error('Error creating role', err);
            alert('Failed to create role.');
          }
        });
    }
  }

  // Status Workflows modal
  openCreateStatusWorkflowModal(): void {
    this.newStatusWorkflow = { roleName: '', currentStatus: '', nextStatus: '', condition: '', isActive: true };
    this.isStatusWorkflowModalOpen = true;
  }

  closeStatusWorkflowModal(): void {
    this.isStatusWorkflowModalOpen = false;
  }

  saveStatusWorkflow(): void {
    if (!this.newStatusWorkflow.roleName || !this.newStatusWorkflow.currentStatus || !this.newStatusWorkflow.nextStatus) {
      alert('Role Name, Current Status, and Next Status are required.');
      return;
    }

    this.http.post(`${API_BASE_URL}/Roles/status-workflows`, this.newStatusWorkflow, { withCredentials: true })
      .subscribe({
        next: () => {
          this.loadStatusWorkflows();
          this.closeStatusWorkflowModal();
        },
        error: (err) => {
          console.error('Error creating status workflow', err);
          alert('Failed to create status workflow.');
        }
      });
  }

  deleteStatusWorkflow(id: number): void {
    if(confirm('Are you sure you want to delete this rule?')) {
      this.http.delete(`${API_BASE_URL}/Roles/status-workflows/${id}`, { withCredentials: true })
        .subscribe({
          next: () => this.loadStatusWorkflows(),
          error: (err) => console.error('Error deleting status workflow', err)
        });
    }
  }
}
