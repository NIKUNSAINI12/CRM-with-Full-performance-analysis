import { Inject, Injectable } from '@angular/core';
import { API_BASE_URL_TOKEN } from '../injection-tokens';
import { Ticket, TicketUpdateRequest,Product,Customer, User } from '../models/ticket.model';
import { ProductData } from '../../customer/ticket-create/ticket-create';
import { firstValueFrom, Observable } from 'rxjs';
import { HttpClient, HttpParams } from '@angular/common/http';
import { HttpErrorResponse } from '@angular/common/http';

// Service interface
export interface TatDashboardStats {
  totalActiveTickets: number;
  openTickets: number;
  inProgressTickets: number;
  closedTickets: number;
  slaCompliancePercent: number;
  averageResolutionTime: number;
  averageSatisfactionScore: number;
  ratingSummary: any[];
  categoryTrend: any[];
  customerStats: any[];
  recentTickets: any[];
}
export interface TatTicket { [key: string]: any; }

export interface TicketService {
  getTicketReworkStats(ticketId: number): Promise<TicketReworkInfo[]>;
  getCommunicationAttachment(communicationId: number, attachmentId: string): Promise<Blob>;

  // ADD THIS NEW METHOD (it replaces the old timeline methods)
  getTicketTimelineAnalysis(ticketId: number): Promise<TicketTimelineAnalysis>;
  getAllTickets(): Promise<Ticket[]>;
  getTicketsByStatus(status: string): Promise<Ticket[]>;
  getAssigneePerformanceStats(
    month?: number | null,
    year?: number | null
  ): Promise<AssigneePerformanceStat[]>;
  getPerformanceOverview(filters?: any): Promise<any>;
  getLocationPerformance(locationId: number): Promise<any>;
  getCustomerPerformance(customerId: number): Promise<any>;
  getUserPerformance(userId: number): Promise<any>;
  getTeamPerformance(managerId: number): Promise<any>;
  exportTickets(format: string): Promise<Blob>;
  createUser(userData: any): Promise<any>;
  createAssignee(assigneeData: { fullName: string; email: string; }): Promise<any>;
  updatePm(id: number, userData: any): Promise<void>;
  createTicket(formData: FormData): Promise<any>;
  getTicketById(id: number): Promise<Ticket>;
  updateTicketAsPm(ticketId: number, pmId: number, payload: PmTicketUpdatePayload): Promise<void>;
  getProductHierarchys(): Promise<ProductData[]>;
  createProduct(product: ProductData): Promise<any>;
  updateProduct(id: number, product: ProductData): Promise<void>;
  getTicketsForPM(filterOptions?: any): Promise<TicketResponse>;
 getDashboardStats(pmId: number): Promise<DashboardStats>
  getTicketAttachment(ticketId: number, attachmentId: string): Promise<Blob>;
  getTicketDetails(ticketId: number): Promise<Ticket>;
  getTicketDetails(ticketId: number): Promise<Ticket>;
  getAssignees(): Promise<Assignee[]>;
  getProducts(): Promise<Product[]>;
  updateTicket(ticketId: number, payload: TicketUpdateRequest): Promise<Ticket>;
  getCustomerById(id: number): Promise<Customer>;
  getTicketsForDeveloper(developerId: string | number,filters?: any)
: Promise<TicketResponse>; // Changed to match the response type
  findCustomerByNumber(customerNumber: string): Promise<{ customerId: number, customerName: string }>;
    getDeveloperDashboardStats(developerId: number): Promise<DashboardStats>; 
    updateTicketStatusAsDeveloper(ticketId: number, status: string, note?: string): Promise<void>;
    getProductHierarchy(customerId: number): Promise<ProductData[]>;
   submitTicketForReview(ticketId: number, developerId: number): Promise<void>;
   getTicketHistory(ticketId: number): Promise<TicketHistory[]>;
   getAssigneeById(id: number): Promise<Assignee>;
    getExecutiveByCustomerId(customerId: number): Promise<Assignee>;
   updateAssignee(id: number, assigneeData: any): Promise<void>;
   getDelayedTicketsForDeveloper(params: { developerId: number, pageNumber: number, pageSize: number }): Promise<TicketResponse>;
  getUsersByRole(role: string): Promise<User[]>;
  getMyCustomers(): Promise<any[]>;
  getUserById(id: number): Promise<User>;
   updateUser(id: number, userData: any): Promise<void>;
  deleteUser(id: number): Promise<void>;
  getUserProducts(userId: number): Promise<number[]>;
  getTicketDeadline(ticketId: number): Promise<TicketDeadline | null>;
    setTicketDeadline(ticketId: number, payload: SetDeadlinePayload): Promise<void>;
     getDelayedTickets(params: { pageNumber: number, pageSize: number }): Promise<TicketResponse>;
     getTicketReworkStats(ticketId: number): Promise<TicketReworkInfo[]>;
     addCommunication(ticketId: number, formData: FormData): Observable<TicketCommunicationDto>;
      getCommunications(ticketId: number): Observable<PmCommunicationResponseDto>;
  // --- Parent-Child Relations ---
  getTicketRelations(ticketId: number): Promise<TicketRelationsResponse>;
  setTicketParent(childTicketId: number, parentTicketId: number | null, pmId: number): Promise<void>;
  
