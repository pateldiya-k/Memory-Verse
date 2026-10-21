using System.Net.Mail;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MemoryVerse.Data;
using MemoryVerse.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")));

// Cookie login. For API calls we return 401 instead of redirecting to a page.
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "memoryverse.auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.ExpireTimeSpan = TimeSpan.FromDays(7);
        o.SlidingExpiration = true;
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
    });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

var allowedMoods = new[] { "Happy", "Excited", "Peaceful", "Nostalgic", "Grateful", "Sad", "Proud" };
var allowedImageExt = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

static int Uid(ClaimsPrincipal u) => int.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!);

static bool IsValidEmail(string email) =>
    email.Length <= 200 && MailAddress.TryCreate(email, out var a) && a.Address == email && email.Contains('.');

static async Task SignInUser(HttpContext http, User u)
{
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, u.Id.ToString()),
        new(ClaimTypes.Name, u.Name),
        new(ClaimTypes.Email, u.Email)
    };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true });
}

static string? Validate(MemoryInput i, string[] moods)
{
    if (string.IsNullOrWhiteSpace(i.Title)) return "Add a title for your memory.";
    if (i.Title.Trim().Length > 120) return "Title can be at most 120 characters.";
    if (string.IsNullOrWhiteSpace(i.Story)) return "Write a few lines about this memory.";
    if (i.Story.Trim().Length > 5000) return "Story can be at most 5000 characters.";
    if (i.MemoryDate is null) return "Pick the date this happened.";
    if (!string.IsNullOrWhiteSpace(i.Mood) && !moods.Contains(i.Mood)) return "Choose a mood from the list.";
    return null;
}

/* =====================  AUTH  ===================== */
var auth = app.MapGroup("/api/auth");

auth.MapPost("/register", async (RegisterInput input, AppDbContext db, IPasswordHasher<User> hasher, HttpContext http) =>
{
    var name = input.Name?.Trim() ?? "";
    var email = input.Email?.Trim().ToLowerInvariant() ?? "";
    var password = input.Password ?? "";

    if (name.Length < 2 || name.Length > 50) return Results.BadRequest(new { error = "Enter your name (2 to 50 characters)." });
    if (!IsValidEmail(email)) return Results.BadRequest(new { error = "Enter a valid email address." });
    if (password.Length < 6) return Results.BadRequest(new { error = "Password must be at least 6 characters." });
    if (await db.Users.AnyAsync(u => u.Email == email))
        return Results.Conflict(new { error = "An account with this email already exists. Try logging in." });

    var user = new User { Name = name, Email = email };
    user.PasswordHash = hasher.HashPassword(user, password);   // never store the plain password
    db.Users.Add(user);
    await db.SaveChangesAsync();

    db.Memories.Add(new Memory
    {
        UserId = user.Id,
        Title = "Welcome to MemoryVerse",
        Story = "This is your first memory. Choose “Write a memory” to add your own, or edit and delete this one.",
        MemoryDate = DateTime.Today,
        Mood = "Happy",
        Category = "Welcome"
    });
    await db.SaveChangesAsync();

    await SignInUser(http, user);
    return Results.Ok(new { user.Name, user.Email });
});

auth.MapPost("/login", async (LoginInput input, AppDbContext db, IPasswordHasher<User> hasher, HttpContext http) =>
{
    var email = input.Email?.Trim().ToLowerInvariant() ?? "";
    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

    var ok = user is not null &&
             hasher.VerifyHashedPassword(user, user.PasswordHash, input.Password ?? "") != PasswordVerificationResult.Failed;
    if (!ok) return Results.Json(new { error = "Email or password is incorrect." }, statusCode: 401);

    await SignInUser(http, user!);
    return Results.Ok(new { user!.Name, user.Email });
});

auth.MapPost("/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.NoContent();
});

auth.MapGet("/me", (ClaimsPrincipal u) =>
    Results.Ok(new { name = u.Identity?.Name, email = u.FindFirstValue(ClaimTypes.Email) }))
    .RequireAuthorization();

/* =====================  MEMORIES (login required)  ===================== */
var api = app.MapGroup("/api").RequireAuthorization();

