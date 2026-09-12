import { Component, OnInit, AfterViewInit, OnDestroy, Inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TICKET_SERVICE_TOKEN } from '../Core/injection-tokens';
import { TicketService } from '../Core/services/ticket.service';

declare const Chart: any;

export interface LocationSummary {
  locationId: number;
  locationCode: string;
  locationName: string;
  totalTickets: number;
  resolvedTickets: number;
  unresolvedTickets: number;
  resolutionRate: number;
  userCount: number;
  customerCount: number;
  avgResolutionHours: number;
}

export interface DepartmentStat {
  moduleId: number;
  moduleName: string;
  totalTickets: number;
  resolvedTickets: number;
  unresolvedTickets: number;
  resolutionRate: number;
}

export interface CustomerStat {
  customerId: number;
  customerName: string;
  customerCode: string;
  totalTickets: number;
  resolvedTickets: number;
  unresolvedTickets: number;
  resolutionRate: number;
  topDepartment?: string;
}

export interface TeamStat {
  roleId: number;
  roleName: string;
  managerName?: string;
  managerRole?: string;
  userCount: number;
  totalTickets: number;
  resolvedTickets: number;
  unresolvedTickets: number;
  resolutionRate: number;
}

export interface UserRank {
  userId: number;
  userName: string;
  locationId: number;
  locationName: string;
  locationCode: string;
  roleName: string;
  totalTickets: number;
  resolvedTickets: number;
  openTickets: number;
  resolutionRate: number;
  avgResolutionHours: number;
}

export interface PerformanceOverview {
  summary: {
    totalTickets: number;
    resolvedTickets: number;
    unresolvedTickets: number;
    inProgressTickets: number;
    resolutionRate: number;
    avgResolutionHours: number;
    totalLocations: number;
    totalCustomers: number;
    totalStaff: number;
  };
  locations: LocationSummary[];
  departments: DepartmentStat[];
  topCustomers: CustomerStat[];
  teams: TeamStat[];
  topUsers: UserRank[];
}

@Component({
  selector: 'app-performance-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './performance-dashboard.html',
  styleUrls: ['./performance-dashboard.scss']
})
export class PerformanceDashboardComponent implements OnInit, OnDestroy {
  // Navigation & View State
  public activeView: 'overview' | 'location' | 'customer' | 'user' | 'team' = 'overview';
  public activeTab: 'locations' | 'customers' | 'departments' | 'teams' | 'users' = 'locations';
  public locationSubTab: 'departments' | 'teams' | 'users' | 'customers' | 'tickets' = 'departments';

  // Loading States
  public isLoadingOverview = false;
  public isLoadingDetail = false;
  public errorMessage: string | null = null;

  // Selected Entities
  public selectedLocationId: number | null = null;
  public selectedCustomerId: number | null = null;
  public selectedUserId: number | null = null;
  public selectedTeamId: number | null = null;

  // Data Objects
  public overviewData: PerformanceOverview | null = null;
  public locationDetail: any = null;
  public customerDetail: any = null;
  public userDetail: any = null;
  public teamDetail: any = null;

  // Global Interactive Filters (build-dashboard skill)
  public selectedPeriod: string = 'all'; // 'all', 'today', 'week', 'month', 'last30', 'custom'
  public customFromDate: string = '';
  public customToDate: string = '';
  public showFilterSection: boolean = false;
  public selectedFilterLocation: number = 0; // 0 = all
  public selectedFilterStatus: string = ''; // '' = all
  public selectedFilterPriority: string = ''; // '' = all

  // Local Search & Sort
  public searchQuery = '';
  public filterActiveOnly = true;
  public sortBy: 'tickets' | 'resolved' | 'rate' | 'name' = 'tickets';
  public ticketStatusFilter = 'ALL';

  // Accordions in Location Detail
  public expandedDeptId: number | null = null;
  public expandedTeamId: number | null = null;

  // Chart.js Instances
  private locationBarChart: any = null;
  private deptDoughnutChart: any = null;
  private drilldownChart1: any = null;
  private drilldownChart2: any = null;

  constructor(
    @Inject(TICKET_SERVICE_TOKEN) private ticketService: TicketService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadOverview();
  }

  ngOnDestroy(): void {
    this.destroyCharts();
  }

