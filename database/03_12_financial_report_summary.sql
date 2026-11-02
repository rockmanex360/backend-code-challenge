CREATE OR ALTER PROCEDURE dbo.usp_FinancialReport_Summary
    @ShipCode varchar(10),
    @Period   date
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @msg Nvarchar(2048), @ShipName Nvarchar(100), @FiscalYearCode CHAR(4), @IsActive BIT, @YtdFrom date;

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

    WITH Months AS (
        SELECT @YtdFrom AS AccountPeriod
        UNION ALL
        SELECT CAST(DATEADD(MONTH, 1, AccountPeriod) AS DATE) FROM Months WHERE AccountPeriod < @Period
    ),
    Act AS (
        SELECT AccountPeriod, SUM(ActualValue) AS Actual
        FROM dbo.AccountTransaction
        WHERE ShipCode = @ShipCode AND AccountPeriod BETWEEN @YtdFrom AND @Period
        GROUP BY AccountPeriod
    ),
    Bud AS (
        SELECT AccountPeriod, SUM(BudgetValue) AS Budget
        FROM dbo.Budget
        WHERE ShipCode = @ShipCode AND AccountPeriod BETWEEN @YtdFrom AND @Period
        GROUP BY AccountPeriod
    )
    SELECT m.AccountPeriod,
           CAST(a.Actual AS DECIMAL(18,2)) AS Actual,
           CAST(b.Budget AS DECIMAL(18,2)) AS Budget,
           CAST(COALESCE(a.Actual, 0) - COALESCE(b.Budget, 0) AS DECIMAL(18,2)) AS Variance
    FROM Months m
    LEFT JOIN Act a ON a.AccountPeriod = m.AccountPeriod
    LEFT JOIN Bud b ON b.AccountPeriod = m.AccountPeriod
    WHERE COALESCE(a.Actual, 0) <> 0 OR COALESCE(b.Budget, 0) <> 0
    ORDER BY m.AccountPeriod;

    SELECT CAST(a.v AS DECIMAL(18,2)) AS ActualYtd,
           CAST(b.v AS DECIMAL(18,2)) AS BudgetYtd,
           CAST(COALESCE(a.v, 0) - COALESCE(b.v, 0) AS DECIMAL(18,2)) AS VarianceYtd
    FROM (SELECT SUM(ActualValue) AS v FROM dbo.AccountTransaction
          WHERE ShipCode = @ShipCode AND AccountPeriod BETWEEN @YtdFrom AND @Period) a
    CROSS JOIN
         (SELECT SUM(BudgetValue) AS v FROM dbo.Budget
          WHERE ShipCode = @ShipCode AND AccountPeriod BETWEEN @YtdFrom AND @Period) b;
END
GO