  // --- Custom Masters ---
  getIssueCategories(): Promise<any[]>;
  createIssueCategory(categoryName: any): Promise<any>;
  deleteIssueCategory(id: number): Promise<void>;
  getTicketSources(): Promise<any[]>;
  addTicketSource(sourceName: string): Promise<any>;
  deleteTicketSource(id: number): Promise<void>;
  getCustomStatuses(): Promise<any[]>;
  createCustomStatus(status: any): Promise<any>;
  updateCustomStatus(id: number, status: any): Promise<void>;
  deleteCustomStatus(id: number): Promise<void>;
  getCustomDeveloperStatuses(): Promise<any[]>;
  createCustomDeveloperStatus(status: any): Promise<any>;
  deleteCustomDeveloperStatus(id: number): Promise<void>;
  getPriorities(): Promise<any[]>;
  createPriority(priority: any): Promise<any>;
  deletePriority(id: number): Promise<void>; 
  getTatDashboardStats(
    pmId: number,
    relationship?: string,
    fromDate?: string,
    toDate?: string,
    status?: string
  ): Promise<TatDashboardStats>;
  getDockets(search?: string, customerId?: number): Promise<any[]>;
  getDocketByNo(docketNo: string): Promise<any>;
  searchGlobalTicket(query: string, searchType?: string): Promise<any[]>;
  bulkUploadUsers(roleKey: string, file: File): Promise<any>;
  getUserPreferences(userId: number, userRole: string): Promise<any>;
  saveUserPreferences(payload: any): Promise<void>;
}

export interface CommunicationAttachmentDto {
  id: number;
  fileName: string;
  fileUrl: string;
}

// Parent-Child Linking
export interface LinkedTicketSummary {
  id: number;
  ticketNumber: string;
  subject: string;
  status: string;
  priority: string;
  difficulty: string;
  createdOn: string;
  assignedToName: string | null;
  parentId?: number | null;
}
export interface TicketRelationsResponse {
  parent: LinkedTicketSummary | null;
  children: LinkedTicketSummary[];
  tree?: LinkedTicketSummary[];
}


export interface TicketCommunicationDto {
  id: number;
  commentText: string;
  postedByUserId?: number;
  postedByName: string;
  postedByUserRole: string;
  postedOn: string; // Keep as string or Date
  attachments: CommunicationAttachmentDto[];
  channel: 'Customer' | 'Developer' | 'Customer(Note)' | 'Developer(Note)' | 'GlobalNotes' | 'GlobalNote' | string;
}

export interface PmCommunicationResponseDto {
  updateNotesDeveloper: TicketCommunicationDto[];
  updateNotesCustomer: TicketCommunicationDto[];
  customerChannel: TicketCommunicationDto[];
  developerChannel: TicketCommunicationDto[];
  updateNotes: TicketCommunicationDto[];
  globalNotes?: TicketCommunicationDto[];
}

export interface GroupedTicketResponse {
  groupedTickets: GroupedTicket[];
  totalTicketCount: number;
}
export interface ProductDatas {
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
export interface TicketDeadline {
  ticketId: number;
  deadlineDate: string; // The backend sends this as an ISO date string
  setByManagerId: number;
  setOn: string;
}
export interface TicketReworkInfo {
  userId: number;
  userName: string;
  userRole: string;
  reworkCount: number;
}
export interface SetDeadlinePayload {
  deadlineDate: string;
  managerId: number;
}
export interface SubModuleData {
  id: string;
  name: string;
  moduleId: string;
}
export interface AssigneePerformanceStat {
  assigneeId: number;
  assigneeName: string;

  // Raw counts (for display)
  totalTicketsCount: number;
  totalTicketsOpen: number;
  totalPendingPmReview: number;
  totalOnHold: number;
  totalTicketsClosed: number;
  totalStayedClosed: number;
  totalReworks: number;
  totalOverdue: number;
  totalWorkDone: number;

  // Difficulty-weighted points (Expert=5, Hard=3, Medium=2, Easy=1)
  weightedAssigned: number;
  weightedClosed: number;
  weightedStayedClosed: number;
  weightedReworks: number;
  weightedOverdue: number;
  weightedOpen: number;
  weightedPmReview: number;
  weightedWorkDone: number;

  // Difficulty insight
  avgDifficultyWeight: number;    // 1.0=Easy only … 5.0=Expert only

  // Pre-computed rates (0-100, difficulty-weighted for fairness)
  closureRate: number;            // stayedClosed / assigned × 100
  reworkRate: number;             // reworks / assigned × 100
  deadlineMissRate: number;       // overdue / assigned × 100
  clearanceRate: number;          // closed / (closed+open+pmReview) × 100