  private destroyCharts(): void {
    if (this.locationBarChart) { this.locationBarChart.destroy(); this.locationBarChart = null; }
    if (this.deptDoughnutChart) { this.deptDoughnutChart.destroy(); this.deptDoughnutChart = null; }
    if (this.drilldownChart1) { this.drilldownChart1.destroy(); this.drilldownChart1 = null; }
    if (this.drilldownChart2) { this.drilldownChart2.destroy(); this.drilldownChart2 = null; }
  }

  // =====================================================================
  // API LOADERS WITH DYNAMIC FILTERS
  // =====================================================================

  public async loadOverview(): Promise<void> {
    this.isLoadingOverview = true;
    this.errorMessage = null;
    this.destroyCharts();

    let fromDate: string | undefined = undefined;
    let toDate: string | undefined = undefined;

    const now = new Date();
    if (this.selectedPeriod === 'today') {
      fromDate = now.toISOString().split('T')[0];
    } else if (this.selectedPeriod === 'week') {
      const d = new Date(now);
      d.setDate(d.getDate() - 7);
      fromDate = d.toISOString().split('T')[0];
    } else if (this.selectedPeriod === 'month') {
      const d = new Date(now.getFullYear(), now.getMonth(), 1);
      fromDate = d.toISOString().split('T')[0];
    } else if (this.selectedPeriod === 'last30') {
      const d = new Date(now);
      d.setDate(d.getDate() - 30);
      fromDate = d.toISOString().split('T')[0];
    } else if (this.selectedPeriod === 'custom') {
      if (this.customFromDate) fromDate = this.customFromDate;
      if (this.customToDate) toDate = this.customToDate;
    }

    const filters: any = {};
    if (fromDate) filters.fromDate = fromDate;
    if (toDate) filters.toDate = toDate;
    if (this.selectedFilterLocation > 0) filters.locationId = this.selectedFilterLocation;
    if (this.selectedFilterStatus) filters.status = this.selectedFilterStatus;
    if (this.selectedFilterPriority) filters.priority = this.selectedFilterPriority;

    try {
      this.overviewData = await this.ticketService.getPerformanceOverview(filters);
      setTimeout(() => this.renderOverviewCharts(), 50);
    } catch (err: any) {
      console.error('Failed to load performance overview:', err);
      this.errorMessage = 'Unable to connect to analytics engine. Please ensure backend is running.';
    } finally {
      this.isLoadingOverview = false;
      this.cdr.detectChanges();
    }
  }

  public toggleFilterSection(): void {
    this.showFilterSection = !this.showFilterSection;
  }

  public getActiveFiltersCount(): number {
    let count = 0;
    if (this.selectedPeriod !== 'all') count++;
    if (Number(this.selectedFilterLocation) !== 0) count++;
    if (this.selectedFilterStatus && this.selectedFilterStatus.trim()) count++;
    if (this.selectedFilterPriority && this.selectedFilterPriority.trim()) count++;
    if (this.selectedPeriod === 'custom' && (this.customFromDate || this.customToDate)) count++;
    return count;
  }

  public getFilterSummary(): string {
    const parts: string[] = [];
    if (this.selectedPeriod !== 'all') {
      const pNames: Record<string, string> = {
        today: 'Today',
        week: 'This Week',
        month: 'This Month',
        last30: 'Last 30 Days',
        custom: 'Custom Range'
      };
      parts.push(`Period: ${pNames[this.selectedPeriod] || this.selectedPeriod}`);
    }
    if (Number(this.selectedFilterLocation) !== 0) {
      const loc = this.overviewData?.locations?.find(l => l.locationId === Number(this.selectedFilterLocation));
      parts.push(`Location: ${loc ? loc.locationName : this.selectedFilterLocation}`);
    }
    if (this.selectedFilterStatus) {
      parts.push(`Status: ${this.selectedFilterStatus}`);
    }
    if (this.selectedFilterPriority) {
      parts.push(`Priority: ${this.selectedFilterPriority}`);
    }
    return parts.length > 0 ? parts.join(' | ') : 'All Data (No Filters)';
  }

