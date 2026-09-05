using HealthGrid.Domain.Entities;
using HealthGrid.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HealthGrid.Infrastructure.Persistence;

public static class SeedData
{
    public static async Task SeedAsync(HealthGridDbContext db, CancellationToken cancellationToken = default)
    {
        var districts = await EnsureDistrictsAsync(db, cancellationToken);
        var phcs = await EnsurePhcsAsync(db, districts, cancellationToken);
        var medicines = await EnsureMedicinesAsync(db, cancellationToken);
        var diseases = await EnsureDiseasesAsync(db, cancellationToken);
        var specializations = await EnsureSpecializationsAsync(db, cancellationToken);
        await EnsureUsersAsync(db, districts, phcs, cancellationToken);

        if (!await db.Doctors.AnyAsync(cancellationToken))
        {
            var doctors = phcs.Take(5).Select((phc, index) => new Doctor
            {
                DistrictId = phc.DistrictId,
                PhcId = phc.Id,
                Name = index % 2 == 0 ? "Dr. Ananya Singh" : "Dr. Ravi Kumar",
                RegistrationNumber = $"JH-MED-{index + 1:0000}",
                SpecializationId = specializations[index % specializations.Count].Id
            }).ToArray();
            db.Doctors.AddRange(doctors);
            await db.SaveChangesAsync(cancellationToken);
            db.DoctorAvailabilities.AddRange(doctors.Select(doctor => new DoctorAvailability
            {
                DistrictId = doctor.DistrictId,
                PhcId = doctor.PhcId,
                DoctorId = doctor.Id,
                DayOfWeek = DayOfWeek.Monday,
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(16, 0)
            }));
        }

        if (!await db.MedicineInventories.AnyAsync(cancellationToken))
        {
            foreach (var phc in phcs)
            {
                foreach (var medicine in medicines)
                {
                    db.MedicineInventories.Add(new MedicineInventory
                    {
                        DistrictId = phc.DistrictId,
                        PhcId = phc.Id,
                        MedicineId = medicine.Id,
                        QuantityOnHand = phc.Code.EndsWith("-01", StringComparison.Ordinal) ? 250 : 80,
                        SafetyStock = 50,
                        ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(12))
                    });
                }
            }
            await db.SaveChangesAsync(cancellationToken);
            db.InventoryTransactions.AddRange(await db.MedicineInventories.Select(inventory => new InventoryTransaction
            {
                DistrictId = inventory.DistrictId,
                PhcId = inventory.PhcId,
                MedicineId = inventory.MedicineId,
                Type = InventoryTransactionType.Receipt,
                Quantity = inventory.QuantityOnHand,
                BalanceAfter = inventory.QuantityOnHand,
                IdempotencyKey = $"seed-receipt-{inventory.Id}",
                Reference = "Initial development stock"
            }).ToListAsync(cancellationToken));
        }

