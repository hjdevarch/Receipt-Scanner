using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace ReceiptScanner.API.Filters;

/// <summary>
/// Operation filter to handle file upload parameters in Swagger documentation
/// </summary>
public class FileUploadOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var fileParameters = context.MethodInfo.GetParameters()
            .Where(p => p.ParameterType == typeof(IFormFile) || 
                       p.ParameterType == typeof(IFormFile[]) ||
                       p.ParameterType == typeof(IEnumerable<IFormFile>) ||
                       p.ParameterType == typeof(List<IFormFile>))
            .ToArray();

        if (!fileParameters.Any())
            return;

        operation.RequestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, OpenApiSchema>()
                    }
                }
            }
        };

        var schema = operation.RequestBody.Content["multipart/form-data"].Schema;

        foreach (var fileParameter in fileParameters)
        {
            schema.Properties[fileParameter.Name!] = new OpenApiSchema
            {
                Type = "string",
                Format = "binary",
                Description = GetParameterDescription(fileParameter)
            };
        }

        // Add other form parameters
        var otherParameters = context.MethodInfo.GetParameters()
            .Where(p => p.GetCustomAttribute<Microsoft.AspNetCore.Mvc.FromFormAttribute>() != null &&
                       p.ParameterType != typeof(IFormFile))
            .ToArray();

        foreach (var parameter in otherParameters)
        {
            var parameterType = parameter.ParameterType;
            var isNullable = parameterType.IsGenericType && parameterType.GetGenericTypeDefinition() == typeof(Nullable<>);
            
            if (isNullable)
            {
                parameterType = Nullable.GetUnderlyingType(parameterType)!;
            }

            schema.Properties[parameter.Name!] = new OpenApiSchema
            {
                Type = GetSwaggerType(parameterType),
                Format = GetSwaggerFormat(parameterType),
                Description = GetParameterDescription(parameter),
                Nullable = isNullable || !parameterType.IsValueType
            };
        }

        // Remove file parameters from the operation parameters to avoid duplication
        var parametersToRemove = operation.Parameters
            .Where(p => fileParameters.Any(fp => fp.Name == p.Name))
            .ToList();

        foreach (var param in parametersToRemove)
        {
            operation.Parameters.Remove(param);
        }
    }

    private static string GetParameterDescription(ParameterInfo parameter)
    {
        if (parameter.ParameterType == typeof(IFormFile))
        {
            return "The file to upload";
        }

        return parameter.Name switch
        {
            "receiptNumber" => "Optional receipt number override",
            "receiptDate" => "Optional receipt date override (format: yyyy-MM-dd or yyyy-MM-ddTHH:mm:ss)",
            _ => $"The {parameter.Name} parameter"
        };
    }

    private static string GetSwaggerType(Type type)
    {
        if (type == typeof(string)) return "string";
        if (type == typeof(int) || type == typeof(long) || type == typeof(short)) return "integer";
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal)) return "number";
        if (type == typeof(bool)) return "boolean";
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset)) return "string";
        if (type == typeof(Guid)) return "string";
        
        return "string";
    }

    private static string? GetSwaggerFormat(Type type)
    {
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset)) return "date-time";
        if (type == typeof(Guid)) return "uuid";
        if (type == typeof(long)) return "int64";
        if (type == typeof(float)) return "float";
        if (type == typeof(double)) return "double";
        
        return null;
    }
}