using System.Security.Claims;
using Eu.EDelivery.AS4.Fe.Database;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Eu.EDelivery.AS4.Fe.Authentication;

/// <summary>
/// Setup authentication
/// </summary>
/// <seealso cref="IAuthenticationSetup" />
public class AuthenticationSetup : IAuthenticationSetup
{
    public void Run(IServiceCollection services, IConfiguration configuration)
    {
        RegisterOptions(services, configuration);

        var databaseSettings = configuration.GetSection("Authentication").Get<AuthenticationConfiguration>()
            ?? throw new InvalidOperationException("Authentication configuration is missing.");

        services
            .AddDbContextFactory<ApplicationDbContext>(options => SqlConnectionBuilder.Build(databaseSettings.Provider, databaseSettings.ConnectionString, options))
            .AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        var jwtOptions = services.BuildServiceProvider().GetRequiredService<IOptionsSnapshot<JwtOptions>>().Value;
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = jwtOptions.SigningKey,
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = false,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            RoleClaimType = ClaimTypes.Role
        };

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = tokenValidationParameters;
            });

        // Update token settings when JwtOptions change
        services
            .BuildServiceProvider()
            .GetRequiredService<IOptionsMonitor<JwtOptions>>()
            .OnChange(x => tokenValidationParameters.IssuerSigningKey = x.SigningKey);

        services.AddScoped<ITokenService, TokenService>();
    }

    public void Run(IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            if (string.IsNullOrWhiteSpace(context.Request.Headers.Authorization))
            {
                if (context.Request.QueryString.HasValue)
                {
                    var token = context.Request.QueryString.Value
                        .Split('&')
                        .SingleOrDefault(x => x.Contains("access_token"))?.Split('=')[1];
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        context.Request.Headers.Authorization = $"Bearer {token}";
                    }
                }
            }
            await next.Invoke();
        });
    }

    private static void RegisterOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AuthenticationConfiguration>(configuration.GetSection("Authentication"));
        services.Configure<JwtOptions>(configuration.GetSection("Authentication:JwtOptions"));
    }
}
