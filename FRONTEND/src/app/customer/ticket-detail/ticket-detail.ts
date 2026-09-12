import { Component, OnInit, Inject } from '@angular/core';
import { CommonModule, Location, DatePipe } from '@angular/common';
import { API_BASE_URL } from '../../app';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, FormGroup, ReactiveFormsModule, FormsModule, Validators } from '@angular/forms';
import { switchMap, firstValueFrom, tap } from 'rxjs';

import { TICKET_SERVICE_TOKEN } from '../../Core/injection-tokens';

// Import new communication DTOs
import {
    TicketService, Assignee, TicketResponse, PmTicketUpdatePayload,
    TicketHistory, TicketDeadline, TicketReworkInfo, TicketTimelineAnalysis,
    PmCommunicationResponseDto, TicketCommunicationDto, CommunicationAttachmentDto,
    TicketRelationsResponse, LinkedTicketSummary
} from '../../Core/services/ticket.service';

import { Ticket, Product, TicketAttachment } from '../../Core/models/ticket.model';

@Component({
    selector: 'app-ticket-detail',
    standalone: true,
    imports: [CommonModule, ReactiveFormsModule, FormsModule, DatePipe],
    templateUrl: './ticket-detail.html',
    styleUrls: ['./ticket-detail.scss']
})
export class TicketDetailComponent implements OnInit {
    // --- Existing State ---
    public ticket: Ticket | null = null;
    public ticketDeadline: TicketDeadline | null = null;
    public totalReworkCount: number = 0;
    public assignees: Assignee[] = [];
    public ticketHistory: TicketHistory[] = [];
    public isLoading = true;
    public errorMessage: string | null = null;

    // Assignee searchable dropdown state
    public assigneeSearchQuery = '';
    public showAssigneeDropdown = false;
    public filteredAssignees: any[] = [];
    public successMessage: string | null = null;
    public updateForm: FormGroup;
    public timelineAnalysis: TicketTimelineAnalysis | null = null;
    private products: Product[] = [];
    private pmId: number = 0;
    public productName: string = 'N/A';
    public customerName: string = 'N/A';
    public statusOptions: string[] = [];
    public priorityOptions: string[] = [];
    public difficultyOptions: { id: string; label: string; color: string }[] = [
        { id: 'Easy',   label: '🟢 Easy',   color: '#22c55e' },
        { id: 'Medium', label: '🟡 Medium', color: '#eab308' },
        { id: 'Hard',   label: '🟠 Hard',   color: '#f97316' },
        { id: 'Expert', label: '🔴 Expert', color: '#ef4444' }
    ];

    // Added missing properties to fix lint errors
    public customerTickets: any[] = [];
    public reworkStats: any[] = [];

    public isLoadingComms = false;
    public globalNotes: TicketCommunicationDto[] = [];
    public customerNotes: TicketCommunicationDto[] = [];
    public activeTab: 'customerChat' | 'globalNotes' | 'timeline' | 'relations' = 'customerChat';
    public newCommentForm: FormGroup;
    public newNoteForm: FormGroup;
    public selectedFiles: File[] = [];
    public selectedNoteFiles: File[] = [];
    public isSubmittingComment = false;
    public isSubmittingNote = false;
    public commsErrorMessage: string | null = null;

    // --- Parent-Child Relations State ---
    public relations: TicketRelationsResponse = { parent: null, children: [] };
    public relationTreeRoot: any[] = [];
    public isLoadingRelations = false;
    public showParentModal = false;
    public showDocketModal = false;
    public docketDetails: any = null;
    public isLoadingDocketDetails = false;
    public activeDocketTab: 'general' | 'tracking' | 'pod' = 'general';
    public parentSearchQuery = '';
    public parentSearchResults: any[] = [];
    public isSearchingParent = false;
    public isSettingParent = false;
    public relationsError: string | null = null;
    public showCloseChildrenWarning = false;  // close-parent guard modal
    private pendingSavePayload: any = null;    // stored while warning is shown

    // Derived: how many children are closed
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

    // --- Current User Info ---
    public currentUser: any = {
        id: 0,
        name: 'Unknown User',
        role: 'Customer'
    };

    // Flat list (kept for total calc)
    filteredBreakdown: { stateOrAssigneeName: string, totalDurationMinutes: number }[] = [];
    totalTimelineMinutes: number = 0;

    // ── User-wise breakdown ──────────────────────────────────────────
    // Category: PM | Assignee | OnHold
    userWiseBreakdown: {
        category: 'PM' | 'Assignee' | 'OnHold';
        label: string;          // e.g. "PM", "John Doe", "On Hold"
        totalMinutes: number;
        expanded: boolean;
        subItems: { label: string; minutes: number }[];  // e.g. ["Pending PM Review: 30m", "PM: 1h"]
    }[] = [];

    getTimelinePct(minutes: number): number {
        return this.totalTimelineMinutes > 0 ? (minutes / this.totalTimelineMinutes) * 100 : 0;
    }

    toggleGroup(group: any): void {
        group.expanded = !group.expanded;
    }

    // Build user-wise breakdown from raw backend data
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

            // On Hold
            if (nameLower === 'on hold') {
                onHoldMinutes += mins;
                continue;
            }

