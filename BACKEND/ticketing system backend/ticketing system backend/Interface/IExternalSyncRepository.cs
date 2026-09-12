using System.Collections.Generic;
using System.Threading.Tasks;
using ticketing_system_backend.Models;

namespace ticketing_system_backend.Interface
{
    public interface IExternalSyncRepository
    {
        Task<bool> UpsertCustomerAsync(CustomerSyncDto customer);
        Task<SyncResultResponse> UpsertCustomersBatchAsync(List<CustomerSyncDto> customers);

        Task<bool> UpsertUserAsync(UserSyncDto user);
        Task<SyncResultResponse> UpsertUsersBatchAsync(List<UserSyncDto> users);

        Task<bool> UpsertDocketAsync(DocketSyncDto docket);
        Task<SyncResultResponse> UpsertDocketsBatchAsync(List<DocketSyncDto> dockets);

        Task<SyncResultResponse> UpsertUnifiedBatchAsync(UnifiedSyncRequest request);
    }
}
