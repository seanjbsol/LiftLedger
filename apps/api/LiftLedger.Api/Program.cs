using System.Text;
using System.Text.Json.Serialization;
using LiftLedger.Api.Auth;
using LiftLedger.Api.Billing;
using LiftLedger.Api.Data;
using LiftLedger.Api.Middleware;
using LiftLedger.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

namespace LiftLedger.Api;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "LiftLedger API",
                Version = "v1",
                Description = "Multi-tenant LOLER / PUWER / trailer & plant inspection records for UK workshops and hire fleets. JWT includes tenant_id and role. Not an HSE-certified service."
            });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header
            });
            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });
        });

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
        builder.Services.AddSingleton<JwtTokenService>();
        builder.Services.AddScoped<AuthService>();
        builder.Services.AddScoped<AssetService>();
        builder.Services.AddScoped<InspectionService>();
        builder.Services.AddScoped<DashboardService>();
        builder.Services.AddScoped<ClientService>();
        builder.Services.AddScoped<CertificateService>();
        builder.Services.AddScoped<DefectService>();
        builder.Services.AddScoped<ClientPortalService>();
        builder.Services.AddScoped<IFeatureGate, FeatureGate>();

        builder.Services.Configure<SubscriptionApiOptions>(
            builder.Configuration.GetSection(SubscriptionApiOptions.SectionName));
        builder.Services.AddHttpClient<SubscriptionClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<SubscriptionApiOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.BaseUrl)
                && Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUrl))
            {
                client.BaseAddress = new Uri(baseUrl.ToString().TrimEnd('/') + "/");
            }

            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });
        builder.Services.AddSingleton<StubSubscriptionClient>();
        builder.Services.AddTransient<ISubscriptionClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<SubscriptionApiOptions>>().Value;
            return options.UseStub
                ? sp.GetRequiredService<StubSubscriptionClient>()
                : sp.GetRequiredService<SubscriptionClient>();
        });

        var databaseProvider = builder.Configuration["Database:Provider"] ?? "Sqlite";
        var connectionString = builder.Configuration["Database:ConnectionString"]
                               ?? builder.Configuration.GetConnectionString("Default")
                               ?? "Data Source=liftledger.dev.db";

        builder.Services.AddDbContext<AppDbContext>(options =>
        {
            if (databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(connectionString);
            }
            else
            {
                options.UseSqlite(connectionString);
            }
        });

        var signingKey = JwtConfiguration.GetSigningKey(builder.Configuration, builder.Environment);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "LiftLedger",
                    ValidAudience = builder.Configuration["Jwt:Audience"] ?? "LiftLedger.Clients",
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    NameClaimType = "email",
                    RoleClaimType = "role",
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });
        builder.Services.AddAuthorization();

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("Mobile", policy =>
            {
                policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin();
            });
        });

        var app = builder.Build();

        app.UseMiddleware<ExceptionMappingMiddleware>();
        app.UseCors("Mobile");

        if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
        {
            app.UseHttpsRedirection();
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<SubscriptionGateMiddleware>();
        app.MapControllers();
        app.MapGet("/", () => Results.Redirect("/swagger"));

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
            var seedEnabled = app.Configuration.GetValue("Seed:Enabled", true);
            if (seedEnabled)
            {
                await DatabaseSeeder.SeedAsync(db);
            }
        }

        await app.RunAsync();
    }
}
