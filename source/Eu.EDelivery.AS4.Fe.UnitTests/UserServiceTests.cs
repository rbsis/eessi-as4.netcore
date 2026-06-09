using Eu.EDelivery.AS4.Fe.Authentication;
using Eu.EDelivery.AS4.Fe.Exceptions;
using Eu.EDelivery.AS4.Fe.Mappers;
using Eu.EDelivery.AS4.Fe.Services;
using Eu.EDelivery.AS4.Fe.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Eu.EDelivery.AS4.Fe.UnitTests;

public class UserServiceTests
{
    protected UserManager<ApplicationUser> _userManager;
    protected UserService _sut;
    protected IDbContextFactory<ApplicationDbContext> _factory;

    protected UserServiceTests()
    {
        var services = new ServiceCollection();
        services
            .AddSingleton<ILogger<UserManager<ApplicationUser>>>(NullLogger<UserManager<ApplicationUser>>.Instance)
            .AddEntityFrameworkInMemoryDatabase()
            .AddDbContextFactory<ApplicationDbContext>(dbOptions => dbOptions.UseInMemoryDatabase(Guid.NewGuid().ToString()))
            .AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        var provider = services.BuildServiceProvider();

        _factory = provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        _userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        _sut = new UserService(_userManager, _factory, new UsersMapper());

        using var context = _factory.CreateDbContext();
        context.Database.EnsureCreated();
    }

    public class Get : UserServiceTests
    {
        [Fact]
        public async Task Calls_ApplicationDbContext()
        {
            using var context = _factory.CreateDbContext();
            context.Users.Add(new ApplicationUser
            {
                UserName = "test"
            });
            context.SaveChanges();

            var result = (await _sut.GetAsync(TestContext.Current.CancellationToken)).ToList();

            Assert.True(result.First(x => x.Name == "test").Name == "test", "Expected user to have name 'test'");
        }
    }

    public class Create : UserServiceTests
    {
        [Fact]
        public async Task Creates_User()
        {
            var username = Guid.NewGuid().ToString();

            await _sut.CreateAsync(new NewUser
            {
                Name = username,
                Password = "CZ#$So7OGoNb",
                Roles = [Roles.Admin]
            }, TestContext.Current.CancellationToken);

            var users = await _sut.GetAsync(TestContext.Current.CancellationToken);

            var search = users.FirstOrDefault(user => user.Name == username);
            Assert.NotNull(search);
            Assert.True(search.Name == username, $"Expected the created user to have '{username}' as username!");
            Assert.True(search.Roles.Contains(Roles.Admin), "Expected the user to be an admin!");
        }

        [Fact]
        public async Task CreatesUserWithReadonlyClaim_WhenIsAdminIsFalse()
        {
            var username = Guid.NewGuid().ToString();
            await _sut.CreateAsync(new NewUser
            {
                Name = username,
                Password = "CZ#$So7OGoNb"
            }, TestContext.Current.CancellationToken);

            var users = await _sut.GetAsync(TestContext.Current.CancellationToken);

            var search = users.FirstOrDefault(user => user.Name == username);
            Assert.NotNull(search);
            Assert.True(search.Name == username, "Expected the created user to have 'test123' as username!");
            Assert.False(search.Roles.Contains(Roles.Admin), "Expected the user to be not be an admin!");
        }

        [Fact]
        public async Task ThrowsException_WhenParametersAreInvalid()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.CreateAsync(new NewUser(), TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.CreateAsync(new NewUser { Name = "test" }, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task ThrowsException_WhenPasswordSettingsDoNotMeetRequirements()
        {
            await Assert.ThrowsAsync<BusinessException>(() => _sut.CreateAsync(new NewUser
            {
                Name = "test",
                Password = "test"
            }, TestContext.Current.CancellationToken));
        }
    }

    [Collection("Update")]
    public class Update : UserServiceTests
    {
        protected async Task<string> SetupUser(CancellationToken cancellationToken)
        {
            var user = Guid.NewGuid().ToString();
            await _sut.CreateAsync(new NewUser
            {
                Name = user,
                Password = "CZ#$So7OGoNb",
                Roles = [Roles.Admin]
            }, cancellationToken);
            return user;
        }

        [Fact]
        public async Task Updates_ExistingUser()
        {
            var user = await SetupUser(TestContext.Current.CancellationToken);

            await _sut.UpdateAsync(user, new UpdateUser { Password = "9*SC!7i*wH3r" }, TestContext.Current.CancellationToken);

            var foundUser = await _userManager.FindByNameAsync(user);
            var claims = await _userManager.GetClaimsAsync(foundUser!);
            var result = await _userManager.CheckPasswordAsync(foundUser!, "9*SC!7i*wH3r");
            Assert.True(result, "CheckPasswordAsync should have returned true");
            Assert.DoesNotContain(claims, claim => claim.Value == Roles.Admin);
        }

        [Fact]
        public async Task DoesntUpdatePassword_WhenPasswordIsEmpty()
        {
            var user = await SetupUser(TestContext.Current.CancellationToken);

            await _sut.UpdateAsync(user, new UpdateUser(), TestContext.Current.CancellationToken);

            var foundUser = await _userManager.FindByNameAsync(user);
            var claims = await _userManager.GetClaimsAsync(foundUser!);
            var result = await _userManager.CheckPasswordAsync(foundUser!, "CZ#$So7OGoNb");
            Assert.True(result, "CheckPasswordAsync should have returned true");
        }

        [Fact]
        public async Task ThrowsBusinessException_WhenUserDoesntExist()
        {
            var user = await SetupUser(TestContext.Current.CancellationToken);

            await Assert.ThrowsAsync<BusinessException>(() => _sut.UpdateAsync("fdsqfdfdsqfezrrzeaerzaerz", new UpdateUser(), TestContext.Current.CancellationToken));
        }
    }

    public class Delete : UserServiceTests
    {
        [Fact]
        public async Task User_IsDeleted()
        {
            var username = Guid.NewGuid().ToString();

            await _sut.CreateAsync(new NewUser
            {
                Name = username,
                Password = "CZ#$So7OGoNb"
            }, TestContext.Current.CancellationToken);

            await _sut.DeleteAsync(username, TestContext.Current.CancellationToken);

            var user = (await _sut.GetAsync(TestContext.Current.CancellationToken)).ToList();
            Assert.True(user.All(find => find.Name != username));
        }

        [Fact]
        public async Task ThrowsException_WhenUserDoesntExist()
        {
            await Assert.ThrowsAsync<BusinessException>(() => _sut.DeleteAsync("fdsqfdsqfdsqfeaq", TestContext.Current.CancellationToken));
        }
    }
}