  // Legacy computed field
  totalTicketsPending: number;    // = open + pmReview + onHold
}

export interface TicketHistory {
  historyId: number;
  changeDate: string;
  eventDescription: string;
  changedByUserRole: string;
  changedByUserName: string;
}
export interface Assignee {
  [x: string]: any;
  id: number;
  fullName: string;
  assigneeNumber:string;
  email:string;
  mobileNo?: string;
  managerId?: number;
  // Add other relevant fields like email, role, etc.
}

export interface GroupedTicket {
  customerName: string;
  customerId: number;
  tickets: Ticket[];
}
// Add this new interface to your ticket.service.ts file

export interface PmTicketUpdatePayload {
  status?: string;
  priority?: string;
  difficulty?: string;      // Easy | Medium | Hard | Expert
  assignedToId?: number | null;
  note?: string;
  deadlineDate?: string | null;
}
export interface TicketSummary {
  id: number;
  subject: string;
  status: string;
  priority: string;
  assignedToName: string | null;
  createdOn: string;
}

export interface TicketListResponse {
  tickets: Ticket[];
  totalCount: number;
}

export interface TicketResponse {
  tickets: Ticket[];
  totalCount: number;
  groupedTickets?: GroupedTicket[];
}

export interface DashboardStats {
  forReview: number;
  totalPending: number;
  urgentTickets: number;
  newToday: number;
  delayedTickets: number;
}
export interface TicketStateDuration {
  stateOrAssigneeName: string;
  totalDurationMinutes: number;
}

export interface TicketTimelineAnalysis {
  summary: TicketTimelineMetrics;
  breakdown: TicketStateDuration[];
}
// --- END OF NEW INTERFACES ---

export interface TicketTimelineMetrics {
  ticketCreatedDate: string | null;
  ticketClosedDate: string | null;
  timeToFirstResponseMinutes: number | null;
  timeToResolutionHours: number | null;
}

// Default implementation
@Injectable()
export class DefaultTicketService implements TicketService {
  private apiUrl: string;

  constructor(
    @Inject(API_BASE_URL_TOKEN) private apiBaseUrl: string,
    private http: HttpClient
  ) {
    this.apiUrl = this.apiBaseUrl;
  }

  async getAllTickets(): Promise<Ticket[]> {
    try {
      const response = await fetch(`${this.apiBaseUrl}/Tickets/customer/1`);
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }
      return await response.json();
    } catch (error) {
      console.error('Failed to fetch tickets:', error);
      return [];
    }
  }
  async getUsersByRole(role: string): Promise<User[]> {
    // This calls your backend to get a list of users (e.g., all "Customer" or all "PM")
    return await firstValueFrom(
      this.http.get<User[]>(`${this.apiUrl}/Manager/users/role/${role}`)
    );
  }
  async getMyCustomers(): Promise<any[]> {
    return await firstValueFrom(
      this.http.get<any[]>(`${this.apiUrl}/Manager/my-customers`)
    );
  }
  async getAssigneePerformanceStats(
  month?: number | null,
  year?: number | null
): Promise<AssigneePerformanceStat[]> {
  let url = `${this.apiUrl}/Manager/performance/assignees/all`;
  let params = new HttpParams();

  // Only add if valid and not null/undefined
  if (month !== null && month !== undefined && month >= 1 && month <= 12) {
    params = params.set('month', month.toString());
  }
  if (year !== null && year !== undefined && year >= 2000 && year <= 9999) {
    params = params.set('year', year.toString());
  }

  try {
    return await firstValueFrom(
      this.http.get<AssigneePerformanceStat[]>(url, { params })  // Pass params correctly
    );
  } catch (error) {
    console.error('Failed to fetch assignee performance stats:', error);
    return [];
  }
}

  async getPerformanceOverview(filters?: any): Promise<any> {
    let params = new HttpParams();
    if (filters) {
      if (filters.fromDate) params = params.set('fromDate', filters.fromDate);
      if (filters.toDate) params = params.set('toDate', filters.toDate);
      if (filters.locationId) params = params.set('locationId', filters.locationId.toString());
      if (filters.status) params = params.set('status', filters.status);
      if (filters.priority) params = params.set('priority', filters.priority);
    }
    return await firstValueFrom(this.http.get<any>(`${this.apiUrl}/PerformanceDashboard/overview`, { params }));
  }

  async getLocationPerformance(locationId: number): Promise<any> {
    const url = `${this.apiUrl}/PerformanceDashboard/location/${locationId}`;
    return await firstValueFrom(this.http.get<any>(url));
  }

  async getCustomerPerformance(customerId: number): Promise<any> {
    const url = `${this.apiUrl}/PerformanceDashboard/customer/${customerId}`;
    return await firstValueFrom(this.http.get<any>(url));
  }

  async getUserPerformance(userId: number): Promise<any> {
    const url = `${this.apiUrl}/PerformanceDashboard/user/${userId}`;
    return await firstValueFrom(this.http.get<any>(url));
  }

