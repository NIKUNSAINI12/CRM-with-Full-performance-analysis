import { Component, OnInit, OnDestroy, Inject, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Observable, Subscription, from, BehaviorSubject } from 'rxjs';
import { finalize } from 'rxjs/operators';
import { TICKET_SERVICE_TOKEN } from '../../Core/injection-tokens';

import { Ticket, User } from '../../Core/models/ticket.model'; 
import { DashboardStats, TicketService } from '../../Core/services/ticket.service';
import { AuthService } from '../../Core/services/auth';

@Component({
  selector: 'app-pm-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './pm-dashboard.html',
  styleUrls: ['./pm-dashboard.scss']
})
export class PmDashboardComponent implements OnInit, OnDestroy {

  // Sidebar state
  public sidebarCollapsed = false;
  public sidebarOpen = false;
  public currentUser: User | null = null;

  // Tickets
  private ticketsSubject = new BehaviorSubject<Ticket[]>([]);
  public tickets$: Observable<Ticket[]> = this.ticketsSubject.asObservable();
  public allTickets: Ticket[] = [];
  public filteredTickets: Ticket[] = [];
  public paginatedTickets: Ticket[] = [];
  
  // Pagination
  public currentPage: number = 1;
  public pageSize: number = 15;
  public totalPages: number = 0;
  public totalRecords: number = 0;
  public pageSizeOptions: number[] = [10, 15, 25, 50];
  
  // Loading and stats
  public stats: DashboardStats | null = null;
  public displayStats = {
    totalPending: 0,
    forReview: 0,
    urgentTickets: 0,
    delayedTickets: 0,
    newToday: 0
  };
  public activeStatFilter: string | null = null;
  public isLoading = false;
  public pmTicketFilter: 'all' | 'mine' | 'juniors' = 'all';

  // Filters
  public showFilterSection: boolean = false;
  public statusOptions: string[] = ['Open', 'Assigned', 'On Hold', 'Rework', 'Work Done', 'Closed'];
  public priorityOptions: string[] = ['Low', 'Medium', 'High', 'Urgent'];
  public selectedStatus: string | null = null;
  public selectedPriority: string | null = null;
  
  public assigneeOptions: { id: number; name: string; assigneeNumber: string }[] = [];
  public selectedAssignee: string | null = null;

  public allCustomers: any[] = [];
  public myCustomers: any[] = [];
  public activeCustomerTab: 'my' | 'all' = 'my';
  public selectedCustomer: number | null = null;

  // Sorting
  public sortField: string = '';
  public sortDirection: 'asc' | 'desc' = 'asc';

  // Detect screen size
  public isMobile = false;
  
  // Customer list modal properties
  public showCustomerSelect = false;
  public filteredCustomers: any[] = [];
  public customerSearchTerm: string = '';
  public customerListIsLoading = false;

  // Prevent duplicate API calls
  private customersLoaded = false;
  private pmId: number = 0;
  private subscription = new Subscription();

  constructor(
    @Inject(TICKET_SERVICE_TOKEN) private ticketService: TicketService,
    private router: Router,
    private route: ActivatedRoute,
    private authService: AuthService
  ) {
    this.checkScreenSize();
  }

  @HostListener('window:resize', ['$event'])
  onResize(event?: Event) {
    this.checkScreenSize();
  }

  private checkScreenSize() {
    this.isMobile = window.innerWidth <= 768;
    if (this.isMobile) {
      this.sidebarOpen = false;
    }
  }