  public resetFilters(): void {
    this.selectedPeriod = 'all';
    this.customFromDate = '';
    this.customToDate = '';
    this.selectedFilterLocation = 0;
    this.selectedFilterStatus = '';
    this.selectedFilterPriority = '';
    this.searchQuery = '';
    this.loadOverview();
  }

  public async openLocation(locationId: number): Promise<void> {
    this.selectedLocationId = locationId;
    this.activeView = 'location';
    this.locationSubTab = 'departments';
    this.isLoadingDetail = true;
    this.errorMessage = null;
    this.locationDetail = null;
    this.expandedDeptId = null;
    this.expandedTeamId = null;
    this.destroyCharts();

    try {
      this.locationDetail = await this.ticketService.getLocationPerformance(locationId);
      if (this.locationDetail?.departments?.length > 0) {
        this.expandedDeptId = this.locationDetail.departments[0].moduleId;
      }
      if (this.locationDetail?.teams?.length > 0) {
        this.expandedTeamId = this.locationDetail.teams[0].roleId;
      }
      setTimeout(() => this.renderLocationCharts(), 50);
    } catch (err: any) {
      console.error('Failed to load location details:', err);
      this.errorMessage = 'Failed to load location performance details.';
    } finally {
      this.isLoadingDetail = false;
      this.cdr.detectChanges();
    }
  }

  public async openCustomer(customerId: number): Promise<void> {
    this.selectedCustomerId = customerId;
    this.activeView = 'customer';
    this.isLoadingDetail = true;
    this.errorMessage = null;
    this.customerDetail = null;
    this.destroyCharts();

    try {
      this.customerDetail = await this.ticketService.getCustomerPerformance(customerId);
      setTimeout(() => this.renderCustomerCharts(), 50);
    } catch (err: any) {
      console.error('Failed to load customer details:', err);
      this.errorMessage = 'Failed to load customer performance summary.';
    } finally {
      this.isLoadingDetail = false;
      this.cdr.detectChanges();
    }
  }

  public async openUser(userId: number): Promise<void> {
    this.selectedUserId = userId;
    this.activeView = 'user';
    this.isLoadingDetail = true;
    this.errorMessage = null;
    this.userDetail = null;
    this.destroyCharts();

    try {
      this.userDetail = await this.ticketService.getUserPerformance(userId);
      setTimeout(() => this.renderUserCharts(), 50);
    } catch (err: any) {
      console.error('Failed to load user details:', err);
      this.errorMessage = 'Failed to load staff member metrics.';
    } finally {
      this.isLoadingDetail = false;
      this.cdr.detectChanges();
    }
  }

  public async openTeam(managerId: number): Promise<void> {
    this.selectedTeamId = managerId;
    this.activeView = 'team';
    this.isLoadingDetail = true;
    this.errorMessage = null;
    this.teamDetail = null;
    this.destroyCharts();

    try {
      this.teamDetail = await this.ticketService.getTeamPerformance(managerId);
      setTimeout(() => this.renderTeamCharts(), 50);
    } catch (err: any) {
      console.error('Failed to load team details:', err);
      this.errorMessage = 'Failed to load team performance and hierarchy details.';
    } finally {
      this.isLoadingDetail = false;
      this.cdr.detectChanges();
    }
  }

  // =====================================================================
  // CHART.JS INTEGRATION (Using build-dashboard patterns)
  // =====================================================================

