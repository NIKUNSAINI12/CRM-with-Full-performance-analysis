
CREATE PROCEDURE [dbo].[usp_UpdateTicketByPM] 
    @TicketId INT, 
    @PmId INT, 
    @Status NVARCHAR(50) = NULL, 
    @Priority NVARCHAR(50) = NULL, 
    @AssignedToId INT = NULL, 
    @Note NVARCHAR(MAX) = NULL, 
    @DeadlineDate DATETIME = NULL 
AS 
BEGIN 
    SET NOCOUNT ON; 
    
    IF @DeadlineDate IS NOT NULL 
    BEGIN 
        MERGE TicketDeadlines AS target 
        USING (SELECT @TicketId AS TicketId) AS source 
        ON (target.TicketId = source.TicketId) 
        WHEN MATCHED THEN 
            UPDATE SET target.DeadlineDate = @DeadlineDate, target.SetByManagerId = @PmId, target.SetOn = DATEADD(MINUTE, 330, GETUTCDATE()) 
        WHEN NOT MATCHED BY TARGET THEN 
            INSERT (TicketId, DeadlineDate, SetByManagerId, SetOn) VALUES (@TicketId, @DeadlineDate, @PmId, DATEADD(MINUTE, 330, GETUTCDATE())); 
    END 
    
    UPDATE Tickets 
    SET Status = ISNULL(@Status, Status), 
        Priority = ISNULL(@Priority, Priority), 
        AssignedTo = ISNULL(@AssignedToId, AssignedTo), 
        LastRepliedOn = DATEADD(MINUTE, 330, GETUTCDATE()), 
        LastUpdatedByUserId = @PmId, 
        LastUpdatedByUserRole = 'PM', 
        LastUpdateNote = CASE WHEN @Note IS NOT NULL AND LTRIM(RTRIM(@Note)) <> '' THEN @Note ELSE LastUpdateNote END, 
        LastDeadlineSet = CASE WHEN @DeadlineDate IS NOT NULL THEN @DeadlineDate ELSE LastDeadlineSet END, 
        ClosedOn = CASE WHEN @Status IN ('Closed', 'Resolved') THEN DATEADD(MINUTE, 330, GETUTCDATE()) ELSE ClosedOn END 
    WHERE TicketId = @TicketId; 
END;

