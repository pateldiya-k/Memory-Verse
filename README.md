# MemoryVerse — Your Life, Your Story

A personal memory journal. Write memories, attach a photo, tag a mood, and see everything on a timeline.

**Features:** register, login/logout (cookie auth, hashed passwords), private memories per user.

**Stack:** ASP.NET Core 8 (Minimal API) · Entity Framework Core · SQLite · HTML / CSS / JavaScript

## Run it

1. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download).
2. In this folder run:
   ```
   dotnet restore
   dotnet run
   ```
3. Open the URL printed in the terminal (for example `http://localhost:5000`).

The database file `memoryverse.db` is created automatically on first run, (delete it if you change the tables).

## Project structure

| Path | Purpose |
|---|---|
| `Program.cs` | App setup and all REST API endpoints |
| `Models/Memory.cs` | `Memory` table model and `MemoryInput` request model |
| `Data/AppDbContext.cs` | EF Core database context and seed data |
| `appsettings.json` | Database connection string |
| `wwwroot/` | Frontend (`index.html`, `css/style.css`, `js/app.js`) and uploaded photos |

## API

| Method | URL | What it does |
|---|---|---|
| GET | `/api/memories?search=&mood=&favorite=` | List memories, newest first |
| GET | `/api/memories/{id}` | Get one memory |
| POST | `/api/memories` | Create a memory |
| PUT | `/api/memories/{id}` | Update a memory |
| PATCH | `/api/memories/{id}/favorite` | Toggle favorite |
| DELETE | `/api/memories/{id}` | Delete a memory |
| GET | `/api/stats` | Totals for the header |
| POST | `/api/upload` | Upload a photo (JPG, PNG, GIF, WebP, max 5 MB) |

## Switch to SQL Server

1. `dotnet add package Microsoft.EntityFrameworkCore.SqlServer --version 8.0.10`
2. In `Program.cs` change `UseSqlite(...)` to `UseSqlServer(...)`.
3. In `appsettings.json` set the connection string, for example:
   `Server=(localdb)\\MSSQLLocalDB;Database=MemoryVerse;Trusted_Connection=True;TrustServerCertificate=True`
4. Delete `memoryverse.db` and run again.

## Ideas to extend

- User login (ASP.NET Core Identity) so each person sees only their own memories
- Tags, albums and a "On this day" view
- Export memories to PDF
