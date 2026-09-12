  using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using ticketing_system_backend.Models;

namespace ticketing_system_backend.Interface
{
  public interface IManager
  {
    // --- Dashboard & Analysis ---
    Task<DashboardStats> GetDashboardStatsAsync(int? pmId = null);
    Task<TicketTimelineAnalysis> GetTicketTimelineAnalysisAsync(int ticketId);
    Task<DashboardStats> GetDeveloperDashboardStatsAsync(int assignedToId);

    // --- Product Management ---
    Task<IEnumerable<Product>> GetAllProductsAsync();
    Task<Product> GetProductByIdAsync(int id);
    Task<Product> CreateProductAsync(Product product);
    Task UpdateProductAsync(int id, Product product);
    Task DeleteProductAsync(int id);
    Task<IEnumerable<ProductData>> GetProductHierarchyAsync();
    Task<int> CreateOrUpdateProductAsync(ProductData product);
    Task<int> CreateProductSimpleAsync(SimpleProductRequest product);

    // --- Ticket Management ---
    Task<TicketListResponse<TicketDashboardView>> GetAllTicketsAsync(
        string? status, string? priority, int? customerId, string? assignedToId, int? createdByUserId,
        DateTime? dateFrom, DateTime? dateTo, int pageNumber, int pageSize, int? userId = null, string? userRole = null);
    Task<TicketListResponse<TicketDashboardView>> GetDelayedTicketsAsync(int pageNumber, int pageSize, int? pmId = null);
    Task<TicketListResponse<TicketDashboardView>> GetDelayedTicketsForDeveloperAsync(int developerId, int pageNumber, int pageSize);

    // --- Ticket Updates ---
    Task UpdateStatusAsync(int ticketId, string status, int? userId = null, string? userRole = null, string? note = null);
    Task UpdateAssignmentAsync(int ticketId, int? assignedToId, int userId, string userRole); 
    Task UpdateTicketByPmAsync(int ticketId, int pmId, PmTicketUpdateRequest request);
    Task UpdateTicketByCustomerAsync(int ticketId, int customerId, CustomerUpdateTicketRequest request);
    Task UpdateTicketByDeveloperAsync(int ticketId, DeveloperUpdateRequest request); 
    Task SubmitForReviewAsync(int ticketId, int changedByUserId, string userRole); 
    Task SetTicketDeadlineAsync(int ticketId, SetDeadlineRequest request);
    Task<TicketDeadline> GetTicketDeadlineAsync(int ticketId);

    // --- User Management ---
    Task<IEnumerable<User>> GetUsersByRoleAsync(string role);
    Task<User> CreateUserAsync(CreateUserRequest request);
    Task UpdateUserAsync(int id, UpdateUserRequest request);
    Task<User> GetUserByIdAsync(int id, string? role = null);
    Task DeleteUserAsync(int id);
    Task<IEnumerable<int>> GetUserProductsAsync(int userId);
    Task UpdatePmAsync(int id, UpdatePmRequest request);
    Task<User> FindCustomerByNumberAsync(string customerNumber);
    Task<CommunicationAttachment?> GetCommunicationAttachmentByIdAsync(int communicationId, int attachmentId);
    Task<IEnumerable<int>> GetPmIdsForCustomerAsync(int customerId);
    Task<Assignee?> GetExecutiveByCustomerIdAsync(int customerId);
    Task<IEnumerable<int>> GetCustomerIdsByAssigneeIdAsync(int assigneeId);
    Task<IEnumerable<int>> GetCustomerIdsByPmIdAsync(int pmId);
    Task<IEnumerable<User>> GetCustomersByAssigneeIdAsync(int assigneeId);

    // --- Assignee Management ---
    Task<IEnumerable<Assignee>> GetAllAssigneesAsync(int? pmId = null);
    Task<Assignee> GetAssigneeByIdAsync(int id);
    Task<Assignee> CreateAssigneeAsync(CreateAssigneeRequest request);
    Task UpdateAssigneeAsync(int id, UpdateAssigneeRequest request);

    // --- Rework & Performance ---
    Task<IEnumerable<TicketReworkInfo>> GetTicketReworkCountByUserAsync(int ticketId);
    Task<TotalReworkCounts> GetTotalReworkCountForAssigneeAsync(int assigneeId);
    Task<IEnumerable<AssigneePerformanceStat>> GetAssigneePerformanceStatsAsync(int? filterMonth = null, int? filterYear = null, int? pmId = null);

    // --- Communication Methods ---
    Task<PmCommunicationResponse> GetPmCommunicationsAsync(int ticketId);
    Task<List<TicketCommunication>> GetChannelCommunicationsAsync(int ticketId, string channel); 
    Task<TicketCommunication> AddCommunicationAsync(int ticketId, NewCommentRequest newComment, List<IFormFile> files);
    Task CreateHistoryEntryAsync(int ticketId, int userId, string userName, string userRole, string eventDescription); 

    // --- Parent-Child Relations ---
    Task<TicketRelationsResponse> GetTicketRelationsAsync(int ticketId);
    Task SetTicketParentAsync(int childTicketId, int? parentTicketId);

    // Custom Statuses
    Task<IEnumerable<CustomTicketStatus>> GetCustomStatusesAsync();
    Task<CustomTicketStatus> CreateCustomStatusAsync(string statusName);
    Task DeleteCustomStatusAsync(int id);

    // Custom Developer Statuses
    Task<IEnumerable<CustomTicketStatus>> GetCustomDeveloperStatusesAsync();
    Task<CustomTicketStatus> CreateCustomDeveloperStatusAsync(string statusName);
    Task DeleteCustomDeveloperStatusAsync(int id);

    // Priorities
    Task<IEnumerable<PriorityMaster>> GetPrioritiesAsync();
    Task<PriorityMaster> CreatePriorityAsync(string priorityName, int tatHours, int? responseSLA);
    Task DeletePriorityAsync(int id);

    // Issue Categories
    Task<IEnumerable<IssueCategoryMaster>> GetIssueCategoriesAsync();
    Task<IEnumerable<dynamic>> GetTicketSourcesAsync();
    Task<int> AddTicketSourceAsync(string sourceName);
    Task DeleteTicketSourceAsync(int id);
    Task<IssueCategoryMaster> CreateIssueCategoryAsync(string categoryName);
    Task DeleteIssueCategoryAsync(int id);
    
    // TAT Dashboard
    Task<TatDashboardStats> GetTatDashboardStatsAsync(int pmId, string? relationshipFilter = null, DateTime? fromDate = null, DateTime? toDate = null, string? statusFilter = null);
  }

  public class TicketListResponse<T>
  {
    public IEnumerable<T> Tickets { get; set; }
    public int TotalCount { get; set; }
  }
}
