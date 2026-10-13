namespace MedClinic.Api.Models;

/// <summary>Роль пользователя. Совпадает со значением claim ClaimTypes.Role в JWT.</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Analyst = "Analyst";
    public const string Operator = "Operator";

    public static readonly string[] All = [Admin, Analyst, Operator];
}

/// <summary>Статус записи на приём.</summary>
public static class AppointmentStatuses
{
    public const string Scheduled = "Scheduled";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = [Scheduled, Completed, Cancelled];
}

/// <summary>Приоритет записи.</summary>
public static class AppointmentPriorities
{
    public const string Normal = "Normal";
    public const string Urgent = "Urgent";

    public static readonly string[] All = [Normal, Urgent];
}

/// <summary>Тип события активности (activity event).</summary>
public static class EventKinds
{
    public const string AppointmentCreated = "AppointmentCreated";
    public const string AppointmentVisited = "AppointmentVisited";
    public const string AppointmentCancelled = "AppointmentCancelled";
}

/// <summary>Источник события: интерфейс оператора или генератор нагрузки.</summary>
public static class EventSources
{
    public const string Ui = "ui";
    public const string Generator = "generator";
}

/// <summary>Учётная запись. Пароль хранится только в виде BCrypt-хеша.</summary>
public class AppUser
{
    public int Id { get; set; }

    /// <summary>Логин, уникален.</summary>
    public string Login { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = Roles.Operator;

    public string DisplayName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Врач.</summary>
public class Doctor
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Specialty { get; set; } = string.Empty;

    /// <summary>Отделение — используется как фильтр аналитики.</summary>
    public string Department { get; set; } = string.Empty;

    /// <summary>Вес для генератора нагрузки: чем больше, тем выше доля записей.</summary>
    public double PopularityWeight { get; set; } = 1.0;
}

/// <summary>Услуга.</summary>
public class Service
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    /// <summary>Вес для генератора: определяет популярность услуги в аналитике.</summary>
    public double PopularityWeight { get; set; } = 1.0;
}

/// <summary>Пациент.</summary>
public class Patient
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;
}

/// <summary>Запись на приём — основная сущность предметной области.</summary>
public class Appointment
{
    public int Id { get; set; }

    /// <summary>Короткий идентификатор для CSV-выгрузки (8 символов).</summary>
    public string PublicId { get; set; } = string.Empty;

    public int PatientId { get; set; }

    public Patient? Patient { get; set; }

    public int DoctorId { get; set; }

    public Doctor? Doctor { get; set; }

    public int ServiceId { get; set; }

    public Service? Service { get; set; }

    public DateTimeOffset AppointmentDate { get; set; }

    public string Status { get; set; } = AppointmentStatuses.Scheduled;

    public string Priority { get; set; } = AppointmentPriorities.Normal;

    public string CreatedBy { get; set; } = string.Empty;

    public string CreatedByRole { get; set; } = string.Empty;

    /// <summary>Идентификатор сквозного логирования, связывает событие с логом.</summary>
    public string CorrelationId { get; set; } = string.Empty;
}

/// <summary>Событие активности: запись на приём, визит или отмена.</summary>
public class ActivityEvent
{
    public long Id { get; set; }

    public int AppointmentId { get; set; }

    public Appointment? Appointment { get; set; }

    /// <summary>AppointmentCreated / AppointmentVisited / AppointmentCancelled.</summary>
    public string Kind { get; set; } = EventKinds.AppointmentCreated;

    public string Actor { get; set; } = string.Empty;

    public string ActorRole { get; set; } = string.Empty;

    /// <summary>ui / generator.</summary>
    public string Source { get; set; } = EventSources.Ui;

    public string CorrelationId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Аудит попыток входа. Хранит только факт и результат, без пароля.</summary>
public class AuthAudit
{
    public long Id { get; set; }

    public string Login { get; set; } = string.Empty;

    public bool Succeeded { get; set; }

    /// <summary>Причина отказа: invalid_credentials / user_inactive / unknown_user.</summary>
    public string? Reason { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Журнал прогонов генератора нагрузки.</summary>
public class SeedRunLog
{
    public long Id { get; set; }

    public int Seed { get; set; }

    public DateTimeOffset From { get; set; }

    public DateTimeOffset To { get; set; }

    public int Total { get; set; }

    public int CreatedCount { get; set; }

    public int VisitedCount { get; set; }

    public int CancelledCount { get; set; }

    public string RequestedBy { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    public long ElapsedMs { get; set; }
}