import { Component, OnInit, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { TICKET_SERVICE_TOKEN } from '../../Core/injection-tokens';
import { Ticket } from '../../Core/models/ticket.model';
import { TicketService, Sprint, SprintAnalytics, CompletedSprintHistory } from '../../Core/services/ticket.service';
import { DragDropModule, CdkDragDrop, moveItemInArray, transferArrayItem } from '@angular/cdk/drag-drop';

@Component({
  selector: 'app-sprint-planning',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, DragDropModule],
  templateUrl: './sprint-planning.html',
  styleUrls: ['./sprint-planning.scss']
})
export class SprintPlanningComponent implements OnInit {
  public Math = Math;
  public activeTab: 'backlog' | 'analytics' | 'completed' = 'backlog';
  public sprintAnalyticsData: SprintAnalytics | null = null;
  public completedSprintHistory: CompletedSprintHistory[] = [];

  public sprints: Sprint[] = [];
  // Rollover options on completion
  public rolloverOption: 'backlog' | 'new-sprint' | 'future-sprint' = 'backlog';
  public rolloverSprintId: number | null = null;
  public rolloverNewSprintName: string = '';
  public allTickets: Ticket[] = [];
  public backlogTickets: Ticket[] = [];
  public sprintTicketsMap: { [sprintId: number]: Ticket[] } = {};

  // Jira-style: each sprint panel is independently collapsible
  public collapsedSprintIds: Set<number> = new Set();
  public isBacklogCollapsed = false;

  // UI states
  public isLoading = false;
  public showCreateModal = false;
  public showEditModal = false;
  public showCompleteModal = false;
  public sprintForm: FormGroup;
  public editingSprintId: number | null = null;
  public successMessage: string | null = null;
  public errorMessage: string | null = null;

  // Complete Sprint modal state
  public completingSprint: Sprint | null = null;
  public completeDestinationSprintId: number | null = null;

  // Selected tickets for bulk assignment/removal
  public selectedBacklogTicketIds: Set<number> = new Set();
  public selectedSprintTicketIds: Set<number> = new Set();

  // Backlog search
  public backlogSearchQuery = '';

  get filteredBacklogTickets(): Ticket[] {
    if (!this.backlogSearchQuery.trim()) {
      return this.backlogTickets;
    }
    const q = this.backlogSearchQuery.toLowerCase().trim();
    return this.backlogTickets.filter(t => 
      (t.ticketNumber && t.ticketNumber.toLowerCase().includes(q)) || 
      (t.subject && t.subject.toLowerCase().includes(q))
    );
  }

  constructor(
    @Inject(TICKET_SERVICE_TOKEN) private ticketService: TicketService,
    private fb: FormBuilder,
    private router: Router
  ) {
    this.sprintForm = this.fb.group({
      sprintName: ['', Validators.required],
      startDate: ['', Validators.required],
      endDate: ['', Validators.required],
    });
  }

  goToTicket(ticketId: number): void {
    if (!ticketId) return;
    this.router.navigate(['/pm/ticket', ticketId]);
  }

  ngOnInit(): void {
    this.loadData();
  }

  async loadData(): Promise<void> {
    this.isLoading = true;
    this.clearMessages();
    try {
      // 1. Get Sprints
      this.sprints = await this.ticketService.getSprints();

      // 2. Get all PM tickets
      const response = await this.ticketService.getTicketsForPM({ pageSize: 1000 });
      this.allTickets = response.tickets || [];
      
      console.log('API returned tickets:', this.allTickets.map(t => ({ id: t.id, sprint: t.sprintId, order: t.sprintOrder })));

      // 3. Partition into backlog vs sprint tickets
      this.backlogTickets = this.allTickets.filter(t => t.sprintId == null);
      
      this.sprintTicketsMap = {};
      this.sprints.forEach(s => {
        if (s.sprintId) {
          // Filter tickets for this sprint
          const sprintTix = this.allTickets.filter(t => t.sprintId === s.sprintId);
          // Sort them explicitly by sprintOrder to ensure perfect order regardless of global position
          sprintTix.sort((a, b) => {
            const orderA = a.sprintOrder !== null && a.sprintOrder !== undefined ? a.sprintOrder : 9999;
            const orderB = b.sprintOrder !== null && b.sprintOrder !== undefined ? b.sprintOrder : 9999;
            return orderA - orderB;
          });
          this.sprintTicketsMap[s.sprintId] = sprintTix;
        }
      });

      // 4. Load data for specific tabs
      if (this.activeTab === 'analytics') {
        this.sprintAnalyticsData = await this.ticketService.getSprintAnalytics();
      } else if (this.activeTab === 'completed') {
        this.completedSprintHistory = await this.ticketService.getCompletedSprintsHistory();
      }

      // Clear selections
      this.selectedBacklogTicketIds.clear();
      this.selectedSprintTicketIds.clear();
    } catch (e) {
      console.error('Failed to load sprint planning data', e);
      this.errorMessage = 'Failed to load sprint planning data.';
    } finally {
      this.isLoading = false;
    }
  }

