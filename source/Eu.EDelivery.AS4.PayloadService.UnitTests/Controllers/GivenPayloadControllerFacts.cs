using System.Text.RegularExpressions;
using Eu.EDelivery.AS4.PayloadService.Controllers;
using Eu.EDelivery.AS4.PayloadService.Models;
using Eu.EDelivery.AS4.PayloadService.Persistance;
using Eu.EDelivery.AS4.PayloadService.UnitTests.Models;
using Eu.EDelivery.AS4.PayloadService.UnitTests.Persistance;
using Eu.EDelivery.AS4.PayloadService.UnitTests.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Eu.EDelivery.AS4.PayloadService.UnitTests.Controllers;

[DeletePayloads]
public class GivenPayloadControllerFacts
{
    private static readonly IPayloadPersister _payloadPersister = new FilePayloadPersister(
            NullLogger<FilePayloadPersister>.Instance,
            new CurrentDirectoryHostEnvironment());

    private const string ExpectedContent = "message data!";
    private const string ExpectedHost = "localhost:4000";
    private const string ExpectedPath = "/api/Payload/";
    private const string ExpectedScheme = "http";

    private static string ExpectedRequestUri => $"{ExpectedScheme}://{ExpectedHost}{ExpectedPath}";

    private static PayloadController AnonymousPayloadController => new(_payloadPersister)
    {
        ControllerContext = { HttpContext = new DefaultHttpContext() }
    };

    [Fact]
    [DeletePayloads]
    public async Task DownloadPayloadResultInNotFound_IfPayloadDoesntExists()
    {
        // Act
        var actualResult = await AnonymousPayloadController.Download("unknown-payload-id");

        // Assert
        Assert.IsType<NotFoundResult>(actualResult);
    }

    [Fact]
    [DeletePayloads]
    public async Task UploadPayloadResultInBadRequest_IfContentTypeIsntMultipart()
    {
        // Act
        var actualResult = await AnonymousPayloadController.Upload();

        // Assert
        Assert.IsType<BadRequestObjectResult>(actualResult);
    }

    [Fact]
    [DeletePayloads]
    public async Task DownloadsTheUploadedFileFromController()
    {
        // Arrange
        using var contentStream = new MemoryStream();
        var controller = AnonymousPayloadController;
        await SerializeExpectedContentStream(contentStream, controller);
        AssignRequestUri(controller.ControllerContext.HttpContext.Request);

        // Act
        var actualResult = await controller.Upload() as ObjectResult;
        var actualUploadResult = actualResult?.Value as UploadResult;

        // Assert
        Assert.NotNull(actualUploadResult);
        Assert.True(IsDownloadUrlAMatch(actualUploadResult), $"Actual Request Uri doesn't match the expected Uri '{ExpectedRequestUri}'");

        var downloadResult = await DownloadPayload(controller, actualUploadResult);

        Assert.NotNull(downloadResult);
        StreamedFileResultAssert.OnContent(
            downloadResult,
            actualContent => Assert.Equal(ExpectedContent, actualContent));
    }

    private static async Task SerializeExpectedContentStream(Stream contentStream, ControllerBase controller)
    {
        var content = new MultipartFormDataContent
        {
            {new StreamContent(ExpectedContent.AsStream()), "filename", "filename"}
        };
        controller.ControllerContext.HttpContext.Request.ContentType = content.Headers.ContentType?.ToString();

        await content.CopyToAsync(contentStream);
        contentStream.Position = 0;
        controller.ControllerContext.HttpContext.Request.Body = contentStream;
    }

    private static void AssignRequestUri(HttpRequest request)
    {
        request.Host = new HostString(ExpectedHost);
        request.Path = $"{ExpectedPath}Upload";
        request.Scheme = ExpectedScheme;
    }

    private static bool IsDownloadUrlAMatch(UploadResult actualResult)
    {
        return Regex.IsMatch(actualResult.DownloadUrl, ExpectedRequestUri + actualResult.PayloadId);
    }

    private static async Task<StreamedFileResult?> DownloadPayload(PayloadController controller, UploadResult actualResult)
    {
        var payloadId = actualResult.PayloadId.Split('/').Last();

        return await controller.Download(payloadId) as StreamedFileResult;
    }
}
