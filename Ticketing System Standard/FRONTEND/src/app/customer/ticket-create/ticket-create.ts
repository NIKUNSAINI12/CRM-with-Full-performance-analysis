// import { Component, OnInit, Inject, Optional, ViewChild, ElementRef, AfterViewInit } from '@angular/core';
// import { CommonModule, Location } from '@angular/common';
// import { FormsModule } from '@angular/forms';
// import { Router, ActivatedRoute } from '@angular/router';
// import { DropDownListModule } from '@syncfusion/ej2-angular-dropdowns';
// import { UploaderModule } from '@syncfusion/ej2-angular-inputs';
// import { ButtonModule } from '@syncfusion/ej2-angular-buttons';
// import { TextBoxModule } from '@syncfusion/ej2-angular-inputs';
// import { API_BASE_URL } from '../../app';
// import { PmTicketUpdatePayload, TicketService } from '../../Core/services/ticket.service';
// import { DefaultTicketService } from '../../Core/services/ticket.service';
// import { DefaultNotificationService } from '../../Core/services/notification.service';
// import { TICKET_SERVICE_TOKEN, NOTIFICATION_SERVICE_TOKEN, API_BASE_URL_TOKEN } from '../../Core/injection-tokens';
// import { NotificationService } from '../home/home';

// // Extended interfaces for the ticket creation
// export interface ProductData {
//   id: string;
//   name: string;
//   modules: ModuleData[];
// }

// export interface ModuleData {
//   id: string;
//   name: string;
//   productId: string;
//   subModules: SubModuleData[];
// }

// export interface SubModuleData {
//   id: string;
//   name: string;
//   moduleId: string;
// }

// export interface CreateTicketRequest {
//   subject: string;
//   description: string;
//   priority: string;
//   ticketType: string;
//   productId: string;
//   moduleId: string;
//   subModuleId: string;
//   customerId: number;
//   attachment?: File;
//   assignedToId?: number;      // <-- NEW
//   deadline?: string;
// }


// @Component({
//   selector: 'app-ticket-create',
//   standalone: true,
//   imports: [
//     CommonModule, 
//     FormsModule, 
//     DropDownListModule, 
//     UploaderModule, 
//     ButtonModule, 
//     TextBoxModule
//   ],
//   providers: [
//     { provide: API_BASE_URL_TOKEN, useValue: API_BASE_URL },
//     { provide: TICKET_SERVICE_TOKEN, useClass: DefaultTicketService },
//     { provide: NOTIFICATION_SERVICE_TOKEN, useClass: DefaultNotificationService },
//   ],
//   templateUrl: './ticket-create.html',
//   styleUrls: ['./ticket-create.scss']
// })
// export class TicketCreateComponent implements OnInit, AfterViewInit {
//   @ViewChild('descriptionEditor', { static: false }) descriptionEditor!: ElementRef;

//   // Form data
//   public ticketForm: CreateTicketRequest = {
//     subject: '',
//     description: '',
//     priority: 'Medium',
//     ticketType: 'Error',
//     productId: '',
//     moduleId: '',
//     subModuleId: '',
//     customerId: 1,

//   };

//   // Dropdown data
//   public products: ProductData[] = [];
//   public availableModules: ModuleData[] = [];
//   public availableSubModules: SubModuleData[] = [];
//   public assigneeOptions: { id: number; name: string }[] = [];
//   public priorityOptions = [
//     { id: 'Low', name: 'Low' },
//     { id: 'Medium', name: 'Medium' },
//     { id: 'High', name: 'High' },
//     { id: 'Urgent', name: 'Urgent' }
//   ];

//   public ticketTypeOptions = [
//     { id: 'Error', name: 'Error/Bug Report' },
//     { id: 'Change Request', name: 'Change Request' },
//     { id: 'DEM', name: 'Data Entry Mistake' }
//   ];
//   public createdByPmId: number | null = null;

//   // File upload
//   public selectedFiles: File[] = [];
//   public uploadSettings = {
//     autoUpload: false,
//     multiple: true,
//     allowedExtensions: '.jpg,.jpeg,.png,.pdf,.doc,.docx,.txt,.xlsx,.xls',
//     maxFileSize: 5242880 // 5MB
//   };

//   // UI state
//   public isLoading = false;
//   public isSubmitting = false;
//   public errorMessage: string | null = null;
//   public validationErrors: { [key: string]: string } = {};
//   public isDescriptionFocused = false;

//   // Dropdown field settings
//   public dropdownFields = { text: 'name', value: 'id' };

//   constructor(
//     private location: Location,
//     private router: Router,
//     private route: ActivatedRoute,
//     @Inject(TICKET_SERVICE_TOKEN) private ticketService: TicketService,
//     @Inject(NOTIFICATION_SERVICE_TOKEN) private notificationService: NotificationService
//   ) {
//     console.log('TicketCreateComponent constructor called');
//     console.log('ticketService:', this.ticketService);
//     console.log('notificationService:', this.notificationService);
//   }

//   ngOnInit(): void {
//     this.loadCustomerIdFromRoute();
//     this.route.queryParamMap.subscribe(params => {
//       const pmId = params.get('pmId');
//       if (pmId) {
//         this.createdByPmId = Number(pmId);
//         this.createdByPmId = Number(pmId);
//       this.loadAssignees();
//       }
//     });
//   }

//   ngAfterViewInit(): void {
//     this.setupTextEditor();
//   }

//   private setupTextEditor(): void {
//     if (this.descriptionEditor) {
//       const element = this.descriptionEditor.nativeElement;

//       // Handle keyboard shortcuts
//       element.addEventListener('keydown', (e: KeyboardEvent) => {
//         if (e.ctrlKey || e.metaKey) {
//           switch(e.key) {
//             case 'b':
//               e.preventDefault();
//               this.formatText('bold');
//               break;
//             case 'i':
//               e.preventDefault();
//               this.formatText('italic');
//               break;
//             case 'u':
//               e.preventDefault();
//               this.formatText('underline');
//               break;
//           }
//         }

//         // Prevent exceeding character limit
//         if (this.getDescriptionLength() >= 2000 && 
//             !['Backspace', 'Delete', 'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'].includes(e.key) &&
//             !e.ctrlKey && !e.metaKey) {
//           e.preventDefault();
//         }
//       });

//       // Handle paste events to maintain formatting
//       element.addEventListener('paste', (e: ClipboardEvent) => {
//         e.preventDefault();
//         const paste = e.clipboardData?.getData('text/plain') || '';
//         const selection = window.getSelection();
//         if (selection && selection.rangeCount > 0) {
//           selection.deleteFromDocument();
//           selection.getRangeAt(0).insertNode(document.createTextNode(paste));
//           selection.collapseToEnd();
//         }
//         this.onDescriptionInput();
//       });
//     }
//   }
// private async loadAssignees(): Promise<void> {
//   try {
//     const devs = await this.ticketService.getAssignees();
//     this.assigneeOptions = devs.map(d => ({
//       id: d.id,
//       name: `${d.assigneeNumber} – ${d.fullName}`
//     }));
//   } catch (e) {
//     console.error('Failed to load assignees', e);
//   }
// }
//   private loadCustomerIdFromRoute(): void {
//     this.route.params.subscribe(params => {
//       const customerId = params['customerId'];
//       if (customerId) {
//         const parsedCustomerId = parseInt(customerId, 10);
//         this.ticketForm.customerId = parsedCustomerId;

//         // IMPORTANT: Call loadProductData from here, after we have the customerId.
//         this.loadProductData(parsedCustomerId);
//       } else {
//           // Handle case where no customerId is in the URL
//           this.errorMessage = "Customer ID not found in URL.";
//           this.showNotification(this.errorMessage, 'error');
//       }
//     });
//   }

//   // Text Editor Methods
//   public formatText(command: string): void {
//     if (this.descriptionEditor) {
//       this.descriptionEditor.nativeElement.focus();
//       setTimeout(() => {
//         document.execCommand(command, false, undefined);
//         this.onDescriptionInput();
//       }, 10);
//     }
//   }

//   public isFormatActive(command: string): boolean {
//     return document.queryCommandState(command);
//   }

//   public onDescriptionFocus(): void {
//     this.isDescriptionFocused = true;
//   }

//   public onDescriptionBlur(): void {
//     this.isDescriptionFocused = false;
//     this.updateFormModel();
//   }

//   public onDescriptionInput(event?: Event): void {
//     // this.updateFormModel();
//   }

//   public onKeyDown(event: KeyboardEvent): void {
//     // Handle Enter key to create proper line breaks
//     if (event.key === 'Enter' && !event.shiftKey) {
//       event.preventDefault();
//       document.execCommand('insertHTML', false, '<br><br>');
//     }
//   }