  async getTeamPerformance(managerId: number): Promise<any> {
    const url = `${this.apiUrl}/PerformanceDashboard/team/${managerId}`;
    return await firstValueFrom(this.http.get<any>(url));
  }
  // Fetches a communication attachment as a Blob
  getCommunicationAttachment(communicationId: number, attachmentId: string): Promise<Blob> {
    const url = `${this.apiUrl}/manager/communications/${communicationId}/attachments/${attachmentId}`;
    // Use firstValueFrom to return a Promise<Blob>
    return firstValueFrom(
        this.http.get(url, { responseType: 'blob' })
    );
  }
  async getTicketTimelineAnalysis(ticketId: number): Promise<TicketTimelineAnalysis> {
    try {
      const url = `${this.apiUrl}/manager/tickets/${ticketId}/timeline-analysis`;
      const response = await firstValueFrom(this.http.get<TicketTimelineAnalysis>(url));
      return response;
    } catch (error) {
      console.error('Failed to fetch ticket timeline analysis:', error);
      // Return a default empty object on error to prevent crashes
      return {
        summary: {
          ticketCreatedDate: null,
          ticketClosedDate: null,
          timeToFirstResponseMinutes: null,
          timeToResolutionHours: null
        },
        breakdown: []
      };
    }
  }
  async updatePm(id: number, userData: any): Promise<void> {
    // This now calls your new, dedicated endpoint for updating PMs
    const url = `${this.apiUrl}/Manager/pms/${id}`;
    await firstValueFrom(this.http.put(url, userData));
  }
  async getUserById(id: number): Promise<User> {
    // This gets the details for a single user to pre-fill the edit form
    return await firstValueFrom(
      this.http.get<User>(`${this.apiUrl}/Manager/users/${id}`)
    );
  }
  public async getDelayedTicketsForDeveloper(params: { developerId: number, pageNumber: number, pageSize: number }): Promise<TicketResponse> {
        const httpParams = new HttpParams()
            .set('pageNumber', params.pageNumber.toString())
            .set('pageSize', params.pageSize.toString());
        
        const url = `${this.apiUrl}/manager/developer/${params.developerId}/tickets/delayed`;
        const response = await this.http.get<TicketResponse>(url, { params: httpParams }).toPromise();
        return response ?? { tickets: [], totalCount: 0 };
    }
  public async getDelayedTickets(params: { pageNumber: number, pageSize: number }): Promise<TicketResponse> {
    const httpParams = new HttpParams()
        .set('pageNumber', params.pageNumber.toString())
        .set('pageSize', params.pageSize.toString());
    
    // Assumes the PM dashboard component is making the call
    const response = await this.http.get<TicketResponse>(`${this.apiUrl}/manager/tickets/delayed`, { params: httpParams }).toPromise();
    return response ?? { tickets: [], totalCount: 0 };
}
async findCustomerByNumber(customerNumber: string): Promise<{ customerId: number, customerName: string }> {
        const params = new HttpParams().set('customerNumber', customerNumber);
        return await firstValueFrom(
            this.http.get<{ customerId: number, customerName: string }>(`${this.apiUrl}/Manager/customers/find`, { params })
        );
    }
async getProductHierarchys(): Promise<ProductData[]> {
    return await firstValueFrom(this.http.get<ProductData[]>(`${this.apiUrl}/Manager/products/hierarchy`));
  }
  async createProduct(product: ProductData): Promise<any> {
    return await firstValueFrom(this.http.post<any>(`${this.apiUrl}/Manager/products/simple`, product));
  }
  async updateProduct(id: number, product: ProductData): Promise<void> {
    // Ensure the product object contains the correct ID
    product.id = id.toString(); 

    // Use POST and the exact route string defined in the controller
    await firstValueFrom(this.http.post(`${this.apiUrl}/Manager/products/createupdate`, product));
}
  async updateUser(id: number, userData: any): Promise<void> {
    // This sends the updated data from the edit form to your backend
    await firstValueFrom(
      this.http.put(`${this.apiUrl}/Manager/users/${id}`, userData)
    );
  }

  async deleteUser(id: number): Promise<void> {
    await firstValueFrom(
      this.http.delete<void>(`${this.apiUrl}/Manager/users/${id}`)
    );
  }

  async getTicketReworkStats(ticketId: number): Promise<TicketReworkInfo[]> {
    try {
      const url = `${this.apiUrl}/manager/tickets/${ticketId}/rework-stats`;
      const response = await firstValueFrom(this.http.get<TicketReworkInfo[]>(url));
      return response || [];
    } catch (error) {
      console.error('Failed to fetch ticket rework stats:', error);
      return []; // Return an empty array on error
    }
  }
  async getUserProducts(userId: number): Promise<number[]> {
    // This gets the list of product IDs assigned to a specific customer
    return await firstValueFrom(
        this.http.get<number[]>(`${this.apiUrl}/Manager/users/${userId}/products`)
    );
  }
 public async getTicketDeadline(ticketId: number): Promise<TicketDeadline | null> {
  try {
    const response = await this.http.get<TicketDeadline>(`${this.apiUrl}/manager/tickets/${ticketId}/deadline`).toPromise();
    
    // 2. FIX: Handle the case where the response might be undefined.
    // The '??' operator returns the right side if the left side is null or undefined.
    return response ?? null;

  } catch (error) {
    // 3. FIX: Check if the error is an HttpErrorResponse before accessing its properties.
    if (error instanceof HttpErrorResponse && error.status === 404) {
      // If the server returns a 404 (Not Found), it means no deadline is set.
      return null;
    }
    
    // For all other errors, log them and re-throw.
    console.error('Failed to get ticket deadline:', error);
    throw error;
  }
}
public async setTicketDeadline(ticketId: number, payload: SetDeadlinePayload): Promise<void> {
  // This will call: PUT /api/manager/tickets/{ticketId}/deadline
  await this.http.put(`${this.apiUrl}/manager/tickets/${ticketId}/deadline`, payload).toPromise();
}