api.MapGet("/memories", async (ClaimsPrincipal user, AppDbContext db, string? search, string? mood, bool? favorite) =>
{
    var uid = Uid(user);
    var q = db.Memories.Where(m => m.UserId == uid);

    if (!string.IsNullOrWhiteSpace(search))
    {
        var s = search.Trim();
        q = q.Where(m => m.Title.Contains(s) || m.Story.Contains(s) ||
                         (m.Category != null && m.Category.Contains(s)));
    }
    if (!string.IsNullOrWhiteSpace(mood)) q = q.Where(m => m.Mood == mood);
    if (favorite == true) q = q.Where(m => m.IsFavorite);

    return Results.Ok(await q.OrderByDescending(m => m.MemoryDate).ThenByDescending(m => m.Id).ToListAsync());
});

api.MapGet("/memories/{id:int}", async (int id, ClaimsPrincipal user, AppDbContext db) =>
{
    var uid = Uid(user);
    var m = await db.Memories.FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid);
    return m is null ? Results.NotFound() : Results.Ok(m);
});

api.MapPost("/memories", async (MemoryInput input, ClaimsPrincipal user, AppDbContext db) =>
{
    var error = Validate(input, allowedMoods);
    if (error is not null) return Results.BadRequest(new { error });

    var m = new Memory
    {
        UserId = Uid(user),
        Title = input.Title!.Trim(),
        Story = input.Story!.Trim(),
        MemoryDate = input.MemoryDate!.Value.Date,
        Mood = string.IsNullOrWhiteSpace(input.Mood) ? "Happy" : input.Mood,
        Category = string.IsNullOrWhiteSpace(input.Category) ? null : input.Category.Trim(),
        ImageUrl = string.IsNullOrWhiteSpace(input.ImageUrl) ? null : input.ImageUrl
    };
    db.Memories.Add(m);
    await db.SaveChangesAsync();
    return Results.Created($"/api/memories/{m.Id}", m);
});

api.MapPut("/memories/{id:int}", async (int id, MemoryInput input, ClaimsPrincipal user, AppDbContext db) =>
{
    var uid = Uid(user);
    var m = await db.Memories.FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid);
    if (m is null) return Results.NotFound();

    var error = Validate(input, allowedMoods);
    if (error is not null) return Results.BadRequest(new { error });

    m.Title = input.Title!.Trim();
    m.Story = input.Story!.Trim();
    m.MemoryDate = input.MemoryDate!.Value.Date;
    m.Mood = string.IsNullOrWhiteSpace(input.Mood) ? "Happy" : input.Mood;
    m.Category = string.IsNullOrWhiteSpace(input.Category) ? null : input.Category.Trim();
    m.ImageUrl = string.IsNullOrWhiteSpace(input.ImageUrl) ? null : input.ImageUrl;
    await db.SaveChangesAsync();
    return Results.Ok(m);
});

api.MapPatch("/memories/{id:int}/favorite", async (int id, ClaimsPrincipal user, AppDbContext db) =>
{
    var uid = Uid(user);
    var m = await db.Memories.FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid);
    if (m is null) return Results.NotFound();
    m.IsFavorite = !m.IsFavorite;
    await db.SaveChangesAsync();
    return Results.Ok(m);
});

api.MapDelete("/memories/{id:int}", async (int id, ClaimsPrincipal user, AppDbContext db) =>
{
    var uid = Uid(user);
    var m = await db.Memories.FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid);
    if (m is null) return Results.NotFound();
    db.Memories.Remove(m);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

api.MapGet("/stats", async (ClaimsPrincipal user, AppDbContext db) =>
{
    var uid = Uid(user);
    var mine = db.Memories.Where(m => m.UserId == uid);
    var total = await mine.CountAsync();
    var favorites = await mine.CountAsync(m => m.IsFavorite);
    var first = await mine.OrderBy(m => m.MemoryDate).Select(m => (DateTime?)m.MemoryDate).FirstOrDefaultAsync();
    return Results.Ok(new { total, favorites, since = first });
});

api.MapPost("/upload", async (IFormFile file, IWebHostEnvironment env) =>
{
    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (!allowedImageExt.Contains(ext)) return Results.BadRequest(new { error = "Upload a JPG, PNG, GIF or WebP image." });
    if (file.Length > 5 * 1024 * 1024) return Results.BadRequest(new { error = "Image must be smaller than 5 MB." });

    var dir = Path.Combine(env.WebRootPath, "uploads");
    Directory.CreateDirectory(dir);
    var name = $"{Guid.NewGuid():N}{ext}";
    await using var fs = File.Create(Path.Combine(dir, name));
    await file.CopyToAsync(fs);
    return Results.Ok(new { url = $"/uploads/{name}" });
}).DisableAntiforgery();

app.Run();