//   private updateFormModel(): void {
//     if (this.descriptionEditor) {
//      this.ticketForm.description = this.descriptionEditor.nativeElement.innerText;
//     }
//   }

//   public getDescriptionLength(): number {
//     if (this.descriptionEditor) {
//       return this.descriptionEditor.nativeElement.textContent?.length || 0;
//     }
//     return this.getDescriptionTextContent().length;
//   }

//   private getDescriptionTextContent(): string {
//     if (!this.ticketForm.description) return '';
//     const div = document.createElement('div');
//     div.innerHTML = this.ticketForm.description;
//     return div.textContent || div.innerText || '';
//   }

//   // Cascading dropdown handlers
//   public onProductChange(args: any): void {
//     const selectedProductId = args.value;
//     this.ticketForm.productId = selectedProductId;
//     this.ticketForm.moduleId = '';
//     this.ticketForm.subModuleId = '';

//     const selectedProduct = this.products.find(p => p.id === selectedProductId);
//     this.availableModules = selectedProduct ? selectedProduct.modules : [];
//     this.availableSubModules = [];
//   }

//   public onModuleChange(args: any): void {
//     const selectedModuleId = args.value;
//     this.ticketForm.moduleId = selectedModuleId;
//     this.ticketForm.subModuleId = '';

//     const selectedModule = this.availableModules.find(m => m.id === selectedModuleId);
//     this.availableSubModules = selectedModule ? selectedModule.subModules : [];
//   }

//   public onSubModuleChange(args: any): void {
//     this.ticketForm.subModuleId = args.value;
//   }

//   // File upload handlers
//   public onFileSelect(args: any): void {
//     this.selectedFiles = args.filesData.map((file: any) => file.rawFile);
//   }

//   public onFileRemove(args: any): void {
//     this.selectedFiles = this.selectedFiles.filter(
//       file => file.name !== args.filesData[0].name
//     );
//   }

//   // Validation
//   private validateForm(): boolean {
//     this.validationErrors = {};
//     let isValid = true;

//     if (!this.ticketForm.subject?.trim()) {
//       this.validationErrors['subject'] = 'Subject is required';
//       isValid = false;
//     }

//     const descriptionText = this.getDescriptionTextContent();
//     if (!descriptionText?.trim()) {
//       this.validationErrors['description'] = 'Description is required';
//       isValid = false;
//     }

//     if (!this.ticketForm.productId) {
//       this.validationErrors['productId'] = 'Product selection is required';
//       isValid = false;
//     }

//     if (this.ticketForm.subject && this.ticketForm.subject.length < 5) {
//       this.validationErrors['subject'] = 'Subject must be at least 5 characters long';
//       isValid = false;
//     }

//     if (descriptionText && descriptionText.length < 20) {
//       this.validationErrors['description'] = 'Description must be at least 20 characters long';
//       isValid = false;
//     }

//     if (descriptionText && descriptionText.length > 2000) {
//       this.validationErrors['description'] = 'Description cannot exceed 2000 characters';
//       isValid = false;
//     }

//     return isValid;
//   }

// public async onSubmit(): Promise<void> {
//   this.updateFormModel();

//   if (!this.validateForm()) {
//     this.showNotification('Please correct the validation errors before submitting.', 'error');
//     return;
//   }

//   this.isSubmitting = true;
//   this.errorMessage = null;

//   try {
//     const formData = new FormData();
//     formData.append('subject', this.ticketForm.subject);
//     formData.append('description', this.ticketForm.description);
//     formData.append('priority', this.ticketForm.priority);
//     formData.append('ticketType', this.ticketForm.ticketType);
//     formData.append('productId', this.ticketForm.productId);
//     formData.append('customerId', this.ticketForm.customerId.toString());

//     if (this.ticketForm.moduleId)   formData.append('moduleId', this.ticketForm.moduleId);
//     if (this.ticketForm.subModuleId) formData.append('subModuleId', this.ticketForm.subModuleId);
//     if (this.createdByPmId)         formData.append('createdByPmId', this.createdByPmId.toString());

//     this.selectedFiles.forEach(f => formData.append('attachments', f, f.name));

//     // === 1. CREATE TICKET ===
//     console.log('%c[DEBUG] Creating base ticket...', 'color: blue; font-weight: bold');

//     const newTicket = await this.ticketService.createTicket(formData);
//     console.log('%c[DEBUG] Base ticket created:', 'color: green; font-weight: bold', newTicket);
//     console.log(`Ticket ID: ${newTicket.id}, Ticket Number: ${newTicket.ticketNumber}`);

//     // === 2. PM-ONLY LOGIC HAS BEEN MOVED TO BACKEND AUTO-ASSIGNMENT ===
//     // Assignment and deadline are now handled automatically by the backend
//     // based on Customer mapping and Priority TAT.
//     console.log('%c[DEBUG] Assignment and Deadline logic are now fully automated in the backend.', 'color: gray');

//     this.showNotification(`Ticket #${newTicket.id} has been created successfully!`, 'success');

//     if (this.createdByPmId) {
//       this.router.navigate(['/pm/dashboard']);
//     } else {
//       this.router.navigate(['/customer/tickets']);
//     }
//   } catch (error: any) {
//     console.error('%c[DEBUG] FATAL ERROR in onSubmit', 'color: red; font-weight: bold', error);
//     this.errorMessage = "An error occurred while submitting your ticket. Please try again.";
//     this.showNotification(this.errorMessage, 'error');
//   } finally {
//     this.isSubmitting = false;
//   }
// }

//   public goBack(): void {
//     this.location.back();
//   }

//   public resetForm(): void {
//     this.ticketForm = {
//       subject: '',
//       description: '',
//       priority: 'Medium',
//       ticketType: 'Error',
//       productId: '',
//       moduleId: '',
//       subModuleId: '',
//       customerId: this.ticketForm.customerId
//     };

//     // Clear the editor content
//     if (this.descriptionEditor) {
//       this.descriptionEditor.nativeElement.innerHTML = '';
//     }

//     this.selectedFiles = [];
//     this.availableModules = [];
//     this.availableSubModules = [];
//     this.validationErrors = {};
//     this.errorMessage = null;
//   }

//   // Utility methods
//   public formatFileSize(bytes: number): string {
//     if (bytes === 0) return '0 Bytes';
//     const k = 1024;
//     const sizes = ['Bytes', 'KB', 'MB', 'GB'];
//     const i = Math.floor(Math.log(bytes) / Math.log(k));
//     return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
//   }

//   public async loadProductData(customerId: number): Promise<void> {
//     this.isLoading = true;
//     try {
//       // Pass the customerId to the service call
//       this.products = await this.ticketService.getProductHierarchy(customerId);
//     } catch (error) {
//       this.errorMessage = 'Failed to load product data from the server. Please try again.';
//       this.showNotification(this.errorMessage, 'error');
//       console.error(error);
//     } finally {
//       this.isLoading = false;
//     }
//   }

//   public getSelectedProductName(): string {
//     const product = this.products.find(p => p.id === this.ticketForm.productId);
//     return product ? product.name : '';
//   }

//   public getSelectedModuleName(): string {
//     const module = this.availableModules.find(m => m.id === this.ticketForm.moduleId);
//     return module ? module.name : '';
//   }

//   public getSelectedSubModuleName(): string {
//     const subModule = this.availableSubModules.find(sm => sm.id === this.ticketForm.subModuleId);
//     return subModule ? subModule.name : '';
//   }

//   private showNotification(message: string, type: 'success' | 'error' | 'info' = 'info'): void {
//     this.notificationService.show(message, type);
//   }
// }
import { Component, OnInit, Inject, Optional, ViewChild, ElementRef, AfterViewInit, HostListener } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, ParamMap } from '@angular/router';
import { DropDownListModule } from '@syncfusion/ej2-angular-dropdowns';
import { UploaderModule } from '@syncfusion/ej2-angular-inputs';
import { ButtonModule } from '@syncfusion/ej2-angular-buttons';
import { TextBoxModule } from '@syncfusion/ej2-angular-inputs';
import { API_BASE_URL } from '../../app';
import { PmTicketUpdatePayload, TicketService } from '../../Core/services/ticket.service';
import { DefaultTicketService } from '../../Core/services/ticket.service';
import { DefaultNotificationService } from '../../Core/services/notification.service';
import { TICKET_SERVICE_TOKEN, NOTIFICATION_SERVICE_TOKEN, API_BASE_URL_TOKEN } from '../../Core/injection-tokens';
import { NotificationService } from '../home/home';

// Extended interfaces
export interface ProductData {
  id: string;
  name: string;
  modules: ModuleData[];
}

export interface ModuleData {
  id: string;
  name: string;
  productId: string;
  subModules: SubModuleData[];
}

export interface SubModuleData {
  id: string;
  name: string;
  moduleId: string;
}

