using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PqrsSaas.Api.Middleware;
using PqrsSaas.Application;
using PqrsSaas.Infrastructure.Persistence;
using PqrsSaas.Infrastructure.Provisioning;
using PqrsSaas.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

// --- Base de control (una sola, compartida) ---
builder.Services.AddDbContext<ControlDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("ControlDb")));

// --- Base operativa (una por tenant, resuelta en runtime) ---
builder.Services.AddScoped<ITenantConnectionAccessor, TenantConnectionAccessor>();
builder.Services.AddDbContext<CoreDbContext>(); // sin UseNpgsql aquí: lo resuelve OnConfiguring

builder.Services.AddScoped<TenantProvisioningService>();
builder.Services.AddScoped<PasswordService>();
builder.Services.AddScoped<TokenService>();

// --- Autenticación JWT ---
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Falta Jwt:Secret en la configuración.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR(); // hub de notificaciones en tiempo real (módulo 7)

// TODO (módulo 5-6): registrar el cliente HttpClient hacia la API de Gemini.

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpsRedirection();
// Orden importante: autenticación primero (llena context.User), luego el
// middleware de tenant (que lee el claim tenantId de context.User), luego
// autorización ([Authorize] ya puede evaluar el usuario autenticado).
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

app.MapControllers();
// TODO (módulo 7): app.MapHub<TicketsHub>("/hubs/tickets");

app.Run();
