import { Component, OnInit, Inject, ElementRef, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TICKET_SERVICE_TOKEN } from '../Core/injection-tokens';
import { AssigneePerformanceStat, TicketService } from '../Core/services/ticket.service';


// =====================================================================
// SCORING PILLARS (total = 100 pts)
// =====================================================================
// P1 (35%) — Permanence Rate  : stayedClosed / assigned (difficulty-weighted)
//             Did the work stick? No rework = full permanence.
// P2 (25%) — Quality Rate     : 100 - reworkRate (difficulty-weighted)
//             First-time-right. No rework = full 25 pts.
// P3 (20%) — Deadline Rate    : 100 - deadlineMissRate (difficulty-weighted)
//             On-time delivery. No deadlines set → 0 miss → full 20 pts.
// P4 (10%) — Clearance Rate   : closed / (closed + open + pmReview) × 100
//             Backlog management — clearing the queue.
// P5 (10%) — Difficulty Bonus : (avgDifficultyWeight − 1) / 4 × 100
//             Reward devs who tackle Expert/Hard tickets.
//             Easy=1 → 0 pts, Medium=2 → 25 pts, Expert=5 → 100 pts.
// =====================================================================

export interface RankedAssignee extends AssigneePerformanceStat {
  score:         number;   // 0–100
  scoreTier:     'Elite' | 'Strong' | 'Developing' | 'Needs Focus' | 'Critical' | 'No Tickets';
  tierIcon:      string;
  // Raw pillar values (0–100 before weight)
  p1Permanence:  number;
  p2Quality:     number;
  p3Deadline:    number;
  p4Clearance:   number;
  p5Difficulty:  number;
  // Weighted contribution of each pillar
  w1: number; w2: number; w3: number; w4: number; w5: number;
}

@Component({
  selector: 'app-performance-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './performance-dashboard.html',
  styleUrls: ['./performance-dashboard.scss']
})
export class PerformanceDashboardComponent implements OnInit {
  public rankedAssignees: RankedAssignee[] = [];
  public isLoading = true;
  public isFullscreen = false;
  public showFsModal = false;
  public fsViewMode: 'detailed' | 'positioning' = 'detailed';

  // Filter state
  public activeFilter: 'currentMonth' | 'lastMonth' | 'currentYear' | 'lastYear' = 'currentMonth';
  public selectedMonth: string = '';

  // Sorting
  public sortField: string = 'score';
  public sortDirection: 'asc' | 'desc' = 'desc';

  constructor(
    @Inject(TICKET_SERVICE_TOKEN) private ticketService: TicketService,
    private el: ElementRef
  ) {}

  @HostListener('document:fullscreenchange')
  onFullscreenChange(): void {
    this.isFullscreen = !!document.fullscreenElement;
    if (!this.isFullscreen) this.showFsModal = false;
  }

  openFsModal(): void {
    this.showFsModal = true;
  }

  closeFsModal(): void {
    this.showFsModal = false;
  }

  async enterFullscreen(mode: 'detailed' | 'positioning'): Promise<void> {
    this.fsViewMode = mode;
    this.showFsModal = false;
    await this.el.nativeElement.requestFullscreen();
  }

  async toggleFullscreen(): Promise<void> {
    if (document.fullscreenElement) {
      await document.exitFullscreen();
    }
  }

  ngOnInit(): void {
    this.loadCurrentMonthStats();
  }

  // ===================================================================
  // FILTER METHODS
  // ===================================================================

  async loadCurrentMonthStats(): Promise<void> {
    this.activeFilter = 'currentMonth';
    const now = new Date();
    await this.load(now.getMonth() + 1, now.getFullYear());
  }

  async loadLastMonthStats(): Promise<void> {
    this.activeFilter = 'lastMonth';
    const now = new Date();
    const d = new Date(now.getFullYear(), now.getMonth() - 1, 1);
    await this.load(d.getMonth() + 1, d.getFullYear());
  }

  async loadCurrentYearStats(): Promise<void> {
    this.activeFilter = 'currentYear';
    await this.load(null, new Date().getFullYear());
  }

  async loadLastYearStats(): Promise<void> {
    this.activeFilter = 'lastYear';
    await this.load(null, new Date().getFullYear() - 1);
  }

  applyCustomFilter(): void {
    if (!this.selectedMonth) return;
    const [year, month] = this.selectedMonth.split('-').map(Number);
    this.activeFilter = 'currentMonth';
    this.load(month, year);
  }

  onCustomMonthChange(): void {}

  // ===================================================================
  // CORE DATA LOADER
  // ===================================================================

