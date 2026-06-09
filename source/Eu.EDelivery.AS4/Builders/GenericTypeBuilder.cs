using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Builders;

/// <summary>
/// Factory implementation to create instance from a given <see cref="Type"/>
/// </summary>
public class GenericTypeBuilder : IGenericTypeBuilder
{
    private readonly ILogger<GenericTypeBuilder> _logger;

    public GenericTypeBuilder(ILogger<GenericTypeBuilder> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Determines whether or not the given <paramref name="typeString"/> can be resolved to a specified generic type or not.
    /// </summary>
    /// <param name="typeString"></param>
    /// <returns></returns>
    public bool CanResolveTypeThatImplements<T>(string? typeString)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(typeString))
            {
                _logger.LogError("Cannot resolve type string: {TypeString} to a {Name} instance because the type string is blank",
                    typeString,
                    typeof(T).Name);
                return false;
            }

            var type = Type.GetType(typeString, throwOnError: false);
            if (type == null)
            {
                _logger.LogError("Cannot resolve type string: {TypeString} to a {Name} instance because the type is not found in this AppDomain",
                    typeString,
                    typeof(T).Name);
                return false;
            }

            LogPossibleObsoleteMessage(typeString, type);

            if (type.GetInterfaces().All(i => i != typeof(T)))
            {
                _logger.LogError("Cannot resolve type string: {TypeString} to a {Name} instance because the type does not implement ",
                    typeString,
                    typeof(T).Name);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cannot resolve type string: {TypeString}", typeString);
            return false;
        }
    }

    /// <summary>
    /// Initializes a new GenericTypeBuilder to instantiate a type for the specified typeString.
    /// </summary>
    /// <param name="typeString"></param>
    /// <returns></returns>
    public T Build<T>(string typeString) where T : class
    {
        var type = ResolveType(typeString);
        if (type == null)
        {
            _logger.LogCritical("Type not found: {TypeString}", typeString);
            throw new TypeLoadException($"Type not found: {typeString}");
        }

        return Build<T>(type);
    }

    /// <summary>
    /// Initializes a new GenericTypeBuilder to instantiate a type for the specified typeString.
    /// </summary>
    /// <param name="typeString"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public T Build<T>(string typeString, params object?[] args) where T : class
    {
        var type = ResolveType(typeString);
        if (type == null)
        {
            _logger.LogCritical("Type not found: {TypeString}", typeString);
            throw new TypeLoadException($"Type not found: {typeString}");
        }

        return Build<T>(type, args);
    }

    /// <summary>
    /// Create an instance of type <see cref="Type"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="type"></param>
    /// <returns></returns>
    public static T Build<T>(Type type) where T : class =>
        Activator.CreateInstance(type) as T ?? throw new InvalidOperationException($"Unable to create {type} as an instance of {typeof(T).Name}");

    /// <summary>
    /// Create an instance of type <see cref="Type"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="type"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public static T Build<T>(Type type, params object?[] args) where T : class =>
        Activator.CreateInstance(type, args) as T ?? throw new InvalidOperationException($"Unable to create {type} as an instance of {typeof(T).Name}");

    private void LogPossibleObsoleteMessage(string typeString, Type type)
    {
        var obsoleteAttrs =
            type.GetCustomAttributes(typeof(ObsoleteAttribute))
                .OfType<ObsoleteAttribute>();

        foreach (var oa in obsoleteAttrs)
        {
            _logger.LogWarning("Type: {TypeString} is obsolete: {Message}", typeString, oa.Message);
        }
    }

    private static Type? ResolveType(string type) =>
        Type.GetType(type, throwOnError: false) ?? Type.GetType(
            type,
            name => AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == name.FullName),
            typeResolver: null,
            throwOnError: false);

}
