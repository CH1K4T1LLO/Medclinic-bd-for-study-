using MedClinic.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text;

namespace MedClinic.Api.Tests;

/// <summary>
/// Проверка жизненного цикла записи на приём и порождаемых событий активности.
/// </summary>
public class EventsWorkflowTests : IAsyncLifetime
{
    private TestApp _app = null!;

    public Task InitializeAsync() { _app = new TestApp(); return Task.CompletedTask; }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    private static StringContent Json(string body)
        => new(body, Encoding.UTF8, "application/json");

    [Fact]
    public async Task CreateAppointment_PersistsAppointmentAndCreatedEvent()
    {
        using var client = _app.As(Roles.Operator);

        var response = await client.PostAsync("/api/events/appointments", Json(
            """{"patientId":1,"doctorId":2,"serviceId":1,"appointmentDate":"2026-10-15T09:30:00Z"}"""));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var appointment = await _app.Db.Appointments.SingleAsync();
        Assert.Equal(AppointmentStatuses.Scheduled, appointment.Status);
        Assert.Equal(AppointmentPriorities.Normal, appointment.Priority);
        Assert.Equal(Roles.Operator, appointment.CreatedByRole);
        Assert.Equal(8, appointment.PublicId.Length);

        var evt = await _app.Db.ActivityEvents.SingleAsync();
        Assert.Equal(EventKinds.AppointmentCreated, evt.Kind);
        Assert.Equal(EventSources.Ui, evt.Source);
        Assert.Equal(appointment.CorrelationId, evt.CorrelationId);
    }

    [Fact]
    public async Task CancelAppointment_ProducesCancelledEvent()
    {
        using var client = _app.As(Roles.Operator);
        var id = await CreateAppointmentAsync();

        var response = await client.PostAsync($"/api/events/appointments/{id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var appointment = await _app.Db.Appointments.FindAsync(id);
        Assert.Equal(AppointmentStatuses.Cancelled, appointment!.Status);

        var kinds = await _app.Db.ActivityEvents.Select(e => e.Kind).ToListAsync();
        Assert.Contains(EventKinds.AppointmentCreated, kinds);
        Assert.Contains(EventKinds.AppointmentCancelled, kinds);
    }

    [Fact]
    public async Task CancelTwice_Returns409()
    {
        using var client = _app.As(Roles.Operator);
        var id = await CreateAppointmentAsync();

        await client.PostAsync($"/api/events/appointments/{id}/cancel", null);
        var second = await client.PostAsync($"/api/events/appointments/{id}/cancel", null);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task VisitAppointment_ProducesVisitedEvent()
    {
        using var client = _app.As(Roles.Operator);
        var id = await CreateAppointmentAsync();

        var response = await client.PostAsync($"/api/events/appointments/{id}/visit", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var kinds = await _app.Db.ActivityEvents.Select(e => e.Kind).ToListAsync();
        Assert.Contains(EventKinds.AppointmentVisited, kinds);
    }

    [Fact]
    public async Task CreateAppointment_WithUnknownDoctor_Returns404()
    {
        using var client = _app.As(Roles.Operator);
        var response = await client.PostAsync("/api/events/appointments", Json(
            """{"patientId":1,"doctorId":9999,"serviceId":1,"appointmentDate":"2026-10-15T09:30:00Z"}"""));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateAppointment_WithInvalidPriority_Returns400()
    {
        using var client = _app.As(Roles.Operator);
        var response = await client.PostAsync("/api/events/appointments", Json(
            """{"patientId":1,"doctorId":1,"serviceId":1,"appointmentDate":"2026-10-15T09:30:00Z","priority":"Critical"}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateAppointment_AsAnalyst_Returns403()
    {
        using var client = _app.As(Roles.Analyst);
        var response = await client.PostAsync("/api/events/appointments", Json(
            """{"patientId":1,"doctorId":1,"serviceId":1,"appointmentDate":"2026-10-15T09:30:00Z"}"""));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_FiltersByKind()
    {
        using var client = _app.As(Roles.Operator);
        var id = await CreateAppointmentAsync();
        await client.PostAsync($"/api/events/appointments/{id}/cancel", null);

        var all = await client.GetAsync("/api/events");
        var created = await client.GetAsync("/api/events?kind=AppointmentCreated");
        var generator = await client.GetAsync("/api/events?source=generator");

        Assert.Equal(2, (await all.Content.ReadAsStringAsync()).Contains("\"total\":2") ? 2 : -1);
        Assert.Contains("\"total\":1", await created.Content.ReadAsStringAsync());
        Assert.Contains("\"total\":0", await generator.Content.ReadAsStringAsync());
    }

    private async Task<int> CreateAppointmentAsync()
    {
        using var client = _app.As(Roles.Operator);
        var response = await client.PostAsync("/api/events/appointments", Json(
            """{"patientId":1,"doctorId":2,"serviceId":1,"appointmentDate":"2026-10-15T09:30:00Z"}"""));
        response.EnsureSuccessStatusCode();
        return (await _app.Db.Appointments.MaxAsync(a => a.Id));
    }
}