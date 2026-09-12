import { Component, OnInit, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TicketService } from '../../Core/services/ticket.service';
import { TICKET_SERVICE_TOKEN } from '../../Core/injection-tokens';

@Component({
  selector: 'app-priority-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './priority-list.html'
})
export class PriorityListComponent implements OnInit {
  priorities: any[] = [];
  isLoading = true;
  newPriorityName = '';
  newTatHours: number | null = null;
  isSubmitting = false;

  constructor(@Inject(TICKET_SERVICE_TOKEN) private ticketService: TicketService) {}

  ngOnInit() {
    this.loadPriorities();
  }

  async loadPriorities() {
    try {
      this.isLoading = true;
      this.priorities = await this.ticketService.getPriorities();
    } catch (e) {
      console.error(e);
    } finally {
      this.isLoading = false;
    }
  }

  async addPriority() {
    if (!this.newPriorityName.trim() || !this.newTatHours) return;
    try {
      this.isSubmitting = true;
      await this.ticketService.createPriority({
        priorityName: this.newPriorityName,
        tatHours: this.newTatHours
      });
      this.newPriorityName = '';
      this.newTatHours = null;
      await this.loadPriorities();
    } catch (e) {
      console.error(e);
    } finally {
      this.isSubmitting = false;
    }
  }

  async deletePriority(id: number) {
    if (!confirm('Are you sure you want to delete this priority?')) return;
    try {
      await this.ticketService.deletePriority(id);
      await this.loadPriorities();
    } catch (e) {
      console.error(e);
      alert('Failed to delete priority. It might be in use.');
    }
  }
}
