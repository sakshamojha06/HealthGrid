namespace HealthGrid.Api.Auth;

/// <summary>
/// Role names. These match the strings the Angular client expects in the
/// <c>role</c> claim (see <c>UI/src/app/core/models/models.ts</c>).
/// </summary>
public static class Roles
{
    public const string SystemAdministrator = "SystemAdministrator";
    public const string DistrictAdministrator = "DistrictAdministrator";
    public const string PhcAdministrator = "PhcAdministrator";
    public const string Doctor = "Doctor";
    public const string PhcStaff = "PhcStaff";

    public static readonly string[] All =
        [SystemAdministrator, DistrictAdministrator, PhcAdministrator, Doctor, PhcStaff];

    /// <summary>Roles allowed to manage reference / organisational data.</summary>
    public const string Administrators =
        $"{SystemAdministrator},{DistrictAdministrator},{PhcAdministrator}";
}

public static class ClaimNames
{
    public const string DistrictId = "district_id";
    public const string PhcId = "phc_id";
    public const string TokenVersion = "token_version";
}
