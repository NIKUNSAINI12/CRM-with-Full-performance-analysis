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
  newNotifyCustomer = false;
  newNotifyPM = false;
  newNotifyAssignee = false;
  newIsDefaultUnassigned = false;
  
  isSubmitting = false;

  constructor(@Inject(TICKET_SERVICE_TOKEN) private ticketService: TicketService) {}

  ngOnInit() {
    this.loadStatuses();
  }

  setTab(tab: 'pm' | 'developer') {
    this.activeTab = tab;
    this.statuses = [];
    this.newStatusName = '';
    this.newNotifyCustomer = false;
    this.newNotifyPM = false;
    this.newNotifyAssignee = false;
    this.newIsDefaultUnassigned = false;
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
          notifyCustomer: this.newNotifyCustomer,
          notifyPM: this.newNotifyPM,
          notifyAssignee: this.newNotifyAssignee,
          isDefaultUnassigned: this.newIsDefaultUnassigned
        });
      } else {
        await this.ticketService.createCustomDeveloperStatus({
          statusName: this.newStatusName
        });
      }
      this.newStatusName = '';
      this.newNotifyCustomer = false;
      this.newNotifyPM = false;
      this.newNotifyAssignee = false;
      this.newIsDefaultUnassigned = false;
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

  async toggleDefaultUnassigned(status: any) {
    const targetVal = !status.isDefaultUnassigned;
    if (targetVal) {
      this.statuses.forEach(s => s.isDefaultUnassigned = false);
    }
    status.isDefaultUnassigned = targetVal;
    
    try {
      await this.ticketService.updateCustomStatus(status.id, {
        statusName: status.statusName,
        notifyCustomer: status.notifyCustomer,
        notifyPM: status.notifyPM,
        notifyAssignee: status.notifyAssignee,
        isDefaultUnassigned: targetVal
      });
      await this.loadStatuses();
    } catch (e) {
      console.error(e);
      alert('Failed to update default status.');
      await this.loadStatuses();
    }
  }

  async toggleEmailTrigger(status: any, field: string) {
    status[field] = !status[field];
    try {
      if (this.activeTab === 'pm') {
        // Assuming your backend has an updateCustomStatus endpoint
        await (this.ticketService as any).updateCustomStatus(status.id, {
          statusName: status.statusName,
          notifyCustomer: status.notifyCustomer,
          notifyPM: status.notifyPM,
          notifyAssignee: status.notifyAssignee
        });
      }
    } catch (e) {
      console.error(e);
      alert('Failed to update status triggers.');
      // Revert on failure
      status[field] = !status[field];
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
}