  switchTab(tab: 'backlog' | 'analytics' | 'completed'): void {
    this.activeTab = tab;
    this.loadData();
  }

  // ---- Sprint Panel Collapse ----
  toggleSprintPanel(sprintId: number): void {
    if (this.collapsedSprintIds.has(sprintId)) {
      this.collapsedSprintIds.delete(sprintId);
    } else {
      this.collapsedSprintIds.add(sprintId);
    }
  }

  isSprintCollapsed(sprintId: number): boolean {
    return this.collapsedSprintIds.has(sprintId);
  }

  // ---- Data Helpers ----
  getSprintTickets(sprintId: number): Ticket[] {
    return this.sprintTicketsMap[sprintId] || [];
  }

  getSprintTicketCounts(sprintId: number): { open: number; progress: number; onhold: number; done: number } {
    const tickets = this.getSprintTickets(sprintId);
    const open = tickets.filter(t => ['Open', 'Assigned', 'Reopen(By Customer)'].includes(t.status)).length;
    const done = tickets.filter(t => ['Closed', 'Close', 'Work Done'].includes(t.status)).length;
    const onhold = tickets.filter(t => t.status === 'On Hold').length;
    return { open, progress: tickets.length - open - done - onhold, onhold, done };
  }

  get futureSprints(): Sprint[] {
    return this.sprints.filter(s => s.status === 'Future');
  }

  get activeAndFutureSprints(): Sprint[] {
    return this.sprints.filter(s => s.status !== 'Completed');
  }

  getCompletedTicketCount(): number {
    if (!this.completingSprint?.sprintId) return 0;
    return this.getSprintTickets(this.completingSprint.sprintId)
      .filter(t => ['Closed', 'Close', 'Work Done'].includes(t.status)).length;
  }

  getIncompleteTicketCount(): number {
    if (!this.completingSprint?.sprintId) return 0;
    return this.getSprintTickets(this.completingSprint.sprintId)
      .filter(t => !['Closed', 'Close', 'Work Done'].includes(t.status)).length;
  }

  // ---- Sprint CRUD ----
  openCreateModal(): void {
    this.sprintForm.reset({
      sprintName: '',
      startDate: this.formatDateForInput(new Date()),
      endDate: this.formatDateForInput(new Date(Date.now() + 14 * 24 * 60 * 60 * 1000)),
    });
    this.showCreateModal = true;
  }

  closeCreateModal(): void {
    this.showCreateModal = false;
  }

  async createSprint(): Promise<void> {
    if (this.sprintForm.invalid) return;
    this.isLoading = true;
    try {
      const payload: Sprint = { ...this.sprintForm.value, status: 'Future' };
      await this.ticketService.createSprint(payload);
      this.showTemporaryMessage('Sprint created successfully!', 'success');
      this.closeCreateModal();
      await this.loadData();
    } catch (e) {
      this.errorMessage = 'Failed to create sprint.';
    } finally {
      this.isLoading = false;
    }
  }

  openEditModal(sprint: Sprint): void {
    this.editingSprintId = sprint.sprintId!;
    this.sprintForm.patchValue({
      sprintName: sprint.sprintName,
      startDate: this.formatDateForInput(sprint.startDate),
      endDate: this.formatDateForInput(sprint.endDate),
    });
    this.showEditModal = true;
  }

  closeEditModal(): void {
    this.showEditModal = false;
    this.editingSprintId = null;
  }

  async updateSprint(): Promise<void> {
    if (this.sprintForm.invalid || !this.editingSprintId) return;
    this.isLoading = true;
    try {
      // Preserve existing status when editing
      const existing = this.sprints.find(s => s.sprintId === this.editingSprintId);
      const payload: Sprint = { ...this.sprintForm.value, status: existing?.status ?? 'Future' };
      await this.ticketService.updateSprint(this.editingSprintId, payload);
      this.showTemporaryMessage('Sprint updated successfully!', 'success');
      this.closeEditModal();
      await this.loadData();
    } catch (e) {
      this.errorMessage = 'Failed to update sprint.';
    } finally {
      this.isLoading = false;
    }
  }

  async deleteSprint(sprintId: number): Promise<void> {
    if (!confirm('Are you sure you want to delete this sprint? Assigned tickets will return to the backlog.')) return;
    this.isLoading = true;
    try {
      await this.ticketService.deleteSprint(sprintId);
      this.showTemporaryMessage('Sprint deleted. Tickets returned to backlog.', 'success');
      await this.loadData();
    } catch (e) {
      this.errorMessage = 'Failed to delete sprint.';
    } finally {
      this.isLoading = false;
    }
  }

