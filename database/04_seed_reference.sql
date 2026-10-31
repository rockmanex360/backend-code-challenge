SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

INSERT INTO dbo.UserRole (Name)
SELECT v.Name
FROM (VALUES (N'Admin'), (N'Manager'), (N'Viewer')) AS v(Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.UserRole r WHERE r.Name = v.Name);

-- Fiscal years: all 12 start months, EndMonth = (StartMonth + 10) % 12 + 1
INSERT INTO dbo.FiscalYear (FiscalYearCode, StartMonth, EndMonth, Description)
SELECT v.FiscalYearCode, v.StartMonth, v.EndMonth, v.Description
FROM (VALUES
    ('0112',  1, 12, N'Jan-Dec'),
    ('0201',  2,  1, N'Feb-Jan'),
    ('0302',  3,  2, N'Mar-Feb'),
    ('0403',  4,  3, N'Apr-Mar'),
    ('0504',  5,  4, N'May-Apr'),
    ('0605',  6,  5, N'Jun-May'),
    ('0706',  7,  6, N'Jul-Jun'),
    ('0807',  8,  7, N'Aug-Jul'),
    ('0908',  9,  8, N'Sep-Aug'),
    ('1009', 10,  9, N'Oct-Sep'),
    ('1110', 11, 10, N'Nov-Oct'),
    ('1211', 12, 11, N'Dec-Nov')
) AS v(FiscalYearCode, StartMonth, EndMonth, Description)
WHERE NOT EXISTS (SELECT 1 FROM dbo.FiscalYear f WHERE f.FiscalYearCode = v.FiscalYearCode);

-- Nationalities: ISO 3166-1 alpha-2 + demonym
INSERT INTO dbo.Nationality (NationalityCode, Name)
SELECT v.NationalityCode, v.Name
FROM (VALUES
    ('GR', N'Greek'),
    ('MX', N'Mexican'),
    ('PH', N'Filipino'),
    ('ID', N'Indonesian'),
    ('IN', N'Indian'),
    ('UA', N'Ukrainian'),
    ('PL', N'Polish'),
    ('HR', N'Croatian'),
    ('CN', N'Chinese'),
    ('VN', N'Vietnamese'),
    ('MM', N'Burmese'),
    ('RU', N'Russian'),
    ('GB', N'British'),
    ('NO', N'Norwegian'),
    ('NL', N'Dutch')
) AS v(NationalityCode, Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Nationality n WHERE n.NationalityCode = v.NationalityCode);

-- Crew ranks (IDENTITY key, natural key Name); SeniorityOrder 1 = most senior
INSERT INTO dbo.CrewRank (Name, Department, SeniorityOrder)
SELECT v.Name, v.Department, v.SeniorityOrder
FROM (VALUES
    (N'Master',                    'Deck',     1),
    (N'Chief Officer',             'Deck',     2),
    (N'Second Officer',            'Deck',     3),
    (N'Third Officer',             'Deck',     4),
    (N'Chief Engineer',            'Engine',   5),
    (N'Second Engineer',           'Engine',   6),
    (N'Third Engineer',            'Engine',   7),
    (N'Electro-Technical Officer', 'Engine',   8),
    (N'Bosun',                     'Deck',     9),
    (N'Able Seaman',               'Deck',    10),
    (N'Oiler',                     'Engine',  11),
    (N'Cook',                      'Catering',12),
    (N'Deck Cadet',                'Cadet',   13)
) AS v(Name, Department, SeniorityOrder)
WHERE NOT EXISTS (SELECT 1 FROM dbo.CrewRank c WHERE c.Name = v.Name);

INSERT INTO dbo.Account (AccountNumber, Description, ParentAccountNumber)
SELECT v.AccountNumber, v.Description, v.ParentAccountNumber
FROM (VALUES
    ('7000000', N'OPERATING EXPENSES', CAST(NULL AS VARCHAR(10)))
) AS v(AccountNumber, Description, ParentAccountNumber)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Account a WHERE a.AccountNumber = v.AccountNumber);

INSERT INTO dbo.Account (AccountNumber, Description, ParentAccountNumber)
SELECT v.AccountNumber, v.Description, v.ParentAccountNumber
FROM (VALUES
    ('7100000', N'AWARD AND GRANT TO INDIVIDUALS',  '7000000'),
    ('7200000', N'CREW COSTS',                      '7000000'),
    ('7300000', N'VESSEL MAINTENANCE',              '7000000'),
    ('7400000', N'FUEL AND LUBRICANTS',             '7000000'),
    ('7500000', N'INSURANCE AND ADMINISTRATION',    '7000000')
) AS v(AccountNumber, Description, ParentAccountNumber)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Account a WHERE a.AccountNumber = v.AccountNumber);

INSERT INTO dbo.Account (AccountNumber, Description, ParentAccountNumber)
SELECT v.AccountNumber, v.Description, v.ParentAccountNumber
FROM (VALUES
    -- 7100000 AWARD AND GRANT TO INDIVIDUALS
    ('7110000', N'CREW WELFARE GRANTS',           '7100000'),
    ('7120000', N'AWARDS',                        '7100000'),
    ('7125000', N'LONG SERVICE AWARDS',           '7100000'),
    ('7135000', N'SCHOLARSHIPS',                  '7100000'),
    ('7140000', N'SEAFARER EDUCATION GRANTS',     '7100000'),
    ('7150000', N'MEDICAL ASSISTANCE GRANTS',     '7100000'),
    -- 7200000 CREW COSTS
    ('7210000', N'WAGES AND SALARIES',            '7200000'),
    ('7220000', N'OVERTIME',                      '7200000'),
    ('7230000', N'CREW TRAVEL AND REPATRIATION',  '7200000'),
    ('7240000', N'PROVISIONS AND VICTUALLING',    '7200000'),
    ('7250000', N'CREW TRAINING AND CERTIFICATION','7200000'),
    -- 7300000 VESSEL MAINTENANCE
    ('7310000', N'DECK MAINTENANCE',              '7300000'),
    ('7320000', N'ENGINE ROOM MAINTENANCE',       '7300000'),
    ('7330000', N'SPARE PARTS',                   '7300000'),
    ('7340000', N'DRYDOCKING AND SURVEYS',        '7300000'),
    ('7350000', N'PAINT AND HULL CLEANING',       '7300000'),
    ('7360000', N'SAFETY EQUIPMENT',              '7300000'),
    -- 7400000 FUEL AND LUBRICANTS
    ('7410000', N'HEAVY FUEL OIL',                '7400000'),
    ('7420000', N'MARINE GAS OIL',                '7400000'),
    ('7430000', N'LUBRICATING OIL',               '7400000'),
    ('7440000', N'CYLINDER OIL',                  '7400000'),
    ('7450000', N'FRESH WATER',                   '7400000'),
    -- 7500000 INSURANCE AND ADMINISTRATION
    ('7510000', N'HULL AND MACHINERY INSURANCE',  '7500000'),
    ('7520000', N'P AND I INSURANCE',             '7500000'),
    ('7530000', N'SHIP MANAGEMENT FEES',          '7500000'),
    ('7540000', N'COMMUNICATIONS',                '7500000'),
    ('7550000', N'PORT AND AGENCY FEES',          '7500000')
) AS v(AccountNumber, Description, ParentAccountNumber)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Account a WHERE a.AccountNumber = v.AccountNumber);

COMMIT;
GO