  private renderOverviewCharts(): void {
    if (typeof Chart === 'undefined' || !this.overviewData) return;

    // 1. Primary Chart: Top Locations Resolution Comparison (Stacked Bar)
    const locCanvas = document.getElementById('overview-loc-chart') as HTMLCanvasElement;
    if (locCanvas) {
      if (this.locationBarChart) this.locationBarChart.destroy();

      const topLocs = this.overviewData.locations.filter(l => l.totalTickets > 0).slice(0, 8);
      const labels = topLocs.map(l => l.locationCode || l.locationName);
      const resolvedData = topLocs.map(l => l.resolvedTickets);
      const unresolvedData = topLocs.map(l => l.unresolvedTickets);

      this.locationBarChart = new Chart(locCanvas, {
        type: 'bar',
        data: {
          labels,
          datasets: [
            {
              label: 'Resolved Tickets',
              data: resolvedData,
              backgroundColor: '#10b981',
              borderRadius: 6
            },
            {
              label: 'Unresolved Tickets',
              data: unresolvedData,
              backgroundColor: '#f43f5e',
              borderRadius: 6
            }
          ]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          plugins: {
            legend: {
              position: 'top',
              labels: { font: { family: 'Plus Jakarta Sans', weight: '600', size: 12 }, usePointStyle: true, padding: 16 }
            },
            tooltip: {
              padding: 12,
              cornerRadius: 10,
              callbacks: {
                footer: (items: any) => {
                  let tot = 0;
                  items.forEach((i: any) => { tot += i.parsed.y; });
                  return `Total: ${tot} tickets`;
                }
              }
            }
          },
          scales: {
            x: {
              stacked: true,
              grid: { display: false },
              ticks: { font: { family: 'Plus Jakarta Sans', weight: '600' } }
            },
            y: {
              stacked: true,
              beginAtZero: true,
              grid: { color: '#f1f5f9' },
              ticks: { precision: 0 }
            }
          }
        }
      });
    }

    // 2. Secondary Chart: Department / Module Doughnut Chart
    const deptCanvas = document.getElementById('overview-dept-chart') as HTMLCanvasElement;
    if (deptCanvas) {
      if (this.deptDoughnutChart) this.deptDoughnutChart.destroy();

      const depts = this.overviewData.departments.filter(d => d.totalTickets > 0);
      const labels = depts.map(d => d.moduleName);
      const data = depts.map(d => d.totalTickets);
      const colors = ['#6366f1', '#3b82f6', '#06b6d4', '#10b981', '#f59e0b', '#ec4899'];

      this.deptDoughnutChart = new Chart(deptCanvas, {
        type: 'doughnut',
        data: {
          labels,
          datasets: [{
            data,
            backgroundColor: colors.slice(0, depts.length),
            borderWidth: 2,
            borderColor: '#ffffff'
          }]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          cutout: '68%',
          plugins: {
            legend: {
              position: 'right',
              labels: { font: { family: 'Plus Jakarta Sans', weight: '600', size: 12 }, usePointStyle: true, padding: 14 }
            },
            tooltip: {
              padding: 12,
              cornerRadius: 10
            }
          }
        }
      });
    }
  }

  private renderLocationCharts(): void {
    if (typeof Chart === 'undefined' || !this.locationDetail) return;

    // Location Department Bar Chart
    const c1 = document.getElementById('loc-dept-chart') as HTMLCanvasElement;
    if (c1) {
      if (this.drilldownChart1) this.drilldownChart1.destroy();
      const depts = this.locationDetail.departments || [];
      this.drilldownChart1 = new Chart(c1, {
        type: 'bar',
        data: {
          labels: depts.map((d: any) => d.moduleName),
          datasets: [
            { label: 'Resolved', data: depts.map((d: any) => d.resolvedTickets), backgroundColor: '#10b981', borderRadius: 4 },
            { label: 'Unresolved', data: depts.map((d: any) => d.unresolvedTickets), backgroundColor: '#f43f5e', borderRadius: 4 }
          ]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          scales: { x: { stacked: true }, y: { stacked: true, beginAtZero: true } }
        }
      });
    }
  }

  private renderCustomerCharts(): void {
    if (typeof Chart === 'undefined' || !this.customerDetail) return;

    const c1 = document.getElementById('cust-dept-chart') as HTMLCanvasElement;
    if (c1) {
      if (this.drilldownChart1) this.drilldownChart1.destroy();
      const depts = this.customerDetail.departmentBreakdown || [];
      this.drilldownChart1 = new Chart(c1, {
        type: 'doughnut',
        data: {
          labels: depts.map((d: any) => d.moduleName),
          datasets: [{
            data: depts.map((d: any) => d.totalTickets),
            backgroundColor: ['#6366f1', '#3b82f6', '#10b981', '#f59e0b', '#8b5cf6'],
            borderWidth: 2
          }]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          cutout: '62%'
        }
      });
    }
  }

