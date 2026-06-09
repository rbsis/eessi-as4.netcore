using System.Security.Claims;
using Eu.EDelivery.AS4.Fe.Authentication;
using Eu.EDelivery.AS4.Fe.Exceptions;
using Eu.EDelivery.AS4.Fe.Mappers;
using Eu.EDelivery.AS4.Fe.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Eu.EDelivery.AS4.Fe.Services;

/// <summary>
/// UserService
/// </summary>
/// <seealso cref="IUserService" />
public class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IMapper<ApplicationUser, User> _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserService"/> class.
    /// </summary>
    /// <param name="userManager">The user manager.</param>
    /// <param name="contextFactory">The context factory.</param>
    /// <param name="mapper">The mapper.</param>
    public UserService(UserManager<ApplicationUser> userManager, IDbContextFactory<ApplicationDbContext> contextFactory, IMapper<ApplicationUser, User> mapper)
    {
        _userManager = userManager;
        _contextFactory = contextFactory;
        _mapper = mapper;
    }

    /// <summary>
    /// Get a list of all users
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<IEnumerable<User>> GetAsync(CancellationToken cancellationToken)
    {
        using var datastoreContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await datastoreContext.Users
            .AsNoTracking()
            .Include(x => x.Claims)
            .Select(u => _mapper.Map(u))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Creates the specified user.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task CreateAsync(NewUser user, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(user.Name, nameof(user.Name));
        ArgumentException.ThrowIfNullOrEmpty(user.Password, nameof(user.Password));

        var result = await _userManager.CreateAsync(new ApplicationUser { UserName = user.Name }, user.Password);
        if (result.Succeeded == false)
        {
            if (result.Errors.Any(err => err.Code == "DuplicateUserName")) throw new BusinessException(@"User already exists");
            throw new BusinessException(@"Could not create the new user please check that all requirements are met!");
        }

        var newUser = await _userManager.FindByNameAsync(user.Name)
            ?? throw new BusinessException($"User {user.Name} doesn't exist");

        var claims = user.Roles.Select(x => new Claim(ClaimTypes.Role, x));

        await _userManager.AddClaimsAsync(newUser, claims);
    }

    /// <summary>
    /// Changes the password.
    /// </summary>
    /// <param name="userName">Name of the user.</param>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">user</exception>
    /// <exception cref="BusinessException"></exception>
    /// <exception cref="ArgumentException">Name cannot be empty - Name
    /// or
    /// Password cannot be empty - Password</exception>
    public async Task UpdateAsync(string userName, UpdateUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userName);
        ArgumentNullException.ThrowIfNull(user);

        var existingUser = await _userManager.FindByNameAsync(userName)
            ?? throw new BusinessException($"Could not find user {userName}");

        var claims = await _userManager.GetClaimsAsync(existingUser);
        if (!string.IsNullOrEmpty(user.Password))
        {
            await _userManager.RemovePasswordAsync(existingUser);
            await _userManager.AddPasswordAsync(existingUser, user.Password);
        }
        await _userManager.RemoveClaimsAsync(existingUser, claims);
        var newClaims = user.Roles.Select(x => new Claim(ClaimTypes.Role, x));
        await _userManager.AddClaimsAsync(existingUser, newClaims);
    }

    /// <summary>
    /// Deletes the specified user.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">user</exception>
    /// <exception cref="BusinessException"></exception>
    public async Task DeleteAsync(string user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var existingUser = await _userManager.FindByNameAsync(user)
            ?? throw new BusinessException($"User {user} doesn't exist");

        await _userManager.DeleteAsync(existingUser);
    }
}
