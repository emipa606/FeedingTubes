using RimWorld;
using Verse;

namespace FeedingTube;

[StaticConstructorOnStartup]
public static class Main
{
    public static readonly NeedDef ThirstDef;

    static Main()
    {
        ThirstDef = DefDatabase<NeedDef>.GetNamedSilentFail("DBHThirst");
        if (ThirstDef != null)
        {
            Log.Message("[FeedingTube]: Dubs Bad Hygiene is loaded, tube will fill thirst need when feeding");
        }
    }
}