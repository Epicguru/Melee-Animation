using System.Globalization;
using ModRequestAPI.Backend.DAL;
using ModRequestAPI.Models;

namespace ModRequestAPI.Backend.Facade;

public class ModReportingFacade
{
    // CHANGE THIS DATE TIME WHEN UPDATING!
    private static readonly DateTime currentBuildDate = DateTime.ParseExact("20240426130022", "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None);

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

            if (req.MeleeAnimationBuildTimeUtc == null)
                return "Missing mod build time, probably very old mod version submitted the request.";

            if (req.MeleeAnimationBuildTimeUtc.Value < currentBuildDate)
                return $"Outdated Melee Animation mod submitted the request, {req.MeleeAnimationBuildTimeUtc.Value} vs current {currentBuildDate}.\nPlease update the mod to the latest version.";
        }

        return await dal.WriteModRequestAsync(requests!) ? null : "Internal error when writing to db.";
    }
}