  ngOnInit(): void {
    const savedState = localStorage.getItem('sidebarCollapsed');
    if (savedState !== null && !this.isMobile) {
      this.sidebarCollapsed = JSON.parse(savedState);
    }
    
    const user = this.authService.getCurrentUser();
    if (user?.userId) {
      this.pmId = Number(user.userId);
      this.loadCurrentUser(this.pmId);
      this.loadStats(this.pmId);
      this.loadStatuses().then(() => {
        this.loadTickets();
      });
      this.loadAssignees();
      this.loadAllCustomers();
    }

    this.subscription.add(
      this.route.queryParamMap.subscribe(queryParams => {
        const openDrawer = queryParams.get('openCreateDrawer');
        if (openDrawer === 'true') {
          this.navigateToSelectCustomer();
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.subscription.unsubscribe();
  }

  private loadCurrentUser(id: number): void {
    try {
      const user = this.authService.getCurrentUser();
      if (user && (Number(user.userId) === id || (user as any).id === id)) {
        this.currentUser = {
          id: Number(user.userId) || id,
          fullName: user.name || 'Staff Member',
          userNumber: user.userNumber || 'STAFF',
          role: user.role,
          email: ''
        } as User;
      } else {
        throw new Error('User ID mismatch or not logged in');
      }
    } catch (error) {
      console.error('Failed to load current user', error);
      this.currentUser = { id: id, fullName: 'Staff Member', userNumber: 'STAFF', role: 'Staff' } as User;
    }
  }

  private normalizeCustomer(c: any): any {
    const rawId = c.id ?? c.Id ?? c.customerId ?? c.CustomerId ?? 0;
    const rawNo = c.userNumber ?? c.UserNumber ?? c.customerCode ?? c.CustomerCode ?? 'N/A';
    const rawName = c.fullName ?? c.FullName ?? c.customerName ?? c.CustomerName ?? 'Unknown Customer';
    return {
      id: Number(rawId),
      userNumber: rawNo,
      fullName: rawName,
      defaultAssigneeId: c.DefaultAssigneeId ?? c.defaultAssigneeId,
      raw: c
    };
  }

  public async loadStats(pmId: number): Promise<void> {
    if (!pmId) return;
    try {
      this.stats = await this.ticketService.getDashboardStats(pmId);
    } catch (error) {
      console.error('Failed to load PM dashboard stats:', error);
    }
  }

  private async loadStatuses(): Promise<void> {
    try {
      const statuses = await this.ticketService.getCustomStatuses();
      if (statuses && statuses.length > 0) {
        this.statusOptions = statuses.map(s => s.statusName || s.StatusName || s);
      }
    } catch (error) {
      console.error('Failed to load dynamic statuses:', error);
      this.statusOptions = ['Open', 'Assigned', 'On Hold', 'Rework', 'Work Done', 'Closed'];
    }
  }

  private async loadAssignees(): Promise<void> {
    try {
      const assignees = await this.ticketService.getAssignees();
      this.assigneeOptions = (assignees || []).map(a => ({
        id: a.id,
        name: `${a.assigneeNumber} - ${a.fullName}`,
        assigneeNumber: a.assigneeNumber
      }));
    } catch (error) {
      console.error('Failed to load assignees:', error);
    }
  }

  // Unified customer loading method
  private async loadAllCustomers(): Promise<void> {
    try {
      const [myCusts, allCusts] = await Promise.all([
        this.ticketService.getMyCustomers().catch(() => []),
        this.ticketService.getUsersByRole('Customer').catch(() => [])
      ]);

      this.myCustomers = (myCusts || []).map((c: any) => this.normalizeCustomer(c));
      this.allCustomers = (allCusts || []).map((c: any) => this.normalizeCustomer(c));
      this.customersLoaded = true;
      this.filterCustomers();
    } catch (error) {
      console.error('Failed to load customers:', error);
    }
  }

  private loadTickets(): void {
    this.isLoading = true;

    const params = { 
        pmId: this.pmId, 
        pageNumber: 1,
        pageSize: 1000,
        status: this.selectedStatus, 
        priority: this.selectedPriority,
        assignedToId: this.selectedAssignee,
        customerId: this.selectedCustomer
    };

    from(this.ticketService.getTicketsForPM(params)).pipe(
        finalize(() => { this.isLoading = false; })
    ).subscribe({
        next: (response) => {
            this.allTickets = response.tickets || [];
            this.updateStatusOptionsWithTicketStatuses();
            this.applyFiltersAndPagination();
            this.loadTicketDeadlines().then(() => {
              this.applyFiltersAndPagination();
            }).catch(() => {});
        },
        error: (error) => {
            console.error('Failed to load tickets:', error);
            this.allTickets = [];
            this.totalRecords = 0;
            this.applyFiltersAndPagination();
        }
    });
  }

  private async loadTicketDeadlines(): Promise<void> {
    if (this.allTickets.length === 0) return;

    const deadlinePromises = this.allTickets.map(async (ticket) => {
      try {
        const deadlineData = await this.ticketService.getTicketDeadline(ticket.id);
        return { ticketId: ticket.id, deadline: deadlineData?.deadlineDate || null };
      } catch (error) {
        return { ticketId: ticket.id, deadline: null };
      }
    });

    try {
      const deadlineResults = await Promise.all(deadlinePromises);
      deadlineResults.forEach(result => {
        const ticket = this.allTickets.find(t => t.id === result.ticketId);
        if (ticket) {
          (ticket as any).deadline = result.deadline;
        }
      });
    } catch (error) {
      console.error('Error loading ticket deadlines:', error);
    }
  }

  public openCreateCustomerForm(): void {
    this.closeMobileSidebar();
    this.router.navigate(['/masterform/customer/new'], { queryParams: { pmId: this.pmId } });
  }

  public openCreateAssigneeForm(): void {
    this.closeMobileSidebar();
    this.router.navigate(['/masterform/assignee/new'], { queryParams: { pmId: this.pmId } });
  }

  public openCustomerList(): void {
    this.closeMobileSidebar();
    this.router.navigate(['/masterform/customers'], { queryParams: { pmId: this.pmId } });
  }

  public openAssigneeList(): void {
    this.closeMobileSidebar();
    this.router.navigate(['/masterform/assignees'], { queryParams: { pmId: this.pmId } });
  }

  public openproductlist(): void {
    this.closeMobileSidebar();
    this.router.navigate(['/masterform/products'], { queryParams: { pmId: this.pmId } });
  }

  public openCreateProduct(): void {
    this.closeMobileSidebar();
    this.router.navigate(['/masterform/product/new'], { queryParams: { pmId: this.pmId } });
  }

  public openPmList(): void {
    this.closeMobileSidebar();
    this.router.navigate(['/masterform/pms'], { queryParams: { pmId: this.pmId } });
  }

  public openCreatePmForm(): void {
    this.closeMobileSidebar();
    this.router.navigate(['/masterform/pm/new'], { queryParams: { pmId: this.pmId } });
  }

  public openPerformanceDashboard(): void {
    this.closeMobileSidebar();
    this.router.navigate(['/pm/performance']);
  }

  private closeMobileSidebar(): void {
    if (this.isMobile) {
      this.sidebarOpen = false;
    }
  }

  private updateStatusOptionsWithTicketStatuses(): void {
    const existingStatuses = new Set(this.statusOptions);
    this.allTickets.forEach(ticket => {
      if (ticket.status && !existingStatuses.has(ticket.status)) {
        this.statusOptions.push(ticket.status);
        existingStatuses.add(ticket.status);
      }
    });
  }

  public setTicketOwnershipFilter(filter: 'all' | 'mine' | 'juniors'): void {
    this.pmTicketFilter = filter;
    this.currentPage = 1;
    this.applyFiltersAndPagination();
  }

  private applyFiltersAndPagination(): void {
    // 1. First, filter by ownership to find the base set of tickets
    let ownershipTickets = [...this.allTickets];

    if (this.pmTicketFilter === 'mine') {
      ownershipTickets = ownershipTickets.filter(ticket => 
        ticket.assignedToNumber === this.currentUser?.userNumber
      );
    } else if (this.pmTicketFilter === 'juniors') {
      ownershipTickets = ownershipTickets.filter(ticket => 
        ticket.assignedToNumber !== this.currentUser?.userNumber && ticket.assignedToNumber != null
      );
    }

    // 2. Compute dynamic stats on the ownership-filtered subset
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const now = new Date();

    const totalPendingCount = ownershipTickets.filter(ticket => {
      const statusLower = ticket.status ? ticket.status.toLowerCase() : '';
      return !['closed', 'resolved'].includes(statusLower);
    }).length;

    const urgentCount = ownershipTickets.filter(ticket => 
      ticket.priority === 'Urgent'
    ).length;

    const delayedCount = ownershipTickets.filter(ticket => {
      const statusLower = ticket.status ? ticket.status.toLowerCase() : '';
      if (['closed', 'resolved', 'on hold'].includes(statusLower)) {
        return false;
      }
      const deadlineStr = (ticket as any).deadline || ticket.deadline;
      if (!deadlineStr) return false;
      const deadlineDate = new Date(deadlineStr);
      return deadlineDate < now;
    }).length;

    const newTodayCount = ownershipTickets.filter(ticket => {
      if (!ticket.createdOn) return false;
      const ticketDate = new Date(ticket.createdOn);
      ticketDate.setHours(0, 0, 0, 0);
      return ticketDate.getTime() === today.getTime();
    }).length;

    this.displayStats = {
      totalPending: totalPendingCount,
      forReview: 0,
      urgentTickets: urgentCount,
      delayedTickets: delayedCount,
      newToday: newTodayCount
    };

    // 3. Now apply additional status presets and filters to the displayed tickets
    let processingTickets = [...ownershipTickets];

    if (this.activeStatFilter) {
      processingTickets = processingTickets.filter(ticket => {
        const ticketStatusLower = ticket.status ? ticket.status.toLowerCase() : '';
        switch (this.activeStatFilter) {
          case 'pending':
            return !['closed', 'resolved'].includes(ticketStatusLower);
          case 'urgent':
            return ticket.priority === 'Urgent';
          case 'new':
            if (!ticket.createdOn) return false;
            const ticketDate = new Date(ticket.createdOn);
            ticketDate.setHours(0, 0, 0, 0);
            return ticketDate.getTime() === today.getTime();
          case 'delayed':
            if (['closed', 'resolved', 'on hold'].includes(ticketStatusLower)) {
              return false;
            }
            const deadlineStr = (ticket as any).deadline || ticket.deadline;
            if (!deadlineStr) return false;
            const deadlineDate = new Date(deadlineStr);
            return deadlineDate < now;
          default:
            return true;
        }
      });
    }

    if (this.sortField) {
      processingTickets.sort((a, b) => {
        const aVal = this.getFieldValue(a, this.sortField);
        const bVal = this.getFieldValue(b, this.sortField);
        let comparison = 0;
        if (aVal < bVal) comparison = -1;
        else if (aVal > bVal) comparison = 1;
        return this.sortDirection === 'desc' ? comparison * -1 : comparison;
      });
    }

    // Assign full filtered list for Board (Kanban) view
    this.filteredTickets = processingTickets;
    this.totalRecords = processingTickets.length;
    this.totalPages = Math.ceil(this.totalRecords / this.pageSize);
    
    // Slice only for Flat List view table pagination
    const startIndex = (this.currentPage - 1) * this.pageSize;
    const endIndex = startIndex + this.pageSize;
    this.paginatedTickets = processingTickets.slice(startIndex, endIndex);
    
    this.ticketsSubject.next(this.paginatedTickets);
  }

  private getFieldValue(obj: any, field: string): any {
    return field.split('.').reduce((o: any, key: string) => o && o[key], obj) || '';
  }

  public onTicketClick(ticket: Ticket): void {
    this.closeMobileSidebar();
    if (ticket.id) {
      this.router.navigate([this.authService.getTicketDetailRoute(ticket.id)]);
    }
  }

  public onFilterChange(): void {
    this.activeStatFilter = null;
    this.currentPage = 1;
    this.loadTickets();
  }

  public onStatCardClick(statType: 'pending' | 'review' | 'urgent' | 'new' | 'delayed'): void {
    if (this.activeStatFilter === statType) {
      this.activeStatFilter = null;
    } else {
      this.activeStatFilter = statType;
    }
    this.selectedStatus = null;
    this.selectedPriority = null;
    this.selectedCustomer = null;
    this.selectedAssignee = null;
    this.currentPage = 1;
    this.loadTickets();
  }

  public clearAllFilters(): void {
    this.selectedStatus = null;
    this.selectedPriority = null;
    this.selectedAssignee = null;
    this.selectedCustomer = null;
    this.activeStatFilter = null;
    this.currentPage = 1;
    this.loadTickets();
  }

  public toggleFilterSection(): void {
    this.showFilterSection = !this.showFilterSection;
  }

  public getActiveFiltersCount(): number {
    let count = 0;
    if (this.selectedStatus) count++;
    if (this.selectedPriority) count++;
    if (this.selectedAssignee !== null) count++;
    if (this.selectedCustomer !== null) count++;
    if (this.activeStatFilter) count++;
    return count;
  }
  
  public onSort(field: string): void {
    if (this.sortField === field) {
      this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortField = field;
      this.sortDirection = 'asc';
    }
    this.applyFiltersAndPagination();
  }

  public getSortIcon(field: string): string {
    if (this.sortField !== field) return 'up-down';
    return this.sortDirection === 'asc' ? 'up' : 'down';
  }

  public onPageChange(page: number): void {
    if (page >= 1 && page <= this.totalPages) {
      this.currentPage = page;
      this.applyFiltersAndPagination();
    }
  }

  public onPageSizeChange(): void {
    this.currentPage = 1;
    this.applyFiltersAndPagination();
  }

  public refresh(): void {
    this.loadTickets();
    this.loadStats(this.pmId);
  }

  public getPageNumbers(): number[] {
    const pages = [];
    const maxVisible = this.isMobile ? 3 : 5;
    let start = Math.max(1, this.currentPage - Math.floor(maxVisible / 2));
    let end = Math.min(this.totalPages, start + maxVisible - 1);
    
    if (end - start + 1 < maxVisible) {
      start = Math.max(1, end - maxVisible + 1);
    }
    
    for (let i = start; i <= end; i++) {
      pages.push(i);
    }
    return pages;
  }

  public getEndRecord(): number {
    const end = this.getStartRecord() + this.paginatedTickets.length - 1;
    return this.totalRecords === 0 ? 0 : end;
  }

  public getStartRecord(): number {
    return this.totalRecords === 0 ? 0 : (this.currentPage - 1) * this.pageSize + 1;
  }
  
  public getAssigneeInitials(ticket: any): string {
    const name = ticket.assignedToName || (ticket.assignedTo != null ? String(ticket.assignedTo) : '');
    return name ? name.substring(0, 2).toUpperCase() : '?';
  }

  public getAssigneeShortName(ticket: any): string {
    const name = ticket.assignedToName || (ticket.assignedTo != null ? String(ticket.assignedTo) : '');
    if (!name) return '—';
    return name.length > 10 ? name.substring(0, 10) + '…' : name;
  }

  public formatDate(dateString: string): string {
    if (!dateString) return '';
    return new Date(dateString).toLocaleDateString();
  }

  public getStatusClass(status: string): string {
    const statusMap: { [key: string]: string } = {
      'New': 'status-new', 'In Progress': 'status-progress',
      'Pending': 'status-pending', 'Resolved': 'status-resolved', 'Closed': 'status-closed',
      'On Hold': 'status-hold', 'Assigned': 'status-assigned'
    };
    return statusMap[status] || 'status-default';
  }

  public getPriorityClass(priority: string): string {
    const priorityMap: { [key: string]: string } = {
      'Low': 'priority-low', 'Medium': 'priority-medium',
      'High': 'priority-high', 'Urgent': 'priority-urgent'
    };
    return priorityMap[priority] || 'priority-default';
  }

  public getTicketAgeClass(dateString: string | null, status?: string, lastUpdatedOn?: string | null): string {
    if (!dateString) return '';

    const now = new Date();
    const createdDate = new Date(dateString);
    const differenceInMs = now.getTime() - createdDate.getTime();
    const daysElapsed = Math.floor(differenceInMs / (1000 * 60 * 60 * 24));

    const classes: string[] = [];

    if (status && status.toLowerCase() === 'closed') {
      classes.push('age-closed');
    } else {
      if (daysElapsed >= 15) {
        classes.push('age-critical', 'blink-aggressive');
      } else if (daysElapsed >= 7) {
        classes.push('age-high', 'blink-subtle');
      } else if (daysElapsed >= 3) {
        classes.push('age-medium');
      } else {
        classes.push('age-new');
      }
    }
    return classes.join(' ');
  }

  public getTimeElapsed(dateString?: string | Date | null, isClosed: boolean = false): string {
    if (!dateString) return '';

    const now = new Date().getTime();
    const eventDate = new Date(dateString).getTime();
    const seconds = Math.floor((now - eventDate) / 1000);

    let interval = seconds / 31536000;
    if (interval > 1) return Math.floor(interval) + " year(s) ago";
    interval = seconds / 2592000;
    if (interval > 1) return Math.floor(interval) + " month(s) ago";
    interval = seconds / 86400;
    if (interval > 1) return Math.floor(interval) + " day(s) ago";
    interval = seconds / 3600;
    if (interval > 1) return Math.floor(interval) + " hour(s) ago";
    interval = seconds / 60;
    if (interval > 1) return Math.floor(interval) + " minute(s) ago";
    return "just now";
  }

  public formatDeadline(deadlineDate: string | null | undefined): string {
    if (!deadlineDate) return 'No deadline';
    
    const deadline = new Date(deadlineDate);
    const now = new Date();
    now.setHours(0, 0, 0, 0);
    deadline.setHours(0, 0, 0, 0);
    
    const diffTime = deadline.getTime() - now.getTime();
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
    
    if (diffDays < 0) {
      return `Overdue by ${Math.abs(diffDays)}d`;
    } else if (diffDays === 0) {
      return 'Due today';
    } else if (diffDays === 1) {
      return 'Due tomorrow';
    } else {
      return `${diffDays}d remaining`;
    }
  }

  public getDeadlineClass(deadlineDate: string | null | undefined): string {
    if (!deadlineDate) return 'deadline-none';
    
    const deadline = new Date(deadlineDate);
    const now = new Date();
    now.setHours(0, 0, 0, 0);
    deadline.setHours(0, 0, 0, 0);
    
    const diffTime = deadline.getTime() - now.getTime();
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
    
    if (diffDays < 0) return 'deadline-overdue';
    if (diffDays <= 1) return 'deadline-urgent';
    if (diffDays <= 3) return 'deadline-soon';
    return 'deadline-normal';
  }

  public getFilterSummary(): string {
    const filters = [];
    if (this.selectedStatus) {
      filters.push(`Status: ${this.selectedStatus}`);
    }
    if (this.selectedPriority) {
      filters.push(`Priority: ${this.selectedPriority}`);
    }
    if (this.selectedAssignee !== null) {
      const assignee = this.assigneeOptions.find(a => a.assigneeNumber === this.selectedAssignee);
      if (assignee) {
        filters.push(`Assignee: ${assignee.name}`);
      }
    }
    if (this.selectedCustomer !== null) {
      const customer = this.allCustomers.find(c => c.id === this.selectedCustomer);
      if (customer) {
        filters.push(`Customer: ${customer.fullName}`);
      }
    }
    if (this.activeStatFilter) {
      const filterName = this.activeStatFilter.charAt(0).toUpperCase() + this.activeStatFilter.slice(1);
      filters.push(`Preset: ${filterName}`);
    }
    return filters.length > 0 ? filters.join(' | ') : 'All Tickets';
  }

  public navigateToSelectCustomer(): void {
    this.closeMobileSidebar();
    this.showCustomerSelect = true;
    this.customerListIsLoading = true;

    // Always ensure data is loaded and filtered list is ready
    this.loadAllCustomers().then(() => {
      this.customerListIsLoading = false;
    }).catch(() => {
      this.customerListIsLoading = false;
    });
  }

  public closeCustomerSelect(): void {
    this.showCustomerSelect = false;
    this.customerSearchTerm = '';
    this.filteredCustomers = [...this.allCustomers];
    
    // Clear the query parameter so clicking 'New Issue' works again without reloading
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { openCreateDrawer: null },
      queryParamsHandling: 'merge'
    });
  }

  public switchCustomerTab(tab: 'my' | 'all'): void {
    this.activeCustomerTab = tab;
    this.filterCustomers();
  }

  public filterCustomers(): void {
    const dataset = this.activeCustomerTab === 'my' ? this.myCustomers : this.allCustomers;
    if (!this.customerSearchTerm) {
      this.filteredCustomers = [...dataset];
      return;
    }
    const lowerTerm = this.customerSearchTerm.toLowerCase();
    
    this.filteredCustomers = dataset.filter(cust =>
      (cust.fullName?.toLowerCase().includes(lowerTerm)) ||
      (cust.userNumber?.toLowerCase().includes(lowerTerm))
    );
  }

  /** Reloads the entire page when the Refresh button is clicked */
  public refreshDashboard(): void {
    window.location.reload();   // <-- forces a full page reload
  }

  public selectCustomer(customer: User): void { 
    this.showCustomerSelect = false; 
    this.customerSearchTerm = '';     
    
    // Clear query param so it doesn't reopen if they click 'Back' in browser
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { openCreateDrawer: null },
      queryParamsHandling: 'merge'
    }).then(() => {
      this.router.navigate(
        ['/customer/ticket-create'], 
        { queryParams: { pmId: this.pmId, customerId: customer.id } }
      );
    });
  }

