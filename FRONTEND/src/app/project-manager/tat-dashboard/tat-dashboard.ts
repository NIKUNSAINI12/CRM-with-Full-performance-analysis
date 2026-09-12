import { Component, OnInit, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TicketService, TatDashboardStats, TatTicket } from '../../Core/services/ticket.service';
import { AuthService } from '../../Core/services/auth';
import { TICKET_SERVICE_TOKEN } from '../../Core/injection-tokens';

@Component({
  selector: 'app-tat-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './tat-dashboard.html',
  styleUrls: []
})
export class TatDashboardComponent implements OnInit {
  isLoading = true;
  stats: TatDashboardStats | null = null;
  errorMessage = '';

  filterRelationship: string = '';
  filterStatus: string = '';
  filterFromDate: string = '';
  filterToDate: string = '';
  statusOptions: string[] = ['Open', 'Assigned', 'On Hold', 'Rework', 'Work Done', 'Closed'];

  get isAssignee(): boolean {
    const role = this.authService.getCurrentUser()?.role;
    return role === 'Assignee' || role === 'Executive' || role === 'Developer';
  }

  constructor(
    @Inject(TICKET_SERVICE_TOKEN) private ticketService: TicketService,
    private authService: AuthService,
    private router: Router
  ) {}

  public openTicket(ticket: any): void {
    const rawId = ticket?.id || ticket?.ticketId || ticket?.ticketNumber;
    if (rawId) {
      const numericId = typeof rawId === 'number' ? rawId : parseInt(String(rawId).replace(/\D/g, ''), 10);
      if (!isNaN(numericId) && numericId > 0) {
        this.router.navigate([this.authService.getTicketDetailRoute(numericId)]);
      }
    }
  }

  ngOnInit(): void {
    this.loadStatuses();
    this.loadStats();
  }

  private async loadStatuses(): Promise<void> {
    try {
      const statuses = await this.ticketService.getCustomStatuses();
      if (statuses && statuses.length > 0) {
        this.statusOptions = statuses.map(s => s.statusName || s.StatusName || s);
      }
    } catch (error) {
      console.error('Failed to load dynamic statuses for TAT:', error);
    }
  }

  clearFilters(): void {
    this.filterRelationship = '';
    this.filterStatus = '';
    this.filterFromDate = '';
    this.filterToDate = '';
    this.loadStats();
  }

  async loadStats(): Promise<void> {
    try {
      this.isLoading = true;
      this.errorMessage = '';
      const pmId = Number(this.authService.getCurrentUser()?.userId);
      if (!pmId || isNaN(pmId)) throw new Error('User not logged in');
      
      this.stats = await this.ticketService.getTatDashboardStats(
        pmId,
        this.filterRelationship || undefined,
        this.filterFromDate || undefined,
        this.filterToDate || undefined,
        this.filterStatus || undefined
      );
    } catch (error: any) {
      console.error('Error fetching TAT stats:', error);
      this.errorMessage = error.message || 'Failed to load TAT dashboard stats';
    } finally {
      this.isLoading = false;
    }
  }

  getSlaClass(slaStatus: string): string {
    switch (slaStatus) {
      case 'On Track': return 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30';
      case 'At Risk': return 'bg-amber-500/20 text-amber-400 border border-amber-500/30';
      case 'Breached': return 'bg-red-500/20 text-red-400 border border-red-500/30';
      default: return 'bg-brand-dark-800 text-brand-text-400';
    }
  }

  getPriorityClass(priority: string): string {
    switch (priority?.toLowerCase()) {
      case 'urgent':
      case 'high':
        return 'text-red-400';
      case 'medium':
        return 'text-amber-400';
      case 'low':
        return 'text-emerald-400';
      default:
        return 'text-brand-text-400';
    }
  }

  getStatusClass(status: string): string {
    switch (status?.toLowerCase()) {
      case 'open':
      case 're-open':
        return 'bg-amber-500/10 text-amber-500 border border-amber-500/20';
      case 'in progress':
      case 'pending':
        return 'bg-blue-500/10 text-blue-400 border border-blue-500/20';
      case 'resolved':
      case 'closed':
      case 'work done':
        return 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/20';
      default:
        return 'bg-brand-dark-800 text-brand-text-300 border border-brand-dark-700';
    }
  }
}
