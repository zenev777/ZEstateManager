using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using ZEstate.Core.Interfaces;
using ZEstate.Infrastructure;
using ZEstate.Infrastructure.Data.DataConstants;
using ZEstate.Infrastructure.Data.IdentityModels;
using ZEstate.Infrastructure.Data.Repository;
using ZEstate.Infrastructure.Services;
using ZEstateApi.Authorization;
using ZEstateApi.Filters;
using ZEstateApi.Hubs;

var builder = WebApplication.CreateBuilder(args);

const string AngularClientCorsPolicy = "AngularClient";

// Render (and most PaaS hosts) inject the port to bind to via $PORT.
var renderPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(renderPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{renderPort}");
}

// Add services to the container.

builder.Services.AddControllers(options => options.Filters.Add<ApiExceptionFilter>());
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IRepository, Repository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBuildingService, BuildingService>();
builder.Services.AddScoped<IFeeService, FeeService>();
builder.Services.AddScoped<IRepairService, RepairService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IMeetingService, MeetingService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IUserNotificationService, UserNotificationService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IUserRoleService, UserRoleService>();
builder.Services.AddScoped<IManagerTransferRequestService, ManagerTransferRequestService>();
builder.Services.AddScoped<IVotingService, VotingService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddSingleton<NotificationEmailQueue>();
builder.Services.AddHostedService<NotificationEmailDispatcher>();
builder.Services.AddScoped<IObligationGenerationService, ObligationGenerationService>();
builder.Services.AddHostedService<ObligationGenerationBackgroundService>();
builder.Services.AddScoped<IObligationStatusService, ObligationStatusService>();
builder.Services.AddHostedService<ObligationStatusBackgroundService>();
builder.Services.AddScoped<IManagerTransferService, ManagerTransferService>();
builder.Services.AddHostedService<ManagerTransferBackgroundService>();

builder.Services
    .AddIdentity<ApplicationUser, ApplicationRole>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };

        // SignalR's WebSocket/SSE transports can't set an Authorization header, so the
        // client sends the JWT as an "access_token" query param for the hub path instead.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options => options.AddZEstatePolicies());

// "Cors:AllowedOrigins" in appsettings/env vars, falling back to the local dev server.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:4200"];

builder.Services.AddCors(options =>
{
    options.AddPolicy(AngularClientCorsPolicy, policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            // SignalR's client negotiation can use cookies/credentials depending on
            // the transport it falls back to; AllowCredentials needs explicit
            // origins (already the case via WithOrigins, never AllowAnyOrigin).
            .AllowCredentials();
    });
});

var app = builder.Build();

// Seed the fixed application roles used by registration.
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    foreach (var roleName in RoleNames.All)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
            await roleManager.CreateAsync(new ApplicationRole { Name = roleName });
    }

    // Off by default everywhere - only seeds a QA login fixture when the host
    // explicitly sets "Seed:QaFixture" (e.g. Seed__QaFixture=true on Render).
    if (builder.Configuration.GetValue<bool>("Seed:QaFixture"))
    {
        await QaSeedData.SeedAsync(scope.ServiceProvider);
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Render terminates TLS at its edge and forwards plain HTTP, so redirecting
// to HTTPS inside the container would loop forever — only redirect locally.
if (string.IsNullOrEmpty(renderPort))
{
    app.UseHttpsRedirection();
}

app.UseCors(AngularClientCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

app.Run();
