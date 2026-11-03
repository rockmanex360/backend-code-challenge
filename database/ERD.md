# ERD

Twelve tables. Reference data is separate from transactional data, and the chart of accounts is a tree that the report rolls up at query time.

```mermaid
erDiagram
    FiscalYear ||--o{ Ship : "period for"
    UserRole ||--o{ AppUser : "role of"
    AppUser ||--o{ UserShip : "is assigned"
    Ship ||--o{ UserShip : "assigned to"
    Nationality ||--o{ CrewMember : "nationality of"
    CrewMember ||--o{ CrewService : "serves on"
    Ship ||--o{ CrewService : "employs"
    CrewRank ||--o{ CrewService : "rank on"
    Account |o--o{ Account : "parent of"
    Ship ||--o{ Budget : "budgeted for"
    Account ||--o{ Budget : "line of"
    Ship ||--o{ AccountTransaction : "spent by"
    Account ||--o{ AccountTransaction : "line of"

    FiscalYear {
        char(4) FiscalYearCode PK
        tinyint StartMonth
        tinyint EndMonth
        nvarchar(50) Description
    }

    UserRole {
        tinyint RoleId PK
        nvarchar(30) Name
    }

    AppUser {
        int UserId PK
        nvarchar(100) Name
        tinyint RoleId FK
        datetime2 CreatedAtUtc
    }

    UserShip {
        int UserId PK, FK
        varchar(10) ShipCode PK, FK
        datetime2 AssignedAtUtc
    }

    Ship {
        varchar(10) ShipCode PK
        nvarchar(100) Name
        char(4) FiscalYearCode FK
        bit IsActive
    }

    Nationality {
        char(2) NationalityCode PK
        nvarchar(50) Name
    }

    CrewMember {
        varchar(20) CrewMemberId PK
        nvarchar(50) FirstName
        nvarchar(50) LastName
        date BirthDate
        char(2) NationalityCode FK
    }

    CrewRank {
        smallint CrewRankId PK
        nvarchar(50) Name
        varchar(20) Department
        smallint SeniorityOrder
    }

    CrewService {
        bigint ServiceId PK
        varchar(20) CrewMemberId FK
        varchar(10) ShipCode FK
        smallint CrewRankId FK
        date SignOnDate
        date SignOffDate
        date EndOfContractDate
    }

    Account {
        varchar(10) AccountNumber PK
        nvarchar(100) Description
        varchar(10) ParentAccountNumber FK
    }

    Budget {
        bigint BudgetId PK
        varchar(10) ShipCode FK
        varchar(10) AccountNumber FK
        date AccountPeriod
        decimal(18,2) BudgetValue
    }

    AccountTransaction {
        bigint TransactionId PK
        varchar(10) ShipCode FK
        varchar(10) AccountNumber FK
        date AccountPeriod
        decimal(18,2) ActualValue
    }
```

A few things that are not obvious from the picture:

- `CrewService` is the many to many between crew and ships, and it is also where the rank lives. A crew member can have an open contract on one ship and a planned one on another, which is why status is derived per contract rather than stored on `CrewMember`.
- `Account` points at itself through `ParentAccountNumber`. Budget and transaction rows only sit on leaves; the report walks the tree with a recursive closure and adds the leaves up for each parent.
- `FiscalYear` holds all twelve possible twelve month patterns (`0112` through `1211`), and `Ship.FiscalYearCode` is a foreign key to it. The check constraint keeps the code consistent with the start and end month, so a typo cannot produce a wrong YTD later.
- `UserShip` is a plain join table with an assignment timestamp; the API assigns and removes rows, nothing else.
