using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using NSubstitute;

namespace Eu.EDelivery.AS4.PayloadService.UnitTests.Persistance;

/// <summary>
/// <see cref="IWebHostEnvironment"/> implementation to 'Stub' the environment as 'Responder' of the current directory.
/// </summary>
public class CurrentDirectoryHostEnvironment : IWebHostEnvironment
{
    /// <summary>
    /// Gets or sets the name of the environment. This property is automatically set by the host to the value
    /// of the "ASPNETCORE_ENVIRONMENT" environment variable.
    /// </summary>
    public string EnvironmentName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the application. This property is automatically set by the host to the assembly containing
    /// the application entry point.
    /// </summary>
    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the absolute path to the directory that contains the web-servable application content files.
    /// </summary>
    public string WebRootPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an <see cref="IFileProvider" /> pointing at <see cref="CurrentDirectoryHostEnvironment.WebRootPath" />.
    /// </summary>
    public IFileProvider WebRootFileProvider { get; set; } = Substitute.For<IFileProvider>();

    /// <summary>
    /// Gets or sets the absolute path to the directory that contains the application content files.
    /// </summary>
    public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

    /// <summary>
    /// Gets or sets an <see cref="T:Microsoft.Extensions.FileProviders.IFileProvider" /> pointing at <see cref="P:Microsoft.AspNetCore.Hosting.IHostingEnvironment.ContentRootPath" />.
    /// </summary>
    public IFileProvider ContentRootFileProvider { get; set; } = Substitute.For<IFileProvider>();
}