            // Pure PM states
            if (name === 'PM' || name === 'PM (Reviewing)') {
                pmMinutes += mins;
                pmSubItemsMap.set(name, (pmSubItemsMap.get(name) || 0) + mins);
                continue;
            }

            // Colon-format: "Developer: John Doe" or "Developer: John Doe (Rework)"
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

            // Plain "Developer" (no colon) - treat as generic assignee
            if (nameLower === 'developer' || nameLower === 'assignee') {
                const genericKey = 'Developer';
                assigneeMap.set(genericKey, (assigneeMap.get(genericKey) || 0) + mins);
                continue;
            }

            // Rework states like "Developer (Rework)"
            if (nameLower.includes('rework') && colonIdx === -1) {
                const genericKey = 'Developer';
                assigneeMap.set(genericKey, (assigneeMap.get(genericKey) || 0) + mins);
                continue;
            }

            // Fallback: treat as PM time
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

        console.log('[UserWiseBreakdown] Input:', breakdown);
        console.log('[UserWiseBreakdown] Output:', this.userWiseBreakdown);
    }

    constructor(
        @Inject(TICKET_SERVICE_TOKEN) private ticketService: TicketService,
        private route: ActivatedRoute,
        private router: Router,
        private fb: FormBuilder,
        private location: Location
    ) {
        // Form for the Quick Update Bar
        this.updateForm = this.fb.group({
            status: ['', Validators.required],
            priority: ['', Validators.required],
            difficulty: ['Medium'],
            assignedToId: [null],
            deadline: [null],
            updateNote: ['']
        });

        // Form for the New Comment section
        this.newCommentForm = this.fb.group({
            commentText: ['', Validators.required],
        });

        // Form for the New Note section (in Quick Update)
        this.newNoteForm = this.fb.group({
            noteText: ['']
        });
    }

    ngOnInit(): void {
        this.loadCurrentUser();
        this.route.paramMap.subscribe(params => {
            const idParam = params.get('id');
            const pmIdParam = params.get('pmId') || this.route.snapshot.paramMap.get('pmId');
            if (idParam) {
                this.loadTicketDetails(+idParam, pmIdParam);
            }
        });
    }

    private loadCurrentUser(): void {
        const sessionStr = sessionStorage.getItem('auth_user') || sessionStorage.getItem('tms_session') || localStorage.getItem('user');
        if (sessionStr) {
            try {
                const session = JSON.parse(sessionStr);
                this.currentUser.id = Number(session.userId || session.id || 0);
                this.currentUser.name = session.fullName || session.userName || session.name || 'Unknown User';
                this.currentUser.role = session.role || 'Customer';
                
                // If PM or SuperManager, ensure pmId is set
                if (['PM', 'Project Manager', 'SuperManager', 'Super Admin'].includes(this.currentUser.role)) {
                    this.pmId = this.currentUser.id;
                }
                console.log('[AUTH] Loaded current user in ticket-detail:', this.currentUser);
            } catch (e) {
                console.error('[AUTH] Failed to parse session:', e);
            }
        }
    }

    // --- Data Loading ---
    private async loadTicketDetails(ticketId: number, pmIdParam: string | null = null): Promise<void> {
        this.isLoading = true;
        this.errorMessage = null;
        this.successMessage = null;

        // PM ID Handling
        if (pmIdParam) {
            this.pmId = Number(pmIdParam);
            this.currentUser.id = this.pmId;
        }

        try {
            // 1. Await the Promise from getTicketById
            const ticketData = await this.ticketService.getTicketById(ticketId);
            this.ticket = ticketData;
            console.log('[DEBUG] Loaded ticketData:', ticketData);
            console.log('[DEBUG] Ticket rating is:', ticketData?.rating);

            if (!this.ticket) {
                throw new Error(`Ticket with ID ${ticketId} not found.`);
            }

            // 2. Once primary details are loaded, fetch related data in parallel
            const [
                assigneesData,
                productsData,
                customerTicketsResponse,
                historyData,
                deadlineData,
                reworkData,
                timelineAnalysisData,
                commsResponse,
                statusesData,
                prioritiesData
            ] = await Promise.all([
                this.ticketService.getAssignees().catch(() => []),
                this.ticketService.getProducts().catch(() => []),
                this.ticketService.getTicketsForPM({ customerId: this.ticket?.customerId, pageSize: 10 }).catch(() => ({ tickets: [], totalCount: 0 })),
                this.ticketService.getTicketHistory(ticketId)
                    .then(history => {
                        return (history || []).map(h => {
                            let changeDate = h.changeDate;
                            if (changeDate && !changeDate.toString().includes('+') && !changeDate.toString().includes('-') && !changeDate.toString().endsWith('Z')) {
                                changeDate = changeDate.toString() + '+05:30';
                            }
                            return { ...h, changeDate };
                        })
                        .sort((a, b) => new Date(b.changeDate).getTime() - new Date(a.changeDate).getTime());
                    })
                    .catch(() => []),
                this.ticketService.getTicketDeadline(ticketId).catch(() => null),
                this.ticketService.getTicketReworkStats(ticketId).catch(() => []),
                this.ticketService.getTicketTimelineAnalysis(ticketId).catch(() => null),
                firstValueFrom(this.ticketService.getCommunications(ticketId)).catch(() => ({ globalNotes: [], developerChannel: [], updateNotesDeveloper: [], customerChannel: [], updateNotesCustomer: [], updateNotes: [] })),
                this.ticketService.getCustomStatuses().catch(() => []),
                this.ticketService.getPriorities().catch(() => [])
            ]);

            // Assign data fetched in parallel
            this.assignees = assigneesData;
            this.filteredAssignees = this.assignees;
            this.products = productsData;
            // Handle different response structures by casting to any
            const custResp = customerTicketsResponse as any;
            this.customerTickets = custResp.data || custResp.Tickets || custResp;
            this.ticketHistory = historyData;

            if (this.ticket) {
                this.customerName = this.ticket.customerName || (this.ticket as any).CustomerName || 'N/A';
                const rawProd = this.ticket.product || (this.ticket as any).Product || '';
                const prodVal = rawProd.toString().trim();
                
                if (prodVal && isNaN(Number(prodVal))) {
                    this.productName = prodVal;
                } else if (prodVal && this.products && this.products.length > 0) {
                    const foundProduct = this.products.find(p => p.id.toString() === prodVal);
                    this.productName = foundProduct ? foundProduct.name : prodVal;
                } else if (prodVal) {
                    this.productName = prodVal;
                } else {
                    this.productName = 'N/A';
                }

                const currentName = (this.ticket.createdByName || (this.ticket as any).createdBy || '').trim();
                const isGenericName = !currentName || ['Internal Staff', 'Internal User', 'Customer', '—'].includes(currentName);
                if (isGenericName && historyData && historyData.length > 0) {
                    const creationEvent = historyData.find((h: any) => 
                        h.eventDescription?.toLowerCase().includes('created')
                    ) || historyData[historyData.length - 1]; // oldest event is creation
                    if (creationEvent) {
                        if (creationEvent.changedByUserName) {
                            this.ticket.createdByName = creationEvent.changedByUserName;
                        }
                        if (creationEvent.changedByUserRole) {
                            this.ticket.createdByRole = creationEvent.changedByUserRole;
                        }
                    }
                }
            }

            this.ticketDeadline = deadlineData;
            this.reworkStats = reworkData; // Added based on instruction
            this.timelineAnalysis = timelineAnalysisData;
            this.totalReworkCount = reworkData.reduce((total: number, stat: any) => total + stat.reworkCount, 0);

            // Populate Master Dropdowns
            // Only add active statuses
            this.statusOptions = statusesData.filter((s: any) => s.isActive).map((s: any) => s.statusName);
            this.priorityOptions = prioritiesData.filter((p: any) => p.isActive).map((p: any) => p.priorityName);

            // Correctly access channels and notes
            const appendIstOffset = (comms: any[]) => {
                return comms.map(c => {
                    if (c.postedOn && !c.postedOn.toString().includes('+') && !c.postedOn.toString().includes('-') && !c.postedOn.toString().endsWith('Z')) {
                        c.postedOn = c.postedOn.toString() + '+05:30';
                    }
                    return c;
                });
            };
            
            // Combine all internal notes (Global notes, developer channel comments, developer update notes)
            const globalNotes = commsResponse.globalNotes || [];
            const devChannel = commsResponse.developerChannel || [];
            const devNotes = commsResponse.updateNotesDeveloper || [];
            const custChannel = commsResponse.customerChannel || [];
            const custNotes = commsResponse.updateNotesCustomer || [];

            this.customerNotes = appendIstOffset([...custChannel, ...custNotes])
                .sort((a, b) => new Date(a.postedOn).getTime() - new Date(b.postedOn).getTime());

            this.globalNotes = appendIstOffset([...globalNotes, ...devChannel, ...devNotes])
                .sort((a, b) => new Date(a.postedOn).getTime() - new Date(b.postedOn).getTime());

            // --- Helper to reload history --- FRONTEND RESOLUTION TIME PATCH ---
            // Recalculate if null (handles casing issues or missing backend calc)
            const summary = (this.timelineAnalysis as any)?.summary || (this.timelineAnalysis as any)?.Summary;
            if (summary && (summary.timeToResolutionHours == null && summary.TimeToResolutionHours == null)) {

                // STRICT CHECK: Only calculate if status is explicitly closed
                const isStatusClosed = this.ticket?.status?.toLowerCase() === 'closed' || this.ticket?.status?.toLowerCase() === 'close';

                if (isStatusClosed) {
                    // Find the CLOSE event
                    const closeEvent = this.ticketHistory.find(h =>
                        h.eventDescription &&
                        h.eventDescription.toLowerCase().includes('status changed') &&
                        (h.eventDescription.toLowerCase().includes("'closed'") || h.eventDescription.toLowerCase().includes("to closed") || h.eventDescription.toLowerCase().includes("'close'") || h.eventDescription.toLowerCase().includes("to close"))
                    );

                    if (closeEvent) {
                        const created = new Date(this.ticket.createdOn);
                        const closed = new Date(closeEvent.changeDate);
                        const diffMs = closed.getTime() - created.getTime();
                        const diffHrs = diffMs / (1000 * 60 * 60);
                        // Store as float for precision formatting
                        summary.timeToResolutionHours = diffHrs;
                        console.log('Frontend Patch: Resolution Time Calculated:', summary.timeToResolutionHours);
                    }
                }
            }
            // --------------------------------------

            // GROUP BREAKDOWN
            // Handle PascalCase (api) or camelCase
            const tAnalysis = this.timelineAnalysis as any;
            let breakdownData = tAnalysis?.breakdown || tAnalysis?.Breakdown;

            // --- FRONTEND BREAKDOWN FALLBACK ---
            // Check if we have any VALID (non-closed) items with positive duration
            const hasValidItems = breakdownData && breakdownData.some((item: any) => {
                const name = item.stateOrAssigneeName || item.StateOrAssigneeName || '';
                const mins = item.totalDurationMinutes || item.TotalDurationMinutes || 0;
                return name.toLowerCase() !== 'closed' && name.toLowerCase() !== 'close' && mins > 0;
            });

            // If no valid items (empty OR only closed/zero-duration items), synthesize data.
            if (!hasValidItems) {
                const created = new Date(this.ticket.createdOn);
                // For closed tickets, determine end date from timeline analysis or lastRepliedOn
                const closedDateStr = tAnalysis?.summary?.ticketClosedDate || 
                                     tAnalysis?.summary?.TicketClosedDate || 
                                     this.ticket.lastRepliedOn;
                const isStatusClosed = this.ticket?.status?.toLowerCase() === 'closed' || this.ticket?.status?.toLowerCase() === 'close';
                const end = (isStatusClosed && closedDateStr)
                    ? new Date(closedDateStr)
                    : new Date();
                const diffMinutes = Math.max(1, Math.floor((end.getTime() - created.getTime()) / (1000 * 60)));

                // Guess owner: If Assigned -> specific developer name; Else -> PM
                const assigneeName = (this.ticket as any).assignedToName;
                const ownerName = assigneeName
                    ? `Developer: ${assigneeName}`
                    : 'PM';

                breakdownData = [{
                    StateOrAssigneeName: ownerName,
                    TotalDurationMinutes: diffMinutes
                }];
                console.log('Frontend Patch: Synthesized Breakdown Data (Fallback):', breakdownData);
            }
            // -----------------------------------

            if (breakdownData && breakdownData.length > 0) {
                // Filter out 'Closed' state and normalize property names
                this.filteredBreakdown = breakdownData
                    .filter((item: any) => {
                        const name = item.stateOrAssigneeName || item.StateOrAssigneeName || '';
                        return name.toLowerCase() !== 'closed' && name.toLowerCase() !== 'close';
                    })
                    .map((item: any) => ({
                        stateOrAssigneeName: item.stateOrAssigneeName || item.StateOrAssigneeName,
                        totalDurationMinutes: item.totalDurationMinutes || item.TotalDurationMinutes || 0
                    }));

                // Calculate total duration for progress percentages
                this.totalTimelineMinutes = this.filteredBreakdown.reduce((sum, item) => sum + item.totalDurationMinutes, 0);

                // Build rich user-wise breakdown
                this.buildUserWiseBreakdown(breakdownData);
                // Debug log to verify data
                console.log('Timeline Analysis Data:', this.timelineAnalysis);
                console.log('User-wise breakdown:', this.userWiseBreakdown);
            } else {
                this.filteredBreakdown = [];
                this.userWiseBreakdown = [];
                this.totalTimelineMinutes = 0;
                console.warn('No breakdown data found in timelineAnalysis', this.timelineAnalysis);
            }

            // Final UI setup
            this.initializeQuickUpdateForm();
            this.setProductName();
            this.setCustomerNameFromList(customerTicketsResponse?.tickets, this.ticket?.customerId);

        } catch (err: any) {
            this.errorMessage = 'Failed to load ticket details.';
            console.error('Error loading ticket details:', err);
        } finally {
            this.isLoading = false;
        }

        // Load parent-child relations separately (non-blocking)
        this.loadRelations();
    }

    // Improve Format: 1.5 -> 1h 30m; 0.4 -> 24m
    public formatDuration(hours: number | null | undefined): string {
        if (hours === null || hours === undefined) return 'N/A';
        if (hours < 1) {
            return Math.floor(hours * 60) + 'm';
        }
        const h = Math.floor(hours);
        const m = Math.floor((hours - h) * 60);
        return m > 0 ? `${h}h ${m}m` : `${h}h`;
    }

    private initializeQuickUpdateForm(): void {
        if (this.ticket) {
            this.updateForm.patchValue({
                status:       this.ticket.status,
                priority:     this.ticket.priority,
                difficulty:   (this.ticket as any).difficulty || 'Medium',
                assignedToId: this.ticket.assignedTo ? Number(this.ticket.assignedTo) : null,
                deadline:     this.ticketDeadline ? this.formatDateForInput(this.ticketDeadline.deadlineDate) : null,
                updateNote:   ''
            });
            this.updateForm.markAsPristine();

            const currentAssignee = this.assignees.find(a => a.id === (this.ticket?.assignedTo ? Number(this.ticket.assignedTo) : null));
            this.assigneeSearchQuery = currentAssignee ? currentAssignee.fullName : '';
        }
    }


    // --- Access Control Helpers ---
    get isDeveloper(): boolean {
        return this.currentUser.role === 'Assignee' || this.currentUser.role === 'Developer' || this.currentUser.role === 'Executive';
    }

    get isCustomer(): boolean {
        return this.currentUser.role === 'Customer';
    }

    public getCustomerDisplayStatus(status: string | undefined): string {
        if (!status) return 'Open';
        const s = status.toLowerCase();
        if (s === 'open' || s === 'new') return 'Open';
        if (s === 'closed' || s === 'close') return 'Closed';
        return 'Assigned';
    }

    get isPM(): boolean {
        return ['PM', 'Project Manager', 'SuperManager', 'Super Admin'].includes(this.currentUser.role);
    }

    get isSuperAdmin(): boolean {
        return this.currentUser.role === 'Super Admin' || this.currentUser.role === 'SuperAdmin';
    }



    get isUpdateLocked(): boolean {
        if (!this.ticket) return true;

        // Developer Lock: Cannot update if 'Closed' OR 'Close' OR 'Pending PM Review'
        // Updates only allowed in active statuses
        if (this.isDeveloper) {
            const status = this.ticket.status?.toLowerCase();
            return status === 'closed' || status === 'close' || status === 'pending pm review';
        }
        return false;
    }

    // --- Unified Notes Getter ---
    // Kept to satisfy any template dependencies temporarily, though unused
    get unifiedCustomerNotes(): TicketCommunicationDto[] {
        return this.globalNotes;
    }

    // --- Main Action (Quick Update Bar) ---
    public async saveChanges(forceClose = false): Promise<void> {
        if (this.isUpdateLocked) {
            this.showTemporaryMessage('Updates are locked as the ticket is closed.', 'error');
            return;
        }

        if (!this.updateForm.valid || !this.ticket) {
            this.showTemporaryMessage('Form is invalid or ticket not loaded.', 'error');
            return;
        }

        const formValue = this.updateForm.value;

        // ── Close-children guard ─────────────────────────────────────────
        // WE CAN'T CLOSE A TICKET WHOSE CHILD IS NON-CLOSED OR NON HOLD
        const isNewStatusClosed = formValue.status === 'Closed' || formValue.status === 'Close';
        const isCurrentStatusClosed = this.ticket.status === 'Closed' || this.ticket.status === 'Close';
        if (isNewStatusClosed && !isCurrentStatusClosed) {
            const blockingChildren = this.relations.children.filter(c => c.status !== 'Closed' && c.status !== 'Close' && c.status !== 'On Hold');
            if (blockingChildren.length > 0) {
                const childNums = blockingChildren.map(c => c.ticketNumber).join(', ');
                this.showTemporaryMessage(`Cannot close this ticket — child ticket(s) ${childNums} are still active (not Closed or On Hold). Please resolve or hold them first.`, 'error');
                return;
            }
        }
        this.showCloseChildrenWarning = false;
        // ─────────────────────────────────────────────────────────────────

        const noteText = formValue.updateNote?.trim();

        const statusControl     = this.updateForm.get('status');
        const priorityControl   = this.updateForm.get('priority');
        const assigneeControl   = this.updateForm.get('assignedToId');
        const deadlineControl   = this.updateForm.get('deadline');
        const updateNoteControl = this.updateForm.get('updateNote');

        const mainFieldsDirty = statusControl?.dirty || priorityControl?.dirty || assigneeControl?.dirty;
        const deadlineDirty = deadlineControl?.dirty;
        const noteDirty = updateNoteControl?.dirty && noteText;

        if (!mainFieldsDirty && !deadlineDirty && !noteDirty) {
            this.showTemporaryMessage('No changes detected in Status, Priority, Assignee, Deadline, or Update Note.', 'info');
            return;
        }

        this.isLoading = true;
        const updatePromises: Promise<any>[] = [];

        // --- 1️⃣ Build FULL payload for main ticket update ---
        if (this.isDeveloper) {
            // Developers can only update Status
            if (statusControl?.dirty && formValue.status) {
                updatePromises.push(
                    this.ticketService.updateTicketStatusAsDeveloper(this.ticket.id, formValue.status, noteText)
                );
            }
        } else {
            // Ensure pmId is properly set (fallback to currentUser.id)
            if (!this.pmId && this.currentUser.id) {
                this.pmId = this.currentUser.id;
            }

            // PMs can update all fields
            const mainPayload: PmTicketUpdatePayload = {
                status:       statusControl?.dirty     ? (formValue.status    || null) : null,
                priority:     priorityControl?.dirty   ? (formValue.priority  || null) : null,
                difficulty:   undefined, // Removed
                assignedToId: assigneeControl?.dirty
                     ? (formValue.assignedToId ? Number(formValue.assignedToId) : null)
                     : null,
                note:         noteText || null,
                deadlineDate: deadlineControl?.dirty ? (formValue.deadline ? formValue.deadline : null) : null
            };

            updatePromises.push(
                this.ticketService.updateTicketAsPm(this.ticket.id, this.pmId, mainPayload)
            );
        }

        // --- 2️⃣ Handle update note for the conversation feed ---
        if (noteText) {
            const formData = new FormData();
            formData.append('CommentText', noteText);
            formData.append('Channel', 'GlobalNotes');
            formData.append('PostedByUserId', this.currentUser.id.toString());
            formData.append('PostedByName', this.currentUser.name);
            formData.append('PostedByUserRole', this.currentUser.role);
            
            updatePromises.push(
                firstValueFrom(this.ticketService.addCommunication(this.ticket.id, formData))
            );
        }

        try {
            await Promise.all(updatePromises);
            this.updateForm.get('updateNote')?.reset('');
            this.updateForm.markAsPristine();
            this.showTemporaryMessage('Ticket updated successfully!', 'success');
            await this.loadTicketDetails(this.ticket.id);
        } catch (error: any) {
            console.error('Error saving ticket changes:', error);
            const errMsg = error?.error?.error || error?.error?.message || error?.message || 'Failed to update ticket.';
            this.showTemporaryMessage(errMsg, 'error');
            // Revert form status on error to match the actual ticket status
            this.initializeQuickUpdateForm();
        } finally {
            this.isLoading = false;
        }
    }


    // --- Communication Actions ---
    public selectTab(tabName: 'customerChat' | 'globalNotes' | 'timeline' | 'relations'): void {
        // Customers cannot access Global Notes tab
        if (tabName === 'globalNotes' && this.isCustomer) return;
        this.activeTab = tabName;
    }

    public onFileSelected(event: Event): void {
        const element = event.target as HTMLInputElement;
        if (element.files && element.files.length > 0) {
            const currentFiles = Array.from(element.files);
            currentFiles.forEach(file => {
                if (!this.selectedFiles.some(f => f.name === file.name && f.size === file.size)) {
                    this.selectedFiles.push(file);
                }
            });
            element.value = '';
        }
    }

    public removeSelectedFile(index: number): void {
        this.selectedFiles.splice(index, 1);
    }

    public onCommentKeyDown(event: Event, channel: string = 'GlobalNotes'): void {
        const keyboardEvent = event as KeyboardEvent;
        if (keyboardEvent.key === 'Enter' && !keyboardEvent.shiftKey) {
            keyboardEvent.preventDefault();
            this.postComment(channel);
        }
    }

    public postComment(channel: string = 'GlobalNotes'): void {
        const commentText = this.newCommentForm.value.commentText?.trim();
        if (!this.ticket || this.isSubmittingComment) return;

        if (!commentText && this.selectedFiles.length === 0) {
            this.showTemporaryMessage('Please enter a comment or add an attachment.', 'error', 'comms');
            return;
        }

        this.isSubmittingComment = true;
        this.commsErrorMessage = null;

        const targetChannel = channel === 'Customer' ? 'Customer' : 'GlobalNotes';

        const formData = new FormData();
        formData.append('CommentText', commentText || '');
        formData.append('Channel', targetChannel);
        formData.append('PostedByUserId', this.currentUser.id.toString());
        formData.append('PostedByName', this.currentUser.name);
        formData.append('PostedByUserRole', this.currentUser.role);

        this.selectedFiles.forEach(file => {
            formData.append('files', file, file.name);
        });

        this.ticketService.addCommunication(this.ticket.id, formData).subscribe({
            next: (newComment) => {
                if (targetChannel === 'Customer') {
                    this.customerNotes = [...this.customerNotes, newComment];
                    this.showTemporaryMessage('Customer message posted!', 'success');
                } else {
                    this.globalNotes = [...this.globalNotes, newComment];
                    this.showTemporaryMessage('Global Note posted!', 'success');
                }
                this.newCommentForm.reset({ commentText: '' });
                this.selectedFiles = [];
                this.loadTicketHistory(this.ticket!.id);
            },
            error: (err) => {
                this.showTemporaryMessage('Failed to post note.', 'error', 'comms');
                console.error('Error posting note:', err);
            },
            complete: () => {
                this.isSubmittingComment = false;
            }
        });
    }

    // --- Helper to reload history ---
    private loadTicketHistory(ticketId: number): void {
        this.ticketService.getTicketHistory(ticketId)
            .then(history => {
                this.ticketHistory = (history || []).map(h => {
                    let changeDate = h.changeDate;
                    if (changeDate && !changeDate.toString().includes('+') && !changeDate.toString().includes('-') && !changeDate.toString().endsWith('Z')) {
                        changeDate = changeDate.toString() + '+05:30';
                    }
                    return { ...h, changeDate };
                });
            })
            .catch(err => console.error("Failed to refresh history:", err));
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
            
            const globalNotes = commsResponse.globalNotes || [];
            const devChannel = commsResponse.developerChannel || [];
            const devNotes = commsResponse.updateNotesDeveloper || [];
            
            this.globalNotes = appendIstOffset([...globalNotes, ...devChannel, ...devNotes])
                .sort((a, b) => new Date(a.postedOn).getTime() - new Date(b.postedOn).getTime());
        } catch (error) {
            console.error('Failed to reload communications', error);
        }
    }

    // --- Existing Helper & Utility Methods ---
    public getTicketAge(createdDate: any): string {
        if (!createdDate) return 'N/A';
        const start = new Date(createdDate);
        const now = new Date();
        const diffMs = now.getTime() - start.getTime();
        const diffHrs = diffMs / (1000 * 60 * 60);

        if (diffHrs < 24) {
            return Math.floor(diffHrs) + 'h ' + Math.floor((diffHrs % 1) * 60) + 'm';
        } else {
            const days = Math.floor(diffHrs / 24);
            const hrs = Math.floor(diffHrs % 24);
            return days + 'd ' + hrs + 'h';
        }
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

    private formatDateForInput(dateString: string | Date | null): string | null {
        if (!dateString) return null;
        try {
            const date = new Date(dateString);
            if (isNaN(date.getTime())) return null;
            const year = date.getFullYear();
            const month = (date.getMonth() + 1).toString().padStart(2, '0');
            const day = date.getDate().toString().padStart(2, '0');
            const hours = date.getHours().toString().padStart(2, '0');
            const minutes = date.getMinutes().toString().padStart(2, '0');
            return `${year}-${month}-${day}T${hours}:${minutes}`;
        } catch (e) {
            console.error("Error formatting date:", dateString, e);
            return null;
        }
    }

    private setProductName(): void {
        if (!this.ticket || !this.ticket.product) {
            this.productName = 'N/A';
            return;
        }

        const prodVal = this.ticket.product.toString().trim();

        // If it's already a product name string (not a numeric ID)
        if (isNaN(Number(prodVal))) {
            this.productName = prodVal;
            return;
        }

        // If it's a numeric ID, find matching product in master list
        if (this.products && this.products.length > 0) {
            const foundProduct = this.products.find(p => p.id.toString() === prodVal);
            if (foundProduct) {
                this.productName = foundProduct.name;
                return;
            }
        }

        this.productName = prodVal;
    }

    private setCustomerNameFromList(allTickets: Ticket[] | null | undefined, customerId?: number): void {
        if (this.ticket?.customerName && this.ticket.customerName !== '—') {
            this.customerName = this.ticket.customerName;
            return;
        }
        if (!customerId || !allTickets || allTickets.length === 0) {
            this.customerName = this.ticket?.customerName || 'Unknown';
            return;
        }
        const foundTicket = allTickets.find(ticket => ticket.customerId === customerId && ticket.customerName);
        this.customerName = foundTicket?.customerName || this.ticket?.customerName || 'Unknown';
    }

    public async downloadAttachment(attachment: TicketAttachment | CommunicationAttachmentDto, commentId?: number): Promise<void> {
        if (!this.ticket) return;

        const isCommunicationAttachment = commentId !== undefined;
        const attachmentId = attachment.id.toString();
        const fileName = attachment.fileName;

        this.showTemporaryMessage(`Preparing download for ${fileName}...`, 'info');

        try {
            let blob: Blob;

            if (isCommunicationAttachment && commentId) {
                blob = await this.ticketService.getCommunicationAttachment(commentId, attachmentId);
            } else if (!isCommunicationAttachment) {
                blob = await this.ticketService.getTicketAttachment(this.ticket.id, attachmentId);
            } else {
                throw new Error("Cannot download communication attachment without parent comment ID.");
            }

            this.triggerBlobDownload(blob, fileName);

        } catch (error: any) {
            console.error("Download error:", error);
            const errorMsg = error.message || (error.status ? `Server error: ${error.status}` : 'Unknown download error');
            this.showTemporaryMessage(`Failed to download attachment: ${fileName}. ${errorMsg}`, 'error');
        }
    }

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

    public goBack(): void {
        this.location.back();
    }

    private showTemporaryMessage(message: string, type: 'success' | 'error' | 'info', target: 'main' | 'comms' = 'main'): void {
        if (target === 'main') this.commsErrorMessage = null;
        if (target === 'comms') { this.successMessage = null; this.errorMessage = null; }

        if (type === 'success') {
            this.successMessage = message;
            setTimeout(() => { if (this.successMessage === message) this.successMessage = null; }, 4000);
        } else if (type === 'error') {
            if (target === 'comms') {
                this.commsErrorMessage = message;
            } else {
                this.errorMessage = message;
            }
        } else {
            this.successMessage = `ℹ️ ${message}`;
            setTimeout(() => { if (this.successMessage === `ℹ️ ${message}`) this.successMessage = null; }, 3000);
        }
    }

    // ──────────────────────────────────── Parent-Child Relations
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

    openParentModal(): void {
        this.parentSearchQuery = '';
        this.parentSearchResults = [];
        this.showParentModal = true;
    }

    closeParentModal(): void {
        this.showParentModal = false;
        this.parentSearchQuery = '';
        this.parentSearchResults = [];
    }

    async searchParentTickets(): Promise<void> {
        const q = this.parentSearchQuery.trim();
        if (!q) { this.parentSearchResults = []; return; }
        this.isSearchingParent = true;
        try {
            const resp = await this.ticketService.getTicketsForPM({ pageSize: 50, pageNumber: 1 });
            const all: any[] = (resp as any).tickets || (resp as any).Tickets || [];
            this.parentSearchResults = all.filter((t: any) => {
                if (t.id === this.ticket?.id) return false; // exclude self
                const num  = (t.ticketNumber || t.TicketNumber || '').toLowerCase();
                const subj = (t.subject || t.Subject || '').toLowerCase();
                return num.includes(q.toLowerCase()) || subj.includes(q.toLowerCase());
            }).slice(0, 15);
        } catch {
            this.parentSearchResults = [];
        } finally {
            this.isSearchingParent = false;
        }
    }

    async selectParent(parentTicket: any): Promise<void> {
        if (!this.ticket) return;
        this.isSettingParent = true;
        try {
            await this.ticketService.setTicketParent(this.ticket.id, parentTicket.id, this.pmId);
            this.closeParentModal();
            await this.loadRelations();
            this.showTemporaryMessage(`Linked as child of ${parentTicket.ticketNumber || parentTicket.TicketNumber}`, 'success', 'main');
        } catch {
            this.showTemporaryMessage('Failed to set parent ticket.', 'error', 'main');
        } finally {
            this.isSettingParent = false;
        }
    }

    async removeParent(): Promise<void> {
        if (!this.ticket) return;
        this.isSettingParent = true;
        try {
            await this.ticketService.setTicketParent(this.ticket.id, null, this.pmId);
            await this.loadRelations();
            this.showTemporaryMessage('Parent link removed.', 'success', 'main');
        } catch {
            this.showTemporaryMessage('Failed to remove parent.', 'error', 'main');
        } finally {
            this.isSettingParent = false;
        }
    }

    navigateToRelated(ticketId: number): void {
        // Navigate preserving pmId if PM, or as developer otherwise
        if (this.isDeveloper) {
            this.router.navigate(['/developer/ticket', ticketId]);
        } else if (this.isPM || this.isSuperAdmin) {
            this.router.navigate(['/pm/ticket', ticketId]);
        } else {
            // Customers
            this.router.navigate(['/customer/ticket', ticketId]);
        }
    }

    getStatusColor(status: string): string {
        const map: Record<string, string> = {
            'Open': '#3b82f6', 'Closed': '#10b981', 'Rework': '#f97316',
            'Work Done': '#8b5cf6', 'On Hold': '#6b7280', 'Reopen(By Customer)': '#f59e0b'
        };
        return map[status] || '#6b7280';
    }

    // ──────────────────────────────── Create Child Ticket shortcut ────────────────────────────────
    createChildTicket(): void {
        if (!this.ticket) return;
        // Navigate to the existing ticket-create route, which the PM uses.
        // Pass parentId + parentNumber so ticket-create can auto-link after creation.
        // customerId comes from the current ticket being viewed.
        this.router.navigate(
            ['/customer/ticket-create'],
            {
                queryParams: {
                    customerId:   this.ticket.customerId,
                    parentId:     this.ticket.id,
                    parentNumber: this.ticket.ticketNumber,
                    pmId:         this.pmId
                }
            }
        );
    }

    // ──────────────────────────────── Close-children warning modal actions ────────────────────────────────
    // PM confirmed: close parent anyway (children stay open)
    confirmCloseWithChildren(): void {
        this.showCloseChildrenWarning = false;
        this.saveChanges(true);  // forceClose = true bypasses the guard
    }
    cancelCloseWarning(): void {
        this.showCloseChildrenWarning = false;
        // Reset status back to original so the form looks un-dirty
        if (this.ticket) {
            this.updateForm.patchValue({ status: this.ticket.status });
        }
    }

    async viewDocketDetails(docketNo: string): Promise<void> {
        this.activeDocketTab = 'general';
        this.showDocketModal = true;
        this.isLoadingDocketDetails = true;
        this.docketDetails = null;
        try {
            this.docketDetails = await this.ticketService.getDocketByNo(docketNo);
            console.log('Loaded Docket Details:', this.docketDetails);
        } catch (e) {
            console.error('Error loading docket details', e);
        } finally {
            this.isLoadingDocketDetails = false;
        }
    }

    getDocketDocUrl(type: 'pod' | 'signature' | 'status-photo', filename: string): string {
        if (!filename) return '';
        return `${API_BASE_URL}/Tickets/dockets/documents/${type}/${filename}`;
    }

    get allStatusPhotos(): { name: string; date: any; location: string }[] {
        if (!this.docketDetails || !this.docketDetails.history) return [];
        return this.docketDetails.history
            .filter((h: any) => h.statusPhotoName)
            .map((h: any) => ({
                name: h.statusPhotoName,
                date: h.activityDateTime,
                location: h.transitLocation
            }));
    }

    closeDocketModal(): void {
        this.showDocketModal = false;
        this.docketDetails = null;
    }

    public filterAssignees(): void {
        const term = this.assigneeSearchQuery.toLowerCase().trim();
        this.filteredAssignees = this.assignees.filter(a => 
            a.fullName?.toLowerCase().includes(term) ||
            a.assigneeNumber?.toLowerCase().includes(term) ||
            a.id?.toString().includes(term)
        );
    }

    public onAssigneeSearchInput(): void {
        this.showAssigneeDropdown = true;
        this.filterAssignees();
        if (!this.assigneeSearchQuery.trim()) {
            this.updateForm.patchValue({ assignedToId: null });
        }
    }

    public selectAssignee(assignee: any): void {
        this.updateForm.patchValue({ assignedToId: assignee.id || null });
        this.updateForm.get('assignedToId')?.markAsDirty();
        this.assigneeSearchQuery = assignee.id ? assignee.fullName : '';
        this.showAssigneeDropdown = false;
    }

    public onAssigneeBlur(): void {
        setTimeout(() => {
            this.showAssigneeDropdown = false;
            const selectedId = this.updateForm.get('assignedToId')?.value;
            const current = this.assignees.find(a => a.id === selectedId);
            this.assigneeSearchQuery = current ? current.fullName : '';
        }, 200);
    }
}