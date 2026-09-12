SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_InsertTicketHistory]
    @TicketId INT,
    @ChangedByUserId INT,
    @ChangedByUserRole NVARCHAR(50),
    @EventDescription NVARCHAR(MAX),
    @ChangeDate DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO TicketHistory
        (TicketId, ChangedByUserId, ChangedByUserRole, ChangeDate, EventDescription)
    VALUES
        (@TicketId, @ChangedByUserId, @ChangedByUserRole, @ChangeDate, @EventDescription);

    SELECT CAST(SCOPE_IDENTITY() AS INT);
END
GO
