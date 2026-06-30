using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.Models;

namespace Data_Access_Layer
{
    public class AppDbContext : DbContext
    {
        public AppDbContext()
        {
        }

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Branch> Branches { get; set; } = null!;
        public DbSet<StudyYear> StudyYears { get; set; } = null!;
        public DbSet<Section> Sections { get; set; } = null!;
        public DbSet<Subject> Subjects { get; set; } = null!;
        public DbSet<FacultyMember> FacultyMembers { get; set; } = null!;
        public DbSet<FacultyMemberSubject> FacultyMemberSubjects { get; set; } = null!;
        public DbSet<Classroom> Classrooms { get; set; } = null!;
        public DbSet<TimeSlot> TimeSlots { get; set; } = null!;
        public DbSet<Schedule> Schedules { get; set; } = null!;
        public DbSet<ScheduleDetailsView> ScheduleDetails { get; set; } = null!;
        public DbSet<ApplicationUser> ApplicationUsers { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer(DatabaseConnection.ConnectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Branch>(entity =>
            {
                entity.ToTable("Branches");
                entity.HasKey(e => e.BranchID);
                entity.HasIndex(e => e.BranchName).IsUnique();

                entity.Property(e => e.BranchID)
                    .ValueGeneratedNever();

                entity.Property(e => e.BranchName)
                    .HasMaxLength(100)
                    .IsRequired();
            });

            modelBuilder.Entity<StudyYear>(entity =>
            {
                entity.ToTable("StudyYears");
                entity.HasKey(e => e.StudyYearID);
                entity.HasIndex(e => e.YearName).IsUnique();

                entity.Property(e => e.StudyYearID)
                    .ValueGeneratedNever();

                entity.Property(e => e.YearName)
                    .HasMaxLength(100)
                    .IsRequired();
            });

            modelBuilder.Entity<Section>(entity =>
            {
                entity.ToTable("Sections");
                entity.HasKey(e => e.SectionID);
                entity.HasIndex(e => new { e.StudyYearID, e.BranchID, e.SectionName }).IsUnique();

                entity.Property(e => e.SectionID)
                    .ValueGeneratedNever();

                entity.Property(e => e.SectionName)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(e => e.StudentCount)
                    .IsRequired();

                entity.HasOne(e => e.StudyYear)
                    .WithMany(sy => sy.Sections)
                    .HasForeignKey(e => e.StudyYearID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Branch)
                    .WithMany(b => b.Sections)
                    .HasForeignKey(e => e.BranchID)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Subject>(entity =>
            {
                entity.ToTable("Subjects");
                entity.HasKey(e => e.SubjectID);
                entity.HasIndex(e => new { e.SubjectName, e.StudyYearID, e.BranchID, e.SemesterNumber }).IsUnique();

                entity.Property(e => e.SubjectID)
                    .ValueGeneratedNever();

                entity.Property(e => e.SubjectName)
                    .HasMaxLength(150)
                    .IsRequired();

                entity.Property(e => e.SemesterNumber)
                    .IsRequired();

                entity.Property(e => e.TheoreticalHours)
                    .IsRequired();

                entity.Property(e => e.PracticalHours)
                    .IsRequired();

                entity.Property(e => e.CreditUnits)
                    .IsRequired();

                entity.Property(e => e.RequirementType)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.HasOne(e => e.StudyYear)
                    .WithMany(sy => sy.Subjects)
                    .HasForeignKey(e => e.StudyYearID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Branch)
                    .WithMany(b => b.Subjects)
                    .HasForeignKey(e => e.BranchID)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<FacultyMember>(entity =>
            {
                entity.ToTable("FacultyMembers");
                entity.HasKey(e => e.FacultyMemberID);
                entity.HasIndex(e => e.FullName).IsUnique();

                entity.Property(e => e.FacultyMemberID)
                    .ValueGeneratedNever();

                entity.Property(e => e.FullName)
                    .HasMaxLength(150)
                    .IsRequired();

                entity.Property(e => e.AcademicTitle)
                    .HasMaxLength(100);
            });

            modelBuilder.Entity<FacultyMemberSubject>(entity =>
            {
                entity.ToTable("FacultyMemberSubjects");
                entity.HasKey(e => new { e.FacultyMemberID, e.SubjectID });

                entity.HasOne(e => e.FacultyMember)
                    .WithMany(f => f.FacultyMemberSubjects)
                    .HasForeignKey(e => e.FacultyMemberID)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Subject)
                    .WithMany(s => s.FacultyMemberSubjects)
                    .HasForeignKey(e => e.SubjectID)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Classroom>(entity =>
            {
                entity.ToTable("Classrooms");
                entity.HasKey(e => e.ClassroomID);
                entity.HasIndex(e => e.ClassroomNumber).IsUnique();

                entity.Property(e => e.ClassroomID)
                    .ValueGeneratedNever();

                entity.Property(e => e.ClassroomNumber)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(e => e.Capacity)
                    .IsRequired();

                entity.Property(e => e.RoomType)
                    .HasMaxLength(50);
            });

            modelBuilder.Entity<TimeSlot>(entity =>
            {
                entity.ToTable("TimeSlots");
                entity.HasKey(e => e.TimeSlotID);
                entity.HasIndex(e => new { e.StartTime, e.EndTime, e.IsBreak }).IsUnique();

                entity.Property(e => e.TimeSlotID)
                    .ValueGeneratedNever();

                entity.Property(e => e.StartTime)
                    .IsRequired();

                entity.Property(e => e.EndTime)
                    .IsRequired();

                entity.Property(e => e.IsBreak)
                    .IsRequired();
            });

            modelBuilder.Entity<Schedule>(entity =>
            {
                entity.ToTable("Schedules");
                entity.HasKey(e => e.ScheduleID);
                entity.HasIndex(e => new { e.ClassroomID, e.SemesterNumber, e.DayOfWeek, e.TimeSlotID })
                    .IsUnique()
                    .HasDatabaseName("UQ_Classroom_Semester_Time");

                entity.HasIndex(e => new { e.FacultyMemberID, e.SemesterNumber, e.DayOfWeek, e.TimeSlotID })
                    .IsUnique()
                    .HasDatabaseName("UQ_Faculty_Semester_Time");

                entity.HasIndex(e => new { e.SectionID, e.SemesterNumber, e.DayOfWeek, e.TimeSlotID, e.GroupName })
                    .IsUnique()
                    .HasDatabaseName("UQ_Section_Semester_Time");

                entity.Property(e => e.SemesterNumber)
                    .IsRequired();

                entity.Property(e => e.LectureType)
                    .HasMaxLength(20)
                    .HasDefaultValue("Theory")
                    .IsRequired();

                entity.Property(e => e.GroupName)
                    .HasMaxLength(20);

                entity.Property(e => e.DayOfWeek)
                    .HasMaxLength(20)
                    .IsRequired();

                entity.HasOne(e => e.Subject)
                    .WithMany(s => s.Schedules)
                    .HasForeignKey(e => e.SubjectID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.FacultyMember)
                    .WithMany(f => f.Schedules)
                    .HasForeignKey(e => e.FacultyMemberID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Classroom)
                    .WithMany(c => c.Schedules)
                    .HasForeignKey(e => e.ClassroomID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.TimeSlot)
                    .WithMany(t => t.Schedules)
                    .HasForeignKey(e => e.TimeSlotID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.StudyYear)
                    .WithMany(sy => sy.Schedules)
                    .HasForeignKey(e => e.StudyYearID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Branch)
                    .WithMany(b => b.Schedules)
                    .HasForeignKey(e => e.BranchID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Section)
                    .WithMany(s => s.Schedules)
                    .HasForeignKey(e => e.SectionID)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ScheduleDetailsView>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("vw_ScheduleDetails");
            });

            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                entity.ToTable("ApplicationUsers");
                entity.HasKey(e => e.ApplicationUserID);
                entity.HasIndex(e => e.NormalizedUserName).IsUnique();

                entity.Property(e => e.ApplicationUserID)
                    .ValueGeneratedNever();

                entity.Property(e => e.FullName)
                    .HasMaxLength(150)
                    .IsRequired();

                entity.Property(e => e.UserName)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(e => e.NormalizedUserName)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(e => e.PasswordHash)
                    .HasMaxLength(128)
                    .IsRequired();

                entity.Property(e => e.PasswordSalt)
                    .HasMaxLength(128)
                    .IsRequired();

                entity.Property(e => e.CreatedAtUtc)
                    .IsRequired();
            });
        }
    }
}
