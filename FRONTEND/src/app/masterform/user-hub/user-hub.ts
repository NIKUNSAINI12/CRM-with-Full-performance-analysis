import { Component, OnInit, Inject, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { TICKET_SERVICE_TOKEN } from '../../Core/injection-tokens';
import { TicketService } from '../../Core/services/ticket.service';
import { Assignee, User } from '../../Core/models/ticket.model';
import { AuthService } from '../../Core/services/auth';

type ActiveTab = 'Customer' | 'Assignee' | 'Manager' | 'SuperManager';

@Component({
  selector: 'app-user-hub',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './user-hub.html',
  styleUrls: ['./user-hub.scss']
})
export class UserHubComponent implements OnInit {
  activeTab: ActiveTab = 'Customer';
  isLoading = false;
  isUploading = false;
  isSaving = false;

  // Messages
  successMessage = '';
  errorMessage = '';

  // Data lists
  customers: User[] = [];
  assignees: Assignee[] = [];
  managers: User[] = [];
  superManagers: User[] = [];
  
  // Filtered lists
  filteredCustomers: User[] = [];
  filteredAssignees: Assignee[] = [];
  filteredManagers: User[] = [];
  filteredSuperManagers: User[] = [];
  products: any[] = [];
  searchTerm = '';
  assigneeFilterId: number | null = null; // Filter customers by assignee

  // Assignee typeahead filter state
  assigneeSearchInput = '';
  showAssigneeSuggestions = false;
  filteredAssigneeSuggestions: Assignee[] = [];

  // Executive searchable dropdown state (for forms)
  executiveSearchQuery = '';
  showExecutiveDropdown = false;
  filteredExecutives: Assignee[] = [];

  // Modal / Form state
  showModal = false;
  modalMode: 'create' | 'edit' = 'create';
  userForm!: FormGroup;
  editingUserId: number | null = null;

  @ViewChild('fileInput') fileInput!: ElementRef<HTMLInputElement>;

  constructor(
    @Inject(TICKET_SERVICE_TOKEN) private ticketService: TicketService,
    private fb: FormBuilder,
    private authService: AuthService
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
      // Mappings
      defaultAssigneeId: [null], // For Customer -> Assignee
      managerId: [null],         // For Assignee -> Manager, or Customer -> Manager
      productIds: [[]]           // For Customer -> Products
    });
  }

  async loadAllData(): Promise<void> {
    this.isLoading = true;
    this.errorMessage = '';
    try {
      const [cust, asm, pms, superMs, prods] = await Promise.all([
        this.ticketService.getUsersByRole('Customer'),
        this.ticketService.getAssignees(),
        this.ticketService.getUsersByRole('PM'), // The backend uses 'PM' or 'Manager'
        this.ticketService.getUsersByRole('SuperManager'),
        this.ticketService.getProducts()
      ]);
      
      this.customers = cust || [];
      this.assignees = (asm || []).filter(a => !a['role'] || a['role'] === 'Assignee');
      this.filteredExecutives = this.assignees;
      this.managers = pms || [];
      this.superManagers = superMs || [];
      this.products = prods || [];

      // Enrich each customer with their assignee's name
      const assigneeMap = new Map<number, string>(this.assignees.map(a => [a.id, a.fullName || '']));
      this.customers.forEach(c => {
        if (c.defaultAssigneeId) {
          c.defaultAssigneeName = assigneeMap.get(c.defaultAssigneeId) || 'Unknown';
        } else {
          c.defaultAssigneeName = '';
        }
      });
      
      this.filterUsers();
    } catch (err: any) {
      console.error('Error loading users:', err);
      this.errorMessage = 'Failed to load user data';
    } finally {
      this.isLoading = false;
    }
  }

  setTab(tab: ActiveTab): void {
    this.activeTab = tab;
    this.searchTerm = '';
    this.assigneeFilterId = null;
    this.filterUsers();
    this.clearMessages();
  }

  clearMessages(): void {
    this.successMessage = '';
    this.errorMessage = '';
  }

  filterUsers(): void {
    const term = this.searchTerm.toLowerCase().trim();
    
    if (this.activeTab === 'Customer') {
      this.filteredCustomers = this.customers.filter(c => {
        const nameMatch = c.fullName?.toLowerCase().includes(term) || 
          c.email?.toLowerCase().includes(term) ||
          c.userNumber?.toLowerCase().includes(term) ||
          c.defaultAssigneeName?.toLowerCase().includes(term);
        const assigneeMatch = !this.assigneeFilterId || c.defaultAssigneeId === this.assigneeFilterId;
        return nameMatch && assigneeMatch;
      });
    } else if (this.activeTab === 'Assignee') {
      this.filteredAssignees = this.assignees.filter(a => 
        a.fullName?.toLowerCase().includes(term) || 
        a.email?.toLowerCase().includes(term) ||
        (a as any).assigneeNumber?.toLowerCase().includes(term)
      );
    } else if (this.activeTab === 'Manager') {
      this.filteredManagers = this.managers.filter(m => 
        m.fullName?.toLowerCase().includes(term) || 
        m.email?.toLowerCase().includes(term) ||
        m.userNumber?.toLowerCase().includes(term)
      );
    } else if (this.activeTab === 'SuperManager') {
      this.filteredSuperManagers = this.superManagers.filter(sm => 
        sm.fullName?.toLowerCase().includes(term) || 
        sm.email?.toLowerCase().includes(term) ||
        sm.userNumber?.toLowerCase().includes(term)
      );
    }
  }

  // --- Assignee Typeahead Filter Methods ---

  onAssigneeSearchInput(): void {
    const q = this.assigneeSearchInput.toLowerCase().trim();
    this.filteredAssigneeSuggestions = q
      ? this.assignees.filter(a => a.fullName?.toLowerCase().includes(q) || (a as any).assigneeNumber?.toLowerCase().includes(q))
      : [...this.assignees];
    this.showAssigneeSuggestions = true;
  }

  selectAssigneeFilter(assignee: Assignee | null): void {
    if (assignee) {
      this.assigneeFilterId = assignee.id;
      this.assigneeSearchInput = assignee.fullName || '';
    } else {
      this.assigneeFilterId = null;
      this.assigneeSearchInput = '';
    }
    this.showAssigneeSuggestions = false;
    this.filterUsers();
  }

  clearAssigneeFilter(): void {
    this.assigneeFilterId = null;
    this.assigneeSearchInput = '';
    this.showAssigneeSuggestions = false;
    this.filterUsers();
  }

  onAssigneeBlur(): void {
    setTimeout(() => { this.showAssigneeSuggestions = false; }, 150);
  }

  getSelectedAssigneeName(): string {
    if (!this.assigneeFilterId) return '';
    return this.assignees.find(a => a.id === this.assigneeFilterId)?.fullName || '';
  }

  openCreateModal(): void {
    this.clearMessages();
    this.modalMode = 'create';
    this.editingUserId = null;
    this.productSearchTerm = '';
    this.executiveSearchQuery = '';
    this.userForm.reset();
    
    // Require password for all roles on creation except Customer
    if (this.activeTab !== 'Customer') {
      this.userForm.get('password')?.setValidators([Validators.required, Validators.minLength(6)]);
    } else {
      this.userForm.get('password')?.clearValidators();
    }
    this.userForm.get('password')?.updateValueAndValidity();
    
    this.showModal = true;
  }

  async openEditModal(user: any): Promise<void> {
    this.clearMessages();
    this.modalMode = 'edit';
    this.editingUserId = user.id;
    this.productSearchTerm = '';
    const currentAssignee = this.assignees.find(a => a.id === user.defaultAssigneeId);
    this.executiveSearchQuery = currentAssignee?.fullName || '';

    this.userForm.reset({
      fullName: user.fullName,
      email: user.email,
      contactPerson: user.contactPerson || '',
      phoneNo: user.phoneNo || '',
      mobileNo: user.mobileNo || '',
      defaultAssigneeId: user.defaultAssigneeId || null,
      managerId: user.managerId || null,
      productIds: []
    });

    if (this.activeTab === 'Customer') {
      try {
        const pIds = await this.ticketService.getUserProducts(user.id);
        this.userForm.patchValue({ productIds: pIds || [] });
      } catch (err) {
        console.error('Failed to load user products:', err);
      }
    }
    
    this.userForm.get('password')?.clearValidators();
    this.userForm.get('password')?.updateValueAndValidity();
    
    this.showModal = true;
  }

  isProductSelected(productId: number): boolean {
    const selected = this.userForm.get('productIds')?.value || [];
    return selected.includes(productId) || selected.includes(productId.toString());
  }

  productSearchTerm: string = '';

  get filteredProducts() {
    if (!this.productSearchTerm) return this.products;
    const term = this.productSearchTerm.toLowerCase();
    return this.products.filter(p => p.name?.toLowerCase().includes(term));
  }

  toggleProduct(productId: number): void {
    const current = this.userForm.get('productIds')?.value || [];
    let updated: any[];
    
    if (this.isProductSelected(productId)) {
      updated = current.filter((id: any) => Number(id) !== productId);
    } else {
      updated = [...current, productId];
    }
    
    this.userForm.patchValue({ productIds: updated });
    this.userForm.get('productIds')?.markAsDirty();
  }

  closeModal(): void {
    this.showModal = false;
    this.userForm.reset();
    this.productSearchTerm = '';
    this.clearMessages();
  }

  async saveUser(): Promise<void> {
    if (this.userForm.invalid) {
      this.userForm.markAllAsTouched();
      return;
    }

    this.isSaving = true;
    this.clearMessages();
    const formData = this.userForm.value;

    try {
      if (this.activeTab === 'Customer') {
        const payload = {
          ...formData,
          productIds: formData.productIds ? formData.productIds.map((id: any) => Number(id)) : [],
          defaultAssigneeId: formData.defaultAssigneeId ? Number(formData.defaultAssigneeId) : null,
          managerId: null, // PM resolved dynamically on backend
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
      } 
      else if (this.activeTab === 'Assignee') {
        const assigneePayload = {
          ...formData,
          managerId: formData.managerId ? Number(formData.managerId) : null
        };
        if (this.modalMode === 'create') {
          await this.ticketService.createAssignee(assigneePayload);
          this.successMessage = 'Assignee created successfully';
        } else {
          await this.ticketService.updateAssignee(this.editingUserId!, assigneePayload);
          this.successMessage = 'Assignee updated successfully';
        }
      } 
      else if (this.activeTab === 'Manager') {
        const payload = {
          ...formData,
          managerId: formData.managerId ? Number(formData.managerId) : null,
          role: 'PM'
        };
        if (this.modalMode === 'create') {
          await this.ticketService.createUser(payload);
          this.successMessage = 'Manager created successfully';
        } else {
          await this.ticketService.updatePm(this.editingUserId!, payload);
          this.successMessage = 'Manager updated successfully';
        }
      }
      else if (this.activeTab === 'SuperManager') {
        const payload = {
          ...formData,
          managerId: formData.managerId ? Number(formData.managerId) : null,
          role: 'SuperManager'
        };
        if (this.modalMode === 'create') {
          await this.ticketService.createUser(payload);
          this.successMessage = 'Super Manager created successfully';
        } else {
          await this.ticketService.updatePm(this.editingUserId!, payload);
          this.successMessage = 'Super Manager updated successfully';
        }
      }

      this.closeModal();
      await this.loadAllData();
      
      // Clear success message after 3 seconds
      setTimeout(() => this.successMessage = '', 3000);
      
    } catch (err: any) {
      console.error('Save error:', err);
      this.errorMessage = err?.error?.message || 'Failed to save user';
    } finally {
      this.isSaving = false;
    }
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
    this.executiveSearchQuery = (executive.id && executive.fullName) ? executive.fullName : '';
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

  async onFileSelected(event: any): Promise<void> {
    const file = event.target.files[0];
    if (!file) return;

    this.isUploading = true;
    this.clearMessages();
    
    try {
      let roleKey: string = this.activeTab;
      if (roleKey === 'Manager') roleKey = 'PM';
      
      await this.ticketService.bulkUploadUsers(roleKey, file);
      this.successMessage = `${this.activeTab}s bulk uploaded successfully`;
      await this.loadAllData();
      
      setTimeout(() => this.successMessage = '', 3000);
    } catch (err: any) {
      console.error('Upload error:', err);
      this.errorMessage = 'Failed to bulk upload users';
    } finally {
      this.isUploading = false;
      if (this.fileInput?.nativeElement) {
        this.fileInput.nativeElement.value = '';
      }
    }
  }

  async deleteUser(userId: number, name?: string): Promise<void> {
    const displayName = name || 'User';
    if (!confirm(`Are you sure you want to delete user "${displayName}"? This will cascade-delete or nullify their associations.`)) {
      return;
    }
    
    this.isLoading = true;
    this.clearMessages();
    try {
      await this.ticketService.deleteUser(userId);
      this.successMessage = `User "${displayName}" deleted successfully`;
      await this.loadAllData();
      setTimeout(() => this.successMessage = '', 3000);
    } catch (err: any) {
      console.error('Delete user error:', err);
      this.errorMessage = err?.error?.message || `Failed to delete user "${displayName}"`;
    } finally {
      this.isLoading = false;
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

  async openPreferencesModal(user: any, tab: ActiveTab): Promise<void> {
    this.prefUser = user;
    this.prefRole = tab === 'Manager' ? 'PM' : tab;
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

    // Role-based defaults fallback (aligned with DB permissions)
    if (this.prefRole === 'PM' || this.prefRole === 'Manager') {
      if (key.startsWith('view_pm_')) return true;
      if (key === 'create_ticket' && this.prefRole === 'PM') return true;
      return false; // Masters are disabled by default for PM/Manager
    }
    if (this.prefRole === 'Assignee') {
      if (key.startsWith('view_developer_')) return true;
      return false;
    }
    if (this.prefRole === 'SuperManager' || this.prefRole === 'Super Admin') {
      if (key.startsWith('view_developer_')) return false; // Dev workspace options are for Assignees
      return true; // All PM features, customer features, and masters are enabled by default
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
        await (this.authService as any).refreshSession();
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
