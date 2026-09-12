ALTER PROCEDURE [dbo].[usp_GetUserById]
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, UserNumber, FullName, Email, Role, ContactPerson, PhoneNo, MobileNo, DefaultAssigneeId, ManagerId
    FROM dbo.Users
    WHERE Id = @Id;
END
GO

ALTER PROCEDURE [dbo].[usp_GetUsersByRole]
    @Role NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, UserNumber, FullName, Email, Role, ContactPerson, PhoneNo, MobileNo, DefaultAssigneeId, ManagerId
    FROM dbo.Users
    WHERE Role = @Role
    ORDER BY FullName;
END
GO