  async createUser(userData: any): Promise<any> {
    // Calls the endpoint in your ManagerController for Customers and PMs
    return await firstValueFrom(
      this.http.post<any>(`${this.apiUrl}/Manager/users`, userData)
    );
  }

  async createAssignee(assigneeData: { fullName: string; email: string; }): Promise<any> {
    // Calls the endpoint in your ManagerController for Assignees
    return await firstValueFrom(
      this.http.post<any>(`${this.apiUrl}/Manager/assignees`, assigneeData)
    );
  }
  async getAssigneeById(id: number): Promise<Assignee> {
    return await firstValueFrom(
      this.http.get<Assignee>(`${this.apiUrl}/Manager/assignees/${id}`)
    );
  }

  async getExecutiveByCustomerId(customerId: number): Promise<Assignee> {
    return await firstValueFrom(
      this.http.get<Assignee>(`${this.apiUrl}/Tickets/customer/${customerId}/executive`)
    );
  }

  async updateAssignee(id: number, assigneeData: any): Promise<void> {
    await firstValueFrom(
      this.http.put(`${this.apiUrl}/Manager/assignees/${id}`, assigneeData)
    );
  }
  async getDeveloperDashboardStats(developerId: number): Promise<DashboardStats> {
    try {
      return await firstValueFrom(
        this.http.get<DashboardStats>(`${this.apiBaseUrl}/manager/dashboard-stats/developer/${developerId}`)
      );
    } catch (error) {
      console.error('Failed to fetch developer dashboard stats:', error);
      // Return default stats on error, matching the developer's needs (no 'forReview')
      return { forReview: 0, totalPending: 0, urgentTickets: 0, newToday: 0 ,delayedTickets: 0};
    }
  }
  // Update the signature in the TicketService interface
updateTicketStatusAsDeveloper(ticketId: number, status: string, note?: string): Promise<void>;

// Update the implementation in the DefaultTicketService class
async updateTicketStatusAsDeveloper(ticketId: number, status: string, note?: string): Promise<void> {
  const payload = { Status: status, Note: note };
  await firstValueFrom(
    this.http.put(`${this.apiUrl}/manager/tickets/${ticketId}/status`, payload)
  );
}
 async getTicketHistory(ticketId: number): Promise<TicketHistory[]> {
        try {
            // This assumes you have a backend endpoint at GET /api/tickets/{id}/history
            return await firstValueFrom(
                this.http.get<TicketHistory[]>(`${this.apiUrl}/tickets/${ticketId}/history`)
            );
        } catch (error) {
            console.error('Failed to fetch ticket history:', error);
            return [];
        }
    }
async getDashboardStats(pmId: number): Promise<DashboardStats> {
    try {
      // The URL now includes the pmId to fetch stats for a specific manager
      const url = `${this.apiBaseUrl}/Manager/dashboard-stats/manager`;
      
      const response = await firstValueFrom(
        this.http.get<DashboardStats>(url)
      );
      
      return {
        forReview: response.forReview || 0,
        totalPending: response.totalPending || 0,
        urgentTickets: response.urgentTickets || 0,
        newToday: response.newToday || 0,
        delayedTickets:  response.delayedTickets || 0
      };
    } catch (error) {
      console.error('Failed to fetch dashboard stats:', error);
      return {
        forReview: 0,
        totalPending: 0,
        urgentTickets: 0,
        newToday: 0,
        delayedTickets:0
      };
    }
  }
  async getProducts(): Promise<Product[]> {
  try {
    // This now calls the correct, simpler endpoint for a flat list of products
    return await firstValueFrom(
      this.http.get<Product[]>(`${this.apiUrl}/Manager/products`)
    );
  } catch (error) {
    console.error('Failed to fetch products:', error);
    return [];
  }}
  getTicketsForDeveloper(
  developerId: string | number,
  extraFilters?: { dateFrom?: string; dateTo?: string }
): Promise<TicketResponse> {
  const filterOptions: any = {
    assignedToId: developerId,
    pageNumber: 1,
    pageSize: 500
  };

  // add date filters if provided
  if (extraFilters?.dateFrom) filterOptions.dateFrom = extraFilters.dateFrom;
  if (extraFilters?.dateTo) filterOptions.dateTo = extraFilters.dateTo;

  return this.getTicketsForPM(filterOptions);
}

