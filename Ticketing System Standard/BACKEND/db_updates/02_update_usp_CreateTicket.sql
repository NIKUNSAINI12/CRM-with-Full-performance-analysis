SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[usp_CreateTicket]
    @CustomerId INT,
    @Subject NVARCHAR(200),
    @Description NVARCHAR(MAX),
    @Priority NVARCHAR(50),
    @TicketType NVARCHAR(50),
    @ProductId NVARCHAR(100),
    @ModuleId NVARCHAR(100),
    @SubModuleId NVARCHAR(100),
    @Difficulty NVARCHAR(50) = 'Medium',
    @CreatedByPmId INT = NULL,
    @DocketNumber NVARCHAR(100) = NULL,
    @TicketSource NVARCHAR(100) = NULL,
    @AssignedToId INT = NULL,
    @CreatedByUserId INT = NULL,
    @CreatedByUserRole NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ProdId INT = TRY_CAST(@ProductId AS INT);
    DECLARE @ModId INT = TRY_CAST(@ModuleId AS INT);
    DECLARE @SubModId INT = TRY_CAST(@SubModuleId AS INT);

    DECLARE @AssignedTo INT = @AssignedToId;
    IF @AssignedTo IS NULL
    BEGIN
        SELECT @AssignedTo = DefaultAssigneeId FROM dbo.Users WHERE Id = @CustomerId;
    END

    DECLARE @TATHours INT = 24;
    DECLARE @CalculatedDeadline DATETIME = NULL;
    
    SELECT TOP 1 @TATHours = ISNULL(TATHours, 24) FROM dbo.Priorities WHERE PriorityName = @Priority;
    
    SET @CalculatedDeadline = DATEADD(hour, @TATHours, SYSDATETIMEOFFSET() AT TIME ZONE 'India Standard Time');

    DECLARE @ResolvedStatus NVARCHAR(100) = 'Open';
    IF @AssignedTo IS NULL AND @CreatedByPmId IS NULL
    BEGIN
        SELECT TOP 1 @ResolvedStatus = StatusName 
        FROM dbo.CustomTicketStatuses 
        WHERE IsDefaultUnassigned = 1 AND IsActive = 1;
    END

    DECLARE @CreatorId INT = ISNULL(@CreatedByUserId, ISNULL(@CreatedByPmId, @CustomerId));
    DECLARE @CreatorRole NVARCHAR(50) = ISNULL(@CreatedByUserRole, CASE WHEN @CreatedByPmId IS NOT NULL THEN 'PM' ELSE 'Customer' END);

    INSERT INTO dbo.Tickets (
        CustomerId, 
        Subject, 
        Description, 
        Priority, 
        Status, 
        CreatedOn, 
        Product, 
        ModuleId,
        SubModuleId,
        TicketType,
        Difficulty,
        AssignedTo,
        LastDeadlineSet,
        DocketNumber,
        TicketSource,
        LastUpdatedByUserId,
        LastUpdatedByUserRole
    )
    VALUES (
        @CustomerId, 
        @Subject, 
        @Description, 
        @Priority, 
        @ResolvedStatus, 
        SYSDATETIMEOFFSET() AT TIME ZONE 'India Standard Time',
        @ProdId, 
        @ModId,
        @SubModId,
        @TicketType,
        @Difficulty,
        @AssignedTo,
        @CalculatedDeadline,
        @DocketNumber,
        @TicketSource,
        @CreatorId,
        @CreatorRole
    );

    DECLARE @NewTicketId INT = SCOPE_IDENTITY();

    DECLARE @LoggingPmId INT = ISNULL(@CreatedByPmId, CASE WHEN @CreatorRole IN ('PM', 'Project Manager', 'SuperManager', 'Super Admin') THEN @CreatorId ELSE NULL END);
    IF @LoggingPmId IS NOT NULL
    BEGIN
        INSERT INTO dbo.TicketCreationLog (TicketId, CreatedByPmId)
        VALUES (@NewTicketId, @LoggingPmId);
    END;

    SELECT 
        TicketId AS Id,
        TicketNumber,
        Subject, 
        Description, 
        Priority, 
        Status, 
        CreatedOn,
        CustomerId,
        LastDeadlineSet,
        TicketSource
    FROM dbo.Tickets 
    WHERE TicketId = @NewTicketId;
END
GO
