CREATE OR ALTER PROCEDURE dbo.usp_Ship_Get
    @ShipCode varchar(10)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @msg Nvarchar(2048);

    IF NOT EXISTS (SELECT 1 FROM dbo.Ship WHERE ShipCode = @ShipCode)
    BEGIN
        SET @msg = CONCAT(N'Ship ''', @ShipCode, N''' was not found.');
        THROW 50001, @msg, 1;
    END

    SELECT s.ShipCode, s.Name, s.FiscalYearCode, s.IsActive
    FROM dbo.Ship s
    WHERE s.ShipCode = @ShipCode;
END
GO