  // ---- Jira-style Sprint State Machine ----
  async startSprint(sprint: Sprint): Promise<void> {
    if (!confirm(`Start "${sprint.sprintName}"? Only one sprint can be active at a time.`)) return;
    this.isLoading = true;
    try {
      await this.ticketService.startSprint(sprint.sprintId!);
      this.showTemporaryMessage(`"${sprint.sprintName}" is now Active!`, 'success');
      await this.loadData();
    } catch (e: any) {
      const msg = e?.error?.message || e?.message || 'Failed to start sprint.';
      this.errorMessage = msg;
    } finally {
      this.isLoading = false;
    }
  }

  openCompleteModal(sprint: Sprint): void {
    this.completingSprint = sprint;
    this.completeDestinationSprintId = null;
    this.rolloverOption = 'backlog';
    this.rolloverSprintId = null;
    this.rolloverNewSprintName = 'Rollover from ' + sprint.sprintName;
    this.showCompleteModal = true;
  }

  closeCompleteModal(): void {
    this.showCompleteModal = false;
    this.completingSprint = null;
  }

  async confirmCompleteSprint(): Promise<void> {
    if (!this.completingSprint) return;
    this.isLoading = true;
    try {
      let destId: number | null = null;
      if (this.rolloverOption === 'new-sprint') {
        const payload: Sprint = {
          sprintName: this.rolloverNewSprintName || 'New Rollover Sprint',
          startDate: this.formatDateForInput(new Date()),
          endDate: this.formatDateForInput(new Date(Date.now() + 14 * 24 * 60 * 60 * 1000)),
          status: 'Future'
        };
        const newSprint = await this.ticketService.createSprint(payload);
        destId = newSprint.sprintId!;
      } else if (this.rolloverOption === 'future-sprint') {
        destId = this.rolloverSprintId;
      }

      await this.ticketService.completeSprint(
        this.completingSprint.sprintId!,
        destId
      );
      this.showTemporaryMessage(`"${this.completingSprint.sprintName}" has been completed!`, 'success');
      this.closeCompleteModal();
      await this.loadData();
    } catch (e: any) {
      const msg = e?.error?.message || e?.message || 'Failed to complete sprint.';
      this.errorMessage = msg;
    } finally {
      this.isLoading = false;
    }
  }

  // ---- Ticket Assignment ----
  async onTicketSprintChange(ticketId: number, event: Event): Promise<void> {
    const selectElement = event.target as HTMLSelectElement;
    const val = selectElement.value;
    if (!val) return;

    this.isLoading = true;
    try {
      const sprintId = Number(val);
      await this.ticketService.assignTicketsToSprint(sprintId, ticketId.toString());
      this.showTemporaryMessage('Ticket added to sprint!', 'success');
      // Reset the select to placeholder
      selectElement.value = '';
      await this.loadData();
    } catch (e) {
      this.errorMessage = 'Failed to assign ticket.';
      this.isLoading = false;
    }
  }

  async removeSingleTicketFromSprint(ticketId: number): Promise<void> {
    this.isLoading = true;
    try {
      await this.ticketService.assignTicketsToSprint(null, ticketId.toString());
      this.showTemporaryMessage('Ticket returned to backlog.', 'success');
      await this.loadData();
    } catch (e) {
      this.errorMessage = 'Failed to remove ticket.';
      this.isLoading = false;
    }
  }

  // ---- Ticket Selection & Bulk Transfer ----
  toggleBacklogTicketSelection(ticketId: number): void {
    if (this.selectedBacklogTicketIds.has(ticketId)) {
      this.selectedBacklogTicketIds.delete(ticketId);
    } else {
      this.selectedBacklogTicketIds.add(ticketId);
    }
  }

  toggleSprintTicketSelection(ticketId: number): void {
    if (this.selectedSprintTicketIds.has(ticketId)) {
      this.selectedSprintTicketIds.delete(ticketId);
    } else {
      this.selectedSprintTicketIds.add(ticketId);
    }
  }

  async moveSelectedToSprint(sprintId: number): Promise<void> {
    if (this.selectedBacklogTicketIds.size === 0) return;
    this.isLoading = true;
    try {
      const ticketIdsStr = Array.from(this.selectedBacklogTicketIds).join(',');
      await this.ticketService.assignTicketsToSprint(sprintId, ticketIdsStr);
      this.showTemporaryMessage('Selected backlog tickets added to sprint!', 'success');
      await this.loadData();
    } catch (e) {
      this.errorMessage = 'Failed to assign selected tickets.';
      this.isLoading = false;
    }
  }

