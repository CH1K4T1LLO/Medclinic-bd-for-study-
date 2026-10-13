namespace MedClinic.Api.Auth;

/// <summary>
/// Параметры подписи и проверки JWT. Значения по умолчанию задаёт
/// <see cref="Configuration.AppConfig"/>, переопределяются переменными окружения.
/// </summary>
public class JwtOptions
{
    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public int ExpiresHours { get; set; } = 8;
}

/// <summary>
/// Имена политик Authorize. Политика описывает, какие роли получают доступ к эндпоинту.
/// </summary>
public static class Policies
{
    /// <summary>Только Admin: управление пользователями, справочниками, генерация нагрузки.</summary>
    public const string AdminOnly = "AdminOnly";

    /// <summary>Admin и Analyst: аналитика и выгрузка отчётов.</summary>
    public const string AnalystAccess = "AnalystAccess";

    /// <summary>Admin и Operator: создание и изменение событий активности.</summary>
    public const string OperatorAccess = "OperatorAccess";
}