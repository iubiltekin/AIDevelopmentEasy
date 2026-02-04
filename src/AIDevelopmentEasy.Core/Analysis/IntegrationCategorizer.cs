namespace AIDevelopmentEasy.Core.Analysis;

/// <summary>
/// Categorizes package/module names (NuGet, go.mod, npm) into third-party integration types
/// for pipeline context (Database, Cache, Cloud/AWS, MessageQueue, etc.).
/// </summary>
public static class IntegrationCategorizer
{
    public const string CategoryDatabase = "Database";
    public const string CategoryCache = "Cache";
    public const string CategoryCloud = "Cloud";
    public const string CategoryMessageQueue = "MessageQueue";
    public const string CategoryHttp = "HTTP/API";
    public const string CategoryAuth = "Auth";
    public const string CategoryMonitoring = "Monitoring";
    public const string CategoryOther = "Other";

    /// <summary>
    /// Returns (Category, DisplayLabel) for a package/module name. Returns null if not a known integration.
    /// </summary>
    public static (string Category, string Label)? GetCategory(string packageName)
    {
        if (string.IsNullOrWhiteSpace(packageName))
            return null;

        var key = packageName.Trim().ToLowerInvariant();

        // C# NuGet / Go / npm / Python PyPI / Rust crates - common patterns
        if (Matches(key, "npgsql", "mysql", "mysql.data", "microsoft.sqlserver", "system.data.sqlclient",
                "sqlite", "mongodb.driver", "neo4j", "oracle", "dapper", "entityframeworkcore", "efcore",
                "prisma", "typeorm", "sequelize", "knex", "mongoose", "pg", "pgx", "go-sql-driver",
                "lib/pq", "modernc.org/sqlite", "database/sql",
                "psycopg2", "psycopg", "sqlalchemy", "pymongo", "motor", "asyncpg", "databases",
                "sqlx", "diesel", "rusqlite", "mongodb"))
            return (CategoryDatabase, SimplifyLabel(packageName, "Database driver/ORM"));

        if (Matches(key, "redis", "stackexchange.redis", "go-redis", "redis/go-redis", "ioredis", "cache", "memorycache", "redis-py", "aioredis"))
            return (CategoryCache, "Redis/Cache");

        if (Matches(key, "aws", "amazon", "awssdk", "s3", "dynamodb", "sqs", "sns", "lambda", "cloudwatch",
                "azure.", "azure.storage", "google.cloud", "gcp", "@aws-sdk", "aws-sdk",
                "boto3", "botocore", "aioboto3", "aws-sdk-rust", "rusoto"))
            return (CategoryCloud, "AWS/Azure/GCP");

        if (Matches(key, "rabbitmq", "rabbitmq.client", "masstransit", "nats", "kafka", "confluent",
                "amqp", "sqs", "sns", "servicebus", "celery", "pika", "aio-pika", "lapin"))
            return (CategoryMessageQueue, "Message queue/Event bus");

        if (Matches(key, "elasticsearch", "elastic.", "nest", "opensearch", "elasticsearch-py", "opensearch-py"))
            return (CategoryOther, "Search (Elastic/OpenSearch)");

        if (Matches(key, "serilog", "nlog", "log4net", "prometheus", "opentelemetry", "datadog", "applicationinsights",
                "structlog", "loguru", "sentry", "tracing", "tracing-subscriber"))
            return (CategoryMonitoring, "Logging/Monitoring");

        if (Matches(key, "identity", "jwt", "openid", "oauth", "auth0", "duende", "keycloak", "pyjwt", "python-jose", "authlib", "oauthlib"))
            return (CategoryAuth, "Auth/Identity");

        if (Matches(key, "grpc", "refit", "polly", "fluentvalidation", "grpcio", "tonic", "tower"))
            return (CategoryOther, "RPC/Resilience");

        // Python: web frameworks
        if (Matches(key, "django", "flask", "fastapi", "starlette", "sanic", "aiohttp", "tornado"))
            return (CategoryOther, "Web framework");

        // Rust: async runtime, serialization, web
        if (Matches(key, "tokio", "async-std", "serde", "serde_json", "axum", "actix", "actix-web", "warp", "rocket"))
            return (CategoryOther, "Runtime/Web/Serialization");

        return null;
    }

    /// <summary>
    /// Build a "Third-party integrations" markdown section from a list of package names (with optional versions).
    /// </summary>
    public static string BuildIntegrationsSection(IEnumerable<(string Name, string? Version)> packages)
    {
        var grouped = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, version) in packages)
        {
            var cat = GetCategory(name);
            if (cat == null) continue;

            var label = string.IsNullOrEmpty(version) ? name : $"{name} ({version})";
            if (!grouped.ContainsKey(cat.Value.Category))
                grouped[cat.Value.Category] = new List<string>();
            if (!grouped[cat.Value.Category].Contains(label))
                grouped[cat.Value.Category].Add(label);
        }

        if (grouped.Count == 0)
            return "";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine();
        sb.AppendLine("## Third-party / external integrations");
        foreach (var kv in grouped.OrderBy(k => k.Key))
        {
            sb.AppendLine($"- **{kv.Key}**: {string.Join(", ", kv.Value)}");
        }

        return sb.ToString();
    }

    private static bool Matches(string key, params string[] patterns)
    {
        foreach (var p in patterns)
        {
            if (key.Contains(p.ToLowerInvariant()))
                return true;
        }
        return false;
    }

    private static string SimplifyLabel(string packageName, string suffix)
    {
        if (packageName.Length > 40)
            return packageName.Substring(0, 37) + "...";
        return packageName;
    }
}
