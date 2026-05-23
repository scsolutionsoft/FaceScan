using FaceScan.Web.Models.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FaceScan.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<GradeLevel> GradeLevels => Set<GradeLevel>();
    public DbSet<Classroom> Classrooms => Set<Classroom>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<StudentPhoto> StudentPhotos => Set<StudentPhoto>();
    public DbSet<FaceProfile> FaceProfiles => Set<FaceProfile>();
    public DbSet<AttendanceTransaction> AttendanceTransactions => Set<AttendanceTransaction>();
    public DbSet<AttendanceDailySummary> AttendanceDailySummaries => Set<AttendanceDailySummary>();
    public DbSet<TeacherFaceProfile> TeacherFaceProfiles => Set<TeacherFaceProfile>();
    public DbSet<TeacherFacePhoto> TeacherFacePhotos => Set<TeacherFacePhoto>();
    public DbSet<TeacherAttendanceTransaction> TeacherAttendanceTransactions => Set<TeacherAttendanceTransaction>();
    public DbSet<TeacherAttendanceDailySummary> TeacherAttendanceDailySummaries => Set<TeacherAttendanceDailySummary>();
    public DbSet<ClassPeriod> ClassPeriods => Set<ClassPeriod>();
    public DbSet<PeriodAttendanceSession> PeriodAttendanceSessions => Set<PeriodAttendanceSession>();
    public DbSet<PeriodAttendanceRecord> PeriodAttendanceRecords => Set<PeriodAttendanceRecord>();
    public DbSet<ScanDevice> ScanDevices => Set<ScanDevice>();
    public DbSet<EdgeAgentHeartbeat> EdgeAgentHeartbeats => Set<EdgeAgentHeartbeat>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportBatchItem> ImportBatchItems => Set<ImportBatchItem>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        builder.Entity<ApplicationUser>()
            .HasOne(x => x.Student)
            .WithOne()
            .HasForeignKey<ApplicationUser>(x => x.StudentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<ApplicationUser>()
            .HasOne(x => x.AssignedClassroom)
            .WithMany(x => x.AssignedUsers)
            .HasForeignKey(x => x.AssignedClassroomId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<ApplicationUser>()
            .HasIndex(x => x.StudentId)
            .IsUnique()
            .HasFilter("[StudentId] IS NOT NULL");

        builder.Entity<ApplicationUser>()
            .HasIndex(x => x.AssignedClassroomId);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        UpdateAuditFields();
        return base.SaveChanges();
    }

    private void UpdateAuditFields()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
