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
  styleUrls: ['./tat-dashboard.css']
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
  showFilters = false;
  drillDownFilter: { key: string; value: any } | null = null;

  toggleFilters(): void {
    this.showFilters = !this.showFilters;
  }

  // Independent Widget Configurations
  donutConfig = {
    priority: 'All',
    timeRange: 'All',
    chartType: 'donut'
  };

  categoriesConfig = {
    status: 'All',
    limit: 5,
    theme: 'blue'
  };

  ratingConfig = {
    style: 'breakdown',
    customer: 'All'
  };

  trendConfig = {
    priority: 'All',
    groupBy: 'day' as 'day' | 'week',
    theme: 'purple'
  };

  // Local Filtered Datasets
  filteredDonutData = { active: 0, closed: 0, total: 0, closedPct: 0 };
  filteredCategories: any[] = [];
  filteredRatingSummary: any[] = [];
  filteredSatisfactionScore: number = 0;
  filteredTrendData: { label: string; count: number }[] = [];
  uniqueCustomers: string[] = [];

  customerSearchText: string = '';
  customerSortKey: string = 'tickets';
  customerSortAsc: boolean = false;

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
    const id = ticket?.ticketId || ticket?.id;
    if (id) {
      this.router.navigate([this.authService.getTicketDetailRoute(Number(id))]);
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
    this.drillDownFilter = null;
    this.loadStats();
  }

  async loadStats(): Promise<void> {
    try {
      this.isLoading = true;
      this.errorMessage = '';
      this.drillDownFilter = null;
      const pmId = Number(this.authService.getCurrentUser()?.userId);
      if (!pmId || isNaN(pmId)) throw new Error('User not logged in');
      
      this.stats = await this.ticketService.getTatDashboardStats(
        pmId,
        this.filterRelationship || undefined,
        this.filterFromDate || undefined,
        this.filterToDate || undefined,
        this.filterStatus || undefined
      );
      if (this.stats && this.stats.ratingSummary) {
        this.stats.ratingSummary.sort((a, b) => b.rating - a.rating);
      }
      this.applyLocalFilters();
    } catch (error: any) {
      console.error('Error fetching TAT stats:', error);
      this.errorMessage = error.message || 'Failed to load TAT dashboard stats';
    } finally {
      this.isLoading = false;
    }
  }

  applyLocalFilters(): void {
    if (!this.stats || !this.stats.recentTickets) return;

    const tickets = this.stats.recentTickets;

    // 1. Populate unique customers for dropdown filters
    const custSet = new Set<string>();
    tickets.forEach(t => { if (t.customer) custSet.add(t.customer); });
    this.uniqueCustomers = Array.from(custSet).sort();

    // 2. Filter Donut Data
    let donutTickets = tickets;
    if (this.donutConfig.priority !== 'All') {
      donutTickets = donutTickets.filter(t => t.priority?.toLowerCase() === this.donutConfig.priority.toLowerCase());
    }
    if (this.donutConfig.timeRange !== 'All') {
      const days = this.donutConfig.timeRange === '7d' ? 7 : 30;
      const cutoff = new Date();
      cutoff.setDate(cutoff.getDate() - days);
      donutTickets = donutTickets.filter(t => t.createdOn && new Date(t.createdOn) >= cutoff);
    }
    const donutActive = donutTickets.filter(t => !this.isTicketClosed(t.status)).length;
    const donutClosed = donutTickets.filter(t => this.isTicketClosed(t.status)).length;
    const donutTotal = donutTickets.length;
    this.filteredDonutData = {
      active: donutActive,
      closed: donutClosed,
      total: donutTotal,
      closedPct: donutTotal > 0 ? Math.round((donutClosed / donutTotal) * 100) : 0
    };

    // 3. Filter Categories Data
    let catTickets = tickets;
    if (this.categoriesConfig.status === 'Active') {
      catTickets = catTickets.filter(t => !this.isTicketClosed(t.status));
    } else if (this.categoriesConfig.status === 'Closed') {
      catTickets = catTickets.filter(t => this.isTicketClosed(t.status));
    }
    const catMap = new Map<string, number>();
    catTickets.forEach(t => {
      const name = t.categoryName || t.productName || 'General Support';
      catMap.set(name, (catMap.get(name) || 0) + 1);
    });
    let catList = Array.from(catMap.entries()).map(([categoryName, count]) => ({ categoryName, count }));
    catList.sort((a, b) => b.count - a.count);
    if (this.categoriesConfig.limit > 0) {
      catList = catList.slice(0, this.categoriesConfig.limit);
    }
    this.filteredCategories = catList;

    // 4. Filter Ratings Data
    let ratTickets = tickets;
    if (this.ratingConfig.customer !== 'All') {
      ratTickets = ratTickets.filter(t => t.customer === this.ratingConfig.customer);
    }
    const ratTicketsWithRating = ratTickets.filter(t => t.rating !== null && t.rating !== undefined);
    const ratDict: { [key: number]: number } = { 5: 0, 4: 0, 3: 0, 2: 0, 1: 0 };
    let ratingSum = 0;
    ratTicketsWithRating.forEach(t => {
      const r = Math.round(Number(t.rating));
      if (r >= 1 && r <= 5) {
        ratDict[r]++;
        ratingSum += r;
      }
    });
    this.filteredRatingSummary = Object.keys(ratDict).map(k => ({ rating: Number(k), count: ratDict[Number(k)] })).reverse();
    this.filteredSatisfactionScore = ratTicketsWithRating.length > 0 
      ? Math.round((ratingSum / ratTicketsWithRating.length) * 10) / 10 
      : 0;

    // 5. Filter Trend Volume Data
    let trendTickets = tickets;
    if (this.trendConfig.priority !== 'All') {
      trendTickets = trendTickets.filter(t => t.priority?.toLowerCase() === this.trendConfig.priority.toLowerCase());
    }
    const sortedForTrend = [...trendTickets].sort((a, b) => new Date(a.createdOn).getTime() - new Date(b.createdOn).getTime());
    const trendMap = new Map<string, number>();
    
    sortedForTrend.forEach(t => {
      if (!t.createdOn) return;
      const date = new Date(t.createdOn);
      let label = '';
      if (this.trendConfig.groupBy === 'day') {
        label = date.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
      } else {
        const oneJan = new Date(date.getFullYear(), 0, 1);
        const numberOfDays = Math.floor((date.getTime() - oneJan.getTime()) / (24 * 60 * 60 * 1000));
        const weekNum = Math.ceil((date.getDay() + 1 + numberOfDays) / 7);
        label = `Wk ${weekNum}`;
      }
      trendMap.set(label, (trendMap.get(label) || 0) + 1);
    });
    
    this.filteredTrendData = Array.from(trendMap.entries()).map(([label, count]) => ({ label, count }));
    if (this.filteredTrendData.length === 0) {
      this.filteredTrendData = [{ label: 'No Data', count: 0 }];
    }
  }

  isTicketClosed(status: string): boolean {
    if (!status) return false;
    const norm = status.toLowerCase();
    return norm === 'closed' || norm === 'resolved' || norm === 'work done';
  }

  getFilteredCustomerStats(): any[] {
    if (!this.stats || !this.stats.customerStats) return [];
    let list = [...this.stats.customerStats];
    
    if (this.customerSearchText) {
      const q = this.customerSearchText.toLowerCase();
      list = list.filter(c => c.customerName?.toLowerCase().includes(q));
    }

    list.sort((a, b) => {
      let valA: any = 0;
      let valB: any = 0;

      if (this.customerSortKey === 'customerName') {
        valA = a.customerName || '';
        valB = b.customerName || '';
      } else if (this.customerSortKey === 'tickets') {
        valA = a.totalTickets;
        valB = b.totalTickets;
      } else if (this.customerSortKey === 'sla') {
        valA = a.slaCompliancePercent;
        valB = b.slaCompliancePercent;
      } else if (this.customerSortKey === 'csat') {
        valA = a.averageSatisfactionScore;
        valB = b.averageSatisfactionScore;
      } else if (this.customerSortKey === 'resTime') {
        valA = a.averageResolutionTime;
        valB = b.averageResolutionTime;
      }

      if (valA < valB) return this.customerSortAsc ? -1 : 1;
      if (valA > valB) return this.customerSortAsc ? 1 : -1;
      return 0;
    });

    return list;
  }

  toggleCustomerSort(key: string): void {
    if (this.customerSortKey === key) {
      this.customerSortAsc = !this.customerSortAsc;
    } else {
      this.customerSortKey = key;
      this.customerSortAsc = false;
    }
  }

  // ---- SVG Chart Helpers ----

  private readonly TREND_WIDTH = 500;
  private readonly TREND_HEIGHT = 150;

  getTrendPoints(): string {
    if (this.filteredTrendData.length === 0) return '0,75 500,75';
    const maxVal = Math.max(...this.filteredTrendData.map(d => d.count), 1);
    const stepX = this.filteredTrendData.length > 1 ? this.TREND_WIDTH / (this.filteredTrendData.length - 1) : this.TREND_WIDTH / 2;
    
    return this.filteredTrendData.map((d, i) => {
      const x = this.filteredTrendData.length === 1 ? this.TREND_WIDTH / 2 : i * stepX;
      const y = this.TREND_HEIGHT - (d.count / maxVal) * (this.TREND_HEIGHT - 25) - 10;
      return `${x.toFixed(1)},${y.toFixed(1)}`;
    }).join(' ');
  }

  getTrendAreaPath(): string {
    if (this.filteredTrendData.length === 0) return '';
    const points = this.getTrendPoints();
    const firstX = this.filteredTrendData.length === 1 ? this.TREND_WIDTH / 2 : 0;
    const lastX = this.filteredTrendData.length === 1 ? this.TREND_WIDTH / 2 : this.TREND_WIDTH;
    return `M ${firstX},${this.TREND_HEIGHT} L ${points} L ${lastX},${this.TREND_HEIGHT} Z`;
  }

  getTrendPointCoords(): { x: number; y: number }[] {
    if (this.filteredTrendData.length === 0) return [];
    const maxVal = Math.max(...this.filteredTrendData.map(d => d.count), 1);
    const stepX = this.filteredTrendData.length > 1 ? this.TREND_WIDTH / (this.filteredTrendData.length - 1) : this.TREND_WIDTH / 2;
    
    return this.filteredTrendData.map((d, i) => {
      const x = this.filteredTrendData.length === 1 ? this.TREND_WIDTH / 2 : i * stepX;
      const y = this.TREND_HEIGHT - (d.count / maxVal) * (this.TREND_HEIGHT - 25) - 10;
      return { x, y };
    });
  }

  // ---- Donut Helpers ----

  private readonly DONUT_CIRCUMFERENCE = 87.96; // 2 * PI * 14

  getActivePercentage(): number {
    return this.filteredDonutData.total > 0 
      ? Math.round((this.filteredDonutData.active / this.filteredDonutData.total) * 100) 
      : 0;
  }

  getClosedPercentage(): number {
    return this.filteredDonutData.total > 0 
      ? Math.round((this.filteredDonutData.closed / this.filteredDonutData.total) * 100) 
      : 0;
  }

  getActiveDashArray(): string {
    const pct = this.getActivePercentage();
    const len = (pct / 100) * this.DONUT_CIRCUMFERENCE;
    return `${len.toFixed(2)} ${this.DONUT_CIRCUMFERENCE.toFixed(2)}`;
  }

  getClosedDashArray(): string {
    const pct = this.getClosedPercentage();
    const len = (pct / 100) * this.DONUT_CIRCUMFERENCE;
    return `${len.toFixed(2)} ${this.DONUT_CIRCUMFERENCE.toFixed(2)}`;
  }

  getClosedDashOffset(): number {
    const activePct = this.getActivePercentage();
    const offset = (activePct / 100) * this.DONUT_CIRCUMFERENCE;
    return -offset;
  }

  // ---- Badge CSS Helpers ----

  getStatusBadgeClass(status: string): string {
    switch (status?.toLowerCase()) {
      case 'open':
      case 're-open':
        return 'badge-status-open';
      case 'in progress':
      case 'pending':
      case 'assigned':
        return 'badge-status-progress';
      case 'resolved':
      case 'closed':
      case 'work done':
        return 'badge-status-closed';
      default:
        return 'badge-status-default';
    }
  }

  getSlaBadgeClass(slaStatus: string): string {
    switch (slaStatus) {
      case 'On Track': return 'badge-sla-track';
      case 'At Risk': return 'badge-sla-risk';
      case 'Breached': return 'badge-sla-breached';
      default: return 'badge-status-default';
    }
  }

  getPriorityClass(priority: string): string {
    switch (priority?.toLowerCase()) {
      case 'urgent':
      case 'high':
        return 'badge-priority-high';
      case 'medium':
        return 'badge-priority-medium';
      case 'low':
        return 'badge-priority-low';
      default:
        return '';
    }
  }

  setDrillDown(key: string, value: any): void {
    if (this.drillDownFilter && this.drillDownFilter.key === key && this.drillDownFilter.value === value) {
      this.drillDownFilter = null;
    } else {
      this.drillDownFilter = { key, value };
    }
  }

  getFilteredTickets(): any[] {
    if (!this.stats || !this.stats.recentTickets) return [];
    let list = [...this.stats.recentTickets];
    
    if (this.drillDownFilter) {
      const { key, value } = this.drillDownFilter;
      if (key === 'status') {
        list = list.filter(t => this.isTicketClosed(t.status) === (value === 'closed'));
      } else if (key === 'category') {
        list = list.filter(t => (t.categoryName || 'General Support') === value);
      } else if (key === 'rating') {
        list = list.filter(t => Math.round(Number(t.rating)) === Number(value));
      } else if (key === 'date') {
        list = list.filter(t => {
          if (!t.createdOn) return false;
          const date = new Date(t.createdOn);
          let label = '';
          if (this.trendConfig.groupBy === 'day') {
            label = date.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
          } else {
            const oneJan = new Date(date.getFullYear(), 0, 1);
            const numberOfDays = Math.floor((date.getTime() - oneJan.getTime()) / (24 * 60 * 60 * 1000));
            const weekNum = Math.ceil((date.getDay() + 1 + numberOfDays) / 7);
            label = `Wk ${weekNum}`;
          }
          return label === value;
        });
      }
    }
    return list;
  }

  formatDate(date: any): string {
    if (!date) return 'N/A';
    return new Intl.DateTimeFormat('en-GB', {
      day: '2-digit', month: 'short', year: 'numeric'
    }).format(new Date(date)).replace(/ /g, '-');
  }
}
