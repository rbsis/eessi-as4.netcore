using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Eu.EDelivery.AS4.Fe.Swagger;

/// <summary>
/// Swagger operation filter to setup the submit tool upload data
/// </summary>
/// <seealso cref="IOperationFilter" />
public class FileUploadOperation : IOperationFilter
{
    /// <summary>
    /// Applies the specified operation.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="context">The context.</param>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.OperationId?.ToLower() == "apisubmittoolpost")
        {
            operation.RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["multipart/form-data"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Object,
                            Properties = new Dictionary<string, IOpenApiSchema>
                            {
                                ["file"] = new OpenApiSchema
                                {
                                    Type = JsonSchemaType.String,
                                    Format = "binary",
                                    Description = "The payload to send with the message",
                                },
                            },
                            Required = new HashSet<string> { "file" }
                        }
                    }
                }
            };
            operation.Parameters =
            [
                new OpenApiParameter
                {
                    Name = "pmode",
                    Style = ParameterStyle.Form,
                    Description = "The pmode to use to build the message",
                    Required = true,
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                    },
                },
                new OpenApiParameter
                {
                    Name = "messages",
                    Style = ParameterStyle.Form,
                    Description = "The number of messages to send",
                    Required = true,
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Number,
                        Default = 1,
                    },
                },
                new OpenApiParameter
                {
                    Name = "payloadLocation",
                    Style = ParameterStyle.Form,
                    Description = "The location to send the payload to. Can be http:// or <c-d-e-...>",
                    Required = true,
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                    },
                },
                new OpenApiParameter
                {
                    Name = "to",
                    Style = ParameterStyle.Form,
                    Description="The location to send the message to. Can be http:// or <c-d-e-...>:\\",
                    Required = true,
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                    },
                }
            ];
        }
    }
}