  async submitTicketForReview(ticketId: number, developerId: number): Promise<void> {
  // This URL now correctly points to the endpoint in your DeveloperController
  const url = `${this.apiUrl}/manager/developer/${developerId}/tickets/${ticketId}/submit-for-review`;
  
  console.log('Submitting for review to URL:', url); // For debugging
  
  await firstValueFrom(
    this.http.put(url, {})
  );
}
  async getCustomerById(id: number): Promise<Customer> {
    const stored = sessionStorage.getItem('auth_user');
    if (stored) {
      try {
        const user = JSON.parse(stored);
        const storedId = Number(user.customerId || user.userId);
        if (storedId === id && user.name) {
          return { id: id, name: user.name };
        }
      } catch { /* ignore */ }
    }

    try {
      const customer = await firstValueFrom(
        this.http.get<Customer>(`${this.apiBaseUrl}/Customers/${id}`)
      );
      return customer;
    } catch (error) {
      console.error(`Failed to fetch customer with ID ${id}:`, error);
      return { id: id, name: 'Unknown Customer' };
    }
  }
  async getAssignees(): Promise<Assignee[]> {
    try {
      // This endpoint gets the list of users who can be assigned a ticket.
      const assignees = await firstValueFrom(
        this.http.get<Assignee[]>(`${this.apiBaseUrl}/Manager/assignees`)
      );
      return assignees;
    } catch (error) {
      console.error('Failed to fetch assignees:', error);
      return []; // Return an empty array on failure
    }
  }
async getProductHierarchy(customerId: number): Promise<ProductData[]> {
        try {
            // It now calls your new, specific endpoint
            return await firstValueFrom(
                this.http.get<ProductData[]>(`${this.apiBaseUrl}/Tickets/customer/${customerId}/products`)
            );
        } catch (error) {
            console.error('Failed to fetch product hierarchy for customer:', error);
            return [];
        }
    }
  async updateTicket(ticketId: number, payload: TicketUpdateRequest): Promise<Ticket> {
    try {
      // This endpoint updates the ticket details.
      const updatedTicket = await firstValueFrom(
        this.http.put<Ticket>(`${this.apiBaseUrl}/Tickets/${ticketId}`, payload)
      );
      return updatedTicket;
    } catch (error) {
      console.error(`Failed to update ticket ${ticketId}:`, error);
      throw error; // Re-throw the error to be handled by the component
    }
  }
// In your TicketService interface


// In your DefaultTicketService class
async updateTicketAsPm(ticketId: number, pmId: number, payload: PmTicketUpdatePayload): Promise<void> {
  // CORRECTED: This URL now correctly points to the dedicated PM update endpoint
  const url = `${this.apiUrl}/Tickets/${pmId}/tickets/${ticketId}`;
  
  // No changes are needed to the payload or the http call itself
  await firstValueFrom(this.http.put(url, payload));
}
  async getTicketsForPM(filterOptions: any = {}): Promise<TicketResponse> {
    let params = new HttpParams();

    // Set default pagination if not provided
    if (!filterOptions.pageNumber) {
      filterOptions.pageNumber = 1;
    }
    if (!filterOptions.pageSize) {
      filterOptions.pageSize = 100000;
    }

    // Add filter parameters if provided
    for (const key in filterOptions) {
      if (filterOptions[key] !== null && filterOptions[key] !== undefined && filterOptions[key] !== '') {
        params = params.append(key, filterOptions[key].toString());
      }
    }

    console.log('Calling API with params:', params.toString());
    console.log('Full URL:', `${this.apiBaseUrl}/Manager/tickets?${params.toString()}`);

    try {
      const response = await firstValueFrom(
        this.http.get<any>(`${this.apiBaseUrl}/Manager/tickets`, { params })
      );

      console.log('API Response:', response);

      // Handle different response formats
      if (response && typeof response === 'object') {
        // Check if response has tickets property
        if (response.tickets && Array.isArray(response.tickets)) {
          console.log('Found tickets array in response:', response.tickets.length);
          return {
            tickets: response.tickets,
            totalCount: response.totalCount || response.tickets.length
          };
        }
        // Check if response has groupedTickets property
        else if (response.groupedTickets && Array.isArray(response.groupedTickets)) {
          console.log('Found grouped tickets in response:', response.groupedTickets.length);
          const flatTickets = this.flattenGroupedTickets(response.groupedTickets);
          return {
            tickets: flatTickets,
            totalCount: response.totalTicketCount || flatTickets.length,
            groupedTickets: response.groupedTickets
          };
        }
        // Check if response is directly an array
        else if (Array.isArray(response)) {
          console.log('Response is direct array:', response.length);
          return {
            tickets: response,
            totalCount: response.length
          };
        }
        // Check if response has data property (common API pattern)
        else if (response.data && Array.isArray(response.data)) {
          console.log('Found data array in response:', response.data.length);
          return {
            tickets: response.data,
            totalCount: response.total || response.count || response.data.length
          };
        }
        // Check if response has results property
        else if (response.results && Array.isArray(response.results)) {
          console.log('Found results array in response:', response.results.length);
          return {
            tickets: response.results,
            totalCount: response.totalCount || response.total || response.results.length
          };
        }
        // Check if response has items property
        else if (response.items && Array.isArray(response.items)) {
          console.log('Found items array in response:', response.items.length);
          return {
            tickets: response.items,
            totalCount: response.totalCount || response.total || response.items.length
          };
        }
        else {
          console.warn('Unknown response format:', Object.keys(response));
          return {
            tickets: [],
            totalCount: 0
          };
        }
      }
      // If response is directly an array
      else if (Array.isArray(response)) {
        console.log('Direct array response:', response.length);
        return {
          tickets: response,
          totalCount: response.length
        };
      }
      else {
        console.warn('Unexpected response type:', typeof response, response);
        return {
          tickets: [],
          totalCount: 0
        };
      }
    } catch (error) {
      console.error('Failed to fetch tickets for Project Manager:', error);
      if (error instanceof Error) {
        console.error('Error message:', error.message);
      }
      throw error;
    }
  }

