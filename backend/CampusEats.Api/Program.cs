using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using CampusEats.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
var connection = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Configure ConnectionStrings__Default before starting the API.");
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connection));
builder.Services.AddProblemDetails();
var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));
var key = builder.Configuration["Jwt:Key"];
var issuer = builder.Configuration["Jwt:Issuer"];
var audience = builder.Configuration["Jwt:Audience"];
var authEnabled = !string.IsNullOrWhiteSpace(key);
if (authEnabled && (Encoding.UTF8.GetByteCount(key!) < 32 || string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience)))
    throw new InvalidOperationException("JWT configuration requires a key of at least 32 UTF-8 bytes, issuer and audience.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    if (authEnabled) o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer=true, ValidateAudience=true, ValidateLifetime=true, ValidateIssuerSigningKey=true,
        ValidIssuer=issuer, ValidAudience=audience, IssuerSigningKey=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key!)), ClockSkew=TimeSpan.FromSeconds(30)
    };
});
builder.Services.AddAuthorization();
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit=10, Window=TimeSpan.FromMinutes(1), QueueLimit=0 }));
});
var app = builder.Build();
app.UseExceptionHandler();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
if (builder.Configuration.GetValue<bool>("Database:ApplyMigrations"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    app.Logger.LogInformation("Database migrations applied successfully.");
}
if (!authEnabled) app.Logger.LogWarning("JWT endpoints are disabled. Configure Jwt__Key to enable authentication.");
app.MapGet("/", () => Results.Ok(new { application="CampusEats API", menu="/api/menu", health="/health/ready" }));
app.MapGet("/health/live", () => Results.Ok(new { status="live" }));
app.MapGet("/health/ready", async (AppDbContext db, CancellationToken ct) =>
{
    try { await db.MenuItems.AsNoTracking().Take(1).ToListAsync(ct); return Results.Ok(new { status="ready" }); }
    catch (Exception e) when (e is not OperationCanceledException) { return Results.Problem("Database or schema unavailable.",statusCode:503); }
});
app.MapGet("/api/menu", async (string? category, AppDbContext db, CancellationToken ct) =>
{
    var query = db.MenuItems.AsNoTracking();
    if (!string.IsNullOrWhiteSpace(category)) query=query.Where(x=>x.Category.ToLower()==category.Trim().ToLower());
    return Results.Ok(await query.OrderBy(x=>x.Id).ToListAsync(ct));
});
app.MapPost("/api/auth/register", async (Credentials input, AppDbContext db, IPasswordHasher<AppUser> hasher, CancellationToken ct) =>
{
    if (!authEnabled) return Results.Problem("Authentication is not configured.",statusCode:503);
    if (!input.IsValid()) return Results.ValidationProblem(new Dictionary<string,string[]> { ["credentials"]=["Enter a valid email and a password of 10–128 characters."] });
    var user = new AppUser { Id=Guid.NewGuid(), Email=input.Email!.Trim().ToLowerInvariant() };
    user.PasswordHash=hasher.HashPassword(user,input.Password!);
    db.Users.Add(user);
    try { await db.SaveChangesAsync(ct); }
    catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
    { return Results.Conflict(new { message="Unable to register with these credentials." }); }
    return Results.Created("/api/auth/me",new { user.Id, user.Email });
}).RequireRateLimiting("auth");
app.MapPost("/api/auth/login", async (Credentials input, AppDbContext db, IPasswordHasher<AppUser> hasher, CancellationToken ct) =>
{
    if (!authEnabled) return Results.Problem("Authentication is not configured.",statusCode:503);
    if (!input.IsValid()) return Results.BadRequest(new { message="Invalid credentials format." });
    var email=input.Email!.Trim().ToLowerInvariant();
    var user=await db.Users.SingleOrDefaultAsync(x=>x.Email==email,ct);
    if (user is null || hasher.VerifyHashedPassword(user,user.PasswordHash,input.Password!)==PasswordVerificationResult.Failed) return Results.Unauthorized();
    var expires=DateTime.UtcNow.AddMinutes(60);
    var token=new JwtSecurityToken(issuer,audience,[new Claim(JwtRegisteredClaimNames.Sub,user.Id.ToString()), new Claim(JwtRegisteredClaimNames.Email,user.Email)],expires:expires,
        signingCredentials:new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key!)),SecurityAlgorithms.HmacSha256));
    return Results.Ok(new { token=new JwtSecurityTokenHandler().WriteToken(token), expiresAt=expires });
}).RequireRateLimiting("auth");
app.MapGet("/api/auth/me", (ClaimsPrincipal user)=>Results.Ok(new { id=user.FindFirstValue(ClaimTypes.NameIdentifier), email=user.FindFirstValue(ClaimTypes.Email) })).RequireAuthorization();
app.Run();
public partial class Program { }
public sealed record Credentials(string? Email, string? Password)
{
    public bool IsValid() => Email is not null && Email.Trim().Length <= 254 && new EmailAddressAttribute().IsValid(Email.Trim()) && Password is { Length: >=10 and <=128 };
}
