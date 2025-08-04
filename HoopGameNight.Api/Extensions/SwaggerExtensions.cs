using Microsoft.OpenApi.Models;
using System.Reflection;

namespace HoopGameNight.Api.Extensions
{
    public static class SwaggerExtensions
    {
        public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services, IConfiguration configuration)
        {
            try
            {
                var apiConfig = configuration.GetSection("ApiSettings");
                services.AddSwaggerGen(c =>
                {
                    c.SwaggerDoc("v1", new OpenApiInfo
                    {
                        Title = apiConfig["Title"] ?? "Hoop Game Night API",
                        Version = apiConfig["Version"] ?? "v1",
                        Description = apiConfig["Description"] ?? "API completa para acompanhamento de jogos da NBA",
                        Contact = new OpenApiContact
                        {
                            Name = apiConfig["Contact:Name"] ?? "Hoop Game Night Team",
                            Email = apiConfig["Contact:Email"] ?? "lucas@hoopgamenight.com"
                        }
                    });

                    c.SupportNonNullableReferenceTypes();

                    c.CustomSchemaIds(type => GetSafeSchemaId(type));

                    c.IgnoreObsoleteActions();
                    c.IgnoreObsoleteProperties();

                    TryAddXmlComments(c);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Swagger configuration error: {ex.Message}");

                services.AddSwaggerGen(c =>
                {
                    c.SwaggerDoc("v1", new OpenApiInfo
                    {
                        Title = "Hoop Game Night API",
                        Version = "v1"
                    });
                });
            }
            return services;
        }

        private static string GetSafeSchemaId(Type type)
        {
            try
            {
                var name = type.Name;

                if (type.IsGenericType)
                {
                    var genericType = type.GetGenericTypeDefinition();
                    var genericArgs = type.GetGenericArguments();

                    name = genericType.Name.Split('`')[0];

                    var argNames = new List<string>();
                    foreach (var arg in genericArgs)
                    {
                        if (arg == typeof(object))
                        {
                            argNames.Add("Object");
                        }
                        else if (arg.IsGenericType)
                        {
                            argNames.Add(GetSafeSchemaId(arg));
                        }
                        else
                        {
                            var argName = arg.Name;
                            argName = argName.Replace("Response", "")
                                           .Replace("Request", "")
                                           .Replace("Dto", "");
                            argNames.Add(argName);
                        }
                    }

                    name = $"{name}Of{string.Join("And", argNames)}";
                }

                if (type.IsNested)
                {
                    name = $"{type.DeclaringType?.Name ?? "Nested"}{name}";
                }

                if (type.Namespace != null)
                {
                    if (type.Namespace.Contains("DTOs.Response"))
                        name = "Response" + name;
                    else if (type.Namespace.Contains("DTOs.Request"))
                        name = "Request" + name;
                    else if (type.Namespace.Contains("DTOs.External"))
                        name = "External" + name;
                }

                name = name.Replace("`", "")
                          .Replace("+", "")
                          .Replace("[]", "Array")
                          .Replace("<", "")
                          .Replace(">", "")
                          .Replace(",", "")
                          .Replace(" ", "");

                if ((name.StartsWith("Response") || name.StartsWith("Request")) &&
                    (name.EndsWith("Response") || name.EndsWith("Request")))
                {
                    if (name.EndsWith("Response"))
                        name = name.Substring(0, name.Length - 8);
                    else if (name.EndsWith("Request"))
                        name = name.Substring(0, name.Length - 7);
                }

                return name;
            }
            catch
            {
                var fullName = type.FullName ?? type.Name;
                return fullName.Replace(".", "")
                              .Replace("+", "")
                              .Replace("`", "")
                              .Replace("[", "")
                              .Replace("]", "")
                              .Replace(",", "")
                              .Replace(" ", "");
            }
        }

        private static void TryAddXmlComments(Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions c)
        {
            try
            {
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not load XML comments: {ex.Message}");
            }
        }
    }
}