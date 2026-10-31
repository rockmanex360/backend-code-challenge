SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER FUNCTION dbo.fn_CrewStatus
(
    @SignOn  DATE,
    @SignOff DATE,
    @Eoc     DATE,
    @AsOf    DATE
)
RETURNS VARCHAR(12)
WITH SCHEMABINDING
AS
BEGIN
    RETURN CASE
        WHEN @SignOff IS NOT NULL                THEN 'Signed Off'
        WHEN @SignOn > @AsOf                     THEN 'Planned'
        WHEN @AsOf > DATEADD(DAY, 30, @Eoc)      THEN 'Relief Due'
        ELSE 'Onboard'
    END;
END
GO

CREATE OR ALTER FUNCTION dbo.fn_AgeAt
(
    @BirthDate DATE,
    @AsOf      DATE
)
RETURNS INT
WITH SCHEMABINDING
AS
BEGIN
    RETURN DATEDIFF(YEAR, @BirthDate, @AsOf)
         - CASE WHEN MONTH(@AsOf) * 100 + DAY(@AsOf) < MONTH(@BirthDate) * 100 + DAY(@BirthDate) THEN 1 ELSE 0 END;
END
GO

CREATE OR ALTER FUNCTION dbo.fn_FiscalYearStart
(
    @FiscalYearCode CHAR(4),
    @Period         DATE
)
RETURNS DATE
WITH SCHEMABINDING
AS
BEGIN
    RETURN DATEFROMPARTS(
        YEAR(@Period) - CASE WHEN MONTH(@Period) < CAST(LEFT(@FiscalYearCode, 2) AS INT) THEN 1 ELSE 0 END,
        CAST(LEFT(@FiscalYearCode, 2) AS INT),
        1);
END
GO

CREATE OR ALTER FUNCTION dbo.fn_FinancialReport
(
    @ShipCode VARCHAR(10),
    @Period   DATE
)
RETURNS TABLE
AS
RETURN
WITH Fy AS (
    SELECT p.Period,
           YtdFrom = dbo.fn_FiscalYearStart(s.FiscalYearCode, p.Period)
    FROM dbo.Ship s
    CROSS APPLY (SELECT Period = DATEFROMPARTS(YEAR(@Period), MONTH(@Period), 1)) p
    WHERE s.ShipCode = @ShipCode
),
Closure (AncestorNo, DescendantNo) AS (
    SELECT a.AccountNumber, a.AccountNumber FROM dbo.Account a
    UNION ALL
    SELECT c.AncestorNo, a.AccountNumber
    FROM Closure c
    JOIN dbo.Account a ON a.ParentAccountNumber = c.DescendantNo
),
Depth AS (
    SELECT DescendantNo AS AccountNumber, COUNT(*) AS Level
    FROM Closure
    GROUP BY DescendantNo
),
LeafActual AS (
    SELECT t.AccountNumber,
           ActualPeriod = SUM(CASE WHEN t.AccountPeriod = f.Period THEN t.ActualValue END),
           ActualYtd    = SUM(t.ActualValue)
    FROM dbo.AccountTransaction t
    CROSS JOIN Fy f
    WHERE t.ShipCode = @ShipCode
      AND t.AccountPeriod BETWEEN f.YtdFrom AND f.Period
    GROUP BY t.AccountNumber
),
LeafBudget AS (
    SELECT b.AccountNumber,
           BudgetPeriod = SUM(CASE WHEN b.AccountPeriod = f.Period THEN b.BudgetValue END),
           BudgetYtd    = SUM(b.BudgetValue)
    FROM dbo.Budget b
    CROSS JOIN Fy f
    WHERE b.ShipCode = @ShipCode
      AND b.AccountPeriod BETWEEN f.YtdFrom AND f.Period
    GROUP BY b.AccountNumber
),
Rolled AS (
    SELECT c.AncestorNo AS AccountNumber,
           SUM(la.ActualPeriod) AS ActualPeriod,
           SUM(lb.BudgetPeriod) AS BudgetPeriod,
           SUM(la.ActualYtd)    AS ActualYtd,
           SUM(lb.BudgetYtd)    AS BudgetYtd
    FROM Closure c
    LEFT JOIN LeafActual la ON la.AccountNumber = c.DescendantNo
    LEFT JOIN LeafBudget lb ON lb.AccountNumber = c.DescendantNo
    GROUP BY c.AncestorNo
    HAVING COALESCE(SUM(la.ActualPeriod), 0) <> 0
        OR COALESCE(SUM(lb.BudgetPeriod), 0) <> 0
        OR COALESCE(SUM(la.ActualYtd), 0)    <> 0
        OR COALESCE(SUM(lb.BudgetYtd), 0)    <> 0
)
SELECT a.AccountNumber,
       a.Description AS CoaDescription,
       CAST(d.Level AS INT) AS Level,
       CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.Account ch WHERE ch.ParentAccountNumber = a.AccountNumber)
                 THEN 1 ELSE 0 END AS BIT) AS IsParent,
       a.ParentAccountNumber,
       CAST(r.ActualPeriod AS DECIMAL(18,2)) AS ActualPeriod,
       CAST(r.BudgetPeriod AS DECIMAL(18,2)) AS BudgetPeriod,
       CAST(COALESCE(r.ActualPeriod, 0) - COALESCE(r.BudgetPeriod, 0) AS DECIMAL(18,2)) AS VarianceActual,
       CAST(r.ActualYtd AS DECIMAL(18,2))    AS ActualYtd,
       CAST(r.BudgetYtd AS DECIMAL(18,2))    AS BudgetYtd,
       CAST(COALESCE(r.ActualYtd, 0) - COALESCE(r.BudgetYtd, 0) AS DECIMAL(18,2))       AS VarianceYtd
FROM Rolled r
JOIN dbo.Account a ON a.AccountNumber = r.AccountNumber
JOIN Depth d       ON d.AccountNumber = r.AccountNumber;
GO