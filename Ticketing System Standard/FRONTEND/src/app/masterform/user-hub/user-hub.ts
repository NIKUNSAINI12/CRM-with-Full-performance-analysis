import { Component, OnInit, Inject, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { TICKET_SERVICE_TOKEN } from '../../Core/injection-tokens';
import { TicketService } from '../../Core/services/ticket.service';
import { Assignee, User } from '../../Core/models/ticket.model';
import { AuthService } from '../../Core/services/auth';
import { HttpClient } from '@angular/common/http';
import { API_BASE_URL } from '../../app';
import { firstValueFrom } from 'rxjs';

type MainTab = 'Customers' | 'InternalUsers';

@Component({
  selector: 'app-user-hub',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './user-hub.html',
  styleUrls: ['./user-hub.scss']
})
export class UserHubComponent implements OnInit {
  mainTab: MainTab = 'Customers';
  isLoading = false;
  isUploading = false;
  isSaving = false;

  // Messages
  successMessage = '';
  errorMessage = '';

  // Data
  customers: User[] = [];
  assignees: Assignee[] = [];
  products: any[] = [];
  pmList: any[] = [];  // Only PM-role users for manager assignment
  systemRoles: string[] = ['SuperManager', 'Manager', 'TL', 'Developer'];

  // Internal users combined
  internalUsers: any[] = [];
  filteredInternalUsers: any[] = [];

  // Customer filter
  filteredCustomers: User[] = [];
  customerPmFilter: number | '' = '';  // filter by assigned PM id

  // Search & filter state
  customerSearch = '';
  internalSearch = '';
  internalRoleFilter = '';

  // Executive searchable dropdown state
  executiveSearchQuery = '';
  showExecutiveDropdown = false;
  filteredExecutives: Assignee[] = [];

  // Product / Assignee search inside modal
  productSearchTerm = '';
  assigneeSearchTerm = '';

  // Modal
  showModal = false;
  modalMode: 'create' | 'edit' = 'create';
  userForm!: FormGroup;
  editingUserId: number | null = null;
  editingUserRole: string = '';

  @ViewChild('fileInput') fileInput!: ElementRef<HTMLInputElement>;

  constructor(
    @Inject(TICKET_SERVICE_TOKEN) private ticketService: TicketService,
    private fb: FormBuilder,
    private authService: AuthService,
    private http: HttpClient
  ) {
    this.initForm();
  }

  get isSuperUser(): boolean {
    const role = this.authService.getCurrentUser()?.role;
    return role === 'SuperManager' || role === 'Super Admin';
  }

  ngOnInit(): void {
    this.loadAllData();
  }

  initForm(): void {
    this.userForm = this.fb.group({
      fullName: ['', Validators.required],
      email: ['', [Validators.required, Validators.email]],
      contactPerson: [''],
      phoneNo: [''],
      mobileNo: [''],
      password: [''],
      role: [''],
      defaultAssigneeId: [null],
      managerId: [null],
      productIds: [[]],
      assigneeIds: [[]],
      isActive: [true]
    });
  }

  async loadAllData(): Promise<void> {
    this.isLoading = true;
    this.errorMessage = '';
    try {
      const [cust, asm, prods] = await Promise.all([
        this.ticketService.getUsersByRole('Customer'),
        this.ticketService.getAssignees(true),
        this.ticketService.getProducts()
      ]);

      this.customers = cust || [];
      this.assignees = asm || [];
      this.products = prods || [];
      this.filteredExecutives = this.assignees;

      // Filter PM-role users for manager dropdown (all non-Customer users who are PMs/Managers/TLs)
      const pmRoles = ['Manager', 'PM', 'Project Manager', 'TL', 'SuperManager', 'Super Admin'];
      this.pmList = (asm || []).filter((a: any) => pmRoles.includes(a.role || a.Role || ''));

      // Load dynamic roles
      const rolesData = await firstValueFrom(this.http.get<any[]>(`${API_BASE_URL}/Roles`, { withCredentials: true }));
      this.systemRoles = (rolesData || []).map(r => r.roleName).filter(r => r !== 'Customer');

      // The dynamic usp_GetAllAssignees returns all non-customer users from dbo.Users
      this.internalUsers = this.assignees;

      this.filterCustomers();
      this.filterInternalUsers();
    } catch (err: any) {
      console.error('Error loading users:', err);
      this.errorMessage = 'Failed to load user data';
    } finally {
      this.isLoading = false;
    }
  }

  getRolePillStyle(role: string): string {
    const r = role || 'User';
    switch (r) {
      case 'SuperManager':
        return 'background:rgba(168,85,247,0.12);border:1px solid rgba(168,85,247,0.25);color:#c084fc;';
      case 'Manager':
        return 'background:rgba(59,130,246,0.12);border:1px solid rgba(59,130,246,0.25);color:#60a5fa;';
      case 'TL':
        return 'background:rgba(20,184,166,0.12);border:1px solid rgba(20,184,166,0.25);color:#2dd4bf;';
      case 'Developer':
        return 'background:rgba(245,158,11,0.12);border:1px solid rgba(245,158,11,0.25);color:#fbbf24;';
      default:
        let hash = 0;
        for (let i = 0; i < r.length; i++) {
          hash = r.charCodeAt(i) + ((hash << 5) - hash);
        }
        const h = Math.abs(hash % 360);
        return `background:hsla(${h},70%,50%,0.12);border:1px solid hsla(${h},70%,50%,0.25);color:hsl(${h},80%,75%);`;
    }
  }

  getRoleIcon(role: string): string {
    const r = role || 'User';
    switch (r) {
      case 'SuperManager': return 'admin_panel_settings';
      case 'Manager': return 'manage_accounts';
      case 'TL': return 'supervisor_account';
      case 'Developer': return 'code';
      default: return 'person';
    }
  }

  getUserAvatarBackground(role: string): string {
    const r = role || 'User';
    switch (r) {
      case 'SuperManager':
        return 'background:linear-gradient(135deg,#9333ea,#7c3aed)';
      case 'Manager':
        return 'background:linear-gradient(135deg,#2563eb,#4f46e5)';
      case 'TL':
        return 'background:linear-gradient(135deg,#0d9488,#0891b2)';
      case 'Developer':
        return 'background:linear-gradient(135deg,#d97706,#b45309)';
      default:
        let hash = 0;
        for (let i = 0; i < r.length; i++) {
          hash = r.charCodeAt(i) + ((hash << 5) - hash);
        }
        const h1 = Math.abs(hash % 360);
        const h2 = (h1 + 40) % 360;
        return `background:linear-gradient(135deg,hsl(${h1},70%,50%),hsl(${h2},70%,40%))`;
    }
  }

  getRoleBadgeStyle(role: string): string {
    const r = role || 'User';
    switch (r) {
      case 'SuperManager':
        return 'background:rgba(168,85,247,0.15);color:#d8b4fe;border:1px solid rgba(168,85,247,0.3);';
      case 'Manager':
        return 'background:rgba(59,130,246,0.15);color:#93c5fd;border:1px solid rgba(59,130,246,0.3);';
      case 'TL':
        return 'background:rgba(20,184,166,0.15);color:#99f6e4;border:1px solid rgba(20,184,166,0.3);';
      case 'Developer':
        return 'background:rgba(245,158,11,0.15);color:#fde047;border:1px solid rgba(245,158,11,0.3);';
      default:
        let hash = 0;
        for (let i = 0; i < r.length; i++) {
          hash = r.charCodeAt(i) + ((hash << 5) - hash);
        }
        const h = Math.abs(hash % 360);
        return `background:hsla(${h},70%,50%,0.15);color:hsl(${h},80%,85%);border:1px solid hsla(${h},70%,50%,0.3);`;
    }
  }

  setMainTab(tab: MainTab): void {
    this.mainTab = tab;
    this.clearMessages();
  }

  clearMessages(): void {
    this.successMessage = '';
    this.errorMessage = '';
  }

  filterCustomers(): void {
    const term = this.customerSearch.toLowerCase().trim();
    const pmId = this.customerPmFilter ? Number(this.customerPmFilter) : null;
    this.filteredCustomers = this.customers.filter(c => {
      const matchSearch = !term ||
        c.fullName?.toLowerCase().includes(term) ||
        c.email?.toLowerCase().includes(term) ||
        c.userNumber?.toLowerCase().includes(term);
      const matchPm = !pmId || (c as any).managerId === pmId;
      return matchSearch && matchPm;
    });
  }

  getPmForCustomer(customer: any): any {
    if (!customer.managerId) return null;
    return this.pmList.find(pm => pm.id === customer.managerId || pm.id === Number(customer.managerId)) || null;
  }

  filterInternalUsers(): void {
    const term = this.internalSearch.toLowerCase().trim();
    const role = this.internalRoleFilter;
    this.filteredInternalUsers = this.internalUsers.filter(u => {
      const uRole = u.role || u.Role || '';
      const uName = u.fullName || u.FullName || '';
      const uEmail = u.email || u.Email || '';
      const uPhone = u.phoneNo || u.PhoneNo || u.mobileNo || u.MobileNo || '';
      const uNum = u.userNumber || u.UserNumber || u.assigneeNumber || u.AssigneeNumber || '';
      const matchRole = !role || uRole === role;
      const matchTerm = !term ||
        uName.toLowerCase().includes(term) ||
        uEmail.toLowerCase().includes(term) ||
        uRole.toLowerCase().includes(term) ||
        uNum.toLowerCase().includes(term) ||
        uPhone.toLowerCase().includes(term);
      return matchRole && matchTerm;
    });
  }

  get internalRoleCounts(): Record<string, number> {
    const counts: Record<string, number> = {};
    for (const u of this.internalUsers) {
      const r = u.role || u.Role || 'Unknown';
      counts[r] = (counts[r] || 0) + 1;
    }
    return counts;
  }

  // ── Modal ──

  openCreateModal(): void {
    this.clearMessages();
    this.modalMode = 'create';
    this.editingUserId = null;
    this.editingUserRole = '';
    this.productSearchTerm = '';
    this.assigneeSearchTerm = '';
    this.executiveSearchQuery = '';
    this.userForm.reset({ 
      role: this.mainTab === 'InternalUsers' ? 'Manager' : 'Customer',
      isActive: true 
    });

    if (this.mainTab === 'InternalUsers') {
      this.userForm.get('password')?.setValidators([Validators.minLength(6)]);
      this.userForm.get('role')?.setValidators([Validators.required]);
    } else {
      this.userForm.get('password')?.clearValidators();
      this.userForm.get('role')?.clearValidators();
    }
    this.userForm.get('password')?.updateValueAndValidity();
    this.userForm.get('role')?.updateValueAndValidity();
    this.showModal = true;
  }

  async openEditModal(user: any): Promise<void> {
    this.clearMessages();
    this.modalMode = 'edit';
    this.editingUserId = user.id;
    this.editingUserRole = user.role || '';
    this.productSearchTerm = '';
    this.assigneeSearchTerm = '';
    const currentAssignee = this.assignees.find(a => a.id === user.defaultAssigneeId);
    this.executiveSearchQuery = currentAssignee?.fullName || '';

    this.userForm.reset({
      fullName: user.fullName,
      email: user.email,
      contactPerson: user.contactPerson || '',
      phoneNo: user.phoneNo || '',
      mobileNo: user.mobileNo || '',
      role: user.role || '',
      defaultAssigneeId: user.defaultAssigneeId || null,
      managerId: user.managerId || null,
      productIds: [],
      assigneeIds: [],
      isActive: user.isActive !== false
    });

    if (this.mainTab === 'Customers') {
      try {
        const [pIds, aIds] = await Promise.all([
          this.ticketService.getUserProducts(user.id),
          this.ticketService.getUserAssignees(user.id)
        ]);
        this.userForm.patchValue({ productIds: pIds || [], assigneeIds: aIds || [] });
      } catch (err) {
        console.error('Failed to load user products/assignees:', err);
      }
    }

    this.userForm.get('password')?.clearValidators();
    this.userForm.get('password')?.updateValueAndValidity();
    this.showModal = true;
  }

  closeModal(): void {
    this.showModal = false;
    this.userForm.reset();
    this.productSearchTerm = '';
    this.assigneeSearchTerm = '';
    this.clearMessages();
  }

  // ── Products / Assignees in modal ──
  get filteredProducts() {
    if (!this.productSearchTerm) return this.products;
    const term = this.productSearchTerm.toLowerCase();
    return this.products.filter(p => p.name?.toLowerCase().includes(term));
  }

  isProductSelected(productId: number): boolean {
    const selected = this.userForm.get('productIds')?.value || [];
    return selected.includes(productId) || selected.includes(productId.toString());
  }

  toggleProduct(productId: number): void {
    const current = this.userForm.get('productIds')?.value || [];
    const updated = this.isProductSelected(productId)
      ? current.filter((id: any) => Number(id) !== productId)
      : [...current, productId];
    this.userForm.patchValue({ productIds: updated });
    this.userForm.get('productIds')?.markAsDirty();
  }

  get filteredAssigneesForCustomer() {
    if (!this.assigneeSearchTerm) return this.assignees;
    const term = this.assigneeSearchTerm.toLowerCase();
    return this.assignees.filter(a => a.fullName?.toLowerCase().includes(term));
  }

  isAssigneeSelected(assigneeId: number): boolean {
    const selected = this.userForm.get('assigneeIds')?.value || [];
    return selected.includes(assigneeId) || selected.includes(assigneeId.toString());
  }

  toggleAssignee(assigneeId: number): void {
    const current = this.userForm.get('assigneeIds')?.value || [];
    const updated = this.isAssigneeSelected(assigneeId)
      ? current.filter((id: any) => Number(id) !== assigneeId)
      : [...current, assigneeId];
    this.userForm.patchValue({ assigneeIds: updated });
    this.userForm.get('assigneeIds')?.markAsDirty();
  }

  filterExecutives(): void {
    const term = this.executiveSearchQuery.toLowerCase().trim();
    this.filteredExecutives = this.assignees.filter(a =>
      a.fullName?.toLowerCase().includes(term) ||
      (a as any).assigneeNumber?.toLowerCase().includes(term) ||
      a.id?.toString().includes(term)
    );
  }

  onExecutiveSearchInput(): void {
    this.showExecutiveDropdown = true;
    this.filterExecutives();
    if (!this.executiveSearchQuery.trim()) {
      this.userForm.patchValue({ defaultAssigneeId: null });
    }
  }

  selectExecutive(executive: any): void {
    this.userForm.patchValue({ defaultAssigneeId: executive.id });
    this.executiveSearchQuery = executive.fullName || '';
    this.showExecutiveDropdown = false;
  }

  onExecutiveBlur(): void {
    setTimeout(() => {
      this.showExecutiveDropdown = false;
      const selectedId = this.userForm.get('defaultAssigneeId')?.value;
      const current = this.assignees.find(a => a.id === selectedId);
      this.executiveSearchQuery = current?.fullName || '';
    }, 200);
  }

  // ── Save ──
  async saveUser(): Promise<void> {
    if (this.userForm.invalid) {
      this.userForm.markAllAsTouched();
      return;
    }

    this.isSaving = true;
    this.clearMessages();
    const formData = this.userForm.value;

    try {
      if (this.mainTab === 'Customers') {
        const payload = {
          ...formData,
          productIds: formData.productIds ? formData.productIds.map((id: any) => Number(id)) : [],
          assigneeIds: formData.assigneeIds ? formData.assigneeIds.map((id: any) => Number(id)) : [],
          defaultAssigneeId: formData.assigneeIds?.length > 0 ? Number(formData.assigneeIds[0]) : null,
          managerId: formData.managerId ? Number(formData.managerId) : null,
          role: 'Customer',
          userType: 'Customer'
        };
        if (this.modalMode === 'create') {
          await this.ticketService.createUser(payload);
          this.successMessage = 'Customer created successfully';
        } else {
          await this.ticketService.updateUser(this.editingUserId!, payload);
          this.successMessage = 'Customer updated successfully';
        }
      } else {
        // Internal user — role determined by form dropdown
        const role = formData.role || this.editingUserRole;
        const payload = { 
          ...formData, 
          role: role,
          managerId: formData.managerId ? Number(formData.managerId) : null
        };
        if (this.modalMode === 'create') {
          await this.ticketService.createUser(payload);
          this.successMessage = `${role} created successfully`;
        } else {
          await this.ticketService.updateUser(this.editingUserId!, payload);
          this.successMessage = `${role} updated successfully`;
        }
      }

      this.closeModal();
      await this.loadAllData();
      setTimeout(() => this.successMessage = '', 3000);
    } catch (err: any) {
      console.error('Save error:', err);
      this.errorMessage = err?.error?.message || 'Failed to save user';
    } finally {
      this.isSaving = false;
    }
  }

  // ── Toggle Active Status ──
  async toggleUserActive(user: any): Promise<void> {
    const action = user.isActive ? 'deactivate' : 'activate';
    if (!confirm(`Are you sure you want to ${action} user "${user.fullName || user.FullName || 'User'}"?`)) return;

    this.isLoading = true;
    this.clearMessages();
    try {
      await this.ticketService.toggleUserActive(user.id, !user.isActive);
      this.successMessage = `User "${user.fullName || user.FullName}" ${user.isActive ? 'deactivated' : 'activated'} successfully`;
      await this.loadAllData();
      setTimeout(() => this.successMessage = '', 3000);
    } catch (err: any) {
      console.error('Toggle status error:', err, err?.error);
      this.errorMessage = err?.error?.message || `Failed to ${action} user "${user.fullName || user.FullName}"`;
    } finally {
      this.isLoading = false;
    }
  }

  // ── Bulk upload ──
  async onFileSelected(event: any): Promise<void> {
    const file = event.target.files[0];
    if (!file) return;
    this.isUploading = true;
    this.clearMessages();
    try {
      const roleKey = this.mainTab === 'Customers' ? 'Customer' : 'PM';
      await this.ticketService.bulkUploadUsers(roleKey, file);
      this.successMessage = 'Bulk upload successful';
      await this.loadAllData();
      setTimeout(() => this.successMessage = '', 3000);
    } catch (err: any) {
      this.errorMessage = 'Failed to bulk upload users';
    } finally {
      this.isUploading = false;
      if (this.fileInput?.nativeElement) this.fileInput.nativeElement.value = '';
    }
  }



  // User Preferences / Overrides State
  showPrefModal = false;
  prefUser: any = null;
  prefRole = '';
  prefDetailViewType = 'Default';
  prefPermissions: any[] = [];
  isSavingPrefs = false;

  pmOptions = [
    { label: 'PM Dashboard', key: 'view_pm_dashboard' },
    { label: 'Performance Analytics', key: 'view_pm_performance' },
    { label: 'PM TAT Dashboard', key: 'view_pm_tat' },
    { label: 'Hierarchy Chart', key: 'view_pm_hierarchy' }
  ];

  devOptions = [
    { label: 'Developer Dashboard', key: 'view_developer_dashboard' },
    { label: 'Developer TAT Dashboard', key: 'view_developer_tat' }
  ];

  masterOptions = [
    { label: 'Products Master', key: 'view_master_products' },
    { label: 'User Hub', key: 'view_master_users' },
    { label: 'Statuses Customizer', key: 'view_master_statuses' },
    { label: 'Priorities Customizer', key: 'view_master_priorities' },
    { label: 'Issue Categories Customizer', key: 'view_master_categories' },
    { label: 'Ticket Sources Customizer', key: 'view_master_sources' }
  ];

  assignmentOptions = [
    { label: 'Can Assign Tickets', key: 'assign_ticket' },
    { label: 'Can Assign to All Developers', key: 'assign_ticket_all' },
    { label: 'Can Assign Only to Juniors', key: 'assign_ticket_juniors' }
  ];

  async openPreferencesModal(user: any): Promise<void> {
    this.prefUser = user;
    const role = user.role || '';
    this.prefRole = role === 'Developer' ? 'Assignee' : (role === 'Manager' || role === 'TL' ? 'PM' : role);
    this.showPrefModal = true;
    this.clearMessages();

    try {
      const response = await this.ticketService.getUserPreferences(user.id, this.prefRole);
      this.prefDetailViewType = response.detailViewType || 'Default';
      this.prefPermissions = response.permissions || [];
    } catch (err) {
      console.error('Failed to load user preferences', err);
      this.prefDetailViewType = 'Default';
      this.prefPermissions = [];
    }
  }

  closePrefModal(): void {
    this.showPrefModal = false;
    this.prefUser = null;
    this.prefPermissions = [];
  }

  isPermissionEnabled(key: string): boolean {
    const ov = this.prefPermissions.find(p => p.permissionKey === key);
    if (ov !== undefined) {
      return ov.isEnabled;
    }

    if (this.prefRole === 'PM' || this.prefRole === 'Manager' || this.prefRole === 'TL') {
      if (key.startsWith('view_pm_')) return true;
      if (key === 'assign_ticket') return true;
      if (key === 'assign_ticket_juniors') return true;
      return false;
    }
    if (this.prefRole === 'Assignee' || this.prefRole === 'Developer') {
      if (key.startsWith('view_developer_')) return true;
      return false;
    }
    if (this.prefRole === 'SuperManager' || this.prefRole === 'Super Admin') {
      if (key.startsWith('view_developer_')) return false;
      return true;
    }
    return false;
  }

  togglePermission(key: string): void {
    const ovIndex = this.prefPermissions.findIndex(p => p.permissionKey === key);
    if (ovIndex !== -1) {
      this.prefPermissions[ovIndex].isEnabled = !this.prefPermissions[ovIndex].isEnabled;
    } else {
      const defaultState = this.isPermissionEnabled(key);
      this.prefPermissions.push({
        permissionKey: key,
        isEnabled: !defaultState
      });
    }
  }

  async savePreferences(): Promise<void> {
    if (!this.prefUser) return;
    this.isSavingPrefs = true;
    this.clearMessages();
    try {
      const payload = {
        userId: this.prefUser.id,
        userRole: this.prefRole,
        detailViewType: this.prefDetailViewType,
        permissions: this.prefPermissions.map(p => ({
          permissionKey: p.permissionKey,
          isEnabled: p.isEnabled
        }))
      };
      await this.ticketService.saveUserPreferences(payload);
      this.successMessage = 'Preferences saved successfully';
      
      const currentUser = this.authService.getCurrentUser();
      if (currentUser && currentUser.userId.toString() === this.prefUser.id.toString() && currentUser.role === this.prefRole) {
        if ((this.authService as any).refreshSession) {
          await (this.authService as any).refreshSession();
        }
      }

      this.closePrefModal();
      setTimeout(() => this.successMessage = '', 3000);
    } catch (err) {
      console.error(err);
      this.errorMessage = 'Failed to save preferences';
    } finally {
      this.isSavingPrefs = false;
    }
  }
}
