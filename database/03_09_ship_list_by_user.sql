CREATE OR ALTER PROCEDURE dbo.usp_Ship_ListByUser
    @UserId int,
    @Status VARCHAR(8) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @msg NVARCHAR(2048);

    IF NOT EXISTS (SELECT 1 FROM dbo.AppUser WHERE UserId = @UserId)
    BEGIN
        SET @msg = CONCAT(N'User ', @UserId, N' was not found.');
        THROW 50002, @msg, 1;
    END
    
    IF @Status is not null and @Status not in ('Active', 'Inactive')
        THROW 50010, N'Status must be Active or Inactive.', 1;

    SELECT s.ShipCode, s.Name, s.FiscalYearCode, s.IsActive
    FROM dbo.UserShip us
    JOIN dbo.Ship s ON s.ShipCode = us.ShipCode
    WHERE us.UserId = @UserId
      AND (@Status IS NULL OR s.IsActive = CASE @Status WHEN 'Active' THEN 1 ELSE 0 END)
    ORDER BY s.ShipCode;
END
GO