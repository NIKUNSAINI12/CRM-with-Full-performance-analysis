using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ticketing_system_backend.Helpers;
using ticketing_system_backend.Interface;
using ticketing_system_backend.Models;

namespace ticketing_system_backend.Repository
{
    public class ExternalSyncRepository : IExternalSyncRepository
    {
        private readonly string _cjDarclConnectionString;
        private readonly IDbConnection _defaultConnection;
        private readonly IConfiguration _config;
        private readonly ILogger<ExternalSyncRepository> _logger;

        public ExternalSyncRepository(
            IConfiguration configuration,
            IDbConnection defaultConnection,
            ILogger<ExternalSyncRepository> logger)
        {
            _config = configuration;
            _cjDarclConnectionString = configuration.GetConnectionString("CJDarclConnection") ?? "";
            _defaultConnection = defaultConnection;
            _logger = logger;
        }

        private string GetCjDarclDbName()
        {
            return _config["CJDarclDbName"] ?? DbConfig.CJDarclDb ?? "CJDarcl";
        }

        private IDbConnection GetConnection()
        {
            if (!string.IsNullOrWhiteSpace(_cjDarclConnectionString))
            {
                return new SqlConnection(_cjDarclConnectionString);
            }
            return _defaultConnection;
        }

        // =====================================================================
        // 1. CL_Master_Customer & CL_Master_Customer_Detail
        // =====================================================================
        public async Task<bool> UpsertCustomerAsync(CustomerSyncDto c)
        {
            using var conn = GetConnection();
            var dbName = GetCjDarclDbName();
            string tablePrefix = string.IsNullOrWhiteSpace(_cjDarclConnectionString) 
                ? $"[{dbName}].dbo." 
                : "dbo.";

            string sql = $@"
                -- 1. Upsert CL_Master_Customer
                IF EXISTS (SELECT 1 FROM {tablePrefix}CL_Master_Customer WHERE CustomerId = @CustomerId)
                BEGIN
                    UPDATE {tablePrefix}CL_Master_Customer
                    SET CustomerName = @CustomerName,
                        CustomerCode = COALESCE(@CustomerCode, CustomerCode),
                        EmailId1 = COALESCE(@Email, EmailId1),
                        IsActive = @IsActive
                    WHERE CustomerId = @CustomerId;
                END
                ELSE
                BEGIN
                    DECLARE @GrpCode VARCHAR(50);
                    SELECT TOP 1 @GrpCode = GroupCode FROM {tablePrefix}CL_Master_CustomerGroup;
                    IF (@GrpCode IS NULL) SET @GrpCode = 'C0005';

                    INSERT INTO {tablePrefix}CL_Master_Customer (GroupCode, CustomerId, CustomerCode, CustomerName, EmailId1, IsActive, CompanyId)
                    VALUES (@GrpCode, @CustomerId, COALESCE(@CustomerCode, CONCAT('CUST-', @CustomerId)), @CustomerName, @Email, @IsActive, 1);
                END

                -- 2. Upsert CL_Master_Customer_Detail
                IF EXISTS (SELECT 1 FROM {tablePrefix}CL_Master_Customer_Detail WHERE CustomerId = @CustomerId)
                BEGIN
                    UPDATE {tablePrefix}CL_Master_Customer_Detail
                    SET ExecutiveId = COALESCE(@ExecutiveId, ExecutiveId),
                        EmailId = COALESCE(@Email, EmailId),
                        MobileNo = COALESCE(@MobileNo, MobileNo),
                        UpdateDate = GETDATE()
                    WHERE CustomerId = @CustomerId;
                END
                ELSE
                BEGIN
                    INSERT INTO {tablePrefix}CL_Master_Customer_Detail (CustomerId, Password, PanNo, GstTinNo, IndustryId, TypeOfOwnershipId, ExecutiveId, EmailId, MobileNo, EntryBy, EntryDate)
                    VALUES (@CustomerId, 'CJDarcl@123', '', '', 1, 1, @ExecutiveId, @Email, @MobileNo, 1, GETDATE());
                END
            ";

            var p = new DynamicParameters();
            p.Add("@CustomerId", c.CustomerId);
            p.Add("@CustomerName", c.CustomerName);
            p.Add("@CustomerCode", c.CustomerNumber);
            p.Add("@Email", c.Email);
            p.Add("@MobileNo", c.MobileNo);
            p.Add("@IsActive", c.IsActive);
            p.Add("@ExecutiveId", c.ExecutiveId);

            var rowsAffected = await conn.ExecuteAsync(sql, p);
            return rowsAffected > 0;
        }

