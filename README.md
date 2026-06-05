# normal ass note

Very small note-taking app:

- React frontend with an editable page, sheet-like note tabs, and save buttons.
- ASP.NET Core backend with Identity Core users, JWT auth, and PostgreSQL note storage.
- Browser saves use `localStorage`; database saves require login/register.
- Register requires password confirmation.

## Backend Layout

- `server/` is the API project. It only configures the app and maps HTTP endpoints.
- `server/src/NormalAssNote.Domain` contains core note entities and constants.
- `server/src/NormalAssNote.Application` contains auth/note contracts and note sync behavior.
- `server/src/NormalAssNote.Infrastructure` contains EF Core, Identity, JWT, PostgreSQL setup, and repositories.
- Notes are soft deleted with `DeletedAt`; regular load queries only return notes where `DeletedAt` is null.
- EF Core migrations live in `server/src/NormalAssNote.Infrastructure/Persistence/Migrations`.

## Run

Build and run the full app:

```powershell
dotnet build server\NormalAssNote.Api.csproj -c Release
dotnet run --no-build --project server\NormalAssNote.Api.csproj -c Release --urls http://localhost:5268
```

Open `http://localhost:5268`.

The .NET build runs `npm.cmd run build`, copies `client/dist` into `server/wwwroot`, and serves the React app from ASP.NET Core.

For frontend-only development:

```powershell
cd client
npm.cmd run dev -- --host 127.0.0.1
```

Open `http://localhost:5173`. Vite proxies `/api` to `http://localhost:5268`.

To point the frontend somewhere else:

```powershell
$env:VITE_API_BASE_URL='https://example.com/api'
npm.cmd run dev -- --host 127.0.0.1
```

## Local Database Config

`server/appsettings.Development.json` is ignored by git. Put the local PostgreSQL connection string there:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=192.168.63.218;Port=5432;Database=normal_ass_note;Username=quangsumi;Password=..."
  }
}
```

The API applies EF Core migrations on startup. For development resets, drop the database and rerun migrations instead of patching schema in application code.

## Migrations

Restore the local EF tool:

```powershell
dotnet tool restore
```

Add a migration:

```powershell
dotnet tool run dotnet-ef migrations add MigrationName --project server\src\NormalAssNote.Infrastructure\NormalAssNote.Infrastructure.csproj --startup-project server\NormalAssNote.Api.csproj --context AppDbContext --output-dir Persistence\Migrations
```

Apply migrations manually:

```powershell
dotnet tool run dotnet-ef database update --project server\src\NormalAssNote.Infrastructure\NormalAssNote.Infrastructure.csproj --startup-project server\NormalAssNote.Api.csproj --context AppDbContext
```