  async moveSelectedToBacklog(): Promise<void> {
    if (this.selectedSprintTicketIds.size === 0) return;
    this.isLoading = true;
    try {
      const ticketIdsStr = Array.from(this.selectedSprintTicketIds).join(',');
      await this.ticketService.assignTicketsToSprint(null, ticketIdsStr);
      this.showTemporaryMessage('Selected tickets returned to backlog!', 'success');
      await this.loadData();
    } catch (e) {
      this.errorMessage = 'Failed to remove selected tickets.';
      this.isLoading = false;
    }
  }

  // ---- Utilities ----
  private formatDateForInput(dateInput: Date | string): string {
    const d = new Date(dateInput);
    const month = '' + (d.getMonth() + 1);
    const day = '' + d.getDate();
    const year = d.getFullYear();
    return [year, month.padStart(2, '0'), day.padStart(2, '0')].join('-');
  }

  private clearMessages(): void {
    this.successMessage = null;
    this.errorMessage = null;
  }

  private showTemporaryMessage(message: string, type: 'success' | 'error'): void {
    if (type === 'success') {
      this.successMessage = message;
      setTimeout(() => { if (this.successMessage === message) this.successMessage = null; }, 4000);
    } else {
      this.errorMessage = message;
      setTimeout(() => { if (this.errorMessage === message) this.errorMessage = null; }, 4000);
    }
  }

  getSprintStatusBadgeClass(status: string): string {
    switch (status) {
      case 'Active': return 'bg-emerald-500/10 text-emerald-500 border-emerald-500/20';
      case 'Future': return 'bg-amber-500/10 text-amber-500 border-amber-500/20';
      case 'Completed': return 'bg-blue-500/10 text-blue-500 border-blue-500/20';
      default: return 'bg-brand-dark-700 text-brand-text-400';
    }
  }

  getPriorityBadgeClass(priority: string): string {
    switch (priority) {
      case 'Urgent': return 'bg-red-500/10 text-red-500 border-red-500/20';
      case 'High': return 'bg-orange-500/10 text-orange-500 border-orange-500/20';
      case 'Medium': return 'bg-amber-500/10 text-amber-500 border-amber-500/20';
      case 'Low': return 'bg-emerald-500/10 text-emerald-500 border-emerald-500/20';
      default: return 'bg-brand-dark-700 text-brand-text-400';
    }
  }

  getStatusColor(status: string): string {
    const map: Record<string, string> = {
      'Open': '#3b82f6', 'Closed': '#10b981', 'Rework': '#f97316', 'Close': '#10b981',
      'Work Done': '#8b5cf6', 'On Hold': '#6b7280', 'Reopen(By Customer)': '#f59e0b',
      'In Progress': '#10b981', 'Pending PM Review': '#8b5cf6', 'Assigned': '#3b82f6'
    };
    return map[status] || '#6b7280';
  }

  getMonthName(monthNum: number): string {
    const months = [
      'January', 'February', 'March', 'April', 'May', 'June',
      'July', 'August', 'September', 'October', 'November', 'December'
    ];
    return months[monthNum - 1] || '';
  }

  // ---- Drag and Drop Handlers ----
  getConnectedListIds(currentSprintId: number | null): string[] {
    const list = ['backlog'];
    this.sprints.forEach(s => {
      if (s.sprintId !== currentSprintId) {
        list.push('sprint-' + s.sprintId);
      }
    });
    return list;
  }

  async onDrop(event: CdkDragDrop<Ticket[]>): Promise<void> {
    const ticket = event.item.data as Ticket;
    
    if (event.previousContainer === event.container) {
      // Reorder locally - Must be synchronous before any awaits!
      moveItemInArray(event.container.data, event.previousIndex, event.currentIndex);
      await this.saveTicketOrder(event.container.data);
    } else {
      const targetSprintId = event.container.id === 'backlog' ? null : Number(event.container.id.replace('sprint-', ''));
      
      // SYNCHRONOUS array mutation to satisfy Angular CDK immediately!
      transferArrayItem(
        event.previousContainer.data,
        event.container.data,
        event.previousIndex,
        event.currentIndex
      );
      
      // Keep local state consistent immediately
      ticket.sprintId = targetSprintId;

      this.isLoading = true;
      try {
        // Perform backend sync after local state is updated
        await this.ticketService.assignTicketsToSprint(targetSprintId, ticket.id.toString());
        await this.saveTicketOrder(event.container.data);
        this.showTemporaryMessage('Ticket moved successfully!', 'success');
      } catch (err) {
        this.errorMessage = 'Failed to move ticket. Reverting...';
        await this.loadData(); // Revert local state on error
      } finally {
        this.isLoading = false;
      }
    }
  }

  async saveTicketOrder(tickets: Ticket[]): Promise<void> {
    const orders = tickets.map((t, index) => ({
      ticketId: t.id,
      order: index
    }));
    try {
      await this.ticketService.updateTicketOrder(orders);
    } catch (e) {
      console.error('Failed to update ticket order indexes', e);
    }
  }

}