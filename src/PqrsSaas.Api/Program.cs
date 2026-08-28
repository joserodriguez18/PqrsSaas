using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PqrsSaas.Api.Cors;
using PqrsSaas.Api.Hubs;
using PqrsSaas.Api.Middleware;
using PqrsSaas.Application;
using PqrsSaas.Infrastructure.Integrations;
using PqrsSaas.Infrastructure.Persistence;
using PqrsSaas.Infrastructure.Provisioning;
using PqrsSaas.Infrastructure.Security;
using PqrsSaas.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Mantener los tipos de claim tal como se emiten en el token (sub, role, tenantId...).
// Sin esto, el handler de JWT re-mapea "sub" a nameidentifier y User.FindFirst("sub")
// devolvería null.
System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

// --- Base de control (una sola, compartida) ---
builder.Services.AddDbContext<ControlDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("ControlDb")));

// --- Base operativa (una por tenant, resuelta en runtime) ---
builder.Services.AddScoped<ITenantConnectionAccessor, TenantConnectionAccessor>();
builder.Services.AddDbContext<CoreDbContext>(); // sin UseNpgsql aquí: lo resuelve OnConfiguring

builder.Services.AddScoped<TenantProvisioningService>();
builder.Services.AddScoped<PasswordService>();
builder.Services.AddScoped<TokenService>();

// Cliente HTTP hacia Gemini + servicios de IA.
builder.Services.AddHttpClient<GeminiService>();
builder.Services.AddScoped<TriajeService>();
builder.Services.AddSingleton<DocumentIngestionService>();

// Email (SMTP). Si no hay Smtp:Host configurado, no se envía correo y las
// credenciales temporales se devuelven en la respuesta (dev).
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

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
        // SignalR no puede poner el header Authorization en el handshake de
        // WebSocket desde el navegador; el token viaja por query string (?access_token=).
        // Lo leemos aquí solo para las rutas del hub.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var path = context.HttpContext.Request.Path;
                if (path.StartsWithSegments("/hubs"))
                {
                    var token = context.Request.Query["access_token"].FirstOrDefault();
                    if (!string.IsNullOrEmpty(token))
                        context.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// CORS dinámico por tenant: la política se resuelve en cada request según el
// Origin y el DominioPermitido del tenant (ver TenantCorsPolicyProvider).
builder.Services.AddCors();
builder.Services.AddSingleton<Microsoft.AspNetCore.Cors.Infrastructure.ICorsPolicyProvider, TenantCorsPolicyProvider>();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR(); // hub de notificaciones en tiempo real (módulo 7)

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpsRedirection();
app.UseCors();
// Orden importante: autenticación primero (llena context.User), luego el
// middleware de tenant (que lee el claim tenantId de context.User), luego
// autorización ([Authorize] ya puede evaluar el usuario autenticado).
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHub<TicketsHub>("/hubs/tickets");

app.Run();