  private async load(month?: number | null, year?: number | null): Promise<void> {
    this.isLoading = true;
    try {
      const stats = await this.ticketService.getAssigneePerformanceStats(month, year);
      this.rankedAssignees = stats
        .map(s => this.computeScore(s))
        .sort((a, b) => b.score - a.score);
    } catch (err) {
      console.error('Failed to load performance stats', err);
      this.rankedAssignees = [];
    } finally {
      this.isLoading = false;
    }
  }

  // ===================================================================
  // ROBUST 5-PILLAR SCORING ENGINE
  // ===================================================================

  private clamp(v: number, min = 0, max = 100): number {
    return Math.max(min, Math.min(max, v));
  }

  private computeScore(s: AssigneePerformanceStat): RankedAssignee {
    const hasData = s.totalTicketsCount > 0;

    // P1: Permanence (35%) — difficulty-weighted stayedClosed / assigned
    const p1 = hasData && s.weightedAssigned > 0
      ? this.clamp((s.weightedStayedClosed / s.weightedAssigned) * 100)
      : 0;

    // P2: Quality/First-Time-Right (25%) — invert reworkRate
    const p2 = hasData ? this.clamp(100 - s.reworkRate) : 0;

    // P3: Deadline Discipline (20%) — invert deadlineMissRate
    const p3 = hasData ? this.clamp(100 - s.deadlineMissRate) : 0;

    // P4: Backlog Clearance (10%) — closureRate from SP
    const p4 = hasData ? this.clamp(s.clearanceRate) : 0;

    // P5: Difficulty Bonus (Removed, defaults to 0)
    const p5 = 0;

    // Weighted pillar contributions
    const w1 = p1 * 0.40;
    const w2 = p2 * 0.30;
    const w3 = p3 * 0.20;
    const w4 = p4 * 0.10;
    const w5 = 0;

    const score = hasData ? Math.round(w1 + w2 + w3 + w4) : 0;

    // Tier classification
    let scoreTier: RankedAssignee['scoreTier'];
    let tierIcon: string;
    if (!hasData) {
      scoreTier = 'No Tickets';
      tierIcon = '⚪';
    } else if (score >= 85) {
      scoreTier = 'Elite';
      tierIcon = '🏆';
    } else if (score >= 70) {
      scoreTier = 'Strong';
      tierIcon = '⭐';
    } else if (score >= 55) {
      scoreTier = 'Developing';
      tierIcon = '📈';
    } else if (score >= 40) {
      scoreTier = 'Needs Focus';
      tierIcon = '⚠️';
    } else {
      scoreTier = 'Critical';
      tierIcon = '🔴';
    }

    return {
      ...s,
      score, scoreTier, tierIcon,
      p1Permanence: Math.round(p1), p2Quality:   Math.round(p2),
      p3Deadline:   Math.round(p3), p4Clearance: Math.round(p4),
      p5Difficulty: 0,
      w1: Math.round(w1), w2: Math.round(w2), w3: Math.round(w3),
      w4: Math.round(w4), w5: 0
    } as RankedAssignee;
  }

  // ===================================================================
  // SORTING
  // ===================================================================

  onSort(field: string): void {
    if (this.sortField === field) {
      this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortField = field;
      this.sortDirection = 'desc';
    }
    this.rankedAssignees.sort((a, b) => {
      const av = (a as any)[field] ?? 0;
      const bv = (b as any)[field] ?? 0;
      return this.sortDirection === 'asc' ? av - bv : bv - av;
    });
  }

  sortIcon(field: string): string {
    if (this.sortField !== field) return '↕';
    return this.sortDirection === 'asc' ? '↑' : '↓';
  }

  // ===================================================================
  // TEMPLATE HELPERS
  // ===================================================================

  getDifficultyLabel(avgWeight: number): string {
    if (avgWeight >= 4.5) return 'Expert';
    if (avgWeight >= 2.5) return 'Hard';
    if (avgWeight >= 1.5) return 'Medium';
    return 'Easy';
  }

  getDifficultyColor(avgWeight: number): string {
    if (avgWeight >= 4.5) return '#ef4444';
    if (avgWeight >= 2.5) return '#f97316';
    if (avgWeight >= 1.5) return '#eab308';
    return '#22c55e';
  }

  getTierClass(tier: string): string {
    return tier.toLowerCase().replace(' ', '-');
  }

  getScoreColor(score: number): string {
    if (score >= 85) return '#22c55e';
    if (score >= 70) return '#84cc16';
    if (score >= 55) return '#eab308';
    if (score >= 40) return '#f97316';
    return '#ef4444';
  }
}