        public async Task<SyncResultResponse> UpsertCustomersBatchAsync(List<CustomerSyncDto> customers)
        {
            var response = new SyncResultResponse { TotalProcessed = customers.Count };
            foreach (var cust in customers)
            {
                try
                {
                    if (cust.CustomerId <= 0 || string.IsNullOrWhiteSpace(cust.CustomerName))
                    {
                        response.FailureCount++;
                        response.Errors.Add($"Invalid Customer record: ID '{cust.CustomerId}', Name '{cust.CustomerName}'");
                        continue;
                    }

                    await UpsertCustomerAsync(cust);
                    response.SuccessCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error syncing customer ID {CustomerId}", cust.CustomerId);
                    response.FailureCount++;
                    response.Errors.Add($"Customer ID {cust.CustomerId}: {ex.Message}");
                }
            }

            response.Success = response.FailureCount == 0;
            response.Message = $"Synced {response.SuccessCount} of {response.TotalProcessed} customers successfully.";
            return response;
        }

        // =====================================================================
        // 2. CL_Master_User
        // =====================================================================
        public async Task<bool> UpsertUserAsync(UserSyncDto u)
        {
            using var conn = GetConnection();
            var dbName = GetCjDarclDbName();
            string tablePrefix = string.IsNullOrWhiteSpace(_cjDarclConnectionString) 
                ? $"[{dbName}].dbo." 
                : "dbo.";

            string sql = $@"
                DECLARE @ValidManagerId SMALLINT = @ManagerId;
                IF (@ValidManagerId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM {tablePrefix}CL_Master_User WHERE UserId = @ValidManagerId))
                BEGIN
                    SET @ValidManagerId = NULL;
                END

                IF EXISTS (SELECT 1 FROM {tablePrefix}CL_Master_User WHERE UserId = @UserId)
                BEGIN
                    UPDATE {tablePrefix}CL_Master_User
                    SET Name = @Name,
                        EmailId = COALESCE(@Email, EmailId),
                        MobileNo = COALESCE(@MobileNo, MobileNo),
                        ManagerId = COALESCE(@ValidManagerId, ManagerId),
                        CrmRole = COALESCE(@Role, CrmRole),
                        IsActive = @IsActive,
                        UpdateDate = GETDATE()
                    WHERE UserId = @UserId;
                END
                ELSE
                BEGIN
                    INSERT INTO {tablePrefix}CL_Master_User (UserId, UserName, Password, LocationId, Name, EmailId, MobileNo, Gender, DoB, DoJ, Address, UserStatusId, UserTypeId, RoleId, ManagerId, CrmRole, IsActive, DefaultCompanyId, EntryBy, EntryDate)
                    VALUES (@UserId, COALESCE(@UserNumber, CONCAT('USER-', @UserId)), 'CJDarcl@123', 1, @Name, @Email, @MobileNo, 1, '1990-01-01', '2020-01-01', '', 1, 2, 1, @ValidManagerId, @Role, @IsActive, 1, 1, GETDATE());
                END
            ";

            var p = new DynamicParameters();
            p.Add("@UserId", u.UserId);
            p.Add("@Name", u.Name);
            p.Add("@Email", u.Email);
            p.Add("@MobileNo", u.MobileNo);
            p.Add("@ManagerId", u.ManagerId);
            p.Add("@UserNumber", u.UserNumber);
            p.Add("@Role", u.Role);
            p.Add("@IsActive", u.IsActive);

            var rowsAffected = await conn.ExecuteAsync(sql, p);
            return rowsAffected > 0;
        }

        public async Task<SyncResultResponse> UpsertUsersBatchAsync(List<UserSyncDto> users)
        {
            var response = new SyncResultResponse { TotalProcessed = users.Count };
            foreach (var user in users)
            {
                try
                {
                    if (user.UserId <= 0 || string.IsNullOrWhiteSpace(user.Name))
                    {
                        response.FailureCount++;
                        response.Errors.Add($"Invalid User record: ID '{user.UserId}', Name '{user.Name}'");
                        continue;
                    }

                    await UpsertUserAsync(user);
                    response.SuccessCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error syncing user ID {UserId}", user.UserId);
                    response.FailureCount++;
                    response.Errors.Add($"User ID {user.UserId}: {ex.Message}");
                }
            }

            response.Success = response.FailureCount == 0;
            response.Message = $"Synced {response.SuccessCount} of {response.TotalProcessed} users successfully.";
            return response;
        }

