CREATE OR ALTER PROCEDURE dbo.usp_Crew_List
    @ShipCode      varchar(10),
    @PageNumber    int           = 1,
    @PageSize      int           = 20,
    @SortBy        varchar(20)   = NULL,
    @SortDirection varchar(4)    = 'ASC',
    @Search        Nvarchar(100) = NULL,
    @AsOfDate      date          = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @msg Nvarchar(2048), @IsActive BIT, @Pattern Nvarchar(500) = NULL;

    SET @SortDirection = COALESCE(@SortDirection, 'ASC');
    SET @Search        = NULLIF(LTRIM(RTRIM(@Search)), N'');
    SET @AsOfDate      = COALESCE(@AsOfDate, CAST(SYSUTCDATETIME() AS DATE));

    SELECT @IsActive = s.IsActive FROM dbo.Ship s WHERE s.ShipCode = @ShipCode;

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

    IF @PageNumber IS NULL OR @PageNumber < 1
        THROW 50010, N'PageNumber must be 1 or greater.', 1;

    IF @PageSize IS NULL OR @PageSize NOT BETWEEN 1 AND 100
        THROW 50010, N'PageSize must be between 1 and 100.', 1;

    IF @SortDirection NOT IN ('ASC', 'DESC')
        THROW 50010, N'SortDirection must be ASC or DESC.', 1;

    IF @SortBy IS NOT NULL AND @SortBy NOT IN
        ('RankName', 'CrewMemberId', 'FirstName', 'LastName', 'Age', 'Nationality', 'SignOnDate', 'Status')
    BEGIN
        SET @msg = CONCAT(N'SortBy ''', @SortBy, N''' is not supported.');
        THROW 50010, @msg, 1;
    END

    IF @Search IS NOT NULL
        SET @Pattern = N'%'
            + REPLACE(REPLACE(REPLACE(REPLACE(@Search, N'\', N'\\'), N'%', N'\%'), N'_', N'\_'), N'[', N'\[')
            + N'%';

    SELECT cs.ServiceId,
           cs.CrewMemberId,
           r.Name AS RankName,
           cm.FirstName,
           cm.LastName,
           x.Age,
           n.Name AS Nationality,
           cs.SignOnDate,
           x.Status,
           r.SeniorityOrder
    INTO #Filtered
    FROM dbo.CrewService cs
    JOIN dbo.CrewMember  cm ON cm.CrewMemberId = cs.CrewMemberId
    JOIN dbo.CrewRank    r  ON r.CrewRankId    = cs.CrewRankId
    JOIN dbo.Nationality n  ON n.NationalityCode = cm.NationalityCode
    CROSS APPLY (
        SELECT  dbo.fn_AgeAt(cm.BirthDate, @AsOfDate) AS Age,
                dbo.fn_CrewStatus(cs.SignOnDate, cs.SignOffDate, cs.EndOfContractDate, @AsOfDate) AS Status
    ) x
    WHERE cs.ShipCode = @ShipCode
      AND cs.SignOffDate IS NULL
      AND x.Status IN ('Onboard', 'Relief Due')
      AND (@Pattern IS NULL
           OR r.Name          like @Pattern ESCAPE '\'
           OR cs.CrewMemberId like @Pattern ESCAPE '\'
           OR cm.FirstName    like @Pattern ESCAPE '\'
           OR cm.LastName     like @Pattern ESCAPE '\'
           OR CAST(x.Age AS varchar(3)) like @Pattern ESCAPE '\'
           OR n.Name          like @Pattern ESCAPE '\'
           OR FORMAT(cs.SignOnDate, 'dd MMM yyyy', 'en-US') like @Pattern ESCAPE '\');

    SELECT COUNT(*) AS TotalCount FROM #Filtered;

    SELECT f.RankName, f.CrewMemberId, f.FirstName, f.LastName, f.Age, f.Nationality, f.SignOnDate, f.Status
    FROM #Filtered f
    CROSS APPLY (SELECT CASE @SortBy
                     WHEN 'RankName'     THEN f.RankName
                     WHEN 'CrewMemberId' THEN f.CrewMemberId
                     WHEN 'FirstName'    THEN f.FirstName
                     WHEN 'LastName'     THEN f.LastName
                     WHEN 'Age'          THEN RIGHT('000' + CAST(f.Age AS varchar(3)), 3)
                     WHEN 'Nationality'  THEN f.Nationality
                     WHEN 'SignOnDate'   THEN CONVERT(CHAR(10), f.SignOnDate, 23)
                     WHEN 'Status'       THEN f.Status
                 END AS SortKey) k
    ORDER BY
        CASE WHEN @SortDirection = 'ASC'  THEN k.SortKey END ASC,
        CASE WHEN @SortDirection = 'DESC' THEN k.SortKey END DESC,
        f.SeniorityOrder,
        f.CrewMemberId,
        f.ServiceId
    OFFSET CAST(@PageNumber - 1 AS BIGINT) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO