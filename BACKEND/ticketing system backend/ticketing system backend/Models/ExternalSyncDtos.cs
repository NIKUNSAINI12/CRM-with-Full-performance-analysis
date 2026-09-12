using System;
using System.Collections.Generic;

namespace ticketing_system_backend.Models
{
    // =====================================================================
    // BASE AUTH FIELDS (Supports username & password directly in payload)
    // =====================================================================
    public class SyncAuthFields
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
    }

    // =====================================================================
    // 1. CL_Master_Customer & CL_Master_Customer_Detail DTO
    // =====================================================================
    public class CustomerSyncDto : SyncAuthFields
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerNumber { get; set; }
        public string? Email { get; set; }
        public string? MobileNo { get; set; }
        public bool IsActive { get; set; } = true;

        // Customer Detail fields
        public int? ExecutiveId { get; set; }
        public string? BranchCode { get; set; }
        public int? DetailId { get; set; }
    }

    public class CustomerBatchSyncRequest : SyncAuthFields
    {
        public List<CustomerSyncDto> Customers { get; set; } = new();
    }

    // =====================================================================
    // 2. CL_Master_User DTO
    // =====================================================================
    public class UserSyncDto : SyncAuthFields
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? MobileNo { get; set; }
        public int? ManagerId { get; set; }
        public string? UserNumber { get; set; }
        public string? Role { get; set; } = "Assignee";
        public bool IsActive { get; set; } = true;
    }

    public class UserBatchSyncRequest : SyncAuthFields
    {
        public List<UserSyncDto> Users { get; set; } = new();
    }

    // =====================================================================
    // 3. CL_Docket (cl_docket) DTO
    // =====================================================================
    public class DocketSyncDto : SyncAuthFields
    {
        public string DocketNo { get; set; } = string.Empty;
        public int? DocketId { get; set; }
        public DateTime? BookingDate { get; set; }
        public string? Origin { get; set; }
        public string? Destination { get; set; }
    }

    public class DocketBatchSyncRequest : SyncAuthFields
    {
        public List<DocketSyncDto> Dockets { get; set; } = new();
    }

    // =====================================================================
    // 4. UNIFIED BATCH SYNC DTO
    // =====================================================================
    public class UnifiedSyncRequest : SyncAuthFields
    {
        public List<CustomerSyncDto>? Customers { get; set; }
        public List<UserSyncDto>? Users { get; set; }
        public List<DocketSyncDto>? Dockets { get; set; }
    }

    // =====================================================================
    // SYNC RESULT RESPONSE
    // =====================================================================
    public class SyncResultResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int TotalProcessed { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
