using HoopGameNight.Core.Interfaces.Infrastructure;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;

namespace HoopGameNight.Infrastructure.Data
{
    public class SqlLoader : ISqlLoader
    {
        private readonly ILogger<SqlLoader> _logger;
        private readonly string _sqlBasePath;
        private readonly ConcurrentDictionary<string, string> _sqlCache = new();

        public SqlLoader(ILogger<SqlLoader> logger)
        {
            _logger = logger;

            _sqlBasePath = FindSqlBasePath();

            _logger.LogInformation("SqlLoader initialized with base path: {BasePath}", _sqlBasePath);
        }

        private string FindSqlBasePath()
        {
            var possiblePaths = new[]
            {
                // 1. Pasta Sql dentro do assembly atual
                Path.Combine(GetAssemblyDirectory(), "Sql"),
                
                // 2. Pasta Sql na Infrastructure
                Path.Combine(GetProjectRoot(), "HoopGameNight.Infrastructure", "Sql"),
                
                // 3. Pasta Database na raiz do projeto
                Path.Combine(GetProjectRoot(), "Database"),
                
                // 4. Pasta raiz do projeto
                GetProjectRoot()
            };

            foreach (var path in possiblePaths)
            {
                if (Directory.Exists(path))
                {
                    _logger.LogInformation("Found SQL base path: {Path}", path);
                    return path;
                }
            }

            // Se não encontrar nenhuma pasta, criar a estrutura padrão
            var defaultPath = Path.Combine(GetAssemblyDirectory(), "Sql");
            Directory.CreateDirectory(defaultPath);
            _logger.LogWarning("Created default SQL path: {Path}", defaultPath);
            return defaultPath;
        }

        private string GetProjectRoot()
        {
            var currentDir = GetAssemblyDirectory();

            // Subir até encontrar o arquivo .sln ou pasta com projetos
            while (currentDir != null && !Directory.GetFiles(currentDir, "*.sln").Any())
            {
                var parent = Directory.GetParent(currentDir);
                if (parent == null) break;
                currentDir = parent.FullName;
            }

            return currentDir ?? GetAssemblyDirectory();
        }

        public async Task<string> LoadSqlAsync(string category, string fileName)
        {
            return await Task.Run(() => LoadSql(category, fileName));
        }

        public string LoadSql(string category, string fileName)
        {
            var cacheKey = $"{category}:{fileName}";

            if (_sqlCache.TryGetValue(cacheKey, out var cachedSql))
            {
                _logger.LogTrace("SQL loaded from cache: {Category}/{FileName}", category, fileName);
                return cachedSql;
            }

            try
            {
                // Tentar múltiplos caminhos para encontrar o arquivo
                var possiblePaths = new[]
                {
                    Path.Combine(_sqlBasePath, category, $"{fileName}.sql"),
                    Path.Combine(_sqlBasePath, $"{fileName}.sql"),
                    Path.Combine(_sqlBasePath, $"{category}_{fileName}.sql")
                };

                string? filePath = null;
                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        filePath = path;
                        break;
                    }
                }

                if (filePath == null)
                {
                    _logger.LogError("SQL file not found in any of these locations: {Paths}",
                        string.Join(", ", possiblePaths));

                    return GetFallbackSql(category, fileName);
                }

                var sql = File.ReadAllText(filePath);

                if (string.IsNullOrWhiteSpace(sql))
                {
                    _logger.LogWarning("SQL file is empty: {FilePath}", filePath);
                }

                _sqlCache[cacheKey] = sql;

                _logger.LogDebug("SQL loaded successfully: {Category}/{FileName} ({Length} chars)",
                    category, fileName, sql.Length);

                return sql;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading SQL file: {Category}/{FileName}", category, fileName);

                return GetFallbackSql(category, fileName);
            }
        }

        private string GetFallbackSql(string category, string fileName)
        {
            _logger.LogWarning("Using fallback SQL for {Category}/{FileName}", category, fileName);

            return fileName.ToLower() switch
            {
                "getall" => $"SELECT * FROM {category.ToLower()};",
                "getbyid" => $"SELECT * FROM {category.ToLower()} WHERE id = @Id;",
                "getbyexternalid" => $"SELECT * FROM {category.ToLower()} WHERE external_id = @ExternalId;",
                "getbyabbreviation" => $"SELECT * FROM {category.ToLower()} WHERE abbreviation = @Abbreviation;",
                "insert" => $"INSERT INTO {category.ToLower()} (external_id, name) VALUES (@ExternalId, @Name); SELECT LAST_INSERT_ID();",
                "update" => $"UPDATE {category.ToLower()} SET updated_at = NOW() WHERE id = @Id;",
                "delete" => $"DELETE FROM {category.ToLower()} WHERE id = @Id;",
                "exists" => $"SELECT COUNT(*) FROM {category.ToLower()} WHERE external_id = @ExternalId;",
                _ => $"SELECT 1; -- Fallback SQL for {category}/{fileName}"
            };
        }

        public async Task<Dictionary<string, string>> LoadAllSqlInCategoryAsync(string category)
        {
            var result = new Dictionary<string, string>();
            var categoryPath = Path.Combine(_sqlBasePath, category);

            if (!Directory.Exists(categoryPath))
            {
                _logger.LogWarning("SQL category directory not found: {CategoryPath}", categoryPath);
                return result;
            }

            try
            {
                var sqlFiles = Directory.GetFiles(categoryPath, "*.sql");

                foreach (var filePath in sqlFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    var sql = await File.ReadAllTextAsync(filePath);
                    result[fileName] = sql;

                    var cacheKey = $"{category}:{fileName}";
                    _sqlCache[cacheKey] = sql;
                }

                _logger.LogInformation("Loaded {Count} SQL files from category: {Category}", result.Count, category);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading SQL files from category: {Category}", category);
                throw;
            }
        }

        public bool SqlExists(string category, string fileName)
        {
            var cacheKey = $"{category}:{fileName}";

            if (_sqlCache.ContainsKey(cacheKey))
            {
                return true;
            }

            var filePath = Path.Combine(_sqlBasePath, category, $"{fileName}.sql");
            return File.Exists(filePath);
        }

        public void ClearCache()
        {
            _sqlCache.Clear();
            _logger.LogInformation("SQL cache cleared completely");
        }

        public void ClearCache(string category)
        {
            var keysToRemove = _sqlCache.Keys.Where(k => k.StartsWith($"{category}:")).ToList();

            foreach (var key in keysToRemove)
            {
                _sqlCache.TryRemove(key, out _);
            }

            _logger.LogInformation("SQL cache cleared for category: {Category} ({Count} items removed)",
                category, keysToRemove.Count);
        }

        private static string GetAssemblyDirectory()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyLocation = assembly.Location;
            var directory = Path.GetDirectoryName(assemblyLocation);

            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException("Could not determine assembly directory");
            }

            return directory;
        }
    }
}