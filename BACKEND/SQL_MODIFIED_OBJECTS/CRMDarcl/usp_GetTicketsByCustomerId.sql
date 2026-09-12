
CREATE PROCEDURE [dbo].[usp_GetTicketsByCustomerId]
    @CustomerId INT,
    @Status NVARCHAR(MAX) = NULL,
    @Priority NVARCHAR(MAX) = NULL,
    @Product NVARCHAR(MAX) = NULL,
    @SubProduct NVARCHAR(MAX) = NULL,
    @AssignedTo NVARCHAR(MAX) = NULL,
    @DateFrom DATETIME = NULL,
    @DateTo DATETIME = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @FilteredTicketIds TABLE (TicketId INT);

    INSERT INTO @FilteredTicketIds (TicketId)
    SELECT TicketId
    FROM dbo.Tickets
    WHERE CustomerId = @CustomerId
      AND (@Status IS NULL OR Status IN (SELECT value FROM STRING_SPLIT(@Status, ',')))
      AND (@Priority IS NULL OR Priority IN (SELECT value FROM STRING_SPLIT(@Priority, ',')))
      AND (@Product IS NULL OR Product IN (SELECT value FROM STRING_SPLIT(@Product, ',')))
      AND (@SubProduct IS NULL OR SubProduct IN (SELECT value FROM STRING_SPLIT(@SubProduct, ',')))
      AND (@AssignedTo IS NULL OR AssignedTo IN (SELECT value FROM STRING_SPLIT(@AssignedTo, ',')))
      AND (@DateFrom IS NULL OR CreatedOn >= @DateFrom)
      AND (@DateTo IS NULL OR CreatedOn < DATEADD(day, 1, @DateTo));

    -- Tickets
    SELECT 
        t.TicketId AS Id,
        t.Subject,
        t.TicketNumber,
        t.Description,
        t.Status,
        t.Priority,
        COALESCE(pProd.Name, t.Product) AS Product,
        t.SubProduct,
        t.AssignedTo,
        t.CreatedOn,
        t.ClosedOn,
        t.LastRepliedOn,
        t.CustomerId,
        t.DocketNumber,
        t.LastUpdateNote AS LastUpdateNote,
        t.LastUpdateNote AS ClosingRemark,
        u1.FullName AS CustomerName,
        u2.FullName AS AssignedToName,
        u2.Email AS AssignedToEmail
    FROM dbo.Tickets t
    LEFT JOIN dbo.Products pProd ON TRY_CAST(t.Product AS INT) = pProd.Id
    LEFT JOIN dbo.Users u1 ON t.CustomerId = u1.Id
    LEFT JOIN dbo.Users u2 ON t.AssignedTo = u2.Id
    WHERE t.TicketId IN (SELECT TicketId FROM @FilteredTicketIds)
    ORDER BY t.CreatedOn DESC;

    -- Attachments
    SELECT 
        ta.AttachmentId AS Id,
        ta.TicketId,
        ta.FileName,
        ta.FilePath,
        ta.FileSize,
        ta.ContentType AS FileType,
        ta.UploadedOn,
        ta.UploadedById,
        u.FullName AS UploadedBy
    FROM dbo.TicketAttachments ta
    LEFT JOIN dbo.Users u ON ta.UploadedById = u.Id
    WHERE ta.TicketId IN (SELECT TicketId FROM @FilteredTicketIds);
END;