  // --- PLANE KANBAN HELPER PROPERTIES & METHODS ---
  public viewMode: 'list' | 'board' = 'board';

  public setViewMode(mode: 'list' | 'board'): void {
    this.viewMode = mode;
  }

  public getTicketsByStatus(status: string): Ticket[] {
    // If a status filter is selected, only show that column or tickets matching it
    if (this.selectedStatus && this.selectedStatus.toLowerCase() !== status.toLowerCase()) {
      return [];
    }
    return this.filteredTickets.filter(t => (t.status || '').trim().toLowerCase() === status.trim().toLowerCase());
  }

  public getStatusDotClass(status: string): string {
    const s = (status || '').toLowerCase().trim();
    if (s === 'open' || s === 'new') return 'bg-blue-500';
    if (s === 'in progress' || s === 'progress') return 'bg-amber-500';
    if (s === 'work done' || s === 'resolved') return 'bg-emerald-500';
    if (s === 'rework') return 'bg-red-500';
    if (s === 'closed') return 'bg-gray-500';
    if (s === 'on hold' || s === 'hold') return 'bg-amber-700';
    if (s === 'assigned') return 'bg-indigo-500';
    if (s === 'pending') return 'bg-cyan-500';
    return 'bg-purple-500';
  }

  public getOrderedStatusOptions(): string[] {
    const statusSet = new Set<string>();
    this.statusOptions.forEach(s => statusSet.add(s));
    this.filteredTickets.forEach(t => {
      if (t.status && t.status.trim()) {
        statusSet.add(t.status.trim());
      }
    });

    let list = Array.from(statusSet);
    if (this.selectedStatus) {
      return list.filter(s => s.toLowerCase() === this.selectedStatus!.toLowerCase());
    }
    const openIndex = list.findIndex(s => s.toLowerCase() === 'open');
    let openValue = 'Open';
    if (openIndex !== -1) {
      openValue = list[openIndex];
      list.splice(openIndex, 1);
    }
    const counts: { [status: string]: number } = {};
    list.forEach(status => {
      counts[status] = this.filteredTickets.filter(t => (t.status || '').trim().toLowerCase() === status.toLowerCase()).length;
    });
    list.sort((a, b) => (counts[b] || 0) - (counts[a] || 0));
    return [openValue, ...list];
  }

