using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace FeedingTube;

internal class Job_FillTube : JobDriver
{
    private const TargetIndex TubeToFill = TargetIndex.A;
    private const TargetIndex FoodToLoad = TargetIndex.B;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return pawn.Reserve(job.GetTarget(FoodToLoad), job, 1, job.GetTarget(FoodToLoad).Thing.stackCount) &&
               pawn.Reserve(job.GetTarget(TubeToFill), job);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TubeToFill);
        this.FailOnForbidden(FoodToLoad);

        yield return Toils_Goto.GotoThing(FoodToLoad, PathEndMode.ClosestTouch);
        var foodNeeded = FeedingTube.maxFoodStored - ((FeedingTube)job.GetTarget(TubeToFill).Thing).foodCount();
        job.count = job.GetTarget(FoodToLoad).Thing.stackCount > foodNeeded
            ? foodNeeded
            : job.GetTarget(FoodToLoad).Thing.stackCount;

        yield return Toils_Haul.StartCarryThing(FoodToLoad, false, true);
        yield return Toils_Goto.GotoThing(TubeToFill, PathEndMode.Touch);
        Toil curToil = null;
        curToil = Toils_General.Wait(240).WithProgressBarToilDelay(TargetIndex.A).FailOn(delegate()
        {
            var actor = curToil?.actor;
            var curJob = actor?.jobs.curJob;
            if (curJob?.GetTarget(TubeToFill).Thing is not FeedingTube dest)
            {
                return true;
            }

            var food = curJob.GetTarget(FoodToLoad).Thing;
            if (!dest.Storeable(food))
            {
                return true;
            }

            return dest.foodCount() >= FeedingTube.maxFoodStored;
        });
        yield return curToil.FailOnSomeonePhysicallyInteracting(TubeToFill)
            .FailOnDespawnedNullOrForbidden(TubeToFill);
        var toil = curToil;
        curToil = new Toil
        {
            initAction = () =>
            {
                var actor = toil.actor;
                var curJob = actor.jobs.curJob;
                if (curJob.GetTarget(TubeToFill).Thing is not FeedingTube dest)
                {
                    return;
                }

                var max = FeedingTube.maxFoodStored - dest.foodCount();
                var food = curJob.GetTarget(FoodToLoad).Thing;
                if (max > food.stackCount)
                {
                    dest.LoadFood(food);
                }
                else
                {
                    Log.Message(
                        $"Having to split off: Max{FeedingTube.maxFoodStored} Cur{dest.foodCount()} Math{max}");
                    dest.LoadFood(food.SplitOff(max));
                }
            }
        };
        yield return curToil.FailOnSomeonePhysicallyInteracting(TubeToFill)
            .FailOnDespawnedNullOrForbidden(TubeToFill);
    }
}