  private renderUserCharts(): void {
    if (typeof Chart === 'undefined' || !this.userDetail) return;

    const c1 = document.getElementById('user-trend-chart') as HTMLCanvasElement;
    if (c1) {
      if (this.drilldownChart1) this.drilldownChart1.destroy();
      const trend = this.userDetail.monthlyTrend || [];
      this.drilldownChart1 = new Chart(c1, {
        type: 'line',
        data: {
          labels: trend.map((t: any) => t.label),
          datasets: [
            {
              label: 'Total Tickets',
              data: trend.map((t: any) => t.totalTickets),
              borderColor: '#6366f1',
              backgroundColor: 'rgba(99, 102, 241, 0.1)',
              fill: true,
              tension: 0.35,
              borderWidth: 3
            },
            {
              label: 'Resolved',
              data: trend.map((t: any) => t.resolvedTickets),
              borderColor: '#10b981',
              borderWidth: 3,
              tension: 0.35
            }
          ]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          scales: { y: { beginAtZero: true } }
        }
      });
    }
  }

  private renderTeamCharts(): void {
    if (typeof Chart === 'undefined' || !this.teamDetail) return;

    // 1. Primary Chart: Member-Level Ticket Resolution Breakdown (Stacked/Grouped Bar Chart)
    const c1 = document.getElementById('team-member-chart') as HTMLCanvasElement;
    if (c1) {
      if (this.drilldownChart1) this.drilldownChart1.destroy();
      const members = this.teamDetail.members || [];
      this.drilldownChart1 = new Chart(c1, {
        type: 'bar',
        data: {
          labels: members.map((m: any) => m.name || m.userName),
          datasets: [
            {
              label: 'Solved / Resolved Tickets',
              data: members.map((m: any) => m.resolved),
              backgroundColor: '#10b981',
              borderRadius: 6
            },
            {
              label: 'Unresolved / Pending Tickets',
              data: members.map((m: any) => m.unresolved),
              backgroundColor: '#f43f5e',
              borderRadius: 6
            }
          ]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          plugins: {
            legend: {
              position: 'top',
              labels: { font: { family: 'Plus Jakarta Sans', weight: '600', size: 12 }, usePointStyle: true, padding: 14 }
            },
            tooltip: {
              callbacks: {
                afterBody: (items: any) => {
                  const idx = items[0]?.dataIndex;
                  if (idx !== undefined && members[idx]) {
                    const m = members[idx];
                    return `Resolution Rate: ${m.resolutionRate}%\nRole: ${m.roleName}\nLocation: ${m.locationName || 'N/A'}`;
                  }
                  return '';
                }
              }
            }
          },
          scales: {
            x: {
              stacked: true,
              grid: { display: false },
              ticks: { font: { family: 'Plus Jakarta Sans', weight: '600' } }
            },
            y: {
              stacked: true,
              beginAtZero: true,
              grid: { color: '#f1f5f9' },
              ticks: { precision: 0 }
            }
          }
        }
      });
    }

    // 2. Secondary Chart: Department / Module Distribution (Doughnut Chart)
    const c2 = document.getElementById('team-dept-chart') as HTMLCanvasElement;
    if (c2) {
      if (this.drilldownChart2) this.drilldownChart2.destroy();
      const depts = this.teamDetail.departmentBreakdown || [];
      this.drilldownChart2 = new Chart(c2, {
        type: 'doughnut',
        data: {
          labels: depts.map((d: any) => d.moduleName),
          datasets: [{
            data: depts.map((d: any) => d.totalTickets),
            backgroundColor: ['#6366f1', '#3b82f6', '#10b981', '#f59e0b', '#ec4899', '#8b5cf6'],
            borderWidth: 2
          }]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          cutout: '62%',
          plugins: {
            legend: {
              position: 'right',
              labels: { font: { family: 'Plus Jakarta Sans', weight: '600', size: 11 }, usePointStyle: true }
            }
          }
        }
      });
    }
  }

  // =====================================================================
  // NAVIGATION HELPERS
  // =====================================================================

  public backToOverview(): void {
    this.activeView = 'overview';
    this.selectedTeamId = null;
    this.teamDetail = null;
    this.destroyCharts();
    setTimeout(() => this.renderOverviewCharts(), 50);
    this.cdr.detectChanges();
  }

  public setTab(tab: 'locations' | 'customers' | 'departments' | 'teams' | 'users'): void {
    this.activeTab = tab;
    this.searchQuery = '';
    if (tab === 'locations') {
      setTimeout(() => this.renderOverviewCharts(), 50);
    }
  }

