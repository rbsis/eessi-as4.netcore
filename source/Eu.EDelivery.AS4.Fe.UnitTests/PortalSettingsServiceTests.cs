using Eu.EDelivery.AS4.Fe.Authentication;
using Eu.EDelivery.AS4.Fe.Services;
using Eu.EDelivery.AS4.Fe.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Eu.EDelivery.AS4.Fe.UnitTests;

/// <summary>
/// Runtime Settings tests
/// </summary>
public class PortalSettingsServiceTests : IDbContextFactory<ApplicationDbContext>
{
    protected IPortalSettingsService _sut;

    public PortalSettingsServiceTests()
    {
        var hostingEnvironment = Substitute.For<IWebHostEnvironment>();
        hostingEnvironment.ContentRootPath = @"c:\temp\";
        var userManager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(),
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<IPasswordHasher<ApplicationUser>>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            Substitute.For<ILookupNormalizer>(),
            Substitute.For<IdentityErrorDescriber>(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<ApplicationUser>>>());
        var settings = Substitute.For<IOptions<PortalSettings>>();

        _sut = new PortalSettingsService(hostingEnvironment, this, userManager, settings);

        using var store = CreateDbContext();
        store.Database.EnsureCreated();
    }

    public ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    public class Save : PortalSettingsServiceTests
    {
        [Fact]
        public async Task SavesTo_AppSettings()
        {
            var test = new PortalSettings()
            {
                Port = "test",
                Authentication = new AuthenticationConfiguration()
                {
                    ConnectionString = "test",
                    Provider = "test",
                    JwtOptions = new Jwt()
                    {
                        Audience = "test",
                        Issuer = "test",
                        Key = "test"
                    }
                }
            };

            await _sut.Save(test);
        }
    }
}
