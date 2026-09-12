import { Component, OnInit, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TicketService } from '../../Core/services/ticket.service';
import { TICKET_SERVICE_TOKEN } from '../../Core/injection-tokens';

@Component({
  selector: 'app-category-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './category-list.html'
})
export class CategoryListComponent implements OnInit {
  categories: any[] = [];
  isLoading = true;
  newCategoryName = '';
  isSubmitting = false;

  constructor(@Inject(TICKET_SERVICE_TOKEN) private ticketService: TicketService) {}

  ngOnInit() {
    this.loadCategories();
  }

  async loadCategories() {
    try {
      this.isLoading = true;
      this.categories = await this.ticketService.getIssueCategories();
    } catch (e) {
      console.error(e);
    } finally {
      this.isLoading = false;
    }
  }

  async addCategory() {
    if (!this.newCategoryName.trim()) return;
    try {
      this.isSubmitting = true;
      await this.ticketService.createIssueCategory({
        categoryName: this.newCategoryName
      });
      this.newCategoryName = '';
      await this.loadCategories();
    } catch (e) {
      console.error(e);
    } finally {
      this.isSubmitting = false;
    }
  }

  async deleteCategory(id: number) {
    if (!confirm('Are you sure you want to delete this category?')) return;
    try {
      await this.ticketService.deleteIssueCategory(id);
      await this.loadCategories();
    } catch (e) {
      console.error(e);
      alert('Failed to delete category. It might be in use.');
    }
  }
}
