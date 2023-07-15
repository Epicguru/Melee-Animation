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
    public async Task<bool> ReportMissingModAsync(IEnumerable<MissingModRequest?> requests)
    {
        foreach (var req in requests)
        {
            if (req == null)
                return false;

            if (string.IsNullOrWhiteSpace(req.ModID) || string.IsNullOrWhiteSpace(req.ModName))
                return false;

            if (req.WeaponCount <= 0)
                return false;

            if (req.ModID.Length > 64)
                return false;

            if (req.ModName.Length > 64)
                return false;
        }

        return await dal.WriteModRequestAsync(requests!);
    }
}
