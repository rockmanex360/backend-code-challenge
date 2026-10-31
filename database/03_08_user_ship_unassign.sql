CREATE OR ALTER PROCEDURE dbo.usp_UserShip_Unassign
    @UserId   int,
    @ShipCode varchar(10)
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

    IF NOT EXISTS (SELECT 1 FROM dbo.Ship WHERE ShipCode = @ShipCode)
    BEGIN
        SET @msg = CONCAT(N'Ship ''', @ShipCode, N''' was not found.');
        THROW 50001, @msg, 1;
    END

    DELETE FROM dbo.UserShip WHERE UserId = @UserId AND ShipCode = @ShipCode;
END
GO