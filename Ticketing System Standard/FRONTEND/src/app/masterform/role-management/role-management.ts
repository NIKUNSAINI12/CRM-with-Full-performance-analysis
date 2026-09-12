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
  permissions?: any[];
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

  activeTab: 'Roles' | 'StatusWorkflows' | 'AssignmentWorkflows' = 'Roles';

  // Role permissions
  permissionsList: any[] = [];
  selectedPermissionIds: number[] = [];

  // Status Workflows
  statusWorkflows: any[] = []; // to populate dropdowns
  isStatusWorkflowModalOpen = false;
  newStatusWorkflow: any = { roleName: '', currentStatus: '', nextStatuses: [], condition: '', isActive: true, isBlocked: false, sendEmail: false };
  allStatuses: any[] = []; // to populate dropdowns
  allDeveloperStatuses: any[] = []; // to populate dropdowns

  // Assignment Workflows
  assignmentWorkflows: any[] = [];
  isAssignmentWorkflowModalOpen = false;
  isEditingAssignmentWorkflow = false;
  editingAssignmentWorkflowId: number | null = null;
  newAssignmentWorkflow: any = { assignerRole: '', assignableToRoles: [], isActive: true };

  constructor(private http: HttpClient) { }

  ngOnInit(): void {
    this.loadRoles();
    this.loadPermissions();
    this.loadStatusWorkflows();
    this.loadAssignmentWorkflows();
    this.loadAllStatuses();
    this.loadAllDeveloperStatuses();
  }

  setTab(tab: 'Roles' | 'StatusWorkflows' | 'AssignmentWorkflows'): void {
    this.activeTab = tab;
  }

  loadRoles(): void {
    this.http.get<Role[]>(`${API_BASE_URL}/Roles`, { withCredentials: true })
      .subscribe({
        next: (data) => this.roles = data,
        error: (err) => console.error('Error loading roles', err)
      });
  }

  loadPermissions(): void {
    this.http.get<any[]>(`${API_BASE_URL}/Roles/permissions`, { withCredentials: true })
      .subscribe({
        next: (data) => this.permissionsList = data || [],
        error: (err) => console.error('Error loading permissionsList', err)
      });
  }

  isPermissionSelected(permissionId: number): boolean {
    return this.selectedPermissionIds.includes(permissionId);
  }

  togglePermission(permissionId: number): void {
    if (this.selectedPermissionIds.includes(permissionId)) {
      this.selectedPermissionIds = this.selectedPermissionIds.filter(id => id !== permissionId);
    } else {
      this.selectedPermissionIds = [...this.selectedPermissionIds, permissionId];
    }
  }

  loadStatusWorkflows(): void {
    this.http.get<any[]>(`${API_BASE_URL}/Roles/status-workflows`, { withCredentials: true })
      .subscribe({
        next: (data) => this.statusWorkflows = data,
        error: (err) => console.error('Error loading status workflows', err)
      });
  }

  expandedRoles: { [role: string]: boolean } = {};
  statusWorkflowSearchQuery: string = '';

  toggleRoleExpanded(role: string): void {
    this.expandedRoles[role] = !this.expandedRoles[role];
  }

  isRoleExpanded(role: string): boolean {
    return !!this.expandedRoles[role];
  }

  get groupedStatusWorkflows(): { roleName: string, workflows: any[] }[] {
    const rolesWithWorkflows = this.roles.map(r => r.roleName);
    const allRoles = Array.from(new Set([...rolesWithWorkflows, ...this.statusWorkflows.map(sw => sw.roleName)]));
    
    let groups = allRoles.map(role => ({
      roleName: role,
      workflows: this.statusWorkflows.filter(sw => sw.roleName === role)
    })).filter(g => g.workflows.length > 0);

    if (this.statusWorkflowSearchQuery && this.statusWorkflowSearchQuery.trim() !== '') {
      const q = this.statusWorkflowSearchQuery.toLowerCase().trim();
      groups = groups.filter(g => g.roleName.toLowerCase().includes(q));
    }

    return groups;
  }

  loadAllStatuses(): void {
    this.http.get<any[]>(`${API_BASE_URL}/Manager/statuses`, { withCredentials: true })
      .subscribe({
        next: (data) => this.allStatuses = data,
        error: (err) => console.error('Error loading statuses', err)
      });
  }

  loadAllDeveloperStatuses(): void {
    this.http.get<any[]>(`${API_BASE_URL}/Manager/developer-statuses`, { withCredentials: true })
      .subscribe({
        next: (data) => this.allDeveloperStatuses = data,
        error: (err) => console.error('Error loading developer statuses', err)
      });
  }

  get modalStatuses(): any[] {
    const role = (this.newStatusWorkflow.roleName || '').toLowerCase();
    if (role === 'developer' || role === 'assignee') {
      return this.allDeveloperStatuses;
    }
    return this.allStatuses;
  }

  // Roles modal
  openCreateModal(): void {
    this.isEditing = false;
    this.editingRoleId = null;
    this.newRoleName = '';
    this.newDashboardRoute = '/pm/dashboard';
    this.selectedPermissionIds = [];
    this.isModalOpen = true;
  }

  openEditModal(role: Role): void {
    this.isEditing = true;
    this.editingRoleId = role.id;
    this.newRoleName = role.roleName;
    this.newDashboardRoute = role.defaultDashboardRoute || role.dashboardRoute || '/pm/dashboard';
    this.selectedPermissionIds = (role.permissions || []).map((p: any) => p.id);
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
      permissionIds: this.selectedPermissionIds
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
  isEditingStatusWorkflow = false;
  editingStatusWorkflowId: number | null = null;

  openCreateStatusWorkflowModal(): void {
    this.isEditingStatusWorkflow = false;
    this.editingStatusWorkflowId = null;
    this.newStatusWorkflow = { roleName: '', currentStatus: '', nextStatuses: [], condition: '', isActive: true, isBlocked: false, sendEmail: false };
    this.isStatusWorkflowModalOpen = true;
  }

  openEditStatusWorkflowModal(flow: any): void {
    this.isEditingStatusWorkflow = true;
    this.editingStatusWorkflowId = flow.id;
    this.newStatusWorkflow = {
      roleName: flow.roleName,
      currentStatus: flow.currentStatus,
      nextStatuses: [...(flow.nextStatuses || [])],
      condition: flow.condition || '',
      isActive: flow.isActive,
      isBlocked: flow.isBlocked || false,
      sendEmail: flow.sendEmail || false
    };
    this.isStatusWorkflowModalOpen = true;
  }

  closeStatusWorkflowModal(): void {
    this.isStatusWorkflowModalOpen = false;
    this.isEditingStatusWorkflow = false;
    this.editingStatusWorkflowId = null;
  }

  isNextStatusSelected(statusName: string): boolean {
    return (this.newStatusWorkflow.nextStatuses || []).includes(statusName);
  }

  toggleNextStatus(statusName: string): void {
    const current = this.newStatusWorkflow.nextStatuses || [];
    if (current.includes(statusName)) {
      this.newStatusWorkflow.nextStatuses = current.filter((s: string) => s !== statusName);
    } else {
      this.newStatusWorkflow.nextStatuses = [...current, statusName];
    }
  }

  saveStatusWorkflow(): void {
    if (!this.newStatusWorkflow.roleName || !this.newStatusWorkflow.currentStatus) {
      alert('Role Name and Current Status are required.');
      return;
    }
    // If not blocking, require at least one next status
    if (!this.newStatusWorkflow.isBlocked && (!this.newStatusWorkflow.nextStatuses || this.newStatusWorkflow.nextStatuses.length === 0)) {
      alert('Please select at least one Next Status, or enable "Block All Updates".');
      return;
    }

    if (this.isEditingStatusWorkflow && this.editingStatusWorkflowId) {
      this.http.put(`${API_BASE_URL}/Roles/status-workflows/${this.editingStatusWorkflowId}`, this.newStatusWorkflow, { withCredentials: true })
        .subscribe({
          next: () => {
            this.loadStatusWorkflows();
            this.closeStatusWorkflowModal();
          },
          error: (err) => {
            console.error('Error updating status workflow', err);
            alert('Failed to update status workflow.');
          }
        });
    } else {
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
  }

  deleteStatusWorkflow(id: number): void {
    if (confirm('Are you sure you want to delete this rule?')) {
      this.http.delete(`${API_BASE_URL}/Roles/status-workflows/${id}`, { withCredentials: true })
        .subscribe({
          next: () => this.loadStatusWorkflows(),
          error: (err) => console.error('Error deleting status workflow', err)
        });
    }
  }

  // Assignment Workflows Logic
  loadAssignmentWorkflows(): void {
    this.http.get<any[]>(`${API_BASE_URL}/Roles/assignment-workflows`, { withCredentials: true })
      .subscribe({
        next: (data) => this.assignmentWorkflows = data,
        error: (err) => console.error('Error loading assignment workflows', err)
      });
  }

  get groupedAssignmentWorkflows(): { assignerRole: string, workflows: any[] }[] {
    const rolesWithWorkflows = this.roles.map(r => r.roleName);
    const allRoles = Array.from(new Set([...rolesWithWorkflows, ...this.assignmentWorkflows.map(aw => aw.assignerRole)]));
    
    return allRoles.map(role => ({
      assignerRole: role,
      workflows: this.assignmentWorkflows.filter(aw => aw.assignerRole === role)
    })).filter(g => g.workflows.length > 0);
  }

  openCreateAssignmentWorkflowModal(): void {
    this.isEditingAssignmentWorkflow = false;
    this.editingAssignmentWorkflowId = null;
    this.newAssignmentWorkflow = { assignerRole: '', assignableToRoles: [], isActive: true };
    this.isAssignmentWorkflowModalOpen = true;
  }

  openEditAssignmentWorkflowModal(flow: any): void {
    this.isEditingAssignmentWorkflow = true;
    this.editingAssignmentWorkflowId = flow.id;
    this.newAssignmentWorkflow = {
      assignerRole: flow.assignerRole,
      assignableToRoles: [flow.assignableToRole],
      isActive: flow.isActive
    };
    this.isAssignmentWorkflowModalOpen = true;
  }

  closeAssignmentWorkflowModal(): void {
    this.isAssignmentWorkflowModalOpen = false;
    this.isEditingAssignmentWorkflow = false;
    this.editingAssignmentWorkflowId = null;
  }

  isAssignableRoleSelected(roleName: string): boolean {
    return (this.newAssignmentWorkflow.assignableToRoles || []).includes(roleName);
  }

  toggleAssignableRole(roleName: string): void {
    const current = this.newAssignmentWorkflow.assignableToRoles || [];
    if (current.includes(roleName)) {
      this.newAssignmentWorkflow.assignableToRoles = current.filter((r: string) => r !== roleName);
    } else {
      this.newAssignmentWorkflow.assignableToRoles = [...current, roleName];
    }
  }

  saveAssignmentWorkflow(): void {
    if (!this.newAssignmentWorkflow.assignerRole || !this.newAssignmentWorkflow.assignableToRoles || this.newAssignmentWorkflow.assignableToRoles.length === 0) {
      alert('Assigner Role and at least one Target Role are required.');
      return;
    }

    if (this.isEditingAssignmentWorkflow && this.editingAssignmentWorkflowId) {
      (async () => {
        try {
          // Update the first one
          const firstRole = this.newAssignmentWorkflow.assignableToRoles[0];
          const updatePayload = {
            assignerRole: this.newAssignmentWorkflow.assignerRole,
            assignableToRole: firstRole,
            isActive: this.newAssignmentWorkflow.isActive
          };
          await this.http.put(`${API_BASE_URL}/Roles/assignment-workflows/${this.editingAssignmentWorkflowId}`, updatePayload, { withCredentials: true }).toPromise();

          // Create the remaining ones sequentially
          const extraRoles = this.newAssignmentWorkflow.assignableToRoles.slice(1);
          for (const role of extraRoles) {
            const payload = {
              assignerRole: this.newAssignmentWorkflow.assignerRole,
              assignableToRole: role,
              isActive: this.newAssignmentWorkflow.isActive
            };
            await this.http.post(`${API_BASE_URL}/Roles/assignment-workflows`, payload, { withCredentials: true }).toPromise();
          }

          this.loadAssignmentWorkflows();
          this.closeAssignmentWorkflowModal();
        } catch (err) {
          console.error('Error saving edited assignment workflows', err);
          alert('Failed to save some assignment rules.');
          this.loadAssignmentWorkflows();
          this.closeAssignmentWorkflowModal();
        }
      })();
    } else {
      // Create mode: save multiple rules sequentially to prevent database deadlocks/race conditions
      (async () => {
        try {
          for (const role of this.newAssignmentWorkflow.assignableToRoles) {
            const payload = {
              assignerRole: this.newAssignmentWorkflow.assignerRole,
              assignableToRole: role,
              isActive: this.newAssignmentWorkflow.isActive
            };
            await this.http.post(`${API_BASE_URL}/Roles/assignment-workflows`, payload, { withCredentials: true }).toPromise();
          }
          this.loadAssignmentWorkflows();
          this.closeAssignmentWorkflowModal();
        } catch (err) {
          console.error('Error creating assignment workflows', err);
          alert('Failed to save some assignment rules.');
          this.loadAssignmentWorkflows();
          this.closeAssignmentWorkflowModal();
        }
      })();
    }
  }

  deleteAssignmentWorkflow(id: number): void {
    if (confirm('Are you sure you want to delete this assignment rule?')) {
      this.http.delete(`${API_BASE_URL}/Roles/assignment-workflows/${id}`, { withCredentials: true })
        .subscribe({
          next: () => this.loadAssignmentWorkflows(),
          error: (err) => console.error('Error deleting assignment workflow', err)
        });
    }
  }
}