export interface CreateTicketRequest {
  subject: string;
  description: string;
  priority: string;
  ticketType: string;

  productId: string;
  moduleId: string;
  subModuleId: string;
  customerId: number;
  attachment?: File;
  assignedToId?: number;
  deadline?: string;
  docketNumber?: string;
  ticketSource?: string;
  productName?: string;
  moduleName?: string;
  subModuleName?: string;
}

@Component({
  selector: 'app-ticket-create',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    DropDownListModule,
    UploaderModule,
    ButtonModule,
    TextBoxModule
  ],
  providers: [
    { provide: API_BASE_URL_TOKEN, useValue: API_BASE_URL },
    { provide: TICKET_SERVICE_TOKEN, useClass: DefaultTicketService },
    { provide: NOTIFICATION_SERVICE_TOKEN, useClass: DefaultNotificationService },
  ],
  templateUrl: './ticket-create.html',
  styleUrls: ['./ticket-create.scss']
})
export class TicketCreateComponent implements OnInit, AfterViewInit {
  @ViewChild('descriptionEditor', { static: false }) descriptionEditor!: ElementRef;
  @ViewChild('pasteReceiver', { static: false }) pasteReceiver!: ElementRef;

  public currentUser: { id: number; name: string; role: string } = { id: 0, name: '', role: '' };

  // Form data
  public ticketForm: CreateTicketRequest = {
    subject: '',
    description: '',
    priority: 'Medium',
    ticketType: 'Error',

    productId: '',
    moduleId: '',
    subModuleId: '',
    customerId: 1,
    assignedToId: undefined,
    deadline: undefined,
    docketNumber: undefined,
    ticketSource: '',
    productName: '',
    moduleName: '',
    subModuleName: ''
  };



  // Dropdown data
  public products: ProductData[] = [];
  public availableModules: ModuleData[] = [];
  public availableSubModules: SubModuleData[] = [];
  public dockets: any[] = [];
  // Custom autocomplete state
  public searchDocketText: string = '';
  public filteredDockets: any[] = [];
  public isDocketDropdownOpen: boolean = false;
  public selectedDocketStatus: string = '';

  // Product searchable dropdown state
  public searchProductText: string = '';
  public filteredProducts: ProductData[] = [];
  public isProductDropdownOpen: boolean = false;

  // Module searchable dropdown state
  public searchModuleText: string = '';
  public filteredModules: ModuleData[] = [];
  public isModuleDropdownOpen: boolean = false;

  // SubModule searchable dropdown state
  public searchSubModuleText: string = '';
  public filteredSubModules: SubModuleData[] = [];
  public isSubModuleDropdownOpen: boolean = false;
  public assigneeOptions: { id: number; name: string }[] = [];
  public priorityOptions: { id: string, name: string }[] = [];
  private rawPriorities: any[] = [];

  // Assignee searchable dropdown state
  public assigneeSearchQuery = '';
  public showAssigneeDropdown = false;
  public filteredAssignees: { id: number; name: string }[] = [];

  private readonly DEFAULT_TICKET_TYPES = [
    { id: 'Error', name: 'Error/Bug Report' },
    { id: 'Change Request', name: 'Change Request' },
    { id: 'DEM', name: 'Data Entry Mistake' }
  ];

  public ticketTypeOptions: { id: string, name: string }[] = [];

  public showAddTypeModal = false;
  public newTypeName = '';

  private readonly DEFAULT_TICKET_SOURCES = [
    { id: 'Email', name: 'Email' },
    { id: 'Phone', name: 'Phone' },
    { id: 'Portal', name: 'Portal' }
  ];

  public ticketSourceOptions = [...this.DEFAULT_TICKET_SOURCES];
  public showAddSourceModal = false;
  public newSourceName = '';

  // Extra contact fields shown based on selected source
  public sourcePhone: string = '';
  public sourceEmail: string = '';

  public customers: any[] = [];
  public selectedCustomerPmName: string = '';  // PM assigned to the selected customer

  public createdByPmId: number | null = null;
  public parentTicketId: number | null = null;       // Pre-filled from query param
  public parentTicketNumber: string | null = null;   // Shown as banner on form

  // File upload
  public selectedFiles: File[] = [];
  public uploadSettings = {
    autoUpload: false,
    multiple: true,
    allowedExtensions: '.jpg,.jpeg,.png,.pdf,.doc,.docx,.txt,.xlsx,.xls',
    maxFileSize: 5242880
  };

  // UI state
  public isLoading = false;
  public isProductsLoading = false;
  public isSubmitting = false;
  public errorMessage: string | null = null;
  public validationErrors: { [key: string]: string } = {};
  public isDescriptionFocused = false;

  // Dropdown field settings
  public dropdownFields = { text: 'name', value: 'id' };

  constructor(
    private location: Location,
    private router: Router,
    private route: ActivatedRoute,
    @Inject(TICKET_SERVICE_TOKEN) private ticketService: TicketService,
    @Inject(NOTIFICATION_SERVICE_TOKEN) private notificationService: NotificationService
  ) {
    console.log('%c[INIT] TicketCreateComponent constructed', 'color: #8B5CF6; font-weight: bold');
  }

  public get isCustomerRole(): boolean {
    const nonCustomerRoles = ['PM', 'Project Manager', 'SuperManager', 'Super Admin', 'Manager', 'Assignee', 'Developer', 'TL', 'Executive'];
    return !nonCustomerRoles.includes(this.currentUser.role);
  }

  ngOnInit(): void {
    console.log('%c[INIT] ngOnInit started', 'color: #3B82F6; font-weight: bold');
    this.loadCurrentUser();
    this.loadCustomTicketTypes();
    this.loadCustomTicketSources();
    this.route.queryParamMap.subscribe(async params => {
      const pmId = params.get('pmId');
      const parentId = params.get('parentId');
      const parentNumber = params.get('parentNumber');
      console.log('%c[ROUTE] Query params:', 'color: #10B981', params.keys, params);

      if (pmId || ['PM', 'Project Manager', 'SuperManager', 'Super Admin', 'Manager', 'TL', 'Assignee', 'Developer'].includes(this.currentUser.role)) {
        if (pmId) {
          this.createdByPmId = Number(pmId);
        } else if (['PM', 'Project Manager', 'SuperManager', 'Super Admin', 'Manager', 'TL'].includes(this.currentUser.role)) {
          this.createdByPmId = this.currentUser.id;
        }
        console.log('%c[PM/Assignee] PM/Assignee ID set:', 'color: #EC4899; font-weight: bold', this.createdByPmId);
        // We MUST await loadAssignees so options are ready BEFORE we auto-fill
        await this.loadAssignees();
      } else {
        // Customer mode – lock source to WEB
        this.ticketForm.ticketSource = 'WEB';
        console.log('%c[PM] No pmId in URL – customer mode, source locked to WEB', 'color: #6B7280');
      }

      if (parentId) {
        this.parentTicketId = Number(parentId);
        this.parentTicketNumber = parentNumber;
        console.log('%c[PARENT] Creating child of ticket:', 'color: #8B5CF6; font-weight: bold',
          this.parentTicketId, this.parentTicketNumber);
      }

      // Now it is safe to set the customer and auto-fill assignee
      this.processCustomerIdFromRoute(params);
    });

    this.loadDockets();
    this.loadDynamicMasters().then(() => {
      this.onPriorityChange('Medium');
    });
  }