  private flattenGroupedTickets(groupedTickets: GroupedTicket[]): Ticket[] {
    const flatTickets: Ticket[] = [];
    
    groupedTickets.forEach(group => {
      if (group.tickets && Array.isArray(group.tickets)) {
        group.tickets.forEach(ticket => {
          flatTickets.push({
            ...ticket,
            customerName: group.customerName,
            customerId: group.customerId
          });
        });
      }
    });
    
    return flatTickets;
  }

  async getTicketById(id: number): Promise<Ticket> {
    try {
      const ticket = await firstValueFrom(
        this.http.get<Ticket>(`${this.apiBaseUrl}/Tickets/${id}`)
      );
      return ticket;
    } catch (error) {
      console.error(`Failed to fetch ticket with ID ${id}:`, error);
      throw error;
    }
  }
  getCommunications(ticketId: number): Observable<PmCommunicationResponseDto> {
    const url = `${this.apiUrl}/manager/tickets/${ticketId}/communications`; // Use manager route
    return this.http.get<PmCommunicationResponseDto>(url);
  }

  // Adds a new comment and optional files
  addCommunication(ticketId: number, formData: FormData): Observable<TicketCommunicationDto> {
    const url = `${this.apiUrl}/manager/tickets/${ticketId}/communications`; // Use manager route
    // Don't set Content-Type header manually for FormData, browser does it better
    return this.http.post<TicketCommunicationDto>(url, formData);
  }

  // ADD THIS NEW METHOD - This was missing and causing the TypeScript error
  async getTicketDetails(ticketId: number): Promise<Ticket> {
    try {
      console.log(`Fetching detailed ticket information for ticket ID: ${ticketId}`);
      
      const ticket = await firstValueFrom(
        this.http.get<Ticket>(`${this.apiBaseUrl}/Tickets/${ticketId}/details`)
      );
      
      console.log(`Successfully loaded ticket details for ID ${ticketId}:`, ticket);
      return ticket;
    } catch (error) {
      console.error(`Failed to fetch detailed ticket information for ID ${ticketId}:`, error);
      
      // Fallback to getTicketById if the details endpoint doesn't exist
      try {
        console.log(`Falling back to getTicketById for ticket ${ticketId}`);
        return await this.getTicketById(ticketId);
      } catch (fallbackError) {
        console.error(`Fallback also failed for ticket ${ticketId}:`, fallbackError);
        throw fallbackError;
      }
    }
  }

  async getTicketsByStatus(status: string): Promise<Ticket[]> {
    try {
      const allTickets = await this.getAllTickets();
      if (status === 'All') {
        return allTickets;
      }
      return allTickets.filter(t => t.status === status);
    } catch (error) {
      console.error('Failed to filter tickets by status:', error);
      return [];
    }
  }

  async getTicketAttachment(ticketId: number, attachmentId: string): Promise<Blob> {
    try {
      const response = await firstValueFrom(
        this.http.get(`${this.apiBaseUrl}/Tickets/${ticketId}/attachments/${attachmentId}`, {
          responseType: 'blob'
        })
      );
      return response;
    } catch (error) {
      console.error(`Failed to fetch attachment ${attachmentId} for ticket ${ticketId}:`, error);
      throw error;
    }
  }

  async exportTickets(format: string): Promise<Blob> {
    console.warn('Server export not implemented, falling back to client-side export');
    try {
      const tickets = await this.getAllTickets();
      return this.createClientSideExport(tickets, format);
    } catch (error) {
      console.error('Failed to export tickets:', error);
      throw error;
    }
  }

  

  async createTicket(formData: FormData): Promise<any> {
    try {
      const newTicket = await firstValueFrom(
        this.http.post<any>(`${this.apiBaseUrl}/Tickets`, formData)
      );
      return newTicket;
    } catch (error) {
      console.error('Failed to create ticket:', error);
      throw error;
    }
  }

  private createClientSideExport(data: any[], format: string): Blob {
    try {
      if (format.toLowerCase() === 'csv') {
        return new Blob([this.convertToCSV(data)], { type: 'text/csv' });
      }
      if (format.toLowerCase() === 'json') {
        return new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
      }
      throw new Error(`Unsupported export format: ${format}`);
    } catch (error) {
      console.error('Failed to create client-side export:', error);
      throw error;
    }
  }
  

  private convertToCSV(data: any[]): string {
    if (!data?.length) return '';
    
    try {
      const headers = Object.keys(data[0]);
      const csvRows = [
        headers.join(','),
        ...data.map(row =>
          headers.map(header => {
            const value = row[header];
            // Handle null/undefined values
            if (value === null || value === undefined) return '';
            // Handle strings with commas or quotes
            const stringValue = value.toString();
            return stringValue.includes(',') || stringValue.includes('"')
              ? `"${stringValue.replace(/"/g, '""')}"`
              : stringValue;
          }).join(',')
        )
      ];
      return csvRows.join('\n');
    } catch (error) {
      console.error('Failed to convert data to CSV:', error);
      throw error;
    }
  }

  // ── Parent-Child Relations ─────────────────────────────────────
  async getTicketRelations(ticketId: number): Promise<TicketRelationsResponse> {
    const url = `${this.apiUrl}/manager/tickets/${ticketId}/relations`;
    return firstValueFrom(this.http.get<TicketRelationsResponse>(url));
  }

