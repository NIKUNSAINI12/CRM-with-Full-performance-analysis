
CREATE PROCEDURE [dbo].[usp_UpdateTicketStatus]
    @TicketId INT,
    @Status NVARCHAR(50),
    @UserId INT = NULL,
    @UserRole NVARCHAR(50) = NULL,
    @Note NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Tickets
    SET 
        Status = @Status,
        LastRepliedOn = DATEADD(MINUTE, 330, GETUTCDATE()),
        LastUpdatedByUserId = ISNULL(@UserId, LastUpdatedByUserId),
        LastUpdatedByUserRole = ISNULL(@UserRole, LastUpdatedByUserRole),
        LastUpdateNote = CASE WHEN @Note IS NOT NULL AND LTRIM(RTRIM(@Note)) <> '' THEN @Note ELSE LastUpdateNote END,
        ClosedOn = CASE WHEN @Status IN ('Closed', 'Resolved') THEN DATEADD(MINUTE, 330, GETUTCDATE()) ELSE ClosedOn END
    WHERE 
        TicketId = @TicketId;
END;