        // =====================================================================
        // 3. CL_Docket (cl_docket)
        // =====================================================================
        public async Task<bool> UpsertDocketAsync(DocketSyncDto d)
        {
            using var conn = GetConnection();
            var dbName = GetCjDarclDbName();
            string tablePrefix = string.IsNullOrWhiteSpace(_cjDarclConnectionString) 
                ? $"[{dbName}].dbo." 
                : "dbo.";

            string sql = $@"
                IF EXISTS (SELECT 1 FROM {tablePrefix}CL_Docket WHERE DocketNo = @DocketNo)
                BEGIN
                    UPDATE {tablePrefix}CL_Docket
                    SET DocketDate = COALESCE(@BookingDate, DocketDate),
                        UpdateDate = GETDATE()
                    WHERE DocketNo = @DocketNo;
                END
                ELSE
                BEGIN
                    DECLARE @NewDocketId BIGINT = @DocketId;
                    IF (@NewDocketId IS NULL OR @NewDocketId <= 0)
                    BEGIN
                        SELECT @NewDocketId = COALESCE(MAX(DocketId), 0) + 1 FROM {tablePrefix}CL_Docket;
                    END

                    DECLARE @BDate DATE = COALESCE(@BookingDate, CAST(GETDATE() AS DATE));

                    INSERT INTO {tablePrefix}CL_Docket (DocketId, DocketNo, DocketDate, FromLocationId, ToLocationId, FromCityId, ToCityId, TransportModeId, PaybasId, Edd, Packages, ActualWeight, ChargedWeight, BusinessTypeId, ServiceTypeId, BillLocationId, EntryBy, EntryDate, IsCancel, IsClosed, CompanyId)
                    VALUES (@NewDocketId, @DocketNo, @BDate, 3, 3, 78, 78, 4, 2, DATEADD(day, 3, @BDate), 1, 10.0, 10.0, 0, 1, 3, 1, GETDATE(), 0, 0, 1);
                END
            ";

            var p = new DynamicParameters();
            p.Add("@DocketNo", d.DocketNo);
            p.Add("@DocketId", d.DocketId);
            p.Add("@BookingDate", d.BookingDate);

            var rowsAffected = await conn.ExecuteAsync(sql, p);
            return rowsAffected > 0;
        }

        public async Task<SyncResultResponse> UpsertDocketsBatchAsync(List<DocketSyncDto> dockets)
        {
            var response = new SyncResultResponse { TotalProcessed = dockets.Count };
            foreach (var docket in dockets)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(docket.DocketNo))
                    {
                        response.FailureCount++;
                        response.Errors.Add("Invalid Docket record: Missing DocketNo");
                        continue;
                    }

                    await UpsertDocketAsync(docket);
                    response.SuccessCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error syncing docket {DocketNo}", docket.DocketNo);
                    response.FailureCount++;
                    response.Errors.Add($"Docket {docket.DocketNo}: {ex.Message}");
                }
            }

            response.Success = response.FailureCount == 0;
            response.Message = $"Synced {response.SuccessCount} of {response.TotalProcessed} dockets successfully.";
            return response;
        }

        // =====================================================================
        // 4. UNIFIED BATCH SYNC
        // =====================================================================
        public async Task<SyncResultResponse> UpsertUnifiedBatchAsync(UnifiedSyncRequest request)
        {
            var response = new SyncResultResponse();

            if (request.Customers != null && request.Customers.Count > 0)
            {
                var custRes = await UpsertCustomersBatchAsync(request.Customers);
                response.TotalProcessed += custRes.TotalProcessed;
                response.SuccessCount += custRes.SuccessCount;
                response.FailureCount += custRes.FailureCount;
                response.Errors.AddRange(custRes.Errors);
            }

            if (request.Users != null && request.Users.Count > 0)
            {
                var userRes = await UpsertUsersBatchAsync(request.Users);
                response.TotalProcessed += userRes.TotalProcessed;
                response.SuccessCount += userRes.SuccessCount;
                response.FailureCount += userRes.FailureCount;
                response.Errors.AddRange(userRes.Errors);
            }

            if (request.Dockets != null && request.Dockets.Count > 0)
            {
                var docketRes = await UpsertDocketsBatchAsync(request.Dockets);
                response.TotalProcessed += docketRes.TotalProcessed;
                response.SuccessCount += docketRes.SuccessCount;
                response.FailureCount += docketRes.FailureCount;
                response.Errors.AddRange(docketRes.Errors);
            }

            response.Success = response.FailureCount == 0;
            response.Message = $"Unified Sync Completed: {response.SuccessCount} successes, {response.FailureCount} failures.";
            return response;
        }
    }
}
