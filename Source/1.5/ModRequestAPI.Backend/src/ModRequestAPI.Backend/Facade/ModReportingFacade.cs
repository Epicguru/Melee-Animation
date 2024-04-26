using System.Globalization;
using ModRequestAPI.Backend.DAL;
using ModRequestAPI.Models;

namespace ModRequestAPI.Backend.Facade;

public class ModReportingFacade
{
    // CHANGE THIS DATE TIME WHEN UPDATING!
    private static readonly DateTime currentBuildDate = DateTime.ParseExact("20240426161658", "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None);

    private readonly ModReportingDAL dal;

    public ModReportingFacade(ModReportingDAL dal)
    {
        this.dal = dal;
    }

    /// <summary>
    /// Attempts to record a missing mod.
    /// Returns the error message if an error occurs.
    /// </summary>
    public async Task<string?> ReportMissingModAsync(IEnumerable<MissingModRequest?> requests)
    {
        foreach (var req in requests)
        {
            if (req == null)
                return "Null request element";

            if (string.IsNullOrWhiteSpace(req.ModID) || string.IsNullOrWhiteSpace(req.ModName))
                return "Missing mod id or name.";

            if (req.WeaponCount <= 0)
                return "Bad weapon count";

            if (req.ModID.Length > 64)
                return "ID too long";

            if (req.ModName.Length > 64)
                return "Mod name too long";

            if (string.IsNullOrEmpty(req.MeleeAnimationBuildTimeUtc))
                return "Missing mod build time, probably very old version of Melee Animation submitting the request.";

            if (!DateTime.TryParseExact(req.MeleeAnimationBuildTimeUtc, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
                return "Invalid mod build time.";

            if (dateTime < currentBuildDate)
            {
                TimeSpan delta = currentBuildDate - dateTime;
                TimeSpan deltaToNow = DateTime.UtcNow - currentBuildDate;
                return "Your version of Melee Animation is not up to date, mod support request is rejected.\nPlease update to the latest version of the mod.\n" + 
                    $"Last Melee Animation mod update: {deltaToNow.TotalDays} days ago. Your mod version is {delta.TotalDays} days out of date.";
            }
        }

        return await dal.WriteModRequestAsync(requests!) ? null : "Internal error when writing to db.";
    }
}
