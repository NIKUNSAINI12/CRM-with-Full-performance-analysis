import { Component, OnInit, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TicketService } from '../../Core/services/ticket.service';
import { TICKET_SERVICE_TOKEN } from '../../Core/injection-tokens';

@Component({
  selector: 'app-source-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './source-list.html'
})
export class SourceListComponent implements OnInit {
  sources: any[] = [];
  isLoading = true;
  newSourceName = '';
  isSubmitting = false;

  constructor(@Inject(TICKET_SERVICE_TOKEN) private ticketService: TicketService) {}

  ngOnInit() {
    this.loadSources();
  }

  async loadSources() {
    try {
      this.isLoading = true;
      this.sources = await this.ticketService.getTicketSources();
    } catch (e) {
      console.error(e);
    } finally {
      this.isLoading = false;
    }
  }

  async addSource() {
    if (!this.newSourceName.trim()) return;
    try {
      this.isSubmitting = true;
      await this.ticketService.addTicketSource(this.newSourceName.trim());
      this.newSourceName = '';
      await this.loadSources();
    } catch (e) {
      console.error(e);
    } finally {
      this.isSubmitting = false;
    }
  }

  async deleteSource(id: number) {
    if (!confirm('Are you sure you want to delete this ticket source?')) return;
    try {
      await this.ticketService.deleteTicketSource(id);
      await this.loadSources();
    } catch (e) {
      console.error(e);
      alert('Failed to delete ticket source. It might be in use.');
    }
  }
}
