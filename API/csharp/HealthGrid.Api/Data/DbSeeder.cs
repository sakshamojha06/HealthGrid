using HealthGrid.Api.Auth;
using HealthGrid.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HealthGrid.Api.Data;

/// <summary>
/// Development seed data. Every generated row is synthetic and clearly marked so
/// it is safe to wipe. Idempotent: re-running only fills gaps.
/// </summary>
public static class DbSeeder
{
    public const string DemoPassword = "HealthGrid!12345";

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HealthGridDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        await db.Database.MigrateAsync(ct);

        foreach (var role in Roles.All)
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole<Guid>(role));

        var districts = await EnsureDistrictsAsync(db, ct);
        var phcs = await EnsurePhcsAsync(db, districts, ct);
        var medicines = await EnsureMedicinesAsync(db, ct);
        var diseases = await EnsureDiseasesAsync(db, ct);
        var specializations = await EnsureSpecializationsAsync(db, ct);
        await EnsureUsersAsync(users, districts, phcs, ct);
        await EnsureDoctorsAsync(db, phcs, specializations, ct);
        await EnsureInventoryAsync(db, phcs, medicines, ct);
        await EnsureVisitsAsync(db, phcs, medicines, diseases, ct);
        await EnsureSampleRequestAsync(db, phcs, medicines, users, ct);
    }

    private static async Task<List<District>> EnsureDistrictsAsync(HealthGridDbContext db, CancellationToken ct)
    {
        var wanted = new[]
        {
            ("Ranchi", "RAN"), ("Bokaro", "BOK"), ("Dhanbad", "DHA"), ("Deoghar", "DEO"), ("Chatra", "CHA"),
        };
        var existing = await db.Districts.ToListAsync(ct);
        foreach (var (name, code) in wanted.Where(w => existing.All(d => d.Code != w.Item2)))
        {
            var district = new District { Name = name, Code = code };
            existing.Add(district);
            db.Districts.Add(district);
        }
        await db.SaveChangesAsync(ct);
        return existing;
    }

    private static async Task<List<Phc>> EnsurePhcsAsync(
        HealthGridDbContext db, List<District> districts, CancellationToken ct)
    {
        var existing = await db.Phcs.ToListAsync(ct);
        foreach (var district in districts)
        {
            for (var i = 1; i <= 2; i++)
            {
                var code = $"{district.Code}-{i:00}";
                if (existing.Any(p => p.Code == code && p.DistrictId == district.Id))
                    continue;
                var phc = new Phc
                {
                    DistrictId = district.Id,
                    Name = $"{district.Name} {(i == 1 ? "Central" : "Community")} PHC",
                    Code = code,
                };
                existing.Add(phc);
                db.Phcs.Add(phc);
            }
        }
        await db.SaveChangesAsync(ct);
        return existing;
    }

    private static async Task<List<Medicine>> EnsureMedicinesAsync(HealthGridDbContext db, CancellationToken ct)
    {
        var wanted = new[]
        {
            ("Paracetamol 500mg", "Paracetamol", "tablet"),
            ("ORS Sachet", "Oral rehydration salts", "sachet"),
            ("Amoxicillin 250mg", "Amoxicillin", "capsule"),
            ("Artemether 40mg", "Artemether", "tablet"),
            ("Iron & Folic Acid", "Ferrous sulphate + folic acid", "tablet"),
        };
        var existing = await db.Medicines.ToListAsync(ct);
        var added = wanted.Where(w => existing.All(m => m.Name != w.Item1))
            .Select(w => new Medicine { Name = w.Item1, GenericName = w.Item2, Unit = w.Item3 }).ToList();
        db.Medicines.AddRange(added);
        existing.AddRange(added);
        await db.SaveChangesAsync(ct);
        return existing;
    }

    private static async Task<List<Disease>> EnsureDiseasesAsync(HealthGridDbContext db, CancellationToken ct)
    {
        var wanted = new[]
        {
            ("Fever", "FEVER"), ("Dengue", "DENGUE"), ("Diarrhoea", "DIARRHOEA"),
            ("Malaria", "MALARIA"), ("Anaemia", "ANAEMIA"), ("Respiratory infection", "ARI"),
        };
        var existing = await db.Diseases.ToListAsync(ct);
        var added = wanted.Where(w => existing.All(d => d.Code != w.Item2))
            .Select(w => new Disease { Name = w.Item1, Code = w.Item2 }).ToList();
        db.Diseases.AddRange(added);
        existing.AddRange(added);
        await db.SaveChangesAsync(ct);
        return existing;
    }

    private static async Task<List<Specialization>> EnsureSpecializationsAsync(
        HealthGridDbContext db, CancellationToken ct)
    {
        var wanted = new[] { "General Medicine", "Paediatrics", "Gynaecology", "Community Health" };
        var existing = await db.Specializations.ToListAsync(ct);
        var added = wanted.Where(n => existing.All(s => s.Name != n))
            .Select(n => new Specialization { Name = n }).ToList();
        db.Specializations.AddRange(added);
        existing.AddRange(added);
        await db.SaveChangesAsync(ct);
        return existing;
    }

    private static async Task EnsureUsersAsync(
        UserManager<ApplicationUser> users, List<District> districts, List<Phc> phcs, CancellationToken ct)
    {
        var ranchi = districts.Single(d => d.Code == "RAN");
        var bokaro = districts.Single(d => d.Code == "BOK");
        var ranchiCentral = phcs.Single(p => p.Code == "RAN-01");
        var bokaroCentral = phcs.Single(p => p.Code == "BOK-01");

        await CreateAsync("admin@healthgrid.local", "System Administrator",
            Roles.SystemAdministrator, null, null);
        await CreateAsync("ranchi.admin@healthgrid.local", "Ranchi District Admin",
            Roles.DistrictAdministrator, ranchi.Id, null);
        await CreateAsync("ranchi.phc@healthgrid.local", "Ranchi Central PHC Admin",
            Roles.PhcAdministrator, ranchi.Id, ranchiCentral.Id);
        await CreateAsync("ranchi.doctor@healthgrid.local", "Dr. Ananya Singh",
            Roles.Doctor, ranchi.Id, ranchiCentral.Id);
        await CreateAsync("bokaro.staff@healthgrid.local", "Bokaro PHC Staff",
            Roles.PhcStaff, bokaro.Id, bokaroCentral.Id);

        async Task CreateAsync(string email, string name, string role, Guid? districtId, Guid? phcId)
        {
            if (await users.FindByEmailAsync(email) is not null)
                return;
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = name,
                DistrictId = districtId,
                PhcId = phcId,
            };
            var result = await users.CreateAsync(user, DemoPassword);
            if (result.Succeeded)
                await users.AddToRoleAsync(user, role);
        }
    }

    private static async Task EnsureDoctorsAsync(
        HealthGridDbContext db, List<Phc> phcs, List<Specialization> specializations, CancellationToken ct)
    {
        if (await db.Doctors.AnyAsync(ct))
            return;

        var names = new[] { "Dr. Ananya Singh", "Dr. Ravi Kumar", "Dr. Meera Das", "Dr. Imran Ali" };
        var doctors = phcs.Take(4).Select((phc, i) => new Doctor
        {
            DistrictId = phc.DistrictId,
            PhcId = phc.Id,
            Name = names[i],
            RegistrationNumber = $"JH-MED-{i + 1:0000}",
            SpecializationId = specializations[i % specializations.Count].Id,
        }).ToList();
        db.Doctors.AddRange(doctors);
        await db.SaveChangesAsync(ct);

        foreach (var doctor in doctors)
        {
            db.DoctorAvailabilities.Add(new DoctorAvailability
            {
                DistrictId = doctor.DistrictId,
                PhcId = doctor.PhcId,
                DoctorId = doctor.Id,
                DayOfWeek = (int)DayOfWeek.Monday,
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(13, 0),
            });
            db.DoctorAvailabilities.Add(new DoctorAvailability
            {
                DistrictId = doctor.DistrictId,
                PhcId = doctor.PhcId,
                DoctorId = doctor.Id,
                DayOfWeek = (int)DayOfWeek.Thursday,
                StartTime = new TimeOnly(10, 0),
                EndTime = new TimeOnly(16, 0),
            });
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureInventoryAsync(
        HealthGridDbContext db, List<Phc> phcs, List<Medicine> medicines, CancellationToken ct)
    {
        if (await db.MedicineInventories.AnyAsync(ct))
            return;

        foreach (var phc in phcs)
        {
            foreach (var medicine in medicines)
            {
                var opening = phc.Code.EndsWith("-01", StringComparison.Ordinal) ? 260m : 90m;
                var inventory = new MedicineInventory
                {
                    DistrictId = phc.DistrictId,
                    PhcId = phc.Id,
                    MedicineId = medicine.Id,
                    QuantityOnHand = opening,
                    SafetyStock = 60m,
                    ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(14)),
                };
                db.MedicineInventories.Add(inventory);
                db.InventoryTransactions.Add(new InventoryTransaction
                {
                    DistrictId = phc.DistrictId,
                    PhcId = phc.Id,
                    MedicineId = medicine.Id,
                    Type = InventoryTransactionType.Receipt,
                    Quantity = opening,
                    BalanceAfter = opening,
                    Reference = "Synthetic opening stock",
                    IdempotencyKey = $"seed-open-{phc.Id}-{medicine.Id}",
                });
            }
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Seeds exactly <c>TargetVisits</c> synthetic patient visits (one patient + one
    /// diagnosis + one prescription line each) spread across every PHC and the last
    /// ~120 days. Only runs on an empty visit table.
    /// </summary>
    private const int TargetVisits = 1000;

    private static async Task EnsureVisitsAsync(
        HealthGridDbContext db, List<Phc> phcs, List<Medicine> medicines, List<Disease> diseases, CancellationToken ct)
    {
        if (await db.PatientVisits.AnyAsync(ct))
            return;

        var rng = new Random(20260906);
        var patientSeq = phcs.ToDictionary(p => p.Id, _ => 0);
        var created = 0;
        var dayOffset = 0;

        while (created < TargetVisits)
        {
            var visitDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-(dayOffset % 120)));
            foreach (var phc in phcs)
            {
                if (created >= TargetVisits)
                    break;
                var visitCount = rng.Next(1, 5);
                for (var v = 0; v < visitCount && created < TargetVisits; v++)
                {
                    var patient = new Patient
                    {
                        DistrictId = phc.DistrictId,
                        PhcId = phc.Id,
                        LocalIdentifier = $"{phc.Code}-P{++patientSeq[phc.Id]:00000}",
                        BirthYear = rng.Next(1950, 2020),
                        Gender = rng.Next(2) == 0 ? "F" : "M",
                    };
                    db.Patients.Add(patient);

                    var disease = diseases[rng.Next(diseases.Count)];
                    var medicine = medicines[rng.Next(medicines.Count)];
                    var qty = rng.Next(5, 20);

                    var visit = new PatientVisit
                    {
                        DistrictId = phc.DistrictId,
                        PhcId = phc.Id,
                        Patient = patient,
                        VisitDate = visitDate,
                        Symptoms = "Synthetic seed visit",
                        Diagnoses = [new VisitDiagnosis
                        {
                            DistrictId = phc.DistrictId, PhcId = phc.Id, DiseaseId = disease.Id,
                        }],
                        PrescriptionItems = [new VisitPrescriptionItem
                        {
                            DistrictId = phc.DistrictId, PhcId = phc.Id, MedicineId = medicine.Id, Quantity = qty,
                        }],
                    };
                    db.PatientVisits.Add(visit);
                    created++;
                }
            }
            dayOffset++;
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureSampleRequestAsync(
        HealthGridDbContext db, List<Phc> phcs, List<Medicine> medicines,
        UserManager<ApplicationUser> users, CancellationToken ct)
    {
        if (await db.MedicineRequests.AnyAsync(ct))
            return;

        var community = phcs.First(p => p.Code == "RAN-02");
        var central = phcs.First(p => p.Code == "RAN-01");
        var requester = await users.FindByEmailAsync("ranchi.phc@healthgrid.local");

        var request = new MedicineRequest
        {
            DistrictId = community.DistrictId,
            SourcePhcId = community.Id,
            DestinationPhcId = central.Id,
            Status = MedicineRequestStatus.Pending,
            Notes = "Synthetic seed request",
            RequestedByUserId = requester?.Id ?? Guid.Empty,
            RequestedByName = requester?.FullName ?? "Seed",
            Items = [new MedicineRequestItem { MedicineId = medicines[0].Id, QuantityRequested = 40 }],
        };
        db.MedicineRequests.Add(request);
        await db.SaveChangesAsync(ct);
    }
}
