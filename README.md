# PICTO SMRS

## New Device Setup

### Requirements

- .NET 10 SDK
- SQL Server LocalDB or SQL Server

### Configure

From the web project folder:

```powershell
cd src\PICTO.SMRS.Web
dotnet restore
```

The default local database is already configured in `appsettings.json`:

```text
Server=(localdb)\mssqllocaldb;Database=PICTO_SMRS;Trusted_Connection=True
```

To use a different database, set it with user secrets:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "YOUR_CONNECTION_STRING"
```

To create the first Department Head account on startup, set the seed account with user secrets:

```powershell
dotnet user-secrets set "Seed:InitialDepartmentHead:UserName" "admin"
dotnet user-secrets set "Seed:InitialDepartmentHead:Email" "admin@example.com"
dotnet user-secrets set "Seed:InitialDepartmentHead:Password" "YourPassword123!"
```

Password must be at least 8 characters and include uppercase, lowercase, number, and symbol.

### Run

```powershell
dotnet run
```

The app applies database migrations automatically on startup.