  // --- GLOBAL SEARCH STATE & METHODS ---
  public globalSearchQuery: string = '';
  public globalSearchType: 'ticket' | 'docket' | 'auto' = 'ticket';
  public globalSearchResults: any[] = [];
  public showGlobalSearchModal: boolean = false;
  public isGlobalSearching: boolean = false;
  public lastSearchedQuery: string = '';

  public async performGlobalSearch(): Promise<void> {
    if (!this.globalSearchQuery || !this.globalSearchQuery.trim()) return;

    const query = this.globalSearchQuery.trim();
    this.lastSearchedQuery = query;
    this.isGlobalSearching = true;

    try {
      const results = await this.ticketService.searchGlobalTicket(query, this.globalSearchType);
      this.globalSearchResults = results || [];

      if (this.globalSearchResults.length === 0) {
        alert(`No tickets found matching Ticket/Docket #: "${query}"`);
      } else {
        // ALWAYS open the popup overlay modal when results are found
        this.showGlobalSearchModal = true;
      }
    } catch (error) {
      console.error('Failed to perform global ticket search:', error);
      alert(`Search failed for query "${query}". Please check your input and try again.`);
    } finally {
      this.isGlobalSearching = false;
    }
  }

  public navigateToSearchedTicket(ticket: any): void {
    this.showGlobalSearchModal = false;
    const ticketId = ticket.id || ticket.ticketId || ticket.Id || ticket.TicketId;
    if (!ticketId) return;

    const routeUrl = this.authService.getTicketDetailRoute(Number(ticketId));
    this.router.navigate([routeUrl]);
  }

  public getStatusBadgeClass(status: string): string {
    const map: { [k: string]: string } = {
      'Open': 'bg-blue-500/10 text-blue-400 border-blue-500/30',
      'In Progress': 'bg-amber-500/10 text-amber-400 border-amber-500/30',
      'Work Done': 'bg-emerald-500/10 text-emerald-400 border-emerald-500/30',
      'Rework': 'bg-red-500/10 text-red-400 border-red-500/30',
      'Closed': 'bg-gray-500/10 text-gray-400 border-gray-500/30'
    };
    return map[status] || 'bg-brand-primary/10 text-brand-primary border-brand-primary/30';
  }

  public getPriorityBadgeClass(priority: string): string {
    const map: { [k: string]: string } = {
      'Urgent': 'bg-red-500/20 text-red-400 border border-red-500/30',
      'High': 'bg-orange-500/20 text-orange-400 border border-orange-500/30',
      'Medium': 'bg-amber-500/20 text-amber-400 border border-amber-500/30',
      'Low': 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30'
    };
    return map[priority] || 'bg-gray-500/20 text-gray-400 border border-gray-500/30';
  }
}
