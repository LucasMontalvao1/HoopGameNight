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

                    // Configurações seguras
                    c.SupportNonNullableReferenceTypes();

                    // Schema IDs únicos e seguros
                    c.CustomSchemaIds(type => GetSafeSchemaId(type));

                    // Ignorar propriedades problemáticas
                    c.IgnoreObsoleteActions();
                    c.IgnoreObsoleteProperties();

                    // Incluir XML comments de forma segura
                    TryAddXmlComments(c);
                });
            }
            catch (Exception ex)
            {
                // Log do erro mas não quebra a aplicação
                Console.WriteLine($"Swagger configuration error: {ex.Message}");

                // Configuração mínima como fallback
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
                // Remover caracteres problemáticos e criar ID único
                var name = type.Name;

                if (type.IsGenericType)
                {
                    name = type.Name.Split('`')[0];
                    var genericArgs = type.GetGenericArguments();
                    if (genericArgs.Length > 0)
                    {
                        name += "Of" + string.Join("", genericArgs.Select(arg => arg.Name));
                    }
                }

                return name.Replace("Response", "").Replace("Request", "").Replace("+", "");
            }
            catch
            {
                return type.FullName?.Replace(".", "") ?? type.Name;
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