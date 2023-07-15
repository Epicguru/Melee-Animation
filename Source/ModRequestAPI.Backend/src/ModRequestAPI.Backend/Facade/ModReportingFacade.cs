using ModRequestAPI.Backend.DAL;
using ModRequestAPI.Models;

namespace ModRequestAPI.Backend.Facade;

public class ModReportingFacade
{
    private readonly ModReportingDAL dal;

    public ModReportingFacade(ModReportingDAL dal)
    {
        this.dal = dal;
    }

    /// <summary>
    /// Attempts to record a missing mod.
    /// Returns false if an error occurs.
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
        }

        return await dal.WriteModRequestAsync(requests!) ? null : "Internal error when writing to db.";
    }
}
