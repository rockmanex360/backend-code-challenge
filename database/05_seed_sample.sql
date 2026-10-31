SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Today DATE = CAST(SYSUTCDATETIME() AS DATE);

BEGIN TRAN;

-- 1. Skip everything if the sample data is already there
IF NOT EXISTS (SELECT 1 FROM dbo.Ship WHERE ShipCode = 'SHIP01')
BEGIN

    -- 2. Number table (1..150) used to generate rows
    CREATE TABLE #Tally (n INT NOT NULL PRIMARY KEY);

    WITH t AS (
        SELECT 1 AS n
        UNION ALL
        SELECT n + 1 FROM t WHERE n < 150
    )
    INSERT INTO #Tally (n)
    SELECT n FROM t
    OPTION (MAXRECURSION 200);

    -- 3. Ships
    INSERT INTO dbo.Ship (ShipCode, Name, FiscalYearCode, IsActive)
    VALUES ('SHIP01', N'Flying Dutchman',        '0112', 1),
           ('SHIP02', N'Thousand Sunny',         '0403', 1),
           ('SHIP03', N'Black Pearl',            '0112', 1),
           ('SHIP04', N'Going Merry',            '0403', 1),
           ('SHIP05', N'Queen Anne''s Revenge',  '0112', 0),
           ('SHIP06', N'Nautilus',               '0403', 0);

    -- 4. Users and their ship assignments
    INSERT INTO dbo.AppUser (Name, RoleId)
    SELECT v.Name, r.RoleId
    FROM (VALUES (N'Maria Santos',  N'Admin'),
                 (N'Dimitri Kosta', N'Manager'),
                 (N'Hana Lee',      N'Manager'),
                 (N'Tomas Novak',   N'Viewer'),
                 (N'Rina Putri',    N'Viewer')) AS v(Name, RoleName)
    JOIN dbo.UserRole r ON r.Name = v.RoleName;

    INSERT INTO dbo.UserShip (UserId, ShipCode)
    SELECT u.UserId, v.ShipCode
    FROM (VALUES (N'Maria Santos',  'SHIP01'),
                 (N'Maria Santos',  'SHIP02'),
                 (N'Maria Santos',  'SHIP03'),
                 (N'Dimitri Kosta', 'SHIP01'),
                 (N'Dimitri Kosta', 'SHIP02'),
                 (N'Hana Lee',      'SHIP03'),
                 (N'Hana Lee',      'SHIP04'),
                 (N'Tomas Novak',   'SHIP04'),
                 (N'Rina Putri',    'SHIP05'),
                 (N'Rina Putri',    'SHIP06')) AS v(UserName, ShipCode)
    JOIN dbo.AppUser u ON u.Name = v.UserName;

    -- 5. Crew layout: 22 rank slots mapped to 132 people on 6 ships
    CREATE TABLE #Slot (
        Slot      SMALLINT     NOT NULL PRIMARY KEY,
        RankName  NVARCHAR(50) NOT NULL,
        SignOnOff INT          NOT NULL,
        SignOffOff INT         NULL,
        EocOff    INT          NOT NULL
    );

    INSERT INTO #Slot (Slot, RankName, SignOnOff, SignOffOff, EocOff)
    VALUES ( 1, N'Master',                    -47, NULL,   53),
           ( 2, N'Chief Engineer',            -64, NULL,   46),
           ( 3, N'Chief Officer',             -81, NULL,   39),
           ( 4, N'Second Engineer',           -58, NULL,   32),
           ( 5, N'Second Officer',            -75, NULL,   25),
           ( 6, N'Third Engineer',            -92, NULL,   18),
           ( 7, N'Third Officer',            -109, NULL,   11),
           ( 8, N'Electro-Technical Officer', -86, NULL,    4),
           ( 9, N'Bosun',                    -103, NULL,   -3),
           (10, N'Able Seaman',              -120, NULL,  -10),
           (11, N'Oiler',                    -137, NULL,  -17),
           (12, N'Cook',                     -114, NULL,  -24),
           (13, N'Deck Cadet',               -120, NULL,  -30),
           (14, N'Able Seaman',              -140, NULL,  -31),
           (15, N'Oiler',                    -160, NULL,  -58),
           (16, N'Cook',                     -180, NULL,  -85),
           (17, N'Third Officer',            -200, -110, -110),
           (18, N'Third Engineer',           -240, -152, -150),
           (19, N'Bosun',                    -280, -194, -190),
           (20, N'Master',                     7,  NULL,  127),
           (21, N'Chief Engineer',            21,  NULL,  141),
           (22, N'Third Officer',             35,  NULL,  155);

    CREATE TABLE #Crew (
        g            INT         NOT NULL PRIMARY KEY,
        CrewMemberId VARCHAR(20) NOT NULL,
        ShipNo       INT         NOT NULL,
        ShipCode     VARCHAR(10) NOT NULL,
        Slot         SMALLINT    NOT NULL
    );

    INSERT INTO #Crew (g, CrewMemberId, ShipNo, ShipCode, Slot)
    SELECT n,
           CONCAT('CREW', RIGHT(CONCAT('000', n), 3)),
           (n - 1) / 22 + 1,
           CONCAT('SHIP0', (n - 1) / 22 + 1),
           (n - 1) % 22 + 1
    FROM #Tally
    WHERE n <= 132;

    -- 6. Crew members: the brief's CREW001 / CREW002
    INSERT INTO dbo.CrewMember (CrewMemberId, FirstName, LastName, BirthDate, NationalityCode)
    VALUES ('CREW001', N'Soka',     N'Philip', '1980-07-30', 'GR'),
           ('CREW002', N'Masteros', N'Philip', '1980-07-30', 'GR');

    -- 7. Crew members: generated for the other slots
    WITH Nat AS (
        SELECT NationalityCode,
               ROW_NUMBER() OVER (ORDER BY NationalityCode) - 1 AS i
        FROM dbo.Nationality
    )
    INSERT INTO dbo.CrewMember (CrewMemberId, FirstName, LastName, BirthDate, NationalityCode)
    SELECT c.CrewMemberId,
           CHOOSE(c.g % 12 + 1, N'Alex', N'Boris', N'Carlos', N'Dmitri', N'Eko', N'Farid',
                                N'Goran', N'Hiro', N'Ivan', N'Jun', N'Kofi', N'Luis'),
           CHOOSE(c.g % 11 + 1, N'Andrade', N'Bakker', N'Castillo', N'Dimitrov', N'Eriksen', N'Fernandez',
                                N'Grabowski', N'Haddad', N'Ismail', N'Jovanovic', N'Kowalski'),
           DATEADD(DAY,
                   -(CASE WHEN c.Slot = 13 THEN 20 + c.g % 4 ELSE 22 + c.g * 7 % 35 END * 365 + c.g * 53 % 365),
                   @Today),
           n.NationalityCode
    FROM #Crew c
    JOIN Nat n ON n.i = (c.g - 1) % 15
    WHERE c.g > 2;

    -- 8. Contracts: 22 per ship with the status mix
    WITH Base AS (
        SELECT c.CrewMemberId, c.ShipNo, c.ShipCode, c.Slot,
               s.RankName, s.SignOnOff, s.SignOffOff, s.EocOff
        FROM #Crew c
        JOIN #Slot s ON s.Slot = c.Slot
        WHERE c.g > 2
    )
    INSERT INTO dbo.CrewService (CrewMemberId, ShipCode, CrewRankId, SignOnDate, SignOffDate, EndOfContractDate)
    SELECT b.CrewMemberId, b.ShipCode, r.CrewRankId,
           DATEADD(DAY, b.SignOnOff, @Today),
           DATEADD(DAY, b.SignOffOff, @Today),
           DATEADD(DAY, b.EocOff, @Today)
    FROM Base b
    JOIN dbo.CrewRank r ON r.Name = b.RankName
    UNION ALL

    -- 9. Contracts: the brief's CREW001 / CREW002 rows
    SELECT v.CrewMemberId, v.ShipCode, r.CrewRankId, v.SignOnDate, v.SignOffDate, v.EndOfContractDate
    FROM (VALUES ('CREW001', 'SHIP01', N'Master',         CAST('2025-04-05' AS DATE), CAST(NULL AS DATE), CAST('2025-07-05' AS DATE)),
                 ('CREW002', 'SHIP01', N'Chief Engineer', CAST('2025-04-04' AS DATE), CAST(NULL AS DATE), CAST('2025-07-04' AS DATE))
         ) AS v(CrewMemberId, ShipCode, RankName, SignOnDate, SignOffDate, EndOfContractDate)
    JOIN dbo.CrewRank r ON r.Name = v.RankName
    UNION ALL

    -- 10. Contracts: past contract on another ship
    SELECT b.CrewMemberId, CONCAT('SHIP0', b.ShipNo % 6 + 1), r.CrewRankId,
           DATEADD(DAY, b.SignOnOff - 200, @Today),
           DATEADD(DAY, b.SignOnOff - 20, @Today),
           DATEADD(DAY, b.SignOnOff - 20, @Today)
    FROM Base b
    JOIN dbo.CrewRank r ON r.Name = N'Third Officer'
    WHERE b.Slot = 5
    UNION ALL

    -- 11. Contracts: planned next contracts on other ships
    SELECT b.CrewMemberId, CONCAT('SHIP0', (b.ShipNo + 1) % 6 + 1), r.CrewRankId,
           DATEADD(DAY, 20, @Today),
           CAST(NULL AS DATE),
           DATEADD(DAY, 140, @Today)
    FROM Base b
    JOIN dbo.CrewRank r ON r.Name = N'Electro-Technical Officer'
    WHERE b.Slot = 8
    UNION ALL
    SELECT b.CrewMemberId, CONCAT('SHIP0', (b.ShipNo + 2) % 6 + 1), r.CrewRankId,
           DATEADD(DAY, 10, @Today),
           CAST(NULL AS DATE),
           DATEADD(DAY, 130, @Today)
    FROM Base b
    JOIN dbo.CrewRank r ON r.Name = N'Able Seaman'
    WHERE b.Slot = 14 AND b.ShipNo % 3 = 0;

    -- 12. Leaf accounts and months to generate for
    CREATE TABLE #Leaf (AccountNumber VARCHAR(10) NOT NULL PRIMARY KEY);

    INSERT INTO #Leaf (AccountNumber)
    SELECT a.AccountNumber
    FROM dbo.Account a
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Account c WHERE c.ParentAccountNumber = a.AccountNumber);

    CREATE TABLE #Period (Period DATE NOT NULL PRIMARY KEY);

    INSERT INTO #Period (Period)
    SELECT DATEADD(MONTH, n - 1, CAST('2024-01-01' AS DATE))
    FROM #Tally
    WHERE n <= 24;

    -- 13. Budgets: generated values
    INSERT INTO dbo.Budget (ShipCode, AccountNumber, AccountPeriod, BudgetValue)
    SELECT s.ShipCode, l.AccountNumber, p.Period,
           ABS(CHECKSUM(s.ShipCode, l.AccountNumber, p.Period)) % 50 * 100
    FROM dbo.Ship s
    CROSS JOIN #Leaf l
    CROSS JOIN #Period p
    WHERE s.ShipCode IN ('SHIP01', 'SHIP02', 'SHIP03', 'SHIP04')
      AND NOT (s.ShipCode = 'SHIP01' AND l.AccountNumber = '7135000'
               AND p.Period IN ('2025-01-01', '2025-02-01', '2025-12-01'))
    UNION ALL

    -- 14. Budgets: the brief's SHIP01 / 7135000 rows
    SELECT v.ShipCode, v.AccountNumber, v.AccountPeriod, v.BudgetValue
    FROM (VALUES ('SHIP01', '7135000', CAST('2025-01-01' AS DATE), CAST(0    AS DECIMAL(18,2))),
                 ('SHIP01', '7135000', CAST('2025-01-01' AS DATE), CAST(1000 AS DECIMAL(18,2))),
                 ('SHIP01', '7135000', CAST('2025-02-01' AS DATE), CAST(1200 AS DECIMAL(18,2))),
                 ('SHIP01', '7135000', CAST('2025-12-01' AS DATE), CAST(900  AS DECIMAL(18,2)))
         ) AS v(ShipCode, AccountNumber, AccountPeriod, BudgetValue);

    -- 15. Actuals: 1-3 rows per account-month, around the budget
    INSERT INTO dbo.AccountTransaction (ShipCode, AccountNumber, AccountPeriod, ActualValue)
    SELECT s.ShipCode, l.AccountNumber, p.Period,
           CASE WHEN k.n <= t.Zr THEN 0
                WHEN k.n < a.Cnt THEN t.Target / (a.Cnt - t.Zr)
                ELSE t.Target - (a.Cnt - t.Zr - 1) * (t.Target / (a.Cnt - t.Zr))
           END
    FROM dbo.Ship s
    CROSS JOIN #Leaf l
    CROSS JOIN #Period p
    CROSS APPLY (SELECT ABS(CHECKSUM(s.ShipCode, l.AccountNumber, p.Period)) % 50 * 100 AS B,
                        1 + ABS(CHECKSUM(N'cnt', s.ShipCode, l.AccountNumber, p.Period)) % 3 AS Cnt) a
    CROSS APPLY (SELECT CASE WHEN a.B = 0
                             THEN ABS(CHECKSUM(N'low', s.ShipCode, l.AccountNumber, p.Period)) % 4 * 100
                             ELSE a.B * (80 + ABS(CHECKSUM(N'pct', s.ShipCode, l.AccountNumber, p.Period)) % 41) / 100
                        END AS Target,
                        CASE WHEN a.Cnt > 1 AND ABS(CHECKSUM(N'zr', s.ShipCode, l.AccountNumber, p.Period)) % 8 = 0
                             THEN 1 ELSE 0 END AS Zr) t
    JOIN #Tally k ON k.n <= a.Cnt
    WHERE s.ShipCode IN ('SHIP01', 'SHIP02', 'SHIP03', 'SHIP04')
      AND p.Period <= '2025-09-01'
      AND ABS(CHECKSUM(N'miss', s.ShipCode, l.AccountNumber, p.Period)) % 10 <> 0
      AND NOT (s.ShipCode = 'SHIP01' AND l.AccountNumber = '7135000' AND p.Period = '2025-01-01')
    UNION ALL

    -- 16. Actuals: the brief's rows
    SELECT v.ShipCode, v.AccountNumber, v.AccountPeriod, v.ActualValue
    FROM (VALUES ('SHIP01', '7135000', CAST('2025-01-01' AS DATE), CAST(300 AS DECIMAL(18,2))),
                 ('SHIP01', '7135000', CAST('2025-01-01' AS DATE), CAST(0   AS DECIMAL(18,2))),
                 ('SHIP01', '7135000', CAST('2025-01-01' AS DATE), CAST(700 AS DECIMAL(18,2)))
         ) AS v(ShipCode, AccountNumber, AccountPeriod, ActualValue);
END

COMMIT;
GO