CREATE OR ALTER PROCEDURE dbo.usp_User_Get
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @msg Nvarchar(2048);

    IF NOT EXISTS (SELECT 1 FROM dbo.AppUser WHERE UserId = @UserId)
    BEGIN
        SET @msg = CONCAT(N'User ', @UserId, N' was not found.');
        THROW 50002, @msg, 1;
    END

    SELECT u.UserId, u.Name, r.Name AS RoleName, u.CreatedAtUtc
    FROM dbo.AppUser u
    JOIN dbo.UserRole r ON r.RoleId = u.RoleId
    WHERE u.UserId = @UserId;
END
GO