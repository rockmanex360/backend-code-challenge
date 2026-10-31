CREATE OR ALTER PROCEDURE dbo.usp_User_Create
    @Name     Nvarchar(100),
    @RoleName Nvarchar(30)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @msg Nvarchar(2048), @RoleId TINYINT, @UserId int;

    SELECT @RoleId = r.RoleId FROM dbo.UserRole r WHERE r.Name = ltrim(rtrim(@RoleName));
    IF @RoleId IS NULL
    BEGIN
        SET @msg = CONCAT(N'Unknown role ''', @RoleName, N'''.');
        THROW 50010, @msg, 1;
    END

    INSERT INTO dbo.AppUser (Name, RoleId) VALUES (ltrim(rtrim(@Name)), @RoleId);
    SET @UserId = SCOPE_IDENTITY();

    SELECT u.UserId, u.Name, r.Name AS RoleName, u.CreatedAtUtc
    FROM dbo.AppUser u
    JOIN dbo.UserRole r ON r.RoleId = u.RoleId
    WHERE u.UserId = @UserId;
END
GO