using MedClinic.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MedClinic.Api.Data;

public class MedClinicDbContext(DbContextOptions<MedClinicDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<ActivityEvent> ActivityEvents => Set<ActivityEvent>();
    public DbSet<AuthAudit> AuthAudits => Set<AuthAudit>();
    public DbSet<SeedRunLog> SeedRuns => Set<SeedRunLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>(e =>
        {
            e.HasIndex(x => x.Login).IsUnique();
            e.Property(x => x.Login).HasMaxLength(64);
            e.Property(x => x.PasswordHash).HasMaxLength(128);
            e.Property(x => x.Role).HasMaxLength(32);
            e.Property(x => x.DisplayName).HasMaxLength(128);
        });

        b.Entity<Doctor>(e =>
        {
            e.Property(x => x.FullName).HasMaxLength(128);
            e.Property(x => x.Specialty).HasMaxLength(128);
            e.Property(x => x.Department).HasMaxLength(64);
            e.HasIndex(x => x.Department);
        });

        b.Entity<Service>(e => e.Property(x => x.Name).HasMaxLength(128));

        b.Entity<Patient>(e =>
        {
            e.Property(x => x.FullName).HasMaxLength(128);
            e.Property(x => x.Phone).HasMaxLength(32);
        });

        b.Entity<Appointment>(e =>
        {
            e.Property(x => x.PublicId).HasMaxLength(8);
            e.HasIndex(x => x.PublicId).IsUnique();
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.Priority).HasMaxLength(16);
            e.Property(x => x.CreatedBy).HasMaxLength(64);
            e.Property(x => x.CreatedByRole).HasMaxLength(32);
            e.Property(x => x.CorrelationId).HasMaxLength(64);

            e.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId);
            e.HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId);
            e.HasOne(x => x.Service).WithMany().HasForeignKey(x => x.ServiceId);

            // Индексы под аналитику: загрузка врачей, отмены, популярность услуг.
            e.HasIndex(x => new { x.DoctorId, x.AppointmentDate });
            e.HasIndex(x => x.ServiceId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.AppointmentDate);
        });

        b.Entity<ActivityEvent>(e =>
        {
            e.Property(x => x.Kind).HasMaxLength(32);
            e.Property(x => x.Actor).HasMaxLength(64);
            e.Property(x => x.ActorRole).HasMaxLength(32);
            e.Property(x => x.Source).HasMaxLength(16);
            e.Property(x => x.CorrelationId).HasMaxLength(64);

            e.HasOne(x => x.Appointment).WithMany().HasForeignKey(x => x.AppointmentId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.Kind, x.CreatedAt });
            e.HasIndex(x => x.CorrelationId);
            e.HasIndex(x => x.Source);
        });

        b.Entity<AuthAudit>(e =>
        {
            e.Property(x => x.Login).HasMaxLength(64);
            e.Property(x => x.Reason).HasMaxLength(64);
            e.Property(x => x.IpAddress).HasMaxLength(64);
            e.Property(x => x.UserAgent).HasMaxLength(256);
            e.HasIndex(x => x.CreatedAt);
        });

        b.Entity<SeedRunLog>(e =>
        {
            e.Property(x => x.RequestedBy).HasMaxLength(64);
            e.HasIndex(x => x.StartedAt);
        });
    }
}