  async setTicketParent(childTicketId: number, parentTicketId: number | null, pmId: number): Promise<void> {
    const url = `${this.apiUrl}/manager/tickets/${childTicketId}/parent`;
    await firstValueFrom(this.http.put(url, { parentTicketId, pmId }));
  }

  // ── CUSTOM MASTERS (Phase 7) ──────────────────────────────────────
  async getIssueCategories(): Promise<any[]> {
    const url = `${this.apiUrl}/Manager/categories`;
    return firstValueFrom(this.http.get<any[]>(url));
  }

  async createIssueCategory(payload: any): Promise<any> {
    const url = `${this.apiUrl}/Manager/categories`;
    return firstValueFrom(this.http.post<any>(url, payload));
  }

  async deleteIssueCategory(id: number): Promise<void> {
    const url = `${this.apiUrl}/Manager/categories/${id}`;
    await firstValueFrom(this.http.delete<void>(url));
  }

  async getTicketSources(): Promise<any[]> {
    const url = `${this.apiUrl}/Manager/ticket-sources`;
    return firstValueFrom(this.http.get<any[]>(url));
  }

  async addTicketSource(sourceName: string): Promise<any> {
    const url = `${this.apiUrl}/Manager/ticket-sources`;
    return firstValueFrom(this.http.post<any>(url, { sourceName }));
  }

  async deleteTicketSource(id: number): Promise<void> {
    const url = `${this.apiUrl}/Manager/ticket-sources/${id}`;
    await firstValueFrom(this.http.delete<void>(url));
  }

  async getCustomStatuses(): Promise<any[]> {
    return await firstValueFrom(this.http.get<any[]>(`${this.apiUrl}/manager/statuses`));
  }
  
  async createCustomStatus(status: any): Promise<any> {
    return await firstValueFrom(this.http.post<any>(`${this.apiUrl}/manager/statuses`, status));
  }
  
  async updateCustomStatus(id: number, status: any): Promise<void> {
    await firstValueFrom(this.http.put<void>(`${this.apiUrl}/manager/statuses/${id}`, status));
  }
  
  async deleteCustomStatus(id: number): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`${this.apiUrl}/manager/statuses/${id}`));
  }

  async getCustomDeveloperStatuses(): Promise<any[]> {
    return await firstValueFrom(this.http.get<any[]>(`${this.apiUrl}/manager/developer-statuses`));
  }
  
  async createCustomDeveloperStatus(status: any): Promise<any> {
    return await firstValueFrom(this.http.post<any>(`${this.apiUrl}/manager/developer-statuses`, status));
  }
  
  async deleteCustomDeveloperStatus(id: number): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`${this.apiUrl}/manager/developer-statuses/${id}`));
  }

  async getPriorities(): Promise<any[]> {
    return await firstValueFrom(this.http.get<any[]>(`${this.apiUrl}/manager/priorities`));
  }
  
  async createPriority(priority: any): Promise<any> {
    return await firstValueFrom(this.http.post<any>(`${this.apiUrl}/manager/priorities`, priority));
  }
  
  async deletePriority(id: number): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`${this.apiUrl}/manager/priorities/${id}`));
  }

  async getTatDashboardStats(
    pmId: number,
    relationship?: string,
    fromDate?: string,
    toDate?: string,
    status?: string
  ): Promise<TatDashboardStats> {
    let params = new HttpParams();
    if (relationship) params = params.set('relationship', relationship);
    if (fromDate) params = params.set('fromDate', fromDate);
    if (toDate) params = params.set('toDate', toDate);
    if (status) params = params.set('status', status);

    return firstValueFrom(this.http.get<TatDashboardStats>(`${this.apiUrl}/Manager/dashboard-stats/tat/${pmId}`, { params }));
  }

  async getDockets(search?: string, customerId?: number): Promise<any[]> {
    let params = new HttpParams();
    if (search) {
      params = params.set('search', search);
    }
    if (customerId) {
      params = params.set('customerId', customerId.toString());
    }
    return firstValueFrom(this.http.get<any[]>(`${this.apiUrl}/Tickets/dockets`, { params }));
  }

  async getDocketByNo(docketNo: string): Promise<any> {
    return firstValueFrom(this.http.get<any>(`${this.apiUrl}/Tickets/dockets/${docketNo}`));
  }

  async searchGlobalTicket(query: string, searchType: string = 'auto'): Promise<any[]> {
    if (!query || !query.trim()) return [];
    const params = new HttpParams()
      .set('query', query.trim())
      .set('searchType', searchType);
    return await firstValueFrom(
      this.http.get<any[]>(`${this.apiUrl}/Tickets/search-global`, { params })
    );
  }

  async bulkUploadUsers(roleKey: string, file: File): Promise<any> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('roleKey', roleKey);
    return firstValueFrom(this.http.post<any>(`${this.apiUrl}/manager/users/bulk-upload`, formData));
  }

  async getUserPreferences(userId: number, userRole: string): Promise<any> {
    return await firstValueFrom(
      this.http.get<any>(`${this.apiUrl}/UserPreferences/${userId}/${userRole}`)
    );
  }

  async saveUserPreferences(payload: any): Promise<void> {
    await firstValueFrom(
      this.http.post<void>(`${this.apiUrl}/UserPreferences`, payload)
    );
  }
}