using Slotgun.EditorTools;

public static class RunDress
{
    public static string Execute()
    {
        SectorScenery.DressAllSectors();
        return "[Dress] scenery rebuilt with an 8 m spawn clearance";
    }
}
