export interface Ticket {
  assignedToNumber: string | null;

  id: number;
  subject: string;
  description?: string;
  status: string;
  priority: string;

  assignedToName?: string | null;
  assignedTo?: number | string | null;
  customerName?: string;
  customerId?: number;
  customerEmail?: string;
  createdOn: string;
  updatedOn?: string;
  resolvedOn?: string | null;
  closedOn?: string | null;
  category?: string;
  subCategory?: string;
  product?: string;
  version?: string;
  environment?: string;
  severity?: string;
  impact?: string;
  urgency?: string;
  lastRepliedOn?: string;
  tags?: string[];
  ticketNumber?: string;
  comments?: TicketComment[];
  workflowState?: string;
  estimatedResolutionTime?: string | null;
  actualResolutionTime?: string | null;
  escalationLevel?: number;
  isEscalated?: boolean;
  lastActivityOn?: string;
  createdBy?: string;
  createdById?: number;
  createdByUserId?: number;
  createdByName?: string;
  createdByRole?: string;
  deadline?: string | null;
  hasAttachment?: boolean;
  attachmentCount?: number;
  attachments?: TicketAttachment[];
  docketNumber?: string | null;
  sourcePhone?: string | null;
  sourceEmail?: string | null;
  ticketSource?: string | null;
  rating?: number;
  lastUpdatedNote?: string | null;
  closingRemark?: string | null;
}
export interface User {
  id: number;
  userNumber: string;
  fullName: string;
  email: string;
  role: string;
  contactPerson?: string;
  phoneNo?: string;
  mobileNo?: string;
  defaultAssigneeId?: number;
  defaultAssigneeName?: string; // enriched on frontend
}

export interface TicketAttachment {
  id: number;
  fileName: string;
  fileSize: number;
  contentType: string;
  uploadedOn: string;
  uploadedBy: string;
  filePath: string;
}

export interface TicketComment {
  id: number;
  content: string;
  createdOn: string;
  createdBy: string;
  createdById: number;
  isInternal?: boolean;
  commentType?: 'Note' | 'Resolution' | 'Escalation' | 'StatusChange';
}

export interface TicketFilter {
  status?: string;
  priority?: string;
  assignedTo?: number;
  customer?: number;
  category?: string;
  dateFrom?: string;
  dateTo?: string;
  searchTerm?: string;
  pageNumber?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
}

export interface TicketCreateRequest {
  subject: string;
  description: string;
  priority: string;
  category?: string;
  subCategory?: string;
  product?: string;
  version?: string;
  environment?: string;
  customerEmail: string;
  attachments?: File[];
  tags?: string[];
}

export interface TicketUpdateRequest {
  subject?: string;
  description?: string;
  status?: string;
  priority?: string;
  assignedToId?: number | null;
  category?: string;
  subCategory?: string;
  product?: string;
  version?: string;
  environment?: string;
  tags?: string[];
  workflowState?: string;
}
export interface Customer {
  id: number;
  name: string; // Or 'fullName', match what your customer API sends
  // Add any other properties like email, company, etc.
}
// ADD THIS INTERFACE
export interface Assignee {
  id: number;
  fullName?: string;
  email?: string;
  mobileNo?: string;
  assigneeNumber?: string;
  managerId?: number;
  role?: string;
}

export interface Product {
  id: number | string;
  name: string;
  // Add any other properties your API provides
}
export interface TicketStatusHistory {
  id: number;
  previousStatus: string;
  newStatus: string;
  changedOn: string;
  changedBy: string;
  changedById: number;
  comments?: string;
}

export interface TicketWorklog {
  id: number;
  description: string;
  timeSpent: number; // in minutes
  dateWorked: string;
  createdBy: string;
  createdById: number;
  workType?: 'Investigation' | 'Development' | 'Testing' | 'Documentation' | 'Communication';
}

// Enums for better type safety
export enum TicketStatus {
  NEW = 'New',
  IN_PROGRESS = 'In Progress',
  PENDING_PM_REVIEW = 'Pending PM Review',
  PENDING = 'Pending',
  RESOLVED = 'Resolved',
  CLOSED = 'Closed',
  REOPENED = 'Reopened',
  CANCELLED = 'Cancelled'
}

export enum TicketPriority {
  LOW = 'Low',
  MEDIUM = 'Medium',
  HIGH = 'High',
  URGENT = 'Urgent'
}

export enum TicketSeverity {
  MINOR = 'Minor',
  NORMAL = 'Normal',
  MAJOR = 'Major',
  CRITICAL = 'Critical',
  BLOCKER = 'Blocker'
}

export enum TicketImpact {
  LOW = 'Low',
  MEDIUM = 'Medium',
  HIGH = 'High',
  CRITICAL = 'Critical'
}

// Helper functions
export class TicketHelpers {
  static getStatusColor(status: string): string {
    const colorMap: { [key: string]: string } = {
      [TicketStatus.NEW]: '#3b82f6',
      [TicketStatus.IN_PROGRESS]: '#f59e0b',
      [TicketStatus.PENDING_PM_REVIEW]: '#8b5cf6',
      [TicketStatus.PENDING]: '#f97316',
      [TicketStatus.RESOLVED]: '#10b981',
      [TicketStatus.CLOSED]: '#6b7280',
      [TicketStatus.REOPENED]: '#ef4444',
      [TicketStatus.CANCELLED]: '#ef4444'
    };
    return colorMap[status] || '#6b7280';
  }

  static getPriorityColor(priority: string): string {
    const colorMap: { [key: string]: string } = {
      [TicketPriority.LOW]: '#10b981',
      [TicketPriority.MEDIUM]: '#f59e0b',
      [TicketPriority.HIGH]: '#f97316',
      [TicketPriority.URGENT]: '#ef4444'
    };
    return colorMap[priority] || '#6b7280';
  }

  static isTicketOverdue(ticket: Ticket): boolean {
    if (!ticket.estimatedResolutionTime) return false;
    const now = new Date();
    const estimatedTime = new Date(ticket.estimatedResolutionTime);
    return now > estimatedTime && !ticket.resolvedOn && !ticket.closedOn;
  }

  static getTicketAge(ticket: Ticket): number {
    const created = new Date(ticket.createdOn);
    const now = new Date();
    return Math.floor((now.getTime() - created.getTime()) / (1000 * 60 * 60 * 24));
  }

  static formatTicketId(id: number): string {
    return `TKT-${id.toString().padStart(6, '0')}`;
  }

  static getStatusDisplayName(status: string): string {
    const displayMap: { [key: string]: string } = {
      [TicketStatus.PENDING_PM_REVIEW]: 'PM Review',
      [TicketStatus.IN_PROGRESS]: 'In Progress'
    };
    return displayMap[status] || status;
  }
}