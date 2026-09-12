using System.Threading.Tasks;
using ticketing_system_backend.Models;

namespace ticketing_system_backend.Interface
{
    public interface IPerformanceAnalyticsRepository
    {
        Task<PerformanceOverviewDto> GetPerformanceOverviewAsync(PerformanceFilterParams? filters = null);
        Task<LocationPerformanceDetailDto?> GetLocationDetailAsync(int locationId);
        Task<CustomerPerformanceDetailDto?> GetCustomerDetailAsync(int customerId);
        Task<UserPerformanceDetailDto?> GetUserDetailAsync(int userId);
        Task<TeamPerformanceDetailDto?> GetTeamDetailAsync(int managerId);
    }
}