  private async loadDynamicMasters(): Promise<void> {
    try {
      console.log('%c[DATA] Loading dynamic masters...', 'color: #F97316; font-weight: bold');
      const [priorities, categories, sources] = await Promise.all([
        this.ticketService.getPriorities(),
        this.ticketService.getIssueCategories(),
        this.ticketService.getTicketSources()
      ]);

      this.rawPriorities = priorities;

      // Filter only active ones, and map them to our dropdown format
      this.priorityOptions = priorities
        .filter((p: any) => p.isActive !== false && p.IsActive !== false)
        .map((p: any) => ({ id: p.priorityName || p.PriorityName, name: p.priorityName || p.PriorityName }));
      if (this.priorityOptions.length > 0) {
        this.ticketForm.priority = this.priorityOptions[0].name;
      }

      // Use ONLY what is in the DB (no hardcoded defaults merged in)
      this.ticketTypeOptions = categories
        .filter((c: any) => c.isActive !== false && c.IsActive !== false)
        .map((c: any) => ({ id: c.categoryName || c.CategoryName, name: c.categoryName || c.CategoryName }));
      if (this.ticketTypeOptions.length > 0) {
        this.ticketForm.ticketType = this.ticketTypeOptions[0].name;
      }

      // Load ticket sources purely from the DB
      this.ticketSourceOptions = sources
        .map((s: any) => ({ id: s.sourceName || s.SourceName, name: s.sourceName || s.SourceName }));
      if (this.ticketSourceOptions.length > 0 && !this.isCustomerRole) {
        this.ticketForm.ticketSource = this.ticketSourceOptions[0].name;
      }

      console.log('%c[DATA] Masters loaded', 'color: #10B981');
    } catch (e) {
      console.error('%c[DATA] Failed to load masters', 'color: #EF4444', e);
      // Fallback to defaults
      this.rawPriorities = [
        { priorityName: 'Low', tatHours: 48 },
        { priorityName: 'Medium', tatHours: 24 },
        { priorityName: 'High', tatHours: 12 },
        { priorityName: 'Urgent', tatHours: 4 }
      ];
      this.priorityOptions = [
        { id: 'Low', name: 'Low' },
        { id: 'Medium', name: 'Medium' },
        { id: 'High', name: 'High' },
        { id: 'Urgent', name: 'Urgent' }
      ];
      if (this.priorityOptions.length > 0) {
        this.ticketForm.priority = this.priorityOptions[0].name;
      }
      this.ticketTypeOptions = [...this.DEFAULT_TICKET_TYPES];
      if (this.ticketTypeOptions.length > 0) {
        this.ticketForm.ticketType = this.ticketTypeOptions[0].name;
      }
      this.ticketSourceOptions = [...this.DEFAULT_TICKET_SOURCES];
      if (this.ticketSourceOptions.length > 0 && !this.isCustomerRole) {
        this.ticketForm.ticketSource = this.ticketSourceOptions[0].name;
      }
    }
  }

  public onPriorityChange(priorityVal: string): void {
    if (!priorityVal) return;

    const matched = this.rawPriorities.find((p: any) => 
      (p.priorityName || p.PriorityName || '').toLowerCase() === priorityVal.toLowerCase()
    );

    let tatHours = 24;
    if (matched) {
      tatHours = matched.tatHours || matched.TATHours || matched.tat_hours || 24;
    } else {
      const fallbackMap: { [key: string]: number } = {
        'low': 48,
        'medium': 24,
        'high': 12,
        'urgent': 4
      };
      tatHours = fallbackMap[priorityVal.toLowerCase()] || 24;
    }

    const now = new Date();
    now.setHours(now.getHours() + tatHours);

    const year = now.getFullYear();
    const month = String(now.getMonth() + 1).padStart(2, '0');
    const day = String(now.getDate()).padStart(2, '0');
    const hours = String(now.getHours()).padStart(2, '0');
    const minutes = String(now.getMinutes()).padStart(2, '0');

    this.ticketForm.deadline = `${year}-${month}-${day}T${hours}:${minutes}`;
    console.log(`[DEADLINE] Calculated for priority ${priorityVal} (${tatHours} hours TAT):`, this.ticketForm.deadline);
  }

  private docketSearchTimeout: any;

  private async loadDockets(customerId?: number): Promise<void> {
    try {
      const res = await this.ticketService.getDockets(undefined, customerId);
      // Normalize to lowercase key for consistency
      this.dockets = res.map((d: any) => ({
        ...d,
        docketNo: d.DocketNo || d.docketNo || '',
        status: d.DocketStatus || d.Status || d.status || ''
      }));
      this.filteredDockets = [...this.dockets];
      console.log('%c[DATA] Dockets loaded:', 'color: #10B981', this.dockets.length, customerId ? `(filtered for customer ${customerId})` : '(all)');
    } catch (e) {
      console.error('Failed to load dockets', e);
      this.dockets = [];
      this.filteredDockets = [];
    }
  }

  public filterDockets(): void {
    const term = (this.searchDocketText || '').trim();
    if (this.docketSearchTimeout) {
      clearTimeout(this.docketSearchTimeout);
    }

    if (!term) {
      this.filteredDockets = [...this.dockets];
      this.isDocketDropdownOpen = true;
      return;
    }

    this.docketSearchTimeout = setTimeout(async () => {
      try {
        const res = await this.ticketService.getDockets(term, this.ticketForm.customerId || undefined);
        this.filteredDockets = res.map((d: any) => ({
          ...d,
          docketNo: d.DocketNo || d.docketNo || '',
          status: d.DocketStatus || d.Status || d.status || ''
        }));
      } catch (e) {
        console.error('Failed to filter dockets from server', e);
        this.filteredDockets = [];
      }
    }, 300);

    this.isDocketDropdownOpen = true;
  }

  public onDocketInput(): void {
    this.ticketForm.docketNumber = '';
    this.selectedDocketStatus = '';
    this.filterDockets();
  }

  public fetchDocketStatus(docketNo: string): void {
    if (!docketNo) {
      this.selectedDocketStatus = '';
      return;
    }

    const fromList = this.dockets.find(d => (d.docketNo || d.DocketNo) === docketNo);
    const listStatus = fromList?.status || fromList?.DocketStatus || fromList?.Status || '';
    if (listStatus) {
      this.selectedDocketStatus = listStatus;
      return;
    }

    this.ticketService.getDocketByNo(docketNo)
      .then((response: any) => {
        this.selectedDocketStatus =
          response?.details?.Status ||
          response?.details?.status ||
          response?.Status ||
          response?.status ||
          '';
      })
      .catch(error => {
        console.warn('Failed to fetch selected docket status', error);
        this.selectedDocketStatus = '';
      });
  }

  public selectDocket(docketNo: string): void {
    this.ticketForm.docketNumber = docketNo;
    this.searchDocketText = docketNo;
    this.isDocketDropdownOpen = false;
    this.fetchDocketStatus(docketNo);
  }

  public closeDocketDropdown(): void {
    // Small delay so mousedown on option fires before blur hides it
    setTimeout(() => {
      this.isDocketDropdownOpen = false;
      // If user typed something but didn't select, keep what they typed as free text
      if (this.searchDocketText && !this.ticketForm.docketNumber) {
        this.ticketForm.docketNumber = this.searchDocketText;
        this.fetchDocketStatus(this.searchDocketText);
      }
    }, 200);
  }

  public getDocketStatusLabel(status?: string | null): string {
    const normalized = (status || '').trim();
    return normalized || 'Unknown';
  }

  ngAfterViewInit(): void {
    console.log('%c[VIEW] ngAfterViewInit – setting up editor', 'color: #6366F1');
    this.setupTextEditor();
  }

  private setupTextEditor(): void {
    if (this.descriptionEditor) {
      const element = this.descriptionEditor.nativeElement;
      console.log('%c[EDITOR] Editor element ready', 'color: #14B8A6');

      element.addEventListener('keydown', (e: KeyboardEvent) => {
        if (e.ctrlKey || e.metaKey) {
          switch (e.key) {
            case 'b': e.preventDefault(); this.formatText('bold'); break;
            case 'i': e.preventDefault(); this.formatText('italic'); break;
            case 'u': e.preventDefault(); this.formatText('underline'); break;
          }
        }

        if (this.getDescriptionLength() >= 2000 &&
          !['Backspace', 'Delete', 'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'].includes(e.key) &&
          !e.ctrlKey && !e.metaKey) {
          e.preventDefault();
        }
      });

      element.addEventListener('paste', (e: ClipboardEvent) => {
        e.preventDefault();
        const paste = e.clipboardData?.getData('text/plain') || '';
        const selection = window.getSelection();
        if (selection && selection.rangeCount > 0) {
          selection.deleteFromDocument();
          selection.getRangeAt(0).insertNode(document.createTextNode(paste));
          selection.collapseToEnd();
        }
        this.onDescriptionInput();
      });
    }
  }

  private async loadAssignees(): Promise<void> {
    console.log('%c[DATA] Loading assignees...', 'color: #F97316; font-weight: bold');
    try {
      let custsPromise;
      if (['Assignee', 'Developer', 'PM', 'Project Manager', 'SuperManager', 'Manager', 'TL'].includes(this.currentUser.role)) {
        custsPromise = this.ticketService.getMyCustomers();
      } else {
        custsPromise = this.ticketService.getUsersByRole('Customer');
      }

      const [devs, custs] = await Promise.all([
        this.ticketService.getAssignees(),
        custsPromise
      ]);
      this.assigneeOptions = devs.map(d => {
        const anyD = d as any;
        const id = anyD.Id || anyD.id;
        const assigneeNo = anyD.AssigneeNumber || anyD.UserNumber || anyD.assigneeNumber || anyD.userNumber || 'N/A';
        const fullName = anyD.FullName || anyD.fullName || 'Unknown';
        return {
          id: id,
          name: `${assigneeNo} – ${fullName}`
        };
      });
      this.filteredAssignees = this.assigneeOptions;
      this.customers = custs || [];
      console.log('%c[DATA] Assignees & Customers loaded:', 'color: #10B981; font-weight: bold', this.assigneeOptions.length, this.customers.length);

      if (this.customers.length > 0) {
        const hasExistingCustomer = this.customers.some(c => (c.id || c.Id) == this.ticketForm.customerId);
        if (!hasExistingCustomer) {
          const firstCustId = this.customers[0].id || this.customers[0].Id;
          if (firstCustId) {
            this.onCustomerChange(firstCustId);
          }
        }
      }
    } catch (e) {
      console.error('%c[DATA] Failed to load assignees/customers', 'color: #EF4444', e);
    }
  }

