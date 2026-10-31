CREATE OR ALTER PROCEDURE dbo.usp_Ship_Create
    @ShipCode       varchar(10),
    @Name           nvarchar(100),
    @FiscalYearCode char(4),
    @IsActive       bit = 1
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @msg Nvarchar(2048);

    IF NOT EXISTS (SELECT 1 FROM dbo.FiscalYear WHERE FiscalYearCode = @FiscalYearCode)
    BEGIN
        SET @msg = CONCAT(N'Unknown fiscal year code ''', @FiscalYearCode, N'''.');
        THROW 50010, @msg, 1;
    END

    IF EXISTS (SELECT 1 FROM dbo.Ship WHERE ShipCode = @ShipCode)
    BEGIN
        SET @msg = CONCAT(N'Ship ''', @ShipCode, N''' already exists.');
        THROW 50003, @msg, 1;
    END

    INSERT INTO dbo.Ship (ShipCode, Name, FiscalYearCode, IsActive)
    VALUES (@ShipCode, ltrim(rtrim(@Name)), @FiscalYearCode, @IsActive);

    SELECT s.ShipCode, s.Name, s.FiscalYearCode, s.IsActive
    FROM dbo.Ship s
    WHERE s.ShipCode = @ShipCode;
END
GO