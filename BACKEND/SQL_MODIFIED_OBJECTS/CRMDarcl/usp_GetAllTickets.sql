
CREATE PROCEDURE [dbo].[usp_GetAllTickets]
    @Status NVARCHAR(MAX) = NULL,
    @Priority NVARCHAR(MAX) = NULL,
    @CustomerId INT = NULL,
    @AssignedToId NVARCHAR(MAX) = NULL,
    @DateFrom DATETIME = NULL,
    @DateTo DATETIME = NULL,
    @PageNumber INT = 1,
    @PageSize INT = 20,
    @UserId INT = NULL,
    @UserRole NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(t.TicketId) AS TotalCount
    FROM dbo.Tickets t
    LEFT JOIN CJDarcl.dbo.CL_Master_User a ON t.AssignedTo = a.UserId
    WHERE 
        (@Status IS NULL OR t.Status IN (SELECT value FROM STRING_SPLIT(@Status, ',')))
        AND (@Priority IS NULL OR t.Priority IN (SELECT value FROM STRING_SPLIT(@Priority, ',')))
        AND (@CustomerId IS NULL OR t.CustomerId = @CustomerId)
        AND (@AssignedToId IS NULL OR a.UserName = @AssignedToId)
        AND (@DateFrom IS NULL OR t.CreatedOn >= @DateFrom)
        AND (@DateTo IS NULL OR t.CreatedOn < DATEADD(day, 1, @DateTo))
        AND (
            @UserRole IS NULL 
            OR @UserRole IN ('SuperManager', 'Super Admin')
            OR (@UserRole = 'Assignee' AND t.AssignedTo = @UserId)
            OR (
                @UserRole IN ('Manager', 'PM', 'Project Manager') 
                AND (
                    t.CustomerId IN (
                        SELECT cd.CustomerId 
                        FROM CJDarcl.dbo.CL_Master_Customer_Detail cd
                        INNER JOIN CJDarcl.dbo.CL_Master_User mu ON cd.ExecutiveId = mu.UserId
                        WHERE mu.ManagerId = @UserId
                    )
                    OR t.AssignedTo IN (SELECT UserId FROM CJDarcl.dbo.CL_Master_User WHERE ManagerId = @UserId)
                    OR t.AssignedTo = @UserId
                )
            )
            OR (@UserRole = 'HOD' AND t.AssignedTo IN (SELECT ExecutiveId FROM dbo.HODExecutiveMappings WHERE HODId = @UserId))
        );

    SELECT
        t.TicketId AS Id,
        t.Subject,
        t.Description,
        t.Status,
        t.Priority,
        t.CreatedOn,
        c.CustomerId AS CustomerId,
        c.CustomerName AS CustomerName,
        a.Name AS AssignedToName,
        a.UserName AS AssignedToNumber, 
        t.TicketNumber,
        t.LastRepliedOn,
        COALESCE(pProd.Name, t.Product) AS Product,
        t.SubProduct,
        t.LastUpdateNote AS LastUpdateNote,
        t.LastUpdateNote AS ClosingRemark,
        t.CreatedByUserId,
        t.IsCreatedByCustomer,
        COALESCE(
            uCreator.Name,
            cdCreator.DecisionMakerName,
            c.CustomerName,
            'Customer'
        ) AS CreatedByName,
        COALESCE(
            CASE 
                WHEN t.IsCreatedByCustomer = 0 THEN 'Internal Staff'
                WHEN t.IsCreatedByCustomer = 1 THEN 'Customer'
            END,
            CASE WHEN uCreator.UserId IS NOT NULL THEN 'Internal Staff' ELSE 'Customer' END
        ) AS CreatedByRole
    FROM dbo.Tickets t
    LEFT JOIN dbo.Products pProd ON TRY_CAST(t.Product AS INT) = pProd.Id
    LEFT JOIN CJDarcl.dbo.CL_Master_Customer c ON t.CustomerId = c.CustomerId
    LEFT JOIN CJDarcl.dbo.CL_Master_Customer_Detail cdCreator ON c.CustomerId = cdCreator.CustomerId
    LEFT JOIN CJDarcl.dbo.CL_Master_User a ON t.AssignedTo = a.UserId
    LEFT JOIN CJDarcl.dbo.CL_Master_User uCreator ON t.CreatedByUserId = uCreator.UserId
    WHERE 
        (@Status IS NULL OR t.Status IN (SELECT value FROM STRING_SPLIT(@Status, ',')))
        AND (@Priority IS NULL OR t.Priority IN (SELECT value FROM STRING_SPLIT(@Priority, ',')))
        AND (@CustomerId IS NULL OR t.CustomerId = @CustomerId)
        AND (@AssignedToId IS NULL OR a.UserName = @AssignedToId)
        AND (@DateFrom IS NULL OR t.CreatedOn >= @DateFrom)
        AND (@DateTo IS NULL OR t.CreatedOn < DATEADD(day, 1, @DateTo))
        AND (
            @UserRole IS NULL 
            OR @UserRole IN ('SuperManager', 'Super Admin')
            OR (@UserRole = 'Assignee' AND t.AssignedTo = @UserId)
            OR (
                @UserRole IN ('Manager', 'PM', 'Project Manager') 
                AND (
                    t.CustomerId IN (
                        SELECT cd.CustomerId 
                        FROM CJDarcl.dbo.CL_Master_Customer_Detail cd
                        INNER JOIN CJDarcl.dbo.CL_Master_User mu ON cd.ExecutiveId = mu.UserId
                        WHERE mu.ManagerId = @UserId
                    )
                    OR t.AssignedTo IN (SELECT UserId FROM CJDarcl.dbo.CL_Master_User WHERE ManagerId = @UserId)
                    OR t.AssignedTo = @UserId
                )
            )
            OR (@UserRole = 'HOD' AND t.AssignedTo IN (SELECT ExecutiveId FROM dbo.HODExecutiveMappings WHERE HODId = @UserId))
        )
    ORDER BY t.CreatedOn DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END;

