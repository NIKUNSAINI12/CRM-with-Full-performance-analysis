import { Component, Inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TICKET_SERVICE_TOKEN } from '../../Core/injection-tokens';
import { TicketService, ProductDatas } from '../../Core/services/ticket.service';

@Component({
  selector: 'app-product-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './product-list.html',
  styleUrls: ['./product-list.scss']
})
export class ProductListComponent implements OnInit {
  public products: ProductDatas[] = [];
  public filteredProducts: ProductDatas[] = [];
  public searchTerm: string = '';
  public isLoading = true;
  private pmId: number = 0; // Property to store the PM's ID

  constructor(
    @Inject(TICKET_SERVICE_TOKEN) private ticketService: TicketService,
    private router: Router,
    private route: ActivatedRoute // Inject ActivatedRoute
  ) {}

  ngOnInit(): void {
    // Read the 'pmId' from the URL's query parameters
    this.route.queryParamMap.subscribe(params => {
      this.pmId = Number(params.get('pmId'));
    });
    this.loadProducts();
  }

  async loadProducts(): Promise<void> {
    this.isLoading = true;
    try {
      this.products = await this.ticketService.getProductHierarchys();
      this.filteredProducts = [...this.products]; // Initialize filtered products
    } catch (error) {
      console.error('Failed to load product hierarchy', error);
    } finally {
      this.isLoading = false;
    }
  }

  editProduct(id: number): void {
    // Pass the pmId along to the edit form
    this.router.navigate(['/masterform/product/edit', id], {
      queryParams: { pmId: this.pmId }
    });
  }

  createNewProduct(): void {
    // Pass the pmId along to the create form
    this.router.navigate(['/masterform/product/new'], {
      queryParams: { pmId: this.pmId }
    });
  }

  goBack(): void {
    // Use the stored pmId to navigate back to the correct dashboard
    if (this.pmId) {
      this.router.navigate(['/pm/dashboard']);
    }
  }

  public toNumber(value: string): number {
    return Number(value);
  }

  // Calculate total sub-modules for a product
  public getTotalSubModules(product: ProductDatas): number {
    return product.modules.reduce((total, module) => total + module.subModules.length, 0);
  }

  // Search functionality methods
  public onSearchChange(): void {
    this.filterProducts();
  }

  public clearSearch(): void {
    this.searchTerm = '';
    this.filterProducts();
  }

  private filterProducts(): void {
    if (!this.searchTerm || this.searchTerm.trim() === '') {
      this.filteredProducts = [...this.products];
    } else {
      const searchTermLower = this.searchTerm.toLowerCase().trim();
      this.filteredProducts = this.products.filter(product => 
        product.name.toLowerCase().includes(searchTermLower)
      );
    }
  }
}