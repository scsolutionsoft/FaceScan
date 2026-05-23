using FaceScan.Web.Models.Entities;
using FaceScan.Web.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FaceScan.Web.Data;

public static class DbInitializer
{
    public static readonly string[] Roles =
    [
        "SuperAdmin",
        "Admin",
        "Staff",
        "Viewer",
        "Student",
        "Executive",
        "Teacher",
        "HomeroomHead"
    ];

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var scopedProvider = scope.ServiceProvider;

        var dbContext = scopedProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = scopedProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scopedProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await dbContext.Database.MigrateAsync();

        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        const string superAdminUsername = "superadmin";
        const string superAdminEmail = "admin@school.local";
        const string superAdminPassword = "Admin@123456";

        var superAdmin = await userManager.FindByNameAsync(superAdminUsername);
        if (superAdmin is null)
        {
            superAdmin = new ApplicationUser
            {
                UserName = superAdminUsername,
                Email = superAdminEmail,
                EmailConfirmed = true,
                FullName = "System Super Admin",
                IsActive = true
            };

            var result = await userManager.CreateAsync(superAdmin, superAdminPassword);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(x => x.Description));
                throw new InvalidOperationException($"Unable to create superadmin user: {errors}");
            }
        }

        if (!await userManager.IsInRoleAsync(superAdmin, "SuperAdmin"))
        {
            await userManager.AddToRoleAsync(superAdmin, "SuperAdmin");
        }

        await SeedDefaultUsersAsync(userManager);

        var academicYear = await SeedAcademicYearAsync(dbContext);
        var gradeLevels = await SeedGradeLevelsAsync(dbContext);
        var classrooms = await SeedClassroomsAsync(dbContext, academicYear.Id, gradeLevels);

        await SeedSystemSettingsAsync(dbContext, academicYear.Id);
        await SeedClassPeriodsAsync(dbContext);
        await SeedScanDevicesAsync(dbContext);
        await SeedStudentsAsync(dbContext, academicYear.Id, gradeLevels, classrooms);
        await SeedDemoStudentAccountsAsync(dbContext, userManager);
        await SeedDemoPeriodAttendanceAsync(dbContext, userManager, classrooms);
    }

    private static async Task SeedDefaultUsersAsync(UserManager<ApplicationUser> userManager)
    {
        var users = new[]
        {
            new { Username = "admin", Email = "admin1@school.local", Password = "Admin@123456", Role = "Admin", FullName = "School Admin" },
            new { Username = "staff", Email = "staff@school.local", Password = "Staff@123456", Role = "Staff", FullName = "Scan Staff" },
            new { Username = "viewer", Email = "viewer@school.local", Password = "Viewer@123456", Role = "Viewer", FullName = "Report Viewer" },
            new { Username = "executive", Email = "executive@school.local", Password = "Executive@123", Role = "Executive", FullName = "School Executive" },
            new { Username = "teacher", Email = "teacher@school.local", Password = "Teacher@123", Role = "Teacher", FullName = "Class Teacher" },
            new { Username = "homeroom", Email = "homeroom@school.local", Password = "Homeroom@123", Role = "HomeroomHead", FullName = "Homeroom Head" }
        };

        foreach (var item in users)
        {
            var user = await userManager.FindByNameAsync(item.Username);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = item.Username,
                    Email = item.Email,
                    EmailConfirmed = true,
                    FullName = item.FullName,
                    IsActive = true
                };

                var result = await userManager.CreateAsync(user, item.Password);
                if (!result.Succeeded)
                {
                    continue;
                }
            }

            if (!await userManager.IsInRoleAsync(user, item.Role))
            {
                await userManager.AddToRoleAsync(user, item.Role);
            }
        }
    }

    private static async Task<AcademicYear> SeedAcademicYearAsync(ApplicationDbContext dbContext)
    {
        var current = await dbContext.AcademicYears.FirstOrDefaultAsync(x => x.IsCurrent);
        if (current is not null)
        {
            return current;
        }

        var academicYear = new AcademicYear
        {
            Name = "2568",
            StartDate = new DateTime(2025, 5, 1),
            EndDate = new DateTime(2026, 3, 31),
            IsCurrent = true,
            IsActive = true
        };

        dbContext.AcademicYears.Add(academicYear);
        await dbContext.SaveChangesAsync();

        return academicYear;
    }

    private static async Task<List<GradeLevel>> SeedGradeLevelsAsync(ApplicationDbContext dbContext)
    {
        var existing = await dbContext.GradeLevels.OrderBy(x => x.SortOrder).ToListAsync();
        if (existing.Count >= 6)
        {
            return existing;
        }

        var names = new[] { "ม.1", "ม.2", "ม.3", "ม.4", "ม.5", "ม.6" };
        for (var i = 0; i < names.Length; i++)
        {
            if (existing.Any(x => x.Name == names[i]))
            {
                continue;
            }

            dbContext.GradeLevels.Add(new GradeLevel
            {
                Name = names[i],
                SortOrder = i + 1,
                IsActive = true
            });
        }

        await dbContext.SaveChangesAsync();
        return await dbContext.GradeLevels.OrderBy(x => x.SortOrder).ToListAsync();
    }

    private static async Task<List<Classroom>> SeedClassroomsAsync(
        ApplicationDbContext dbContext,
        int academicYearId,
        IReadOnlyList<GradeLevel> gradeLevels)
    {
        var existing = await dbContext.Classrooms
            .Where(x => x.AcademicYearId == academicYearId)
            .ToListAsync();

        if (existing.Count >= 12)
        {
            return existing;
        }

        foreach (var grade in gradeLevels)
        {
            for (var room = 1; room <= 2; room++)
            {
                var name = $"{grade.SortOrder}/{room}";
                if (existing.Any(x => x.GradeLevelId == grade.Id && x.Name == name))
                {
                    continue;
                }

                dbContext.Classrooms.Add(new Classroom
                {
                    Name = name,
                    RoomCode = name,
                    AcademicYearId = academicYearId,
                    GradeLevelId = grade.Id,
                    IsActive = true
                });
            }
        }

        await dbContext.SaveChangesAsync();

        return await dbContext.Classrooms
            .Where(x => x.AcademicYearId == academicYearId)
            .OrderBy(x => x.GradeLevelId)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }

    private static async Task SeedSystemSettingsAsync(ApplicationDbContext dbContext, int academicYearId)
    {
        if (await dbContext.SystemSettings.AnyAsync())
        {
            return;
        }

        dbContext.SystemSettings.Add(new SystemSetting
        {
            DuplicateWindowMinutes = 3,
            LateAfterTime = new TimeSpan(8, 0, 0),
            CheckOutStartTime = new TimeSpan(15, 30, 0),
            TeacherLateAfterTime = new TimeSpan(8, 0, 0),
            TeacherCheckOutStartTime = new TimeSpan(15, 30, 0),
            SaveSnapshots = true,
            FaceConfidenceThreshold = 0.80m,
            AllowManualOverride = false,
            SchoolName = "FaceScan Demo School",
            ApplicationDisplayName = "FaceScan",
            ApplicationTagline = "ระบบเช็กเวลาเข้า-ออกด้วยการสแกนใบหน้า",
            ThemePrimaryColor = "#7C3AED",
            ThemePrimarySoftColor = "#A855F7",
            ThemeAccentColor = "#E9D5FF",
            ThemeBackgroundColor = "#F7F3FF",
            ThemeSurfaceColor = "#FFFFFF",
            ThemeSidebarStartColor = "#7C3AED",
            ThemeSidebarEndColor = "#4C1D95",
            AcademicYearCurrentId = academicYearId
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedScanDevicesAsync(ApplicationDbContext dbContext)
    {
        if (await dbContext.ScanDevices.AnyAsync())
        {
            return;
        }

        dbContext.ScanDevices.Add(new ScanDevice
        {
            Name = "Main Gate Scanner",
            StationCode = "MAIN-GATE",
            AccessToken = "SCAN-12345",
            Location = "หน้าโรงเรียน",
            IsActive = true
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedStudentsAsync(
        ApplicationDbContext dbContext,
        int academicYearId,
        IReadOnlyList<GradeLevel> gradeLevels,
        IReadOnlyList<Classroom> classrooms)
    {
        const int targetStudentsPerClassroom = 5;

        var firstNames = new[]
        {
            "ธนา", "กิตติ", "ณัฐ", "อริยา", "พิมพ์ชนก", "ศุภกร", "ชลธิชา", "ปภัสรา", "วรพล", "เมธา",
            "วิภา", "กานต์", "ธิดา", "พรชัย", "สุชาดา", "อนุชา", "สุทธิพร", "ปริญญา", "จิราพร", "อรทัย"
        };

        var lastNames = new[]
        {
            "ใจดี", "แซ่ลิ้ม", "บุญส่ง", "รัตนกุล", "วัฒนะ", "ทองสุข", "ศรีแก้ว", "พูนทรัพย์", "ธนากร", "เพชรดี",
            "สุขสันต์", "ธรรมรักษ์", "อินทร์ใจ", "พงศ์ไพบูลย์", "เนียมทอง", "รักษ์เรียน", "พิบูลย์", "กล้าหาญ", "เขียวหวาน", "ศิริวงศ์"
        };

        var readableFirstNames = new[]
        {
            "ธนา", "กิตติ", "ณัฐ", "อริยา", "พิมพ์ชนก", "ศุภกร", "ชลธิชา", "ภัสรา", "วรพล", "เมธา",
            "วิภา", "กานต์", "ธิดา", "พรชัย", "สุชาดา", "อนุชา", "สุทธิพร", "ปริญญา", "จิราพร", "อรทัย"
        };

        var readableLastNames = new[]
        {
            "ใจดี", "แซ่ลิ้ม", "บุญส่ง", "รัตนกุล", "วัฒนา", "ทองสุข", "ศรีแก้ว", "พูลทรัพย์", "ธนากร", "เพชรดี",
            "สุขสันต์", "ธรรมรักษ์", "อินทร์ใจ", "พงศ์ไพบูลย์", "เนียมทอง", "รักษ์เรียน", "พิบูลย์", "กล้าหาญ", "เขียวหวาน", "ศิริวงศ์"
        };

        var existingStudents = await dbContext.Students
            .Where(x => x.IsActive)
            .OrderBy(x => x.StudentCode)
            .ToListAsync();

        var maxStudentCode = existingStudents
            .Select(x => x.StudentCode)
            .Where(x => !string.IsNullOrWhiteSpace(x) && x.StartsWith('S'))
            .Select(x => int.TryParse(x[1..], out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();

        var nextStudentCode = maxStudentCode + 1;
        var classroomSeedRandom = new Random(20260321);
        var newStudents = new List<Student>();

        foreach (var classroom in classrooms.OrderBy(x => x.GradeLevelId).ThenBy(x => x.Name))
        {
            var grade = gradeLevels.First(x => x.Id == classroom.GradeLevelId);
            var classroomStudents = existingStudents
                .Where(x => x.ClassroomId == classroom.Id && x.IsActive)
                .OrderBy(x => x.StudentCode)
                .ToList();

            var missingCount = Math.Max(targetStudentsPerClassroom - classroomStudents.Count, 0);
            for (var offset = 0; offset < missingCount; offset++)
            {
                var seedIndex = nextStudentCode + offset - 1;
                var gender = seedIndex % 2 == 0 ? GenderType.Female : GenderType.Male;
                var firstName = readableFirstNames[seedIndex % readableFirstNames.Length];
                var lastName = readableLastNames[(seedIndex / 2) % readableLastNames.Length];
                var student = new Student
                {
                    StudentCode = $"S{nextStudentCode:0000}",
                    Prefix = gender == GenderType.Male ? "ด.ช." : "ด.ญ.",
                    FirstName = firstName,
                    LastName = lastName,
                    Gender = gender,
                    BirthDate = new DateTime(2018 - grade.SortOrder, classroomSeedRandom.Next(1, 13), classroomSeedRandom.Next(1, 28)),
                    AcademicYearId = academicYearId,
                    GradeLevelId = grade.Id,
                    ClassroomId = classroom.Id,
                    StudentNo = (classroomStudents.Count + offset + 1).ToString(),
                    Status = StudentStatus.Active,
                    GuardianName = $"ผู้ปกครอง {firstName}",
                    GuardianPhone = $"08{classroomSeedRandom.Next(10000000, 99999999)}",
                    Address = "กรุงเทพมหานคร",
                    IsActive = true
                };

                newStudents.Add(student);
                existingStudents.Add(student);
                nextStudentCode++;
            }
        }

        if (newStudents.Count > 0)
        {
            dbContext.Students.AddRange(newStudents);
            await dbContext.SaveChangesAsync();

            dbContext.FaceProfiles.AddRange(newStudents.Select((student, index) => new FaceProfile
            {
                StudentId = student.Id,
                EnrollmentStatus = index % 3 == 0 ? EnrollmentStatus.Pending : EnrollmentStatus.Ready,
                TemplateVersion = "v1",
                LastTrainedAt = DateTime.UtcNow.AddDays(-classroomSeedRandom.Next(1, 60)),
                EmbeddingJson = "{\"mock\":true}"
            }));

            await dbContext.SaveChangesAsync();
        }

        var random = new Random(2026);
        for (var i = 1; i <= 0; i++)
        {
            var studentCode = $"S{i:0000}";
            if (await dbContext.Students.AnyAsync(x => x.StudentCode == studentCode))
            {
                continue;
            }

            var grade = gradeLevels[random.Next(gradeLevels.Count)];
            var classCandidates = classrooms.Where(x => x.GradeLevelId == grade.Id).ToList();
            var room = classCandidates[random.Next(classCandidates.Count)];

            var firstName = firstNames[i - 1];
            var lastName = lastNames[i - 1];
            var gender = i % 2 == 0 ? GenderType.Female : GenderType.Male;

            var student = new Student
            {
                StudentCode = studentCode,
                Prefix = gender == GenderType.Male ? "ด.ช." : "ด.ญ.",
                FirstName = firstName,
                LastName = lastName,
                Gender = gender,
                BirthDate = new DateTime(2012, random.Next(1, 12), random.Next(1, 28)),
                AcademicYearId = academicYearId,
                GradeLevelId = grade.Id,
                ClassroomId = room.Id,
                StudentNo = i.ToString(),
                Status = StudentStatus.Active,
                GuardianName = $"ผู้ปกครอง {firstName}",
                GuardianPhone = $"08{random.Next(10000000, 99999999)}",
                Address = "กรุงเทพมหานคร",
                IsActive = true
            };

            dbContext.Students.Add(student);
            await dbContext.SaveChangesAsync();

            dbContext.FaceProfiles.Add(new FaceProfile
            {
                StudentId = student.Id,
                EnrollmentStatus = i % 3 == 0 ? EnrollmentStatus.Pending : EnrollmentStatus.Ready,
                TemplateVersion = "v1",
                LastTrainedAt = DateTime.UtcNow.AddDays(-random.Next(1, 60)),
                EmbeddingJson = "{\"mock\":true}"
            });

            await dbContext.SaveChangesAsync();
        }
    }

    private static async Task SeedClassPeriodsAsync(ApplicationDbContext dbContext)
    {
        if (await dbContext.ClassPeriods.AnyAsync())
        {
            return;
        }

        var periods = new List<ClassPeriod>
        {
            new() { Name = "หน้าเสาธง", SortOrder = 0, StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(8, 30, 0), IsVisibleForCheck = true, IsActive = true }
        };

        var start = new TimeSpan(8, 30, 0);
        for (var i = 1; i <= 10; i++)
        {
            var periodStart = start.Add(TimeSpan.FromMinutes((i - 1) * 50));
            periods.Add(new ClassPeriod
            {
                Name = $"คาบ {i}",
                SortOrder = i,
                StartTime = periodStart,
                EndTime = periodStart.Add(TimeSpan.FromMinutes(50)),
                IsVisibleForCheck = true,
                IsActive = true
            });
        }

        dbContext.ClassPeriods.AddRange(periods);
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedDemoPeriodAttendanceAsync(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IReadOnlyList<Classroom> classrooms)
    {
        var orderedClassrooms = classrooms
            .OrderBy(x => x.GradeLevelId)
            .ThenBy(x => x.Name)
            .ToList();

        var teacher = await userManager.FindByNameAsync("teacher");
        var homeroom = await userManager.FindByNameAsync("homeroom");

        var teacherClassroom = orderedClassrooms.FirstOrDefault();
        var homeroomClassroom = orderedClassrooms.Skip(1).FirstOrDefault() ?? teacherClassroom;

        if (teacher is not null && teacherClassroom is not null && teacher.AssignedClassroomId != teacherClassroom.Id)
        {
            teacher.AssignedClassroomId = teacherClassroom.Id;
            await userManager.UpdateAsync(teacher);
        }

        if (homeroom is not null && homeroomClassroom is not null && homeroom.AssignedClassroomId != homeroomClassroom.Id)
        {
            homeroom.AssignedClassroomId = homeroomClassroom.Id;
            await userManager.UpdateAsync(homeroom);
        }

        var existingSessionKeys = (await dbContext.PeriodAttendanceSessions
                .AsNoTracking()
                .Select(x => new { x.Date, x.ClassroomId, x.ClassPeriodId })
                .ToListAsync())
            .Select(x => (x.Date, x.ClassroomId, x.ClassPeriodId))
            .ToHashSet();

        if (teacherClassroom is null)
        {
            return;
        }

        var checker = homeroom?.Id ?? teacher?.Id;
        if (string.IsNullOrWhiteSpace(checker))
        {
            var superAdmin = await userManager.FindByNameAsync("superadmin");
            checker = superAdmin?.Id;
        }

        if (string.IsNullOrWhiteSpace(checker))
        {
            return;
        }

        var students = await dbContext.Students
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.StudentCode)
            .ToListAsync();

        if (students.Count == 0)
        {
            return;
        }

        var periods = await dbContext.ClassPeriods
            .AsNoTracking()
            .Where(x => x.IsActive && x.IsVisibleForCheck)
            .OrderBy(x => x.SortOrder)
            .ToListAsync();

        if (periods.Count == 0)
        {
            return;
        }

        var studentGroups = students
            .GroupBy(x => x.ClassroomId)
            .ToDictionary(x => x.Key, x => x.OrderBy(student => student.StudentCode).ToList());

        var newSessions = new List<PeriodAttendanceSession>();
        var random = new Random(20260321);
        for (var dayOffset = 0; dayOffset < 10; dayOffset++)
        {
            var date = DateTime.Today.AddDays(-dayOffset).Date;
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            foreach (var classroom in orderedClassrooms)
            {
                if (!studentGroups.TryGetValue(classroom.Id, out var classroomStudents) || classroomStudents.Count == 0)
                {
                    continue;
                }

                foreach (var period in periods)
                {
                    var sessionKey = (date, classroom.Id, period.Id);
                    if (existingSessionKeys.Contains(sessionKey))
                    {
                        continue;
                    }

                    var session = new PeriodAttendanceSession
                    {
                        Date = date,
                        ClassroomId = classroom.Id,
                        ClassPeriodId = period.Id,
                        CheckedByUserId = checker,
                        CheckedAt = date.AddHours(8).AddMinutes(20 + (period.SortOrder * 50)),
                        TeacherStatus = random.Next(0, 100) < 8 ? TeacherTeachingStatus.Abnormal : TeacherTeachingStatus.Normal,
                        TeacherStatusNote = null
                    };

                    foreach (var student in classroomStudents)
                    {
                        var roll = random.Next(0, 100);
                        var status = roll switch
                        {
                            < 55 => PeriodAttendanceStatus.Present,
                            < 68 => PeriodAttendanceStatus.Late,
                            < 79 => PeriodAttendanceStatus.Absent,
                            < 88 => PeriodAttendanceStatus.Leave,
                            < 95 => PeriodAttendanceStatus.Truancy,
                            _ => PeriodAttendanceStatus.Other
                        };

                        session.Records.Add(new PeriodAttendanceRecord
                        {
                            StudentId = student.Id,
                            Status = status,
                            Remark = status == PeriodAttendanceStatus.Other ? "สถานะพิเศษสำหรับทดสอบระบบ" : null
                        });
                    }

                    newSessions.Add(session);
                    existingSessionKeys.Add(sessionKey);
                }
            }
        }

        if (newSessions.Count == 0)
        {
            return;
        }

        dbContext.PeriodAttendanceSessions.AddRange(newSessions);
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedDemoStudentAccountsAsync(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager)
    {
        var students = await dbContext.Students
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.StudentCode)
            .Take(3)
            .ToListAsync();

        foreach (var student in students)
        {
            var username = student.StudentCode.ToLowerInvariant();
            var user = await userManager.FindByNameAsync(username);
            if (user is null)
            {
                var email = $"{username}@student.local";
                user = new ApplicationUser
                {
                    UserName = username,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = student.FullName,
                    StudentId = student.Id,
                    IsActive = true
                };

                var result = await userManager.CreateAsync(user, "Student@123");
                if (!result.Succeeded)
                {
                    continue;
                }
            }
            else if (user.StudentId != student.Id)
            {
                user.StudentId = student.Id;
                await userManager.UpdateAsync(user);
            }

            if (!await userManager.IsInRoleAsync(user, "Student"))
            {
                await userManager.AddToRoleAsync(user, "Student");
            }
        }
    }
}