  public filterAssignees(): void {
    const term = this.assigneeSearchQuery.toLowerCase().trim();
    this.filteredAssignees = this.assigneeOptions.filter(a =>
      a.name?.toLowerCase().includes(term)
    );
  }

  public onAssigneeSearchInput(): void {
    this.showAssigneeDropdown = true;
    this.filterAssignees();
    if (!this.assigneeSearchQuery.trim()) {
      this.ticketForm.assignedToId = undefined;
    }
  }

  public selectAssignee(assignee: any): void {
    this.ticketForm.assignedToId = assignee.id || undefined;
    this.assigneeSearchQuery = assignee.id ? assignee.name : '';
    this.showAssigneeDropdown = false;
  }

  public onAssigneeBlur(): void {
    setTimeout(() => {
      this.showAssigneeDropdown = false;
      const selectedId = this.ticketForm.assignedToId;
      const current = this.assigneeOptions.find(a => a.id === selectedId);
      this.assigneeSearchQuery = current ? current.name : '';
    }, 200);
  }

  private loadCurrentUser(): void {
    const sessionStr = sessionStorage.getItem('auth_user') || sessionStorage.getItem('tms_session') || localStorage.getItem('user');
    if (sessionStr) {
      try {
        const session = JSON.parse(sessionStr);
        this.currentUser.role = session.role || 'Customer';
        this.currentUser.name = session.fullName || session.userName || session.name || 'Unknown User';
        // For Customer role, prefer customerId (the customer record ID), else userId
        if (this.currentUser.role === 'Customer') {
          this.currentUser.id = Number(session.customerId || session.userId || session.id || 0);
        } else {
          this.currentUser.id = Number(session.userId || session.id || 0);
        }
        console.log('[AUTH] Loaded current user in ticket-create:', this.currentUser);
      } catch (e) {
        console.error('[AUTH] Failed to parse session:', e);
      }
    }
  }

  private processCustomerIdFromRoute(params: ParamMap): void {
    console.log('%c[ROUTE] Reading customerId', 'color: #8B5CF6');
    let parsedCustomerId: number | null = null;

    const queryCustomerId = params.get('customerId');
    if (queryCustomerId) {
      parsedCustomerId = Number(queryCustomerId);
      console.log('%c[FORM] customerId set from query params:', 'color: #10B981', parsedCustomerId);
    } else {
      // Fallback to session — read from the same key auth service writes to
      const session = sessionStorage.getItem('auth_user') || sessionStorage.getItem('tms_session') || localStorage.getItem('user');
      if (session) {
        try {
          const user = JSON.parse(session);
          // AuthUser shape: { userId, customerId, role } OR legacy { id, role }
          if (!user.role || user.role === 'Customer') {
            parsedCustomerId = Number(user.customerId || user.userId || user.id || 0) || null;
            console.log('%c[FORM] customerId set from session (auth_user):', 'color: #10B981', parsedCustomerId);
          }
        } catch (e) {
          this.errorMessage = "Failed to parse session data.";
          this.showNotification(this.errorMessage, 'error');
        }
      }
    }

    if (parsedCustomerId) {
      this.ticketForm.customerId = parsedCustomerId;
      this.loadProductData(parsedCustomerId);
      // Reload dockets filtered to this customer
      this.loadDockets(parsedCustomerId);

      // Fetch customer details to resolve PM/Manager name
      this.ticketService.getUserById(parsedCustomerId).then((customer: any) => {
        const managerId = customer?.managerId || customer?.ManagerId;
        if (managerId) {
          this.ticketService.getAssignees().then(devs => {
            const pm = devs.find((d: any) => d.id === managerId || d.Id === managerId) as any;
            if (pm) {
              const assigneeNo = pm.assigneeNumber || pm['AssigneeNumber'] || pm.userNumber || pm['UserNumber'] || 'PM';
              const fullName = pm.fullName || pm['FullName'] || '';
              this.selectedCustomerPmName = `${assigneeNo} – ${fullName}`;
            } else {
              this.selectedCustomerPmName = `PM #${managerId}`;
            }
          });
        } else {
          this.selectedCustomerPmName = '';
        }
      }).catch(err => {
        console.warn('Could not fetch customer info for PM resolution:', err);
      });

      // Auto-fill Default Assignee if available
      this.ticketService.getExecutiveByCustomerId(parsedCustomerId).then((execObj: any) => {
        console.log('%c[DEBUG] Fetched mapped executive from backend API:', 'color: #3B82F6', execObj);
        const assigneeId = execObj?.Id || execObj?.id;
        console.log('%c[DEBUG] Parsed executive ID from backend:', 'color: #EC4899; font-weight: bold; font-size: 14px', assigneeId);

        if (assigneeId) {
          this.ticketForm.assignedToId = assigneeId;
          console.log('%c[FORM] Auto-assigned developer on page to ID:', 'color: #10B981; font-weight: bold; font-size: 14px', assigneeId);
          const assigneeNo = execObj.AssigneeNumber || execObj.assigneeNumber || execObj.UserNumber || execObj.userNumber || 'N/A';
          const fullName = execObj.FullName || execObj.fullName || 'Unknown';
          this.assigneeSearchQuery = `${assigneeNo} – ${fullName}`;
        } else {
          console.log('%c[FORM] Customer has no mapped executive, leaving unassigned', 'color: #EF4444');
          this.assigneeSearchQuery = '';
        }
      }).catch(error => {
        console.warn('Could not fetch customer details for default assignee auto-fill', error);
      });
    } else {
      if (!['PM', 'Project Manager', 'SuperManager', 'Super Admin', 'Manager', 'TL', 'Assignee', 'Developer'].includes(this.currentUser.role)) {
        this.errorMessage = "Customer ID not found.";
        this.showNotification(this.errorMessage, 'error');
      }
    }
  }

  // Text Editor Methods
  public formatText(command: string): void {
    if (this.descriptionEditor) {
      this.descriptionEditor.nativeElement.focus();
      setTimeout(() => {
        document.execCommand(command, false, undefined);
        this.onDescriptionInput();
      }, 10);
    }
  }

  public isFormatActive(command: string): boolean {
    return document.queryCommandState(command);
  }

  public onDescriptionFocus(): void {
    this.isDescriptionFocused = true;
  }

  public onDescriptionBlur(): void {
    this.isDescriptionFocused = false;
    this.updateFormModel();
  }

  public onDescriptionInput(event?: Event): void { }

