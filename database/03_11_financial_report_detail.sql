CREATE OR ALTER PROCEDURE dbo.usp_FinancialReport_Detail
    @ShipCode VARCHAR(10),
    @Period   DATE
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @msg NVARCHAR(2048), @ShipName NVARCHAR(100), @FiscalYearCode CHAR(4), @IsActive BIT, @YtdFrom DATE;

    SELECT @ShipName = s.Name, @FiscalYearCode = s.FiscalYearCode, @IsActive = s.IsActive
    FROM dbo.Ship s WHERE s.ShipCode = @ShipCode;

    IF @IsActive IS NULL
    BEGIN
        SET @msg = CONCAT(N'Ship ''', @ShipCode, N''' was not found.');
        THROW 50001, @msg, 1;
    END

    IF @IsActive = 0
    BEGIN
        SET @msg = CONCAT(N'Ship ''', @ShipCode, N''' is inactive.');
        THROW 50004, @msg, 1;
    END
    
    IF @Period IS NULL
        THROW 50010, N'Period is required.', 1;

    SET @Period  = DATEFROMPARTS(YEAR(@Period), MONTH(@Period), 1);
    SET @YtdFrom = dbo.fn_FiscalYearStart(@FiscalYearCode, @Period);

    SELECT @ShipCode AS ShipCode, @ShipName AS ShipName, @FiscalYearCode AS FiscalYearCode,
           @Period AS Period, @YtdFrom AS YtdFrom, @Period AS YtdTo;

    SELECT AccountNumber, CoaDescription, Level, IsParent, ParentAccountNumber,
           ActualPeriod, BudgetPeriod, VarianceActual, ActualYtd, BudgetYtd, VarianceYtd
    FROM dbo.fn_FinancialReport(@ShipCode, @Period)
    ORDER BY AccountNumber;
END
GO