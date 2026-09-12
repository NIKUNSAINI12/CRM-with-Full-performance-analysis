import { Component, OnInit, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TicketService } from '../../Core/services/ticket.service';
import { TICKET_SERVICE_TOKEN } from '../../Core/injection-tokens';

@Component({
  selector: 'app-status-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './status-list.html'
})
export class StatusListComponent implements OnInit {
  activeTab: 'pm' | 'developer' = 'pm';
  statuses: any[] = [];
  isLoading = true;
  newStatusName = '';
  isSubmitting = false;

  constructor(@Inject(TICKET_SERVICE_TOKEN) private ticketService: TicketService) {}

  ngOnInit() {
    this.loadStatuses();
  }

  setTab(tab: 'pm' | 'developer') {
    this.activeTab = tab;
    this.statuses = [];
    this.newStatusName = '';
    this.loadStatuses();
  }

  async loadStatuses() {
    try {
      this.isLoading = true;
      if (this.activeTab === 'pm') {
        this.statuses = await this.ticketService.getCustomStatuses();
      } else {
        this.statuses = await this.ticketService.getCustomDeveloperStatuses();
      }
    } catch (e) {
      console.error(e);
    } finally {
      this.isLoading = false;
    }
  }

  async addStatus() {
    if (!this.newStatusName.trim()) return;
    try {
      this.isSubmitting = true;
      if (this.activeTab === 'pm') {
        await this.ticketService.createCustomStatus({
          statusName: this.newStatusName,
          notifyCustomer: false,
          notifyPM: false,
          notifyAssignee: false
        });
      } else {
        await this.ticketService.createCustomDeveloperStatus({
          statusName: this.newStatusName
        });
      }
      this.newStatusName = '';
      await this.loadStatuses();
    } catch (e) {
      console.error(e);
      const err = e as any;
      const errorMsg = err.error?.message || err.error || 'Failed to add status.';
      alert(errorMsg);
    } finally {
      this.isSubmitting = false;
    }
  }

  async deleteStatus(id: number) {
    const statusType = this.activeTab === 'pm' ? 'ticket status' : 'developer status';
    if (!confirm(`Are you sure you want to delete this ${statusType}?`)) return;
    try {
      if (this.activeTab === 'pm') {
        await this.ticketService.deleteCustomStatus(id);
      } else {
        await this.ticketService.deleteCustomDeveloperStatus(id);
      }
      await this.loadStatuses();
    } catch (e) {
      console.error(e);
      alert('Failed to delete status. It might be in use.');
    }
  }

  async updateStatusFlags(s: any) {
    if (this.activeTab !== 'pm') return; // Email triggers only for PM statuses currently
    try {
      await this.ticketService.updateCustomStatus(s.id, {
        statusName: s.statusName,
        notifyCustomer: s.notifyCustomer,
        notifyPM: s.notifyPM,
        notifyAssignee: s.notifyAssignee
      });
    } catch (e) {
      console.error(e);
      alert('Failed to update email triggers.');
      await this.loadStatuses(); // revert UI
    }
  }
}
