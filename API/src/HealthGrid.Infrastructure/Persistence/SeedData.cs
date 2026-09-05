using HealthGrid.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HealthGrid.Infrastructure.Persistence;

public static class SeedData
{
    public static async Task SeedAsync(HealthGridDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Districts.AnyAsync(cancellationToken)) return;
        var districts = new[]
        {
            new District { Name = "Ranchi", Code = "RAN" },
            new District { Name = "Bokaro", Code = "BOK" },
            new District { Name = "Dhanbad", Code = "DHA" },
            new District { Name = "Deoghar", Code = "DEO" },
            new District { Name = "Chatra", Code = "CHA" }
        };
        db.Districts.AddRange(districts);
        await db.SaveChangesAsync(cancellationToken);
        db.Phcs.AddRange(districts.Select((district, index) => new Phc
        {
            DistrictId = district.Id,
            Name = $"{district.Name} Central PHC",
            Code = $"{district.Code}-01"
        }));
        db.Medicines.AddRange(
            new Medicine { Name = "Paracetamol 500mg", GenericName = "Paracetamol", Unit = "tablet" },
            new Medicine { Name = "ORS", GenericName = "Oral rehydration salts", Unit = "sachet" });
        db.Diseases.AddRange(
            new Disease { Name = "Fever", Code = "FEVER" },
            new Disease { Name = "Dengue", Code = "DENGUE" });
        await db.SaveChangesAsync(cancellationToken);
    }
}
