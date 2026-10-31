CREATE OR ALTER PROCEDURE dbo.usp_User_List
    @PageNumber INT = 1,
    @PageSize   INT = 20
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @PageNumber is null or @PageNumber < 1
        THROW 50010, N'PageNumber must be 1 or greater.', 1;
        
    IF @PageSize is null or @PageSize not between 1 and 100
        THROW 50010, N'PageSize must be between 1 and 100.', 1;

    SELECT COUNT(*) AS TotalCount FROM dbo.AppUser;

    SELECT u.UserId, u.Name, r.Name AS RoleName, u.CreatedAtUtc
    FROM dbo.AppUser u
    JOIN dbo.UserRole r ON r.RoleId = u.RoleId
    ORDER BY u.UserId
    OFFSET CAST(@PageNumber - 1 AS BIGINT) * @PageSize rows fetch next @PageSize rows ONLY;
END
GO