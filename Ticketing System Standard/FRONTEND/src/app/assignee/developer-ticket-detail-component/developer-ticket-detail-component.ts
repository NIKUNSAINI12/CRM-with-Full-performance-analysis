import { Component, OnInit, Inject } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { API_BASE_URL } from '../../app';
import { ActivatedRoute, Router } from '@angular/router';

// ADDED: Imports for the new note form
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators, FormsModule } from '@angular/forms';
import { firstValueFrom, switchMap, tap } from 'rxjs';
import { TICKET_SERVICE_TOKEN } from '../../Core/injection-tokens';
import { AuthService } from '../../Core/services/auth';

// MODIFIED: Import all necessary DTOs
import {
  TicketResponse, TicketHistory, TicketDeadline, TicketService,
  PmCommunicationResponseDto, TicketCommunicationDto, CommunicationAttachmentDto,
  TicketRelationsResponse, LinkedTicketSummary
} from '../../Core/services/ticket.service';

import { Product, Ticket, TicketAttachment } from '../../Core/models/ticket.model';

@Component({
  selector: 'app-developer-ticket-detail',
  standalone: true,
  // ADDED: Import ReactiveFormsModule, FormsModule
  imports: [CommonModule, ReactiveFormsModule, FormsModule],
  templateUrl: './developer-ticket-detail-component.html',
  styleUrls: ['./developer-ticket-detail-component.scss']
})
export class DeveloperTicketDetailComponent implements OnInit {
  public ticket: Ticket | null = null;
  public ticketHistory: TicketHistory[] = [];
  public ticketDeadline: TicketDeadline | null = null;
  public isLoading = true;
  public errorMessage: string | null = null;
  public productName: string = 'N/A';
  public customerName: string = 'N/A';
  private developerId: number = 0;

  // --- ADDED: Communication & Note State ---
  public newNoteForm: FormGroup;
  public selectedNoteFiles: File[] = [];
  public isSubmittingNote = false;
  public statusUpdateNote = '';
  public devStatusOptions: string[] = [];
  public selectedDevStatus = '';
  public statusWorkflows: any[] = [];
  public commsErrorMessage: string | null = null;
  public successMessage: string | null = null; // For feedback

  // --- MODIFIED: Combine comms into one list ---
  public developerChannelComments: TicketCommunicationDto[] = []; // Keep for loading
  public updateNotesDeveloper: TicketCommunicationDto[] = [];     // Keep for loading
  public allDeveloperComms: TicketCommunicationDto[] = []; // NEW: Combined & sorted list
  public activeTab: 'developer' | 'timeline' | 'relations' = 'developer';

  get isDeveloper(): boolean {
    return true;
  }

  // ADDED: Current user info for posting notes
  private currentUser = {
    id: 0,
    name: 'Developer', // Using a generic name
    role: 'Developer'
  };

  // ── User-wise breakdown ──
  public timelineAnalysis: any = null;
  public filteredBreakdown: { stateOrAssigneeName: string, totalDurationMinutes: number }[] = [];
  public totalTimelineMinutes: number = 0;
  public userWiseBreakdown: {
    category: 'PM' | 'Assignee' | 'OnHold';
    label: string;
    totalMinutes: number;
    expanded: boolean;
    subItems: { label: string; minutes: number }[];
  }[] = [];

  // ──────────────────────── Parent-Child Chain Relations
  public relations: TicketRelationsResponse = { parent: null, children: [], tree: [] };
  public relationTreeRoot: any[] = [];
  public isLoadingRelations = false;
  public relationsError: string | null = null;

  // Configurable properties for developers
  public permissions: string[] = [];
  public priorityOptions: string[] = ['Low', 'Medium', 'High', 'Urgent'];
  public difficultyOptions: any[] = [
    { id: 'Easy', label: 'Easy' },
    { id: 'Medium', label: 'Medium' },
    { id: 'Hard', label: 'Hard' }
  ];
  public selectedPriority = '';
  public selectedDifficulty = '';
  public selectedDeadline = '';
  public isPropertiesDirty = false;

