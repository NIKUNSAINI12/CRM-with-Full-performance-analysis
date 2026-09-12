using System.Collections.Generic;
using System.Threading.Tasks;
using ticketing_system_backend.Models;

namespace ticketing_system_backend.Interface
{
    public interface IRolesRepository
    {
        Task<IEnumerable<SystemRole>> GetRolesAsync();
        Task<IEnumerable<SystemPermission>> GetPermissionsAsync();
        Task<SystemRole> GetRoleByIdAsync(int id);
        Task<int> CreateRoleAsync(CreateRoleRequest request);
        Task UpdateRoleAsync(int id, UpdateRoleRequest request);
        Task DeleteRoleAsync(int id);

        Task<IEnumerable<StatusWorkflow>> GetStatusWorkflowsAsync();
        Task<int> CreateStatusWorkflowAsync(StatusWorkflow workflow);
        Task UpdateStatusWorkflowAsync(int id, StatusWorkflow workflow);
        Task DeleteStatusWorkflowAsync(int id);

        Task<IEnumerable<AssignmentWorkflow>> GetAssignmentWorkflowsAsync();
        Task<int> CreateAssignmentWorkflowAsync(AssignmentWorkflow workflow);
        Task UpdateAssignmentWorkflowAsync(int id, AssignmentWorkflow workflow);
        Task DeleteAssignmentWorkflowAsync(int id);
    }
}
