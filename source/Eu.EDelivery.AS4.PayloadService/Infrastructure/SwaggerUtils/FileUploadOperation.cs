using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Eu.EDelivery.AS4.PayloadService.Infrastructure.SwaggerUtils;

/// <summary>
/// <see cref="IOperationFilter"/> implementation to implement a 'File Upload' into Swagger.
/// </summary>
[Obsolete("Swagger recognizes automatically IFormFile as a multipart/form-data media type")]
public class FileUploadOperation : IOperationFilter
{
    /// <summary>
    /// Apply the 'File Upload' to the given <paramref name="operation"/>.
    /// </summary>
    /// <param name="operation">The Operation.</param>
    /// <param name="context">The Context.</param>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.OperationId?.ToLower() == "apipayloaduploadpost")
        {
            operation.Parameters ??= [];

            // https://stackoverflow.com/questions/59288658/file-upload-with-swagger-5-0-0-rcx-and-net-core-3
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "File",
                In = ParameterLocation.Header,
                Description = "Upload Payload content",
                Required = true,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" }
            });
        }
    }
}