  get canUpdateDeadline(): boolean {
    return this.permissions.includes('update_ticket_deadline');
  }
  get canUpdatePriority(): boolean {
    return this.permissions.includes('update_ticket_priority');
  }
  get canUpdateDifficulty(): boolean {
    return this.permissions.includes('update_ticket_priority') || this.permissions.includes('update_ticket_details');
  }
  get showPropertiesSave(): boolean {
    return (this.canUpdateDeadline || this.canUpdatePriority || this.canUpdateDifficulty) && this.isPropertiesDirty;
  }



  get closedChildrenCount(): number {
    return this.relations.children.filter(c => c.status === 'Closed' || c.status === 'Close').length;
  }
  get openChildrenCount(): number {
    return this.relations.children.filter(c => c.status !== 'Closed' && c.status !== 'Close').length;
  }
  get childCompletionPct(): number {
    if (!this.relations.children.length) return 0;
    return Math.round((this.closedChildrenCount / this.relations.children.length) * 100);
  }


  constructor(
    @Inject(TICKET_SERVICE_TOKEN) private ticketService: TicketService,
    private route: ActivatedRoute,
    private router: Router,
    private location: Location,
    private fb: FormBuilder, // ADDED: FormBuilder
    private authService: AuthService
  ) {

    // ADDED: Initialize the new note form
    this.newNoteForm = this.fb.group({
      noteText: ['']
    });
  }

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const ticketId = Number(params.get('id'));
      if (ticketId) {
        this.loadTicketDetails(ticketId);
      } else {
        this.errorMessage = 'Ticket ID not found in URL.';
        this.isLoading = false;
      }
    });
  }

  private loadTicketDetails(ticketId: number): void {
    this.isLoading = true;
    this.errorMessage = null;

    const currentUser = this.authService.getCurrentUser();
    this.developerId = currentUser ? Number(currentUser.userId) : 0;
    this.currentUser.id = this.developerId;
    if (currentUser) {
      this.currentUser.name = currentUser.name || 'Developer';
      this.currentUser.role = currentUser.role || 'Developer';
      this.permissions = currentUser.permissions || [];
    }

    Promise.all([
      this.ticketService.getTicketById(ticketId),
      this.loadProducts(),
      this.ticketService.getTicketsForPM({ pageSize: 500 }),
      this.ticketService.getTicketHistory(ticketId),
      this.ticketService.getTicketDeadline(ticketId),
      firstValueFrom(this.ticketService.getCommunications(ticketId)),
      this.ticketService.getTicketTimelineAnalysis(ticketId),
      this.ticketService.getCustomDeveloperStatuses(),
      this.ticketService.getStatusWorkflows().catch(() => [])
    ]).then(([ticketData, productsData, allTicketsResponse, historyData, deadlineData, commsResponse, timelineAnalysisData, devStatusesData, workflowsData]) => {
      this.ticket = ticketData;
      this.setProductName(productsData);
      this.setCustomerNameFromList(allTicketsResponse.tickets, this.ticket?.customerId);
      this.statusWorkflows = workflowsData || [];
      
      let rawStatuses: string[] = [];
      if (devStatusesData && devStatusesData.length > 0) {
        rawStatuses = devStatusesData.filter((s: any) => s.isActive).map((s: any) => s.statusName);
      } else {
        rawStatuses = ['Open', 'In Progress', 'Work Done', 'Pending', 'Resolved', 'Closed', 'On Hold'];
      }

      if (this.ticket) {
        this.selectedDevStatus = this.ticket.status;
        const currentStatus = this.ticket.status || 'Open';
        const userRole = this.currentUser.role || 'Developer';

        // Find matching workflow
        const userWorkflows = this.statusWorkflows.filter((w: any) => {
          const wRole = w.roleName?.toLowerCase();
          const uRole = userRole?.toLowerCase();
          return wRole === uRole ||
                 (wRole === 'developer' && uRole === 'assignee') ||
                 (wRole === 'manager' && (uRole === 'pm' || uRole === 'project manager')) ||
                 (wRole === 'supermanager' && (uRole === 'super admin' || uRole === 'superadmin'));
        });

        const matchedWorkflow = userWorkflows.find((w: any) => 
          w.currentStatus?.toLowerCase() === currentStatus.toLowerCase() || 
          w.currentStatus === '*'
        );

        if (matchedWorkflow) {
          if (matchedWorkflow.isBlocked) {
            this.devStatusOptions = [currentStatus];
          } else {
            const allowedNext = matchedWorkflow.nextStatuses || [];
            this.devStatusOptions = rawStatuses.filter((s: string) => 
              s.toLowerCase() === currentStatus.toLowerCase() ||
              allowedNext.some((n: string) => n.toLowerCase() === s.toLowerCase())
            );
          }
        } else {
          this.devStatusOptions = rawStatuses;
        }
      } else {
        this.devStatusOptions = rawStatuses;
      }

      // Add timezone suffix to history changeDate if missing
      this.ticketHistory = (historyData || []).map(h => {
        if (h.changeDate && !h.changeDate.toString().includes('+') && !h.changeDate.toString().includes('-') && !h.changeDate.toString().endsWith('Z')) {
          h.changeDate = h.changeDate.toString() + '+05:30';
        }
        return h;
      });
      this.ticketDeadline = deadlineData;
      this.selectedPriority = this.ticket?.priority || '';
      this.selectedDifficulty = (this.ticket as any)?.difficulty || 'Medium';
      this.selectedDeadline = deadlineData ? this.formatDateForInput(deadlineData.deadlineDate) || '' : '';
      this.isPropertiesDirty = false;
      this.timelineAnalysis = timelineAnalysisData;

      const appendIstOffset = (comms: any[]) => {
        return comms.map(c => {
          if (c.postedOn && !c.postedOn.toString().includes('+') && !c.postedOn.toString().includes('-') && !c.postedOn.toString().endsWith('Z')) {
            c.postedOn = c.postedOn.toString() + '+05:30';
          }
          return c;
        });
      };

      // Combine all internal communications (global notes, developer comments, and developer update notes)
      const globalNotes = appendIstOffset(commsResponse.globalNotes || []);
      this.developerChannelComments = appendIstOffset(commsResponse.developerChannel || []);
      this.updateNotesDeveloper = appendIstOffset(commsResponse.updateNotesDeveloper || []);

      // Combine and sort the lists
      this.allDeveloperComms = [...globalNotes, ...this.developerChannelComments, ...this.updateNotesDeveloper]
        .sort((a, b) => new Date(a.postedOn).getTime() - new Date(b.postedOn).getTime());

      // Time breakdown processing
      const tAnalysis = this.timelineAnalysis as any;
      let breakdownData = tAnalysis?.breakdown || tAnalysis?.Breakdown;

      const hasValidItems = breakdownData && breakdownData.some((item: any) => {
          const name = item.stateOrAssigneeName || item.StateOrAssigneeName || '';
          const mins = item.totalDurationMinutes || item.TotalDurationMinutes || 0;
          return name.toLowerCase() !== 'closed' && mins > 0;
      });

      if (!hasValidItems && this.ticket) {
          const created = new Date(this.ticket.createdOn);
          const closedDateStr = tAnalysis?.summary?.ticketClosedDate || 
                               tAnalysis?.summary?.TicketClosedDate || 
                               this.ticket.lastRepliedOn;
          const end = (this.ticket.status?.toLowerCase() === 'closed' && closedDateStr)
              ? new Date(closedDateStr)
              : new Date();
          const diffMinutes = Math.max(1, Math.floor((end.getTime() - created.getTime()) / (1000 * 60)));

          const assigneeName = (this.ticket as any).assignedToName;
          const ownerName = assigneeName
              ? `Developer: ${assigneeName}`
              : 'PM';

          breakdownData = [{
              StateOrAssigneeName: ownerName,
              TotalDurationMinutes: diffMinutes
          }];
      }

      if (breakdownData && breakdownData.length > 0) {
          this.filteredBreakdown = breakdownData
              .filter((item: any) => {
                  const name = item.stateOrAssigneeName || item.StateOrAssigneeName || '';
                  return name.toLowerCase() !== 'closed';
              })
              .map((item: any) => ({
                  stateOrAssigneeName: item.stateOrAssigneeName || item.StateOrAssigneeName,
                  totalDurationMinutes: item.totalDurationMinutes || item.TotalDurationMinutes || 0
              }));

          this.totalTimelineMinutes = this.filteredBreakdown.reduce((sum, item) => sum + item.totalDurationMinutes, 0);
          this.buildUserWiseBreakdown(breakdownData);
      } else {
          this.filteredBreakdown = [];
          this.userWiseBreakdown = [];
          this.totalTimelineMinutes = 0;
      }

    }).catch(err => {
      this.errorMessage = 'Failed to load ticket details.';
      console.error(err);
    }).finally(() => {
      this.isLoading = false;
      if (this.ticket) this.loadRelations();
    });
  }

  private async loadProducts(): Promise<Product[]> {
    try {
      return await this.ticketService.getProducts();
    } catch (error) {
      console.error('Failed to load products:', error);
      return [];
    }
  }

  private setProductName(products: Product[]): void {
    if (this.ticket?.product && products.length > 0) {
      const foundProduct = products.find(p => p.id.toString() === this.ticket!.product!.toString());
      this.productName = foundProduct ? foundProduct.name : 'Unknown Product';
    }
  }

  private setCustomerNameFromList(allTickets: Ticket[], customerId?: number): void {
    if (!customerId || !allTickets || allTickets.length === 0) {
      this.customerName = 'Unknown';
      return;
    }
    const foundTicket = allTickets.find(ticket => ticket.customerId === customerId);
    this.customerName = foundTicket?.customerName || 'Unknown';
  }

  // --- MODIFIED: downloadAttachment (to handle BOTH original and comment attachments) ---
  public async downloadAttachment(attachment: TicketAttachment | CommunicationAttachmentDto, commentId?: number): Promise<void> {
    if (!this.ticket) return;

    const isCommunicationAttachment = commentId !== undefined;
    const attachmentId = attachment.id.toString();
    const fileName = attachment.fileName;

    this.showTemporaryMessage(`Preparing download for ${fileName}...`, 'info');

    try {
      let blob: Blob;

      if (isCommunicationAttachment && commentId) {
        // It's an attachment on a note or comment
        blob = await this.ticketService.getCommunicationAttachment(commentId, attachmentId);
      } else if (!isCommunicationAttachment) {
        // It's an original ticket attachment
        blob = await this.ticketService.getTicketAttachment(this.ticket.id, attachmentId);
      } else {
        throw new Error("Cannot download attachment without parent ID.");
      }

      this.triggerBlobDownload(blob, fileName);

    } catch (error: any) {
      console.error("Download error:", error);
      this.showTemporaryMessage(`Failed to download attachment: ${fileName}.`, 'error');
    }
  }

  // ADDED: Helper for triggering the download
  private triggerBlobDownload(blob: Blob, fileName: string): void {
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    window.URL.revokeObjectURL(url);
    a.remove();
    this.showTemporaryMessage(`Download started for ${fileName}.`, 'success');
  }


  // --- ADDED: Lock Logic ---
  get isUpdateLocked(): boolean {
    if (!this.ticket) return true;

    // 1. Check if matching workflow is blocked
    const currentStatus = this.ticket.status || 'Open';
    const userRole = this.currentUser.role || 'Developer';
    const userWorkflows = this.statusWorkflows.filter((w: any) => {
      const wRole = w.roleName?.toLowerCase();
      const uRole = userRole?.toLowerCase();
      return wRole === uRole ||
             (wRole === 'developer' && uRole === 'assignee') ||
             (wRole === 'manager' && (uRole === 'pm' || uRole === 'project manager')) ||
             (wRole === 'supermanager' && (uRole === 'super admin' || uRole === 'superadmin'));
    });

    const matchedWorkflow = userWorkflows.find((w: any) => 
      w.currentStatus?.toLowerCase() === currentStatus.toLowerCase() || 
      w.currentStatus === '*'
    );

    if (matchedWorkflow && matchedWorkflow.isBlocked) {
      return true;
    }

    return false;
  }

  async updateStatusByDeveloper(): Promise<void> {
    if (!this.ticket || !this.selectedDevStatus) return;

    // --- ADDED: Guard Clause ---
    if (this.isUpdateLocked) {
      this.showTemporaryMessage(`Action locked: Status is ${this.ticket.status}`, 'error');
      return;
    }

    const newStatus = this.selectedDevStatus;

    // Check if checkpoints (child tickets) are active if marking work done
    if (newStatus === 'Work Done') {
      const activeChildren = this.relations.children.filter(c => c.status !== 'Closed' && c.status !== 'Close' && c.status !== 'On Hold');
      if (activeChildren.length > 0) {
        const childNums = activeChildren.map(c => c.ticketNumber).join(', ');
        this.showTemporaryMessage(`Cannot set status to Work Done — child ticket(s) ${childNums} (checkpoints) are still active (not Closed or On Hold). Please resolve or hold them first.`, 'error');
        // Revert UI dropdown selection back to current status
        this.selectedDevStatus = this.ticket.status;
        return;
      }
    }

    this.isLoading = true;
    try {
      const noteText = this.statusUpdateNote?.trim() || undefined;
      if (noteText) {
        const formData = new FormData();
        formData.append('CommentText', noteText);
        formData.append('Channel', 'Developer(Note)');
        formData.append('PostedByUserId', this.currentUser.id.toString());
        formData.append('PostedByName', this.currentUser.name);
        formData.append('PostedByUserRole', this.currentUser.role);
        await firstValueFrom(this.ticketService.addCommunication(this.ticket.id, formData));
        this.statusUpdateNote = ''; // clear note
      }

      await this.ticketService.updateTicketStatusAsDeveloper(this.ticket.id, newStatus, noteText);
      this.ticket.status = newStatus;
      this.selectedDevStatus = newStatus;
      this.showTemporaryMessage(`Status updated to ${newStatus}!`, 'success');
      this.loadTicketHistory(this.ticket.id);
      this.loadCommunications(this.ticket.id);
    } catch (error: any) {
      console.error('Failed to update status:', error);
      const errMsg = (typeof error?.error === 'string' && !error.error.trim().startsWith('<')) 
        ? error.error 
        : (error?.error?.error || error?.error?.message || error?.message || 'Failed to update status.');
      this.showTemporaryMessage(errMsg, 'error');
      // Revert UI select option on failure
      this.selectedDevStatus = this.ticket.status;
    } finally {
      this.isLoading = false;
    }
  }

  selectTab(tab: 'developer' | 'timeline' | 'relations'): void {
    this.activeTab = tab;
  }

  goBack(): void {
    this.location.back();
  }

  // --- ADDED: All methods for posting notes ---

  public onNoteFileSelected(event: Event): void {
    const element = event.target as HTMLInputElement;
    if (element.files && element.files.length > 0) {
      const currentFiles = Array.from(element.files);
      currentFiles.forEach(file => {
        if (!this.selectedNoteFiles.some(f => f.name === file.name && f.size === file.size)) {
          this.selectedNoteFiles.push(file);
        }
      });
      element.value = ''; // Clear input to allow re-adding same file
    }
  }

  public removeSelectedNoteFile(index: number): void {
    this.selectedNoteFiles.splice(index, 1);
  }

  public postNote(): void {
    const noteText = this.newNoteForm.value.noteText?.trim();
    if (!this.ticket || this.isSubmittingNote) return;

    if (!noteText && this.selectedNoteFiles.length === 0) {
      this.showTemporaryMessage('Please enter a note or add an attachment.', 'error', 'comms');
      return;
    }

    this.isSubmittingNote = true;
    this.commsErrorMessage = null;

    const formData = new FormData();
    formData.append('CommentText', noteText || '');
    // Hardcoded channel, as developer only posts developer notes
    formData.append('Channel', 'Developer(Note)');
    formData.append('PostedByUserId', this.currentUser.id.toString());
    formData.append('PostedByName', this.currentUser.name);
    formData.append('PostedByUserRole', this.currentUser.role);

    this.selectedNoteFiles.forEach(file => {
      formData.append('files', file, file.name);
    });

    this.ticketService.addCommunication(this.ticket.id, formData).subscribe({
      next: (newNote) => {
        this.newNoteForm.reset({ noteText: '' });
        this.selectedNoteFiles = [];
        this.showTemporaryMessage('Note added successfully!', 'success');
        // Refresh history log as well
        this.loadTicketHistory(this.ticket!.id);
        this.loadCommunications(this.ticket!.id);
      },
      error: (err) => {
        const errMsg = (typeof err?.error === 'string' && !err.error.trim().startsWith('<')) 
          ? err.error 
          : (err?.error?.error || err?.error?.message || err?.message || 'Failed to post note.');
        this.showTemporaryMessage(errMsg, 'error', 'comms');
        console.error('Error posting note:', err);
        this.isSubmittingNote = false; // <-- BUG FIX: Ensure flag is reset on error
      },
      complete: () => {
        this.isSubmittingNote = false;
      }
    });
  }

  // ADDED: Helper to refresh history after a note
  private loadTicketHistory(ticketId: number): void {
    this.ticketService.getTicketHistory(ticketId)
      .then(history => {
        this.ticketHistory = (history || []).map(h => {
          if (h.changeDate && !h.changeDate.toString().includes('+') && !h.changeDate.toString().includes('-') && !h.changeDate.toString().endsWith('Z')) {
            h.changeDate = h.changeDate.toString() + '+05:30';
          }
          return h;
        });
      })
      .catch(err => console.error("Failed to refresh history:", err));
  }

  // ADDED: Helper for showing temporary messages
  private showTemporaryMessage(message: string, type: 'success' | 'error' | 'info', target: 'main' | 'comms' = 'main'): void {
    if (target === 'main') this.commsErrorMessage = null;
    if (target === 'comms') { this.successMessage = null; this.errorMessage = null; }

    if (type === 'success') {
      this.successMessage = message;
      setTimeout(() => { if (this.successMessage === message) this.successMessage = null; }, 4000);
    } else if (type === 'error') {
      if (target === 'comms') {
        this.commsErrorMessage = message;
        // Auto-clear comms error
        setTimeout(() => { if (this.commsErrorMessage === message) this.commsErrorMessage = null; }, 4000);
      } else {
        this.errorMessage = message;
      }
    } else { // info
      this.successMessage = `ℹ️ ${message}`;
      setTimeout(() => { if (this.successMessage === `ℹ️ ${message}`) this.successMessage = null; }, 3000);
    }
  }

  // --- ADDED: Helper functions for new HTML logic ---

  /** Checks if a communication item is a Note */
  public isNote(comm: TicketCommunicationDto): boolean {
    return comm.channel.includes('(Note)');
  }

  /** Checks if a communication item is a Comment */
  public isComment(comm: TicketCommunicationDto): boolean {
    return !comm.channel.includes('(Note)');
  }

  // ───────────────────────── Ticket Chain Navigation
  async loadRelations(): Promise<void> {
    if (!this.ticket) return;
    this.isLoadingRelations = true;
    this.relationsError = null;
    try {
      this.relations = await this.ticketService.getTicketRelations(this.ticket.id);
      this.buildRelationTree(this.relations.tree || []);
    } catch (e) {
      this.relationsError = 'Could not load ticket relations.';
    } finally {
      this.isLoadingRelations = false;
    }
  }

  public buildRelationTree(flatList: LinkedTicketSummary[]): void {
    if (!flatList || flatList.length === 0) {
      this.relationTreeRoot = [];
      return;
    }

    const map = new Map<number, { ticket: LinkedTicketSummary; children: any[]; isCurrent: boolean }>();
    flatList.forEach(t => {
      map.set(t.id, { 
        ticket: t, 
        children: [], 
        isCurrent: this.ticket ? t.id === this.ticket.id : false 
      });
    });

    const roots: any[] = [];
    flatList.forEach(t => {
      const node = map.get(t.id)!;
      if (t.parentId && map.has(t.parentId)) {
        map.get(t.parentId)!.children.push(node);
      } else {
        roots.push(node);
      }
    });

    this.relationTreeRoot = roots;
  }

  navigateToRelated(ticketId: number): void {
    this.router.navigate(['/developer/ticket', ticketId]);
  }

  getStatusColor(status: string): string {
    const map: Record<string, string> = {
      'Open': '#3b82f6', 'Closed': '#10b981', 'Rework': '#f97316',
      'Work Done': '#8b5cf6', 'On Hold': '#6b7280', 'Reopen(By Customer)': '#f59e0b'
    };
    return map[status] || '#6b7280';
  }

  toggleGroup(group: any): void {
    group.expanded = !group.expanded;
  }

  public getTimelinePct(minutes: number): number {
    return this.totalTimelineMinutes > 0 ? (minutes / this.totalTimelineMinutes) * 100 : 0;
  }

  public formatMinutes(minutes: number | null | undefined): string {
    if (minutes === null || minutes === undefined || minutes < 0) return 'N/A';
    if (minutes < 1) return '< 1 min';
    if (minutes < 60) return `${Math.round(minutes)} min`;
    const hours = Math.floor(minutes / 60);
    const remainingMinutes = Math.round(minutes % 60);
    if (hours >= 24) {
      const days = Math.floor(hours / 24);
      const remainingHours = hours % 24;
      if (remainingHours === 0 && remainingMinutes === 0) return `${days}d`;
      if (remainingHours === 0) return `${days}d ${remainingMinutes}m`;
      return `${days}d ${remainingHours}h`;
    }
    if (remainingMinutes === 0) return `${hours}h`;
    return `${hours}h ${remainingMinutes}m`;
  }

  private buildUserWiseBreakdown(breakdown: any[]): void {
    let pmMinutes = 0;
    const pmSubItemsMap: Map<string, number> = new Map();

    // assigneeName -> total minutes
    const assigneeMap: Map<string, number> = new Map();

    let onHoldMinutes = 0;

    for (const item of breakdown) {
      const name: string = (item.stateOrAssigneeName || item.StateOrAssigneeName || 'Unknown').trim();
      const mins: number = item.totalDurationMinutes || item.TotalDurationMinutes || 0;
      const nameLower = name.toLowerCase();

      if (nameLower === 'closed') continue;
      if (mins <= 0) continue;

      if (nameLower === 'on hold') {
        onHoldMinutes += mins;
        continue;
      }

      if (name === 'PM' || name === 'PM (Reviewing)') {
        pmMinutes += mins;
        pmSubItemsMap.set(name, (pmSubItemsMap.get(name) || 0) + mins);
        continue;
      }

      const colonIdx = name.indexOf(': ');
      if (colonIdx !== -1) {
        const rawAssigneePart = name.substring(colonIdx + 2).trim();
        const isRework = rawAssigneePart.endsWith('(Rework)');
        const assigneeName = isRework
          ? rawAssigneePart.replace('(Rework)', '').trim()
          : rawAssigneePart;

        assigneeMap.set(assigneeName, (assigneeMap.get(assigneeName) || 0) + mins);
        continue;
      }

      if (nameLower === 'developer' || nameLower === 'assignee') {
        const genericKey = 'Developer';
        assigneeMap.set(genericKey, (assigneeMap.get(genericKey) || 0) + mins);
        continue;
      }

      if (nameLower.includes('rework') && colonIdx === -1) {
        const genericKey = 'Developer';
        assigneeMap.set(genericKey, (assigneeMap.get(genericKey) || 0) + mins);
        continue;
      }

      pmMinutes += mins;
      pmSubItemsMap.set(name, (pmSubItemsMap.get(name) || 0) + mins);
    }

    const result: typeof this.userWiseBreakdown = [];

    // 1. PM
    if (pmMinutes > 0) {
      const pmSubItems: { label: string; minutes: number }[] = [];
      pmSubItemsMap.forEach((mins, label) => {
        pmSubItems.push({ label, minutes: mins });
      });
      result.push({
        category: 'PM',
        label: 'PM',
        totalMinutes: pmMinutes,
        expanded: false,
        subItems: pmSubItems
      });
    }

    // 2. Assignee
    let totalAssigneeMinutes = 0;
    const assigneeSubItems: { label: string; minutes: number }[] = [];
    assigneeMap.forEach((mins, name) => {
      totalAssigneeMinutes += mins;
      assigneeSubItems.push({ label: name, minutes: mins });
    });

    if (totalAssigneeMinutes > 0) {
      assigneeSubItems.sort((a, b) => b.minutes - a.minutes);
      result.push({
        category: 'Assignee',
        label: 'Assignee',
        totalMinutes: totalAssigneeMinutes,
        expanded: false,
        subItems: assigneeSubItems
      });
    }

    // 3. On Hold
    if (onHoldMinutes > 0) {
      result.push({
        category: 'OnHold',
        label: 'On Hold',
        totalMinutes: onHoldMinutes,
        expanded: false,
        subItems: []
      });
    }

    result.sort((a, b) => b.totalMinutes - a.totalMinutes);
    this.userWiseBreakdown = result;
  }



  private async loadCommunications(ticketId: number): Promise<void> {
    try {
      const commsResponse = await firstValueFrom(this.ticketService.getCommunications(ticketId));
      const appendIstOffset = (comms: any[]) => {
        return comms.map(c => {
          if (c.postedOn && !c.postedOn.toString().includes('+') && !c.postedOn.toString().includes('-') && !c.postedOn.toString().endsWith('Z')) {
            c.postedOn = c.postedOn.toString() + '+05:30';
          }
          return c;
        });
      };

      const globalNotes = appendIstOffset(commsResponse.globalNotes || []);
      this.developerChannelComments = appendIstOffset(commsResponse.developerChannel || []);
      this.updateNotesDeveloper = appendIstOffset(commsResponse.updateNotesDeveloper || []);

      this.allDeveloperComms = [...globalNotes, ...this.developerChannelComments, ...this.updateNotesDeveloper]
        .sort((a, b) => new Date(a.postedOn).getTime() - new Date(b.postedOn).getTime());
    } catch (error) {
      console.error('Failed to load communications:', error);
    }
  }

  public onPropertyChange(): void {
    this.isPropertiesDirty = true;
  }

  public formatDateForInput(dateStr: string | null | undefined): string | null {
    if (!dateStr) return null;
    const date = new Date(dateStr);
    if (isNaN(date.getTime())) return null;
    
    const pad = (n: number) => n.toString().padStart(2, '0');
    const yyyy = date.getFullYear();
    const MM = pad(date.getMonth() + 1);
    const dd = pad(date.getDate());
    const hh = pad(date.getHours());
    const mm = pad(date.getMinutes());
    
    return `${yyyy}-${MM}-${dd}T${hh}:${mm}`;
  }

  async saveProperties(): Promise<void> {
    if (!this.ticket) return;
    this.isLoading = true;
    try {
      const payload: any = {
        priority: this.canUpdatePriority ? this.selectedPriority : null,
        difficulty: this.canUpdateDifficulty ? this.selectedDifficulty : null,
        deadlineDate: this.canUpdateDeadline ? (this.selectedDeadline || null) : null
      };

      await this.ticketService.updateTicketAsPm(this.ticket.id, this.developerId, payload);
      this.isPropertiesDirty = false;
      this.showTemporaryMessage('Properties updated successfully!', 'success');
      this.loadTicketDetails(this.ticket.id);
    } catch (error: any) {
      console.error('Failed to save properties:', error);
      const errMsg = (typeof error?.error === 'string' && !error.error.trim().startsWith('<')) 
        ? error.error 
        : (error?.error?.error || error?.error?.message || error?.message || 'Failed to update properties.');
      this.showTemporaryMessage(errMsg, 'error');
      this.isLoading = false;
    }
  }
}