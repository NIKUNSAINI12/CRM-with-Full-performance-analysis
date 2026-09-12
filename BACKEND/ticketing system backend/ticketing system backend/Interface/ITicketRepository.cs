using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using ticketing_system_backend.Models;

namespace ticketing_system_backend.API.Interfaces
{
  public interface ITicketRepository
  {
    Task<Ticket> CreateAsync(CreateTicketRequest ticketRequest);
    Task<TicketResponse> GetAllForCustomerAsync(int customerId, string? status = null, string? priority = null, string? product = null, string? subProduct = null, string? assignedTo = null, DateTime? dateFrom = null, DateTime? dateTo = null);
    Task<Ticket?> GetByIdAsync(int id);
    Task<TicketResponse> GetByStatusAsync(string status);

    Task<IEnumerable<Product>> GetProductsByCustomerIdAsync(int customerId);
    Task<TicketAttachment?> GetAttachmentAsync(int ticketId, int attachmentId);
    Task<TicketAttachment> SaveAttachmentAsync(
          IFormFile file,
          int ticketId,
          int uploadedById,
          IDbConnection connection,
          IDbTransaction transaction);
    Task<IEnumerable<ProductData>> GetProductHierarchyAsync(int customerId);
    Task UpdateAsync(int ticketId, UpdateTicketRequest request);
    Task<IEnumerable<TicketHistory>> GetTicketHistoryAsync(int ticketId);
    Task<IEnumerable<dynamic>> GetDocketsAsync(string? search = null, int? customerId = null);
    Task<object?> GetDocketDetailsAsync(string docketNo);
    Task<bool> UpdateRatingAsync(int ticketId, int rating);
    Task<IEnumerable<dynamic>> SearchGlobalTicketsAsync(string query, string searchType = "auto");
  }
}
