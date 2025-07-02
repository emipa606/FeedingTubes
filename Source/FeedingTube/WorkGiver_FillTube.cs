using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace FeedingTube;

internal class WorkGiver_FillTube : WorkGiver_Scanner
{
    public override bool ShouldSkip(Pawn pawn, bool forced = false)
    {
        return ShouldSkipStatic(pawn);
    }

    public override Danger MaxPathDanger(Pawn pawn)
    {
        return Danger.Deadly;
    }

    public static bool ShouldSkipStatic(Pawn pawn)
    {
        if (pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hauling))
        {
            return true;
        }

        if (pawn.WorkTagIsDisabled(WorkTags.ManualDumb))
        {
            return true;
        }

        if (pawn.WorkTagIsDisabled(WorkTags.Hauling))
        {
            return true;
        }

        return pawn.Faction != Faction.OfPlayer && !pawn.RaceProps.Humanlike;
    }

    public static Job GenerateFillJob(Pawn pawn, Thing t)
    {
        if (ShouldSkipStatic(pawn))
        {
            return null;
        }

        if (t is not FeedingTube tube)
        {
            return null;
        }

        if (!pawn.CanReserve(tube))
        {
            return null;
        }

        var food = GenClosest.ClosestThingReachable(pawn.Position, pawn.Map,
            ThingRequest.ForGroup(ThingRequestGroup.FoodSourceNotPlantOrTree), PathEndMode.ClosestTouch,
            TraverseParms.For(pawn), 9999f, Validator);
        if (food is null)
        {
            return null;
        }

        var job = new Job(FillTube_JobDefOf.FillTube, t, food);
        return job;

        bool Validator(Thing x)
        {
            return !x.IsForbidden(pawn) && pawn.CanReserve(x) && tube.Storeable(x) &&
                   tube.foodCount() < FeedingTube.maxFoodStored;
        }
    }

    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
        return pawn.Map.listerThings.AllThings.Where(t =>
            t is FeedingTube tube && tube.foodCount() < FeedingTube.maxFoodStored);
    }

    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        return GenerateFillJob(pawn, t);
    }
}