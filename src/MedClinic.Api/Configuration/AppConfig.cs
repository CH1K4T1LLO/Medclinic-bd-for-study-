namespace MedClinic.Api.Configuration;

/// <summary>
/// Конфигурация приложения целиком на C#: внешние файлы appsettings.json не используются.
/// Каждое значение можно переопределить переменной окружения — это делает
/// конфигурацию настраиваемой без правки кода.
/// </summary>
public static class AppConfig
{
    /// <summary>
    /// Строка подключения к PostgreSQL. Переопределяется переменной
    /// MEDCLINIC_CONNECTION.
    /// </summary>
    public static string ConnectionString =>
        Env("MEDCLINIC_CONNECTION", "Host=localhost;Port=5432;Database=medclinic;Username=medclinic;Password=medclinic");

    /// <summary>
    /// Ключ подписи JWT. Требование — не менее 32 байт для HMAC-SHA256.
    /// Переопределяется переменной MEDCLINIC_JWT_KEY.
    /// </summary>
    public static string JwtKey =>
        Env("MEDCLINIC_JWT_KEY", "dev_only_change_me_key_longer_than_32_bytes_!!");

    public static string JwtIssuer => Env("MEDCLINIC_JWT_ISSUER", "medclinic.api");

    public static string JwtAudience => Env("MEDCLINIC_JWT_AUDIENCE", "medclinic.client");

    public static int JwtExpiresHours => int.TryParse(Env("MEDCLINIC_JWT_EXPIRES_HOURS", "8"), out var h) ? h : 8;

    /// <summary>Swagger UI включается только в режиме разработки.</summary>
    public static bool SwaggerEnabled =>
        !string.Equals(Env("MEDCLINIC_ENVIRONMENT", "Development"), "Production", StringComparison.OrdinalIgnoreCase);

    /// <summary>Порт HTTP-сервера. Используется, если не задан ASPNETCORE_URLS.</summary>
    public static int Port => int.TryParse(Env("MEDCLINIC_PORT", "5000"), out var p) ? p : 5000;

    /// <summary>Пароли тестовых пользователей, задаются переменными окружения.</summary>
    public static class Passwords
    {
        public static string Admin => Env("MEDCLINIC_PWD_ADMIN", "MedClinic!Admin2026");
        public static string Analyst => Env("MEDCLINIC_PWD_ANALYST", "MedClinic!Analyst2026");
        public static string Operator => Env("MEDCLINIC_PWD_OPERATOR", "MedClinic!Operator2026");
    }

    private static string Env(string name, string fallback)
        => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : fallback;
}