  public setLocationSubTab(subTab: 'departments' | 'teams' | 'users' | 'customers' | 'tickets'): void {
    this.locationSubTab = subTab;
  }

  public toggleDeptAccordion(moduleId: number): void {
    this.expandedDeptId = this.expandedDeptId === moduleId ? null : moduleId;
  }

  public toggleTeamAccordion(roleId: number): void {
    this.expandedTeamId = this.expandedTeamId === roleId ? null : roleId;
  }

  // =====================================================================
  // FILTERING & SORTING COMPUTED GETTERS
  // =====================================================================

  public get filteredLocations(): LocationSummary[] {
    if (!this.overviewData?.locations) return [];
    let list = [...this.overviewData.locations];

    if (this.filterActiveOnly) {
      list = list.filter(l => l.totalTickets > 0 || l.userCount > 0);
    }

    if (this.searchQuery.trim()) {
      const q = this.searchQuery.toLowerCase();
      list = list.filter(l =>
        l.locationName.toLowerCase().includes(q) ||
        l.locationCode.toLowerCase().includes(q)
      );
    }

    switch (this.sortBy) {
      case 'tickets':
        list.sort((a, b) => b.totalTickets - a.totalTickets);
        break;
      case 'resolved':
        list.sort((a, b) => b.resolvedTickets - a.resolvedTickets);
        break;
      case 'rate':
        list.sort((a, b) => b.resolutionRate - a.resolutionRate);
        break;
      case 'name':
        list.sort((a, b) => a.locationName.localeCompare(b.locationName));
        break;
    }

    return list;
  }

  public get filteredCustomers(): CustomerStat[] {
    if (!this.overviewData?.topCustomers) return [];
    let list = [...this.overviewData.topCustomers];
    if (this.searchQuery.trim()) {
      const q = this.searchQuery.toLowerCase();
      list = list.filter(c =>
        c.customerName.toLowerCase().includes(q) ||
        c.customerCode.toLowerCase().includes(q)
      );
    }
    return list;
  }

  public get filteredUsers(): UserRank[] {
    if (!this.overviewData?.topUsers) return [];
    let list = [...this.overviewData.topUsers];
    if (this.searchQuery.trim()) {
      const q = this.searchQuery.toLowerCase();
      list = list.filter(u =>
        u.userName.toLowerCase().includes(q) ||
        u.locationName.toLowerCase().includes(q) ||
        u.roleName.toLowerCase().includes(q)
      );
    }
    return list;
  }

  public get filteredTeams(): TeamStat[] {
    if (!this.overviewData?.teams) return [];
    let list = [...this.overviewData.teams];
    if (this.searchQuery.trim()) {
      const q = this.searchQuery.toLowerCase();
      list = list.filter(t =>
        (t.managerName && t.managerName.toLowerCase().includes(q)) ||
        (t.roleName && t.roleName.toLowerCase().includes(q)) ||
        (t.managerRole && t.managerRole.toLowerCase().includes(q))
      );
    }
    return list;
  }

  // =====================================================================
  // UI UTILITIES
  // =====================================================================

  public getRateColor(rate: number): string {
    if (rate >= 70) return '#10b981';
    if (rate >= 40) return '#f59e0b';
    return '#ef4444';
  }

  public getStatusBadgeClass(status: string): string {
    const s = (status || '').toLowerCase();
    if (s === 'closed' || s === 'resolved') return 'badge-resolved';
    if (s === 'in progress' || s === 'rework') return 'badge-progress';
    if (s === 'open') return 'badge-open';
    return 'badge-neutral';
  }

  public getPriorityBadgeClass(priority: string): string {
    const p = (priority || '').toLowerCase();
    if (p === 'urgent') return 'badge-urgent';
    if (p === 'high') return 'badge-high';
    if (p === 'medium') return 'badge-medium';
    return 'badge-low';
  }

  public getUserInitials(name: string): string {
    if (!name) return 'U';
    const parts = name.trim().split(' ');
    if (parts.length >= 2) {
      return (parts[0][0] + parts[1][0]).toUpperCase();
    }
    return name.slice(0, 2).toUpperCase();
  }
}