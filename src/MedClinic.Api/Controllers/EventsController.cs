using System.Security.Claims;
using MedClinic.Api.Auth;
using MedClinic.Api.Data;
using MedClinic.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MedClinic.Api.Controllers;

public record CreateAppointmentRequest(int PatientId, int DoctorId, int ServiceId, DateTimeOffset AppointmentDate, string? Priority);

public record AppointmentDto(
    int Id, string PublicId, int PatientId, string PatientName,
    int DoctorId, string DoctorName, string Department,
    int ServiceId, string ServiceName,
    DateTimeOffset AppointmentDate, string Status, string Priority,
    string CreatedBy, string CreatedByRole);

/// <summary>
/// Записи на приём и события активности. Создание доступно Admin и Operator.
/// </summary>
[ApiController]
[Route("api/events")]
[Authorize(Policy = Policies.OperatorAccess)]
public class EventsController(MedClinicDbContext db, ILogger<EventsController> log) : ControllerBase
{
    /// <summary>Создать запись на приём. Порождает событие AppointmentCreated.</summary>
    [HttpPost("appointments")]
    public async Task<IActionResult> Create(CreateAppointmentRequest r, CancellationToken ct)
    {
        if (!await db.Doctors.AnyAsync(d => d.Id == r.DoctorId, ct)) return NotFound(new { error = "doctor_not_found" });
        if (!await db.Patients.AnyAsync(p => p.Id == r.PatientId, ct)) return NotFound(new { error = "patient_not_found" });
        if (!await db.Services.AnyAsync(s => s.Id == r.ServiceId, ct)) return NotFound(new { error = "service_not_found" });
        if (r.Priority is not null && !AppointmentPriorities.All.Contains(r.Priority))
            return BadRequest(new { error = "invalid_priority", allowed = AppointmentPriorities.All });

        var (login, role) = CurrentUser();

        var appointment = new Appointment
        {
            PublicId = NewPublicId(),
            PatientId = r.PatientId,
            DoctorId = r.DoctorId,
            ServiceId = r.ServiceId,
            AppointmentDate = r.AppointmentDate,
            Priority = r.Priority ?? AppointmentPriorities.Normal,
            Status = AppointmentStatuses.Scheduled,
            CreatedBy = login,
            CreatedByRole = role,
            CorrelationId = HttpContext.TraceIdentifier
        };

        db.Appointments.Add(appointment);
        db.ActivityEvents.Add(new ActivityEvent
        {
            Appointment = appointment,
            Kind = EventKinds.AppointmentCreated,
            Actor = login,
            ActorRole = role,
            Source = EventSources.Ui,
            CorrelationId = HttpContext.TraceIdentifier
        });

        await db.SaveChangesAsync(ct);
        log.LogInformation("event.created {Kind} appt={AppointmentId} doctor={DoctorId} by={Actor} role={Role}",
            EventKinds.AppointmentCreated, appointment.Id, appointment.DoctorId, login, role);

        var dto = await BuildDto(appointment.Id, ct);
        return Created($"/api/events/appointments/{dto!.Id}", dto);
    }

    /// <summary>Отметить визит состоявшимся. Порождает событие AppointmentVisited.</summary>
    [HttpPost("appointments/{id:int}/visit")]
    public async Task<IActionResult> Visit(int id, CancellationToken ct)
        => await Transition(id, AppointmentStatuses.Completed, EventKinds.AppointmentVisited, ct);

    /// <summary>Отменить запись. Порождает событие AppointmentCancelled.</summary>
    [HttpPost("appointments/{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
        => await Transition(id, AppointmentStatuses.Cancelled, EventKinds.AppointmentCancelled, ct);

    /// <summary>Список событий активности с фильтрами.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? kind, [FromQuery] string? source,
                                           [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
                                           [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var q = db.ActivityEvents.AsNoTracking().Include(e => e.Appointment).AsQueryable();
        if (kind is not null) q = q.Where(e => e.Kind == kind);
        if (source is not null) q = q.Where(e => e.Source == source);
        if (from.HasValue) q = q.Where(e => e.CreatedAt >= from.Value);
        if (to.HasValue) q = q.Where(e => e.CreatedAt <= to.Value);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(e => e.CreatedAt)
                           .Skip((page - 1) * pageSize).Take(pageSize)
                           .Select(e => new
                           {
                               e.Id, e.Kind, e.Actor, e.ActorRole, e.Source, e.CorrelationId, e.CreatedAt,
                               appointmentId = e.AppointmentId,
                               publicId = e.Appointment!.PublicId,
                               doctorId = e.Appointment.DoctorId
                           }).ToListAsync(ct);

        return Ok(new { total, page, pageSize, items });
    }

    private async Task<IActionResult> Transition(int id, string status, string kind, CancellationToken ct)
    {
        var appointment = await db.Appointments.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (appointment is null) return NotFound(new { error = "appointment_not_found" });
        if (appointment.Status == AppointmentStatuses.Cancelled)
            return Conflict(new { error = "already_cancelled" });
        if (kind == EventKinds.AppointmentVisited && appointment.Status == AppointmentStatuses.Completed)
            return Conflict(new { error = "already_completed" });

        appointment.Status = status;

        var (login, role) = CurrentUser();
        db.ActivityEvents.Add(new ActivityEvent
        {
            AppointmentId = appointment.Id,
            Kind = kind,
            Actor = login,
            ActorRole = role,
            Source = EventSources.Ui,
            CorrelationId = HttpContext.TraceIdentifier
        });

        await db.SaveChangesAsync(ct);
        log.LogInformation("event.created {Kind} appt={AppointmentId} by={Actor} role={Role}",
            kind, appointment.Id, login, role);

        return Ok(await BuildDto(appointment.Id, ct));
    }

    private (string Login, string Role) CurrentUser()
        => (User.FindFirst(ClaimTypes.Name)?.Value ?? "unknown",
            User.FindFirst(ClaimTypes.Role)?.Value ?? "unknown");

    private Task<AppointmentDto?> BuildDto(int id, CancellationToken ct)
        => db.Appointments.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new AppointmentDto(
                a.Id, a.PublicId, a.PatientId, a.Patient!.FullName,
                a.DoctorId, a.Doctor!.FullName, a.Doctor.Department,
                a.ServiceId, a.Service!.Name,
                a.AppointmentDate, a.Status, a.Priority,
                a.CreatedBy, a.CreatedByRole))
            .FirstOrDefaultAsync(ct);

    private static string NewPublicId()
        => Guid.NewGuid().ToString("N")[..8];
}