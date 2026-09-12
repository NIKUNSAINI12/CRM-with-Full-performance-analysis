namespace ticketing_system_backend.Models
{
    public class SystemRole
    {
        public int Id { get; set; }
        public string RoleName { get; set; }
        public string DefaultDashboardRoute { get; set; }
        public bool IsActive { get; set; }
        public List<SystemPermission> Permissions { get; set; } = new List<SystemPermission>();
    }

    public class SystemPermission
    {
        public int Id { get; set; }
        public string PermissionKey { get; set; }
        public string ModuleName { get; set; }
        public string Description { get; set; }
    }

    public class CreateRoleRequest
    {
        public string RoleName { get; set; }
        public string DefaultDashboardRoute { get; set; }
        public List<int> PermissionIds { get; set; } = new List<int>();
    }

    public class UpdateRoleRequest
    {
        public string RoleName { get; set; }
        public string DefaultDashboardRoute { get; set; }
        public bool IsActive { get; set; }
        public List<int> PermissionIds { get; set; } = new List<int>();
    }

    public class StatusWorkflow
    {
        public int Id { get; set; }
        public string RoleName { get; set; }
        public string CurrentStatus { get; set; }
        public List<string> NextStatuses { get; set; } = new List<string>();
        public string Condition { get; set; }
        public bool IsActive { get; set; }
        public bool IsBlocked { get; set; }
        public bool SendEmail { get; set; }
    }

    public class AssignmentWorkflow
    {
        public int Id { get; set; }
        public string AssignerRole { get; set; }
        public string AssignableToRole { get; set; }
    }
}