        if (!await db.PatientVisits.AnyAsync(cancellationToken))
        {
            var patients = phcs.Take(5).SelectMany((phc, districtIndex) => Enumerable.Range(1, 2).Select(patientIndex => new Patient
            {
                DistrictId = phc.DistrictId,
                PhcId = phc.Id,
                LocalIdentifier = $"{phc.Code}-PAT-{patientIndex:000}",
                BirthYear = 1980 + districtIndex * 5 + patientIndex,
                Gender = patientIndex % 2 == 0 ? "F" : "M"
            })).ToArray();
            db.Patients.AddRange(patients);
            await db.SaveChangesAsync(cancellationToken);

            var visits = patients.Select((patient, index) => new PatientVisit
            {
                DistrictId = patient.DistrictId,
                PhcId = patient.PhcId,
                PatientId = patient.Id,
                VisitDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-index)),
                Symptoms = index % 2 == 0 ? "Fever and fatigue" : "Dehydration and weakness"
            }).ToArray();
            db.PatientVisits.AddRange(visits);
            await db.SaveChangesAsync(cancellationToken);

            db.VisitDiagnoses.AddRange(visits.Select((visit, index) => new VisitDiagnosis
            {
                DistrictId = visit.DistrictId,
                PhcId = visit.PhcId,
                PatientVisitId = visit.Id,
                DiseaseId = diseases[index % diseases.Count].Id
            }));
            db.Prescriptions.AddRange(visits.Select(visit => new Prescription
            {
                DistrictId = visit.DistrictId,
                PhcId = visit.PhcId,
                PatientVisitId = visit.Id
            }));
            await db.SaveChangesAsync(cancellationToken);

            var prescriptions = await db.Prescriptions.ToListAsync(cancellationToken);
            db.PrescriptionItems.AddRange(prescriptions.Select((prescription, index) => new PrescriptionItem
            {
                DistrictId = prescription.DistrictId,
                PhcId = prescription.PhcId,
                PrescriptionId = prescription.Id,
                MedicineId = medicines[index % medicines.Count].Id,
                Quantity = 10
            }));
        }

        if (!await db.MedicineRequests.AnyAsync(cancellationToken))
        {
            var source = phcs.First(phc => phc.Code.EndsWith("-01", StringComparison.Ordinal));
            var destination = phcs.First(phc => phc.DistrictId == source.DistrictId && phc.Id != source.Id);
            var request = new MedicineRequest
            {
                DistrictId = source.DistrictId,
                PhcId = destination.Id,
                DestinationPhcId = destination.Id,
                Status = MedicineRequestStatus.Accepted,
                Notes = "Development seed request"
            };
            db.MedicineRequests.Add(request);
            await db.SaveChangesAsync(cancellationToken);
            db.MedicineRequestItems.Add(new MedicineRequestItem
            {
                DistrictId = request.DistrictId,
                PhcId = request.PhcId,
                MedicineRequestId = request.Id,
                MedicineId = medicines[0].Id,
                QuantityRequested = 40,
                QuantityFulfilled = 0
            });
            db.MedicineTransfers.Add(new MedicineTransfer
            {
                DistrictId = request.DistrictId,
                PhcId = destination.Id,
                MedicineRequestId = request.Id,
                SourcePhcId = source.Id,
                DestinationPhcId = destination.Id,
                Status = MedicineTransferStatus.Pending
            });
        }

        if (!await db.ModelVersions.AnyAsync(cancellationToken))
        {
            db.ModelVersions.Add(new ModelVersion
            {
                Name = "Patient volume baseline",
                Version = "seed-1.0",
                MetricsJson = "{\"mae\":2.1,\"dataStatus\":\"Synthetic\"}"
            });
        }

        if (!await db.PredictionSnapshots.AnyAsync(cancellationToken))
        {
            var phc = phcs[0];
            var snapshot = new PredictionSnapshot
            {
                DistrictId = phc.DistrictId,
                PhcId = phc.Id,
                ModelVersion = "seed-1.0",
                ForecastThroughUtc = DateTime.UtcNow.AddDays(7),
                Status = "Ready"
            };
            db.PredictionSnapshots.Add(snapshot);
            await db.SaveChangesAsync(cancellationToken);
            db.PredictionValues.AddRange(Enumerable.Range(1, 7).Select(day => new PredictionValue
            {
                DistrictId = phc.DistrictId,
                PhcId = phc.Id,
                PredictionSnapshotId = snapshot.Id,
                ForecastDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(day)),
                PointForecast = 35 + day,
                LowerBound = 28 + day,
                UpperBound = 42 + day
            }));
            db.AiAlerts.Add(new AiAlert
            {
                DistrictId = phc.DistrictId,
                PhcId = phc.Id,
                AlertType = "StockOutRisk",
                Message = "Paracetamol may stock out in approximately 5 days.",
                Severity = "Warning"
            });
        }

        if (!await db.Notifications.AnyAsync(cancellationToken))
        {
            var phc = phcs[0];
            db.Notifications.Add(new Notification
            {
                DistrictId = phc.DistrictId,
                PhcId = phc.Id,
                Title = "Low stock alert",
                Message = "Review the seeded inventory and pending medicine request.",
                Severity = "Warning"
            });
        }

        if (!await db.AuditLogs.AnyAsync(cancellationToken))
        {
            var phc = phcs[0];
            db.AuditLogs.Add(new AuditLog
            {
                DistrictId = phc.DistrictId,
                PhcId = phc.Id,
                Action = "Seeded",
                EntityName = "DevelopmentDataset",
                DetailsJson = "{\"synthetic\":true}"
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<List<District>> EnsureDistrictsAsync(HealthGridDbContext db, CancellationToken cancellationToken)
    {
        var values = new[] { ("Ranchi", "RAN"), ("Bokaro", "BOK"), ("Dhanbad", "DHA"), ("Deoghar", "DEO"), ("Chatra", "CHA") };
        var districts = await db.Districts.ToListAsync(cancellationToken);
        foreach (var (name, code) in values.Where(value => districts.All(district => district.Code != value.Item2)))
        {
            var district = new District { Name = name, Code = code };
            districts.Add(district);
            db.Districts.Add(district);
        }
        await db.SaveChangesAsync(cancellationToken);
        return districts;
    }

    private static async Task<List<Phc>> EnsurePhcsAsync(HealthGridDbContext db, IReadOnlyCollection<District> districts, CancellationToken cancellationToken)
    {
        var phcs = await db.Phcs.ToListAsync(cancellationToken);
        foreach (var district in districts)
        {
            for (var index = 1; index <= 2; index++)
            {
                var code = $"{district.Code}-{index:00}";
                if (phcs.All(phc => phc.Code != code))
                {
                    var phc = new Phc { DistrictId = district.Id, Name = $"{district.Name} {(index == 1 ? "Central" : "Community")} PHC", Code = code };
                    phcs.Add(phc);
                    db.Phcs.Add(phc);
                }
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        return phcs;
    }

    private static async Task<List<Medicine>> EnsureMedicinesAsync(HealthGridDbContext db, CancellationToken cancellationToken)
    {
        var values = new[] { ("Paracetamol 500mg", "Paracetamol", "tablet"), ("ORS", "Oral rehydration salts", "sachet"), ("Amoxicillin 250mg", "Amoxicillin", "capsule"), ("Artemether 40mg", "Artemether", "tablet") };
        var medicines = await db.Medicines.ToListAsync(cancellationToken);
        var newMedicines = values.Where(value => medicines.All(medicine => medicine.Name != value.Item1)).Select(value => new Medicine { Name = value.Item1, GenericName = value.Item2, Unit = value.Item3 }).ToArray();
        medicines.AddRange(newMedicines);
        db.Medicines.AddRange(newMedicines);
        await db.SaveChangesAsync(cancellationToken);
        return medicines;
    }

    private static async Task<List<Disease>> EnsureDiseasesAsync(HealthGridDbContext db, CancellationToken cancellationToken)
    {
        var values = new[] { ("Fever", "FEVER"), ("Dengue", "DENGUE"), ("Diarrhoea", "DIARRHOEA"), ("Malaria", "MALARIA") };
        var diseases = await db.Diseases.ToListAsync(cancellationToken);
        var newDiseases = values.Where(value => diseases.All(disease => disease.Code != value.Item2)).Select(value => new Disease { Name = value.Item1, Code = value.Item2 }).ToArray();
        diseases.AddRange(newDiseases);
        db.Diseases.AddRange(newDiseases);
        await db.SaveChangesAsync(cancellationToken);
        return diseases;
    }

    private static async Task<List<Specialization>> EnsureSpecializationsAsync(HealthGridDbContext db, CancellationToken cancellationToken)
    {
        var values = new[] { "General Medicine", "Paediatrics", "Gynaecology" };
        var specializations = await db.Specializations.ToListAsync(cancellationToken);
        var newSpecializations = values.Where(name => specializations.All(specialization => specialization.Name != name)).Select(name => new Specialization { Name = name }).ToArray();
        specializations.AddRange(newSpecializations);
        db.Specializations.AddRange(newSpecializations);
        await db.SaveChangesAsync(cancellationToken);
        return specializations;
    }

    private static async Task EnsureUsersAsync(HealthGridDbContext db, IReadOnlyCollection<District> districts, IReadOnlyCollection<Phc> phcs, CancellationToken cancellationToken)
    {
        var roles = new[] { "System Administrator", "District Administrator", "PHC Administrator", "Doctor", "PHC Staff" };
        var existingRoles = await db.Roles.ToListAsync(cancellationToken);
        db.Roles.AddRange(roles.Where(role => existingRoles.All(existing => existing.Name != role)).Select(role => new IdentityRole<Guid> { Name = role, NormalizedName = role.ToUpperInvariant() }));
        await db.SaveChangesAsync(cancellationToken);
        if (await db.Users.AnyAsync(cancellationToken)) return;

        var ranchi = districts.Single(district => district.Code == "RAN");
        var ranchiPhc = phcs.Single(phc => phc.Code == "RAN-01");
        var bokaro = districts.Single(district => district.Code == "BOK");
        var bokaroPhc = phcs.Single(phc => phc.Code == "BOK-01");
        var users = new[]
        {
            new ApplicationUser { UserName = "admin@healthgrid.local", Email = "admin@healthgrid.local", EmailConfirmed = true },
            new ApplicationUser { UserName = "ranchi.admin@healthgrid.local", Email = "ranchi.admin@healthgrid.local", EmailConfirmed = true, DistrictId = ranchi.Id, PhcId = ranchiPhc.Id },
            new ApplicationUser { UserName = "bokaro.staff@healthgrid.local", Email = "bokaro.staff@healthgrid.local", EmailConfirmed = true, DistrictId = bokaro.Id, PhcId = bokaroPhc.Id }
        };
        var hasher = new PasswordHasher<ApplicationUser>();
        foreach (var user in users) user.PasswordHash = hasher.HashPassword(user, "HealthGrid!12345");
        db.Users.AddRange(users);
        await db.SaveChangesAsync(cancellationToken);

        var roleMap = await db.Roles.ToDictionaryAsync(role => role.Name!, cancellationToken);
        db.UserRoles.AddRange(new IdentityUserRole<Guid> { UserId = users[0].Id, RoleId = roleMap["System Administrator"].Id }, new IdentityUserRole<Guid> { UserId = users[1].Id, RoleId = roleMap["District Administrator"].Id }, new IdentityUserRole<Guid> { UserId = users[2].Id, RoleId = roleMap["PHC Staff"].Id });
        db.UserPhcMemberships.AddRange(new UserPhcMembership { UserId = users[1].Id, DistrictId = ranchi.Id, PhcId = ranchiPhc.Id }, new UserPhcMembership { UserId = users[2].Id, DistrictId = bokaro.Id, PhcId = bokaroPhc.Id });
        db.RefreshTokens.Add(new RefreshToken { UserId = users[1].Id, TokenHash = "seed-refresh-token-hash", ExpiresAtUtc = DateTime.UtcNow.AddDays(30) });
        await db.SaveChangesAsync(cancellationToken);
    }
}
