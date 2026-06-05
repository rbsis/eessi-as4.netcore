using Eu.EDelivery.AS4.Fe.Exceptions;
using Eu.EDelivery.AS4.Fe.Users;

namespace Eu.EDelivery.AS4.Fe.Services;

/// <summary>
/// Interface to be implemented be a service to manager users.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Get a list of all users
    /// </summary>
    /// <returns></returns>
    Task<IEnumerable<User>> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Creates the specified user.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task CreateAsync(NewUser user, CancellationToken cancellationToken);

    /// <summary>
    /// Changes the password.
    /// </summary>
    /// <param name="userName">Name of the user.</param>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">user</exception>
    /// <exception cref="ArgumentException">Name cannot be empty - Name
    /// or
    /// Password cannot be empty - Password</exception>
    Task UpdateAsync(string userName, UpdateUser user, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the specified user.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">user</exception>
    /// <exception cref="BusinessException"></exception>
    Task DeleteAsync(string user, CancellationToken cancellationToken);
}
