using System.Diagnostics;

namespace VALORANT_Overlay.Services;

public static class ValorantProcessService
{
    private const string ValorantProcessName = "VALORANT-Win64-Shipping";
    private const string ValorantProcessNameAlt = "VALORANT";

    public static bool IsValorantRunning()
    {
        return Process.GetProcessesByName(ValorantProcessName).Length > 0 || Process.GetProcessesByName(ValorantProcessNameAlt).Length > 0;
    }
}