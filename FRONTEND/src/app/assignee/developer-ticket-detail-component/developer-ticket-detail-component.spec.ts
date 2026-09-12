import { Component, OnInit, Inject } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { firstValueFrom, switchMap } from 'rxjs';
import { TICKET_SERVICE_TOKEN } from '../../Core/injection-tokens';
import { TicketService } from '../../Core/services/ticket.service';
import { Product, Ticket, TicketAttachment } from '../../Core/models/ticket.model';


@Component({
  selector: 'app-developer-ticket-detail',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './developer-ticket-detail-component.html',
  styleUrls: ['./developer-ticket-detail-component.scss']
})
export class DeveloperTicketDetailComponent implements OnInit {
  public ticket: Ticket | null = null;
  public isLoading = true;
  public errorMessage: string | null = null;
  
  public productName: string = 'N/A';
  public customerName: string = 'N/A';

  constructor(
    @Inject(TICKET_SERVICE_TOKEN) private ticketService: TicketService,
    private route: ActivatedRoute,
    private location: Location
  ) {}

  ngOnInit(): void {
    // This data-loading logic remains the same
    // It fetches the ticket, product name, and customer name.
    this.isLoading = true;
    const ticketDetails$ = this.route.paramMap.pipe(
      switchMap(params => {
        const ticketId = params.get('id');
        if (!ticketId) {
          throw new Error('Ticket ID not found in URL.');
        }
        return this.ticketService.getTicketById(Number(ticketId));
      })
    );

    Promise.all([
      firstValueFrom(ticketDetails$),
      this.loadProducts(),
      this.ticketService.getTicketsForPM({ pageSize: 500 })
    ]).then(([ticketData, productsData, allTicketsResponse]) => {
      this.ticket = ticketData;
      this.setProductName(productsData);
      this.setCustomerNameFromList(allTicketsResponse.tickets, this.ticket?.customerId);
    }).catch(err => {
      this.errorMessage = 'Failed to load ticket details.';
      console.error(err);
    }).finally(() => {
      this.isLoading = false;
    });
  }

  private async loadProducts(): Promise<Product[]> {
    try {
      return await this.ticketService.getProducts();
    } catch (error) {
      console.error('Failed to load products:', error);
      return [];
    }
  }
  

  private setProductName(products: Product[]): void {
    if (this.ticket?.product && products.length > 0) {
      const foundProduct = products.find(p => p.id.toString() === this.ticket!.product!.toString());
      this.productName = foundProduct ? foundProduct.name : 'Unknown Product';
    }
  }

  private setCustomerNameFromList(allTickets: Ticket[], customerId?: number): void {
    if (!customerId || !allTickets || allTickets.length === 0) {
      this.customerName = 'Unknown';
      return;
    }
    const foundTicket = allTickets.find(ticket => ticket.customerId === customerId);
    this.customerName = foundTicket?.customerName || 'Unknown';
  }

  // Add this method to your DeveloperTicketDetailComponent class

public async downloadAttachment(attachment: any): Promise<void> {
    if (!this.ticket) return;
    this.errorMessage = null; // Clear previous errors
    try {
      // Ensure attachment.id is a string if your service expects it
      const attachmentId = attachment.id.toString(); 
      const blob = await this.ticketService.getTicketAttachment(this.ticket.id, attachmentId);
      
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = attachment.fileName;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      a.remove();
    } catch (error) {
      this.errorMessage = `Failed to download attachment: ${attachment.fileName}`;
      console.error(error);
    }
  }
  async submitForReview(): Promise<void> {
    if (!this.ticket) return;

    this.isLoading = true;
    try {
      // MODIFIED: Added this.developerId as the second argument
      await this.ticketService.submitTicketForReview(this.ticket.id, this.developerId);
      this.goBack();
    } catch (error) {
      this.errorMessage = 'Failed to submit ticket for review.';
      console.error('Error submitting for review:', error); // Good to log the error
    } finally {
      this.isLoading = false;
    }
  }

  goBack(): void {
    this.location.back();
  }
}