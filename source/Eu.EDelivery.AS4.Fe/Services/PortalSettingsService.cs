using System.Security.Claims;
using Eu.EDelivery.AS4.Fe.Authentication;
using Eu.EDelivery.AS4.Fe.Models;
using Eu.EDelivery.AS4.Fe.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Eu.EDelivery.AS4.Fe.Services;

/// <summary>
/// Implementation to manage portal settings using appsettings.json
/// </summary>
/// <seealso cref="IPortalSettingsService" />
public class PortalSettingsService : IPortalSettingsService
{
    private readonly IWebHostEnvironment _hostingEnvironment;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IOptions<PortalSettings> _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortalSettingsService" /> class.
    /// </summary>
    /// <param name="hostingEnvironment">The hosting environment.</param>
    /// <param name="contextFactory">The application database context factory.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="settings">The settings.</param>
    public PortalSettingsService(IWebHostEnvironment hostingEnvironment, IDbContextFactory<ApplicationDbContext> contextFactory, UserManager<ApplicationUser> userManager, IOptions<PortalSettings> settings)
    {
        _hostingEnvironment = hostingEnvironment;
        _contextFactory = contextFactory;
        _userManager = userManager;
        _settings = settings;
    }

    /// <summary>
    /// Saves the specified save.
    /// </summary>
    /// <param name="save">The save.</param>
    /// <returns></returns>
    public async Task Save(PortalSettings save)
    {
        var path = _hostingEnvironment.ContentRootPath;
        var result = JsonConvert.SerializeObject(save);
        var fileName = Path.Combine(path, $"appsettings.{_hostingEnvironment.EnvironmentName}.json");
        if (!File.Exists(fileName)) fileName = $"{path}appsettings.json";

        File.WriteAllText(fileName, result);
        await Task.FromResult(0);
    }

    /// <summary>
    /// Determines whether the portal is in setup state
    /// </summary>
    /// <returns></returns>
    public async Task<bool> IsSetup()
    {
        var context = await _contextFactory.CreateDbContextAsync();
        return await context.Users.AnyAsync();
    }

    /// <summary>
    /// Saves the setup.
    /// </summary>
    /// <returns></returns>
    public async Task SaveSetup(Setup setup)
    {
        // Create the admin & readonly user 
        await CreateUsers(setup);

        _settings.Value.Authentication.JwtOptions.Key = setup.JwtKey;
        await Save(_settings.Value);
    }

    private async Task CreateUsers(Setup setup)
    {
        var context = await _contextFactory.CreateDbContextAsync();
        context.Database.EnsureCreated();
        context.SaveChanges();

        var adminUser = new ApplicationUser { UserName = "admin" };
        var readonlyUser = new ApplicationUser { UserName = "readonly" };

        await _userManager.CreateAsync(adminUser, setup.AdminPassword);
        await _userManager.CreateAsync(readonlyUser, setup.ReadonlyPassword);

        _userManager.AddClaimsAsync(adminUser, [new Claim(ClaimTypes.Role, Roles.Admin)]).Wait();
        _userManager.AddClaimsAsync(readonlyUser, [new Claim(ClaimTypes.Role, Roles.Readonly)]).Wait();
    }
}
