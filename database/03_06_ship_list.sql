CREATE OR ALTER PROCEDURE dbo.usp_Ship_List
    @Status     varchar(8) = NULL,
    @PageNumber int = 1,
    @PageSize   int = 20
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Status is not null and @Status not in ('Active', 'Inactive')
        THROW 50010, N'Status must be Active or Inactive.', 1;

    IF @PageNumber is null or @PageNumber < 1
        THROW 50010, N'PageNumber must be 1 or greater.', 1;

    IF @PageSize is null or @PageSize NOT BETWEEN 1 AND 100
        THROW 50010, N'PageSize must be between 1 and 100.', 1;

    SELECT COUNT(*) AS TotalCount
    FROM dbo.Ship s
    WHERE @Status is null or s.IsActive = CASE @Status WHEN 'Active' THEN 1 ELSE 0 END;

    SELECT s.ShipCode, s.Name, s.FiscalYearCode, s.IsActive
    FROM dbo.Ship s
    WHERE @Status is null or s.IsActive = CASE @Status WHEN 'Active' THEN 1 ELSE 0 END
    ORDER BY s.ShipCode
    OFFSET CAST(@PageNumber - 1 AS bigint) * @PageSize rows fetch next @PageSize rows ONLY;
END
GO