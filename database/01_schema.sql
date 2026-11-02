SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.UserRole', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserRole (
        RoleId TINYINT IDENTITY(1,1) CONSTRAINT PK_UserRole PRIMARY KEY,
        Name   NVARCHAR(30) NOT NULL CONSTRAINT UQ_UserRole_Name UNIQUE
    );
END
GO

IF OBJECT_ID(N'dbo.AppUser', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppUser (
        UserId       INT IDENTITY(1,1) CONSTRAINT PK_AppUser PRIMARY KEY,
        Name         NVARCHAR(100) NOT NULL,
        RoleId       TINYINT NOT NULL CONSTRAINT FK_AppUser_UserRole REFERENCES dbo.UserRole(RoleId),
        CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_AppUser_Created DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_AppUser_Name CHECK (LEN(LTRIM(RTRIM(Name))) > 0)
    );
END
GO

IF OBJECT_ID(N'dbo.FiscalYear', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FiscalYear (
        FiscalYearCode CHAR(4)      NOT NULL CONSTRAINT PK_FiscalYear PRIMARY KEY,   -- 'MMMM': start month + end month
        StartMonth     TINYINT      NOT NULL,
        EndMonth       TINYINT      NOT NULL,
        Description    NVARCHAR(50) NOT NULL,
        CONSTRAINT UQ_FiscalYear_StartMonth UNIQUE (StartMonth),
        CONSTRAINT CK_FiscalYear_StartMonth CHECK (StartMonth BETWEEN 1 AND 12),
        CONSTRAINT CK_FiscalYear_EndMonth   CHECK (EndMonth = (StartMonth + 10) % 12 + 1), 
        CONSTRAINT CK_FiscalYear_Code       CHECK (FiscalYearCode = RIGHT('0' + CAST(StartMonth AS VARCHAR(2)), 2)
                                                                  + RIGHT('0' + CAST(EndMonth   AS VARCHAR(2)), 2))
    );
END
GO

IF OBJECT_ID(N'dbo.Ship', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Ship (
        ShipCode       VARCHAR(10)   NOT NULL CONSTRAINT PK_Ship PRIMARY KEY,
        Name           NVARCHAR(100) NOT NULL,
        FiscalYearCode CHAR(4)       NOT NULL CONSTRAINT FK_Ship_FiscalYear REFERENCES dbo.FiscalYear(FiscalYearCode),
        IsActive       BIT           NOT NULL CONSTRAINT DF_Ship_IsActive DEFAULT 1,
        CONSTRAINT CK_Ship_Code CHECK (LEN(ShipCode) >= 2 AND ShipCode NOT LIKE '%[^A-Z0-9]%' COLLATE Latin1_General_BIN)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Ship_FiscalYear' AND object_id = OBJECT_ID(N'dbo.Ship'))
    CREATE INDEX IX_Ship_FiscalYear ON dbo.Ship(FiscalYearCode);
GO

IF OBJECT_ID(N'dbo.UserShip', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserShip (
        UserId        INT          NOT NULL CONSTRAINT FK_UserShip_User REFERENCES dbo.AppUser(UserId),
        ShipCode      VARCHAR(10)  NOT NULL CONSTRAINT FK_UserShip_Ship REFERENCES dbo.Ship(ShipCode),
        AssignedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_UserShip_At DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_UserShip PRIMARY KEY (UserId, ShipCode)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UserShip_Ship' AND object_id = OBJECT_ID(N'dbo.UserShip'))
    CREATE INDEX IX_UserShip_Ship ON dbo.UserShip(ShipCode);
GO

IF OBJECT_ID(N'dbo.CrewRank', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CrewRank (
        CrewRankId     SMALLINT IDENTITY(1,1) CONSTRAINT PK_CrewRank PRIMARY KEY,
        Name           NVARCHAR(50) NOT NULL CONSTRAINT UQ_CrewRank_Name UNIQUE,
        Department     VARCHAR(20)  NOT NULL,
        SeniorityOrder SMALLINT     NOT NULL,          -- 1 = most senior
        CONSTRAINT CK_CrewRank_Department CHECK (Department IN ('Deck','Engine','Catering','Cadet'))
    );
END
GO

IF OBJECT_ID(N'dbo.Nationality', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Nationality (
        NationalityCode CHAR(2)      NOT NULL CONSTRAINT PK_Nationality PRIMARY KEY,   -- ISO 3166-1 alpha-2 (GR, MX, ...)
        Name            NVARCHAR(50) NOT NULL CONSTRAINT UQ_Nationality_Name UNIQUE,   -- demonym shown in output: Greek, Mexican
        CONSTRAINT CK_Nationality_Code CHECK (NationalityCode LIKE '[A-Z][A-Z]' COLLATE Latin1_General_BIN)
    );
END
GO

IF OBJECT_ID(N'dbo.CrewMember', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CrewMember (
        CrewMemberId    VARCHAR(20)  NOT NULL CONSTRAINT PK_CrewMember PRIMARY KEY,
        FirstName       NVARCHAR(50) NOT NULL,
        LastName        NVARCHAR(50) NOT NULL,
        BirthDate       DATE         NOT NULL,
        NationalityCode CHAR(2)      NOT NULL CONSTRAINT FK_CrewMember_Nationality REFERENCES dbo.Nationality(NationalityCode),
        CONSTRAINT CK_CrewMember_BirthDate CHECK (BirthDate >= '1900-01-01')
    );
END
GO

IF OBJECT_ID(N'dbo.CrewService', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CrewService (
        ServiceId         BIGINT IDENTITY(1,1) CONSTRAINT PK_CrewService PRIMARY KEY,
        CrewMemberId      VARCHAR(20) NOT NULL CONSTRAINT FK_CrewService_Crew REFERENCES dbo.CrewMember(CrewMemberId),
        ShipCode          VARCHAR(10) NOT NULL CONSTRAINT FK_CrewService_Ship REFERENCES dbo.Ship(ShipCode),
        CrewRankId        SMALLINT    NOT NULL CONSTRAINT FK_CrewService_CrewRank REFERENCES dbo.CrewRank(CrewRankId),
        SignOnDate        DATE NOT NULL,
        SignOffDate       DATE NULL,
        EndOfContractDate DATE NOT NULL,
        CONSTRAINT CK_CrewService_Eoc     CHECK (EndOfContractDate >= SignOnDate),
        CONSTRAINT CK_CrewService_SignOff CHECK (SignOffDate IS NULL OR SignOffDate >= SignOnDate)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CrewService_ShipOpen' AND object_id = OBJECT_ID(N'dbo.CrewService'))
    CREATE INDEX IX_CrewService_ShipOpen ON dbo.CrewService(ShipCode)
        INCLUDE (CrewMemberId, CrewRankId, SignOnDate, EndOfContractDate) WHERE SignOffDate IS NULL;
GO

IF OBJECT_ID(N'dbo.Account', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Account (
        AccountNumber       VARCHAR(10)   NOT NULL CONSTRAINT PK_Account PRIMARY KEY,
        Description         NVARCHAR(100) NOT NULL,
        ParentAccountNumber VARCHAR(10)   NULL CONSTRAINT FK_Account_Parent REFERENCES dbo.Account(AccountNumber),
        CONSTRAINT CK_Account_Number  CHECK (LEN(AccountNumber) > 0 AND AccountNumber NOT LIKE '%[^0-9]%'),
        CONSTRAINT CK_Account_NotSelf CHECK (ParentAccountNumber IS NULL OR ParentAccountNumber <> AccountNumber)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Account_Parent' AND object_id = OBJECT_ID(N'dbo.Account'))
    CREATE INDEX IX_Account_Parent ON dbo.Account(ParentAccountNumber);
GO

IF OBJECT_ID(N'dbo.Budget', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Budget (
        BudgetId      BIGINT IDENTITY(1,1) CONSTRAINT PK_Budget PRIMARY KEY,
        ShipCode      VARCHAR(10)   NOT NULL CONSTRAINT FK_Budget_Ship REFERENCES dbo.Ship(ShipCode),
        AccountNumber VARCHAR(10)   NOT NULL CONSTRAINT FK_Budget_Account REFERENCES dbo.Account(AccountNumber),
        AccountPeriod DATE          NOT NULL,                       -- first day of month
        BudgetValue   DECIMAL(18,2) NOT NULL,
        CONSTRAINT CK_Budget_Value  CHECK (BudgetValue >= 0),
        CONSTRAINT CK_Budget_Period CHECK (DAY(AccountPeriod) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Budget_ShipPeriod' AND object_id = OBJECT_ID(N'dbo.Budget'))
    CREATE INDEX IX_Budget_ShipPeriod ON dbo.Budget(ShipCode, AccountPeriod) INCLUDE (AccountNumber, BudgetValue);
GO

IF OBJECT_ID(N'dbo.AccountTransaction', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AccountTransaction (
        TransactionId BIGINT IDENTITY(1,1) CONSTRAINT PK_AccountTransaction PRIMARY KEY,
        ShipCode      VARCHAR(10)   NOT NULL CONSTRAINT FK_Txn_Ship REFERENCES dbo.Ship(ShipCode),
        AccountNumber VARCHAR(10)   NOT NULL CONSTRAINT FK_Txn_Account REFERENCES dbo.Account(AccountNumber),
        AccountPeriod DATE          NOT NULL,
        ActualValue   DECIMAL(18,2) NOT NULL,
        CONSTRAINT CK_Txn_Value  CHECK (ActualValue >= 0),
        CONSTRAINT CK_Txn_Period CHECK (DAY(AccountPeriod) = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Txn_ShipPeriod' AND object_id = OBJECT_ID(N'dbo.AccountTransaction'))
    CREATE INDEX IX_Txn_ShipPeriod ON dbo.AccountTransaction(ShipCode, AccountPeriod) INCLUDE (AccountNumber, ActualValue);
GO