  public onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      document.execCommand('insertHTML', false, '<br><br>');
    }
  }

  @HostListener('window:paste', ['$event'])
  public onPaste(event: ClipboardEvent): void {
    const items = event.clipboardData?.items;
    if (!items) return;

    let hasImage = false;
    for (let i = 0; i < items.length; i++) {
      const item = items[i];
      if (item.type.indexOf('image') !== -1) {
        const file = item.getAsFile();
        if (file) {
          hasImage = true;
          const extension = file.type.split('/')[1] || 'png';
          const timestamp = new Date().getTime();
          const newName = `pasted-image-${timestamp}.${extension}`;
          const pastedFile = new File([file], newName, { type: file.type });
          this.selectedFiles = [...this.selectedFiles, pastedFile];
          this.showNotification(`Image "${newName}" attached from clipboard`, 'success');
          console.log('%c[PASTE] Image attached:', 'color: #10B981', newName, pastedFile);
        }
      }
    }

    if (hasImage) {
      event.preventDefault();
    }
  }

  public onPasteButtonClick(): void {
    // Focus the hidden textarea to receive Ctrl+V paste event
    if (this.pasteReceiver) {
      this.pasteReceiver.nativeElement.focus();
      this.showNotification('Pasting area focused. If direct pasting is blocked, press Ctrl+V now.', 'info');
    }
    // Also try to read from clipboard directly if supported
    this.pasteFromClipboard();
  }

  public onPasteReceiverPaste(event: ClipboardEvent): void {
    const items = event.clipboardData?.items;
    if (!items) return;

    let hasImage = false;
    for (let i = 0; i < items.length; i++) {
      const item = items[i];
      if (item.type.indexOf('image') !== -1) {
        const file = item.getAsFile();
        if (file) {
          hasImage = true;
          const extension = file.type.split('/')[1] || 'png';
          const timestamp = new Date().getTime();
          const newName = `pasted-image-${timestamp}.${extension}`;
          const pastedFile = new File([file], newName, { type: file.type });
          this.selectedFiles = [...this.selectedFiles, pastedFile];
          this.showNotification(`Image "${newName}" attached from clipboard`, 'success');
          console.log('%c[PASTE] Image attached to receiver:', 'color: #10B981', newName, pastedFile);
        }
      }
    }

    if (hasImage) {
      event.preventDefault();
    }
  }

  public async pasteFromClipboard(): Promise<void> {
    try {
      if (!navigator.clipboard || !navigator.clipboard.read) {
        // Quiet fallback, user is already told to press Ctrl+V by the button click
        return;
      }
      
      const clipboardItems = await navigator.clipboard.read();
      let hasImage = false;
      for (const item of clipboardItems) {
        for (const type of item.types) {
          if (type.startsWith('image/')) {
            const blob = await item.getType(type);
            const extension = type.split('/')[1] || 'png';
            const timestamp = new Date().getTime();
            const newName = `pasted-image-${timestamp}.${extension}`;
            const pastedFile = new File([blob], newName, { type: type });
            
            this.selectedFiles = [...this.selectedFiles, pastedFile];
            this.showNotification(`Image "${newName}" pasted from clipboard`, 'success');
            hasImage = true;
          }
        }
      }
      if (hasImage) {
        // Success
      }
    } catch (err: any) {
      console.warn('Direct clipboard reading denied or unsupported, falling back to Ctrl+V focused receiver.', err);
    }
  }

  private updateFormModel(): void {
    if (this.descriptionEditor) {
      this.ticketForm.description = this.descriptionEditor.nativeElement.innerText;
      console.log('%c[EDITOR] Description updated:', 'color: #6366F1', this.ticketForm.description);
    }
  }

  public getDescriptionLength(): number {
    if (this.descriptionEditor) {
      return this.descriptionEditor.nativeElement.textContent?.length || 0;
    }
    return this.getDescriptionTextContent().length;
  }

  private getDescriptionTextContent(): string {
    if (!this.ticketForm.description) return '';
    const div = document.createElement('div');
    div.innerHTML = this.ticketForm.description;
    return div.textContent || div.innerText || '';
  }

  // Searchable Product Dropdown Handlers
  public filterProducts(): void {
    const term = (this.searchProductText || '').toLowerCase().trim();
    if (!term) {
      this.filteredProducts = [...this.products];
    } else {
      this.filteredProducts = this.products.filter(p => p.name.toLowerCase().includes(term));
    }
  }

  public onProductInput(): void {
    this.ticketForm.productId = '';
    this.ticketForm.productName = this.searchProductText;
    this.ticketForm.moduleId = '';
    this.ticketForm.moduleName = '';
    this.searchModuleText = '';
    this.ticketForm.subModuleId = '';
    this.ticketForm.subModuleName = '';
    this.searchSubModuleText = '';
    this.availableModules = [];
    this.availableSubModules = [];
    this.filterProducts();
  }

  public selectProduct(product: ProductData): void {
    this.ticketForm.productId = product.id;
    this.ticketForm.productName = product.name;
    this.searchProductText = product.name;
    this.isProductDropdownOpen = false;

    this.availableModules = product.modules || [];
    this.filteredModules = [...this.availableModules];

    // Clear child selections
    this.ticketForm.moduleId = '';
    this.ticketForm.moduleName = '';
    this.searchModuleText = '';
    this.ticketForm.subModuleId = '';
    this.ticketForm.subModuleName = '';
    this.searchSubModuleText = '';

    // Auto-select module if only one module is available
    if (this.availableModules.length === 1) {
      this.selectModule(this.availableModules[0]);
    }
  }

  public closeProductDropdown(): void {
    setTimeout(() => {
      this.isProductDropdownOpen = false;
      if (this.searchProductText && !this.ticketForm.productId) {
        this.ticketForm.productName = this.searchProductText;
      }
    }, 200);
  }

  // Searchable Module Dropdown Handlers
  public filterModules(): void {
    const term = (this.searchModuleText || '').toLowerCase().trim();
    if (!term) {
      this.filteredModules = [...this.availableModules];
    } else {
      this.filteredModules = this.availableModules.filter(m => m.name.toLowerCase().includes(term));
    }
  }

  public onModuleInput(): void {
    this.ticketForm.moduleId = '';
    this.ticketForm.moduleName = this.searchModuleText;
    this.ticketForm.subModuleId = '';
    this.ticketForm.subModuleName = '';
    this.searchSubModuleText = '';
    this.availableSubModules = [];
    this.filterModules();
  }

  public selectModule(module: ModuleData): void {
    this.ticketForm.moduleId = module.id;
    this.ticketForm.moduleName = module.name;
    this.searchModuleText = module.name;
    this.isModuleDropdownOpen = false;

    this.availableSubModules = module.subModules || [];
    this.filteredSubModules = [...this.availableSubModules];

    // Clear child selection
    this.ticketForm.subModuleId = '';
    this.ticketForm.subModuleName = '';
    this.searchSubModuleText = '';

    // Auto-select submodule if only one submodule is available
    if (this.availableSubModules.length === 1) {
      this.selectSubModule(this.availableSubModules[0]);
    }
  }

  public closeModuleDropdown(): void {
    setTimeout(() => {
      this.isModuleDropdownOpen = false;
      if (this.searchModuleText && !this.ticketForm.moduleId) {
        this.ticketForm.moduleName = this.searchModuleText;
      }
    }, 200);
  }

  // Searchable SubModule Dropdown Handlers
  public filterSubModules(): void {
    const term = (this.searchSubModuleText || '').toLowerCase().trim();
    if (!term) {
      this.filteredSubModules = [...this.availableSubModules];
    } else {
      this.filteredSubModules = this.availableSubModules.filter(sm => sm.name.toLowerCase().includes(term));
    }
  }

  public onSubModuleInput(): void {
    this.ticketForm.subModuleId = '';
    this.ticketForm.subModuleName = this.searchSubModuleText;
    this.filterSubModules();
  }

  public selectSubModule(subModule: SubModuleData): void {
    this.ticketForm.subModuleId = subModule.id;
    this.ticketForm.subModuleName = subModule.name;
    this.searchSubModuleText = subModule.name;
    this.isSubModuleDropdownOpen = false;
  }

  public closeSubModuleDropdown(): void {
    setTimeout(() => {
      this.isSubModuleDropdownOpen = false;
      if (this.searchSubModuleText && !this.ticketForm.subModuleId) {
        this.ticketForm.subModuleName = this.searchSubModuleText;
      }
    }, 200);
  }

  public onCustomerChange(args: any): void {
    const customerId = (args && args.value !== undefined) ? args.value : args;
    this.ticketForm.customerId = customerId;
    console.log('%c[DEBUG] onCustomerChange triggered for customerId:', 'color: #F59E0B', customerId);

    // Auto-populate Assigned To based on mapped developer
    const customer: any = this.customers.find(c => c.id == customerId || (c as any).Id == customerId);
    if (customer) {
      console.log('%c[DEBUG] Found Customer Data:', 'color: #3B82F6', customer);
      const assigneeId = customer.DefaultAssigneeId || customer.defaultAssigneeId;
      console.log('%c[DEBUG] Parsed DefaultAssigneeId from Customer:', 'color: #EC4899; font-weight: bold; font-size: 14px', assigneeId);

      if (assigneeId) {
        this.ticketForm.assignedToId = assigneeId;
        console.log('%c[FORM] Auto-assigned developer on page to ID:', 'color: #10B981; font-weight: bold; font-size: 14px', assigneeId);
        const current = this.assigneeOptions.find(a => a.id === assigneeId);
        this.assigneeSearchQuery = current ? current.name : '';
      } else {
        this.ticketForm.assignedToId = undefined;
        this.assigneeSearchQuery = '';
        console.log('%c[FORM] No mapped developer found, setting to Unassigned', 'color: #EF4444');
      }
    } else {
      this.ticketForm.assignedToId = undefined;
      this.assigneeSearchQuery = '';
      console.log('%c[FORM] Customer not found in list, setting developer to Unassigned', 'color: #EF4444');
    }

    // Show the customer's assigned PM
    if (customer && customer.managerId) {
      const allUsers = this.assigneeOptions as any[];
      const pm = allUsers.find((a: any) => a.id === customer.managerId || a.id === Number(customer.managerId));
      this.selectedCustomerPmName = pm ? (pm.name || pm.fullName || '') : `PM #${customer.managerId}`;
    } else {
      this.selectedCustomerPmName = '';
    }

    // Reload products for the new customer
    this.loadProductData(customerId);

    // Reload dockets filtered to the newly selected customer
    this.loadDockets(customerId);
  }

  public openAddSourceDialog(): void {
    this.newSourceName = '';
    this.showAddSourceModal = true;
  }

  public closeAddSourceDialog(): void {
    this.showAddSourceModal = false;
    this.newSourceName = '';
  }

  public async addCustomTicketSource(): Promise<void> {
    const sourceName = this.newSourceName.trim();
    if (!sourceName) return;

    try {
      const exists = this.ticketSourceOptions.find(o => o.name.toLowerCase() === sourceName.toLowerCase());
      if (!exists) {
        await this.ticketService.addTicketSource(sourceName);
        this.ticketSourceOptions = [
          ...this.ticketSourceOptions,
          { id: sourceName, name: sourceName }
        ];
      }

      this.ticketForm.ticketSource = sourceName;
      this.closeAddSourceDialog();
      this.showNotification(`Custom source "${sourceName}" added and selected.`, 'success');
    } catch (error) {
      console.error('Failed to add custom ticket source', error);
      this.showNotification('Failed to add custom source. Please try again.', 'error');
    }
  }

  private async loadCustomTicketSources(): Promise<void> {
    try {
      const sources = await this.ticketService.getTicketSources();
      const dynamicSources = sources.map((s: any) => {
        const mappedName = s.SourceName || s.sourceName || s.name;
        return {
          id: mappedName,
          name: mappedName
        };
      });
      // Deduplicate defaults + dynamic
      const merged = [...this.DEFAULT_TICKET_SOURCES];
      dynamicSources.forEach(ds => {
        if (!merged.some(m => m.name.toLowerCase() === ds.name.toLowerCase())) {
          merged.push(ds);
        }
      });
      this.ticketSourceOptions = merged;
    } catch (e) {
      console.error('Failed to load custom ticket sources from API', e);
      this.ticketSourceOptions = [...this.DEFAULT_TICKET_SOURCES];
    }
  }

  // File upload
  public onFileSelect(event: any): void {
    let newFiles: File[] = [];
    if (event.filesData) {
      newFiles = event.filesData.map((f: any) => f.rawFile);
    } else if (event.target && event.target.files) {
      newFiles = Array.from(event.target.files);
    }

    const existingNames = this.selectedFiles.map(f => f.name);
    const uniqueNewFiles = newFiles.filter(f => !existingNames.includes(f.name));
    this.selectedFiles = [...this.selectedFiles, ...uniqueNewFiles];
    console.log('%c[FILE] Files selected (appended):', 'color: #F59E0B', this.selectedFiles.map(f => f.name));
  }

  public onFileRemove(args: any): void {
    const nameToRemove = args.filesData ? args.filesData[0].name : args;
    this.selectedFiles = this.selectedFiles.filter(f => f.name !== nameToRemove);
    console.log('%c[FILE] File removed:', 'color: #EF4444', nameToRemove);
  }

  public previewFile(file: File): void {
    const objectUrl = URL.createObjectURL(file);
    const a = document.createElement('a');
    a.href = objectUrl;
    a.target = '_blank';
    a.download = file.name;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    setTimeout(() => URL.revokeObjectURL(objectUrl), 100);
  }

  // Validation
  private validateForm(): boolean {
    this.validationErrors = {};
    let isValid = true;

    if (!this.ticketForm.subject?.trim()) {
      this.validationErrors['subject'] = 'Subject is required';
      isValid = false;
    }

    const desc = this.getDescriptionTextContent();
    if (!desc?.trim()) {
      this.validationErrors['description'] = 'Description is required';
      isValid = false;
    }


    if (this.ticketForm.subject && this.ticketForm.subject.length < 5) {
      this.validationErrors['subject'] = 'Subject must be at least 5 characters';
      isValid = false;
    }

    if (desc && desc.length < 20) {
      this.validationErrors['description'] = 'Description must be at least 20 characters';
      isValid = false;
    }

    if (desc && desc.length > 2000) {
      this.validationErrors['description'] = 'Description cannot exceed 2000 characters';
      isValid = false;
    }

    if (!this.ticketForm.productId && !this.ticketForm.productName?.trim()) {
      this.validationErrors['productId'] = 'Product selection is required';
      isValid = false;
    }

    console.log('%c[VALIDATION] Form valid:', 'color: #10B981', isValid, this.validationErrors);
    return isValid;
  }

  // ========================================================================
  // MAIN SUBMIT WITH FULL DEBUGGING
  // ========================================================================
  public async onSubmit(): Promise<void> {
    console.clear();
    console.log('%c[SUBMIT] onSubmit() triggered', 'color: #EC4899; font-size: 16px; font-weight: bold');

    this.updateFormModel();

    if (!this.validateForm()) {
      this.showNotification('Please correct the validation errors before submitting.', 'error');
      return;
    }

    this.isSubmitting = true;
    this.errorMessage = null;

    // --------------------------------------------------------------------
    // Containers for results / errors
    // --------------------------------------------------------------------
    let newTicket: any = null;
    const pmResults: any[] = [];
    const allErrors: string[] = [];

    try {
      // ----------------------------------------------------------------
      // 1. BUILD FORM DATA
      // ----------------------------------------------------------------
      const formData = new FormData();
      formData.append('subject', this.ticketForm.subject);
      formData.append('description', this.ticketForm.description);
      formData.append('priority', this.ticketForm.priority);
      formData.append('ticketType', this.ticketForm.ticketType);

      formData.append('customerId', this.ticketForm.customerId.toString());

      // Optional fields — only append if actually filled
      if (this.ticketForm.productId) formData.append('productId', this.ticketForm.productId.toString());
      if (this.ticketForm.productName) formData.append('productName', this.ticketForm.productName);
      if (this.ticketForm.moduleId) formData.append('moduleId', this.ticketForm.moduleId.toString());
      if (this.ticketForm.moduleName) formData.append('moduleName', this.ticketForm.moduleName);
      if (this.ticketForm.subModuleId) formData.append('subModuleId', this.ticketForm.subModuleId.toString());
      if (this.ticketForm.subModuleName) formData.append('subModuleName', this.ticketForm.subModuleName);
      if (this.createdByPmId) formData.append('createdByPmId', this.createdByPmId.toString());
      if (this.ticketForm.docketNumber) formData.append('docketNumber', this.ticketForm.docketNumber);
      if (this.ticketForm.ticketSource) formData.append('ticketSource', this.ticketForm.ticketSource);
      // Source contact fields (Phone / Email)
      if (this.sourcePhone?.trim()) formData.append('sourcePhone', this.sourcePhone.trim());
      if (this.sourceEmail?.trim()) formData.append('sourceEmail', this.sourceEmail.trim());

      if (this.currentUser.role === 'Customer') {
        const tatHours: { [key: string]: number } = {
          'Low': 48,
          'Medium': 24,
          'High': 12,
          'Urgent': 4
        };
        const hours = tatHours[this.ticketForm.priority] || 24;
        const deadlineDate = new Date();
        deadlineDate.setHours(deadlineDate.getHours() + hours);
        formData.append('deadlineDate', deadlineDate.toISOString());
      }

      if (this.ticketForm.assignedToId) formData.append('assignedToId', this.ticketForm.assignedToId.toString());
      this.selectedFiles.forEach(f => formData.append('attachments', f, f.name));

      console.log('%c[FORMDATA] Final FormData:', 'color: #8B5CF6; font-weight: bold');
      ;

      // ----------------------------------------------------------------
      // 2. CREATE TICKET (may fail)
      // ----------------------------------------------------------------
      console.log('%c[API] Calling createTicket...', 'color: #F97316; font-weight: bold');
      try {
        newTicket = await this.ticketService.createTicket(formData);
        console.log('%c[API] createTicket SUCCESS', 'color: #10B981; font-weight: bold', newTicket);
        console.log(`Ticket ID: ${newTicket.id} | Number: ${newTicket.ticketNumber}`);
      } catch (err: any) {
        console.error('%c[API] createTicket FAILED', 'color: #EF4444; font-weight: bold', err);
        allErrors.push(`Ticket creation failed: ${err?.message || err}`);
        // **DO NOT** `return` – continue to PM APIs if possible
      }

      // ----------------------------------------------------------------
      // 3. PM-ONLY LOGIC – ALWAYS RUN IF PM + fields filled
      // ----------------------------------------------------------------
      console.log('%c[PM CHECK] createdByPmId =', 'color: #EC4899', this.createdByPmId);
      console.log('%c[PM CHECK] assignedToId =', 'color: #F59E0B', this.ticketForm.assignedToId);
      console.log('%c[PM CHECK] deadline =', 'color: #8B5CF6', this.ticketForm.deadline);

      if (this.createdByPmId && (this.ticketForm.assignedToId || this.ticketForm.deadline)) {
        const pmPromises: Promise<any>[] = [];

        // ---- ASSIGN DEVELOPER (only if ticket exists) ----
        if (newTicket && this.ticketForm.assignedToId) {
          const payload: PmTicketUpdatePayload = {
            status: 'Open',
            priority: this.ticketForm.priority,
            assignedToId: Number(this.ticketForm.assignedToId)
          };

          console.log('%c[PM API] Calling updateTicketAsPm', 'color: #F97316; font-weight: bold');
          console.log('   Ticket ID:', newTicket.id, 'PM ID:', this.createdByPmId, 'Payload:', payload);

          pmPromises.push(
            this.ticketService.updateTicketAsPm(newTicket.id, this.createdByPmId, payload)
              .then(res => {
                console.log('%c[PM API] updateTicketAsPm SUCCESS', 'color: #10B981; font-weight: bold', res);
                pmResults.push(res);
              })
              .catch(err => {
                console.error('%c[PM API] updateTicketAsPm FAILED', 'color: #EF4444; font-weight: bold', err);
                allErrors.push(`Assignee update failed: ${err?.message || err}`);
              })
          );
        } else if (!newTicket) {
          console.warn('%c[PM SKIP] No ticket ID → cannot assign developer', 'color: #F59E0B');
        } else {
          console.log('%c[PM SKIP] No assignee selected', 'color: #6B7280');
        }

        // ---- SET DEADLINE (only if ticket exists) ----
        if (newTicket && this.ticketForm.deadline) {
          const payload = {
            deadlineDate: this.ticketForm.deadline,
            managerId: this.createdByPmId
          };

          console.log('%c[PM API] Calling setTicketDeadline', 'color: #8B5CF6; font-weight: bold');
          console.log('   Ticket ID:', newTicket.id, 'Payload:', payload);

          pmPromises.push(
            this.ticketService.setTicketDeadline(newTicket.id, payload)
              .then(res => {
                console.log('%c[PM API] setTicketDeadline SUCCESS', 'color: #10B981; font-weight: bold', res);
                pmResults.push(res);
              })
              .catch(err => {
                console.error('%c[PM API] setTicketDeadline FAILED', 'color: #EF4444; font-weight: bold', err);
                allErrors.push(`Deadline update failed: ${err?.message || err}`);
              })
          );
        } else if (!newTicket) {
          console.warn('%c[PM SKIP] No ticket ID → cannot set deadline', 'color: #F59E0B');
        } else {
          console.log('%c[PM SKIP] No deadline selected', 'color: #6B7280');
        }

        // ---- EXECUTE PM CALLS ----
        if (pmPromises.length > 0) {
          console.log(`%c[PM EXEC] Running ${pmPromises.length} PM update(s)...`, 'color: #14B8A6; font-weight: bold');
          await Promise.allSettled(pmPromises);   // never throws
          console.log('%c[PM DONE] PM updates finished (some may have failed)', 'color: #10B981; font-weight: bold');
        }
      } else {
        console.log('%c[PM SKIP] Not a PM or no PM fields → skip PM APIs', 'color: #6B7280');
      }

      // ----------------------------------------------------------------
      // 3b. PARENT-CHILD LINKING (if launched from "Create Child" flow)
      // ----------------------------------------------------------------
      if (newTicket && this.parentTicketId) {
        try {
          const actorId = this.createdByPmId || this.currentUser.id || 0;
          await this.ticketService.setTicketParent(newTicket.id, this.parentTicketId, actorId);
          console.log('%c[PARENT] Child link set: ticket', newTicket.id, '→ parent', this.parentTicketId,
            'color: #10B981; font-weight: bold');
        } catch (err: any) {
          console.error('%c[PARENT] Failed to set parent link', 'color: #EF4444', err);
          allErrors.push(`Parent-child linking failed: ${err?.message || err}`);
        }
      }

      // ----------------------------------------------------------------
      // 4. FINAL RESULT
      // ----------------------------------------------------------------
      if (allErrors.length === 0) {
        this.showNotification(`Ticket #${newTicket?.id || '??'} created & updated!`, 'success');
        console.log('%c[SUCCESS] Full flow completed', 'color: #10B981; font-weight: bold');

        if (this.createdByPmId) {
          this.router.navigate(['/pm/dashboard']);
        } else {
          this.router.navigate(['/customer/tickets']);
        }
      } else {
        const msg = `Partial success. Errors:\n${allErrors.join('\n')}`;
        this.showNotification(msg, 'error');
        console.error('%c[PARTIAL] Errors collected:', 'color: #EF4444; font-weight: bold', allErrors);
      }

    } catch (unexpected: any) {
      console.error('%c[FATAL] Unexpected error in onSubmit', 'color: #EF4444; font-weight: bold', unexpected);
      this.showNotification('Unexpected error. Check console.', 'error');
    } finally {
      this.isSubmitting = false;
    }
  }

  public goBack(): void {
    this.location.back();
  }

  public resetForm(): void {
    this.ticketForm = {
      subject: '',
      description: '',
      priority: 'Medium',
      ticketType: 'Error',
      productId: '',
      moduleId: '',
      subModuleId: '',
      customerId: this.ticketForm.customerId,
      assignedToId: undefined,
      deadline: undefined,
      productName: '',
      moduleName: '',
      subModuleName: ''
    };
    this.selectedDocketStatus = '';
    this.searchDocketText = '';
    this.searchProductText = '';
    this.searchModuleText = '';
    this.searchSubModuleText = '';
    this.filteredProducts = [];
    this.filteredModules = [];
    this.filteredSubModules = [];

    if (this.descriptionEditor) {
      this.descriptionEditor.nativeElement.innerHTML = '';
    }

    this.selectedFiles = [];
    this.availableModules = [];
    this.availableSubModules = [];
    this.validationErrors = {};
    this.errorMessage = null;

    console.log('%c[FORM] Form reset', 'color: #6366F1');
  }

  public formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }



  public async loadProductData(customerId: number): Promise<void> {
    this.isProductsLoading = true;
    console.log('%c[DATA] Loading product hierarchy for customer:', 'color: #F59E0B', customerId);
    try {
      this.products = await this.ticketService.getProductHierarchy(customerId);
      this.filteredProducts = [...this.products];
      console.log('%c[DATA] Products loaded:', 'color: #10B981', this.products.length, 'items');
      if (this.products.length === 1) {
        this.selectProduct(this.products[0]);
      }
    } catch (error) {
      this.errorMessage = 'Failed to load product data.';
      this.showNotification(this.errorMessage, 'error');
      console.error('%c[DATA] Product load failed', 'color: #EF4444', error);
    } finally {
      this.isProductsLoading = false;
    }
  }

  public getSelectedProductName(): string {
    const p = this.products.find(x => x.id === this.ticketForm.productId);
    return p ? p.name : '';
  }

  public getSelectedModuleName(): string {
    const m = this.availableModules.find(x => x.id === this.ticketForm.moduleId);
    return m ? m.name : '';
  }

  public getSelectedSubModuleName(): string {
    const sm = this.availableSubModules.find(x => x.id === this.ticketForm.subModuleId);
    return sm ? sm.name : '';
  }

  private showNotification(message: string, type: 'success' | 'error' | 'info' = 'info'): void {
    this.notificationService.show(message, type);
  }



  // ── Custom Ticket Type Dialog ──
  public openAddTypeDialog(): void {
    this.newTypeName = '';
    this.showAddTypeModal = true;
    console.log('%c[DIALOG] Open Add Ticket Type modal', 'color: #8B5CF6; font-weight: bold');
  }

  public closeAddTypeDialog(): void {
    this.showAddTypeModal = false;
    this.newTypeName = '';
    console.log('%c[DIALOG] Closed Add Ticket Type modal', 'color: #EF4444');
  }

  public async addCustomTicketType(): Promise<void> {
    const typeName = this.newTypeName.trim();
    if (!typeName) return;

    try {
      const exists = this.ticketTypeOptions.some(opt => opt.name.toLowerCase() === typeName.toLowerCase());
      if (!exists) {
        await this.ticketService.createIssueCategory({ categoryName: typeName });
        this.ticketTypeOptions = [
          ...this.ticketTypeOptions,
          { id: typeName, name: typeName }
        ];
      }

      this.ticketForm.ticketType = typeName;
      this.closeAddTypeDialog();
      this.showNotification(`Custom type "${typeName}" added and selected.`, 'success');
    } catch (error) {
      console.error('Failed to add custom ticket type', error);
      this.showNotification('Failed to add custom type. Please try again.', 'error');
    }
  }

  private async loadCustomTicketTypes(): Promise<void> {
    try {
      const categories = await this.ticketService.getIssueCategories();
      // Use ONLY what is in the DB (no hardcoded defaults merged in)
      this.ticketTypeOptions = categories.map((c: any) => ({
        id: c.categoryName || c.name,
        name: c.categoryName || c.name
      }));
    } catch (e) {
      console.error('Failed to load ticket types from API', e);
      this.ticketTypeOptions = [];
    }
  }
}
