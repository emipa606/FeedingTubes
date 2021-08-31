using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace FeedingTube
{
    public class FeedingTube : Building, ISlotGroupParent
    {
        public static int maxFoodStored = 75;
        private List<IntVec3> cachedOccupiedCells;
        public List<Thing> foodStored = new List<Thing>();
        public StorageSettings settings;
        public SlotGroup slotGroup;

        public FeedingTube()
        {
            slotGroup = new SlotGroup(this);
        }

        public bool StorageTabVisible => true;

        public bool IgnoreStoredThingsBeauty => def.building.ignoreStoredThingsBeauty;

        public SlotGroup GetSlotGroup()
        {
            return slotGroup;
        }

        public virtual void Notify_ReceivedThing(Thing newItem)
        {
            if (Faction == Faction.OfPlayer && newItem.def.storedConceptLearnOpportunity != null)
            {
                LessonAutoActivator.TeachOpportunity(newItem.def.storedConceptLearnOpportunity,
                    OpportunityType.GoodToKnow);
            }
        }

        public virtual void Notify_LostThing(Thing newItem)
        {
        }

        public virtual IEnumerable<IntVec3> AllSlotCells()
        {
            foreach (var intVec in GenAdj.CellsOccupiedBy(this))
            {
                yield return intVec;
            }
        }

        public List<IntVec3> AllSlotCellsList()
        {
            if (cachedOccupiedCells == null)
            {
                cachedOccupiedCells = AllSlotCells().ToList();
            }

            return cachedOccupiedCells;
        }

        public StorageSettings GetStoreSettings()
        {
            return settings;
        }

        public StorageSettings GetParentStoreSettings()
        {
            return def.building.fixedStorageSettings;
        }

        public string SlotYielderLabel()
        {
            return LabelCap;
        }

        public bool Accepts(Thing t)
        {
            return false;
        }

        public bool Storeable(Thing t)
        {
            return settings.AllowedToAccept(t);
        }

        public override void PostMake()
        {
            base.PostMake();
            settings = new StorageSettings(this);
            foodStored = new List<Thing>();
            if (def.building.defaultStorageSettings != null)
            {
                settings.CopyFrom(def.building.defaultStorageSettings);
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            cachedOccupiedCells = null;
            base.SpawnSetup(map, respawningAfterLoad);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref settings, "settings", this);
            Scribe_Collections.Look(ref foodStored, "foodStored", LookMode.Deep);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            foreach (var gizmo2 in StorageSettingsClipboard.CopyPasteGizmosFor(settings))
            {
                yield return gizmo2;
            }

            yield return new Command_Action
            {
                action = Empty,
                hotKey = KeyBindingDefOf.Misc1,
                defaultDesc = "Empty the tube",
                icon = ContentFinder<Texture2D>.Get("UI/Designators/Open"),
                defaultLabel = "Empty"
            };
        }

        public int foodCount()
        {
            if (foodStored == null)
            {
                foodStored = new List<Thing>();
            }

            return foodStored.Sum(t => t.stackCount);
        }

        public void LoadFood(Thing food)
        {
            if (foodStored == null)
            {
                foodStored = new List<Thing>();
            }

            Log.Message($"Received {food.stackCount} food.");
            foreach (var stackable in foodStored.Where(t => t.CanStackWith(food)))
            {
                stackable.TryAbsorbStack(food, true);
                if (food.stackCount <= 0)
                {
                    return;
                }
            }

            Log.Message($"Loading in {food.stackCount} remainer.");
            if (food.stackCount > 0)
            {
                foodStored.Add(food.SplitOff(food.stackCount));
            }
        }

        public override string GetInspectString()
        {
            if (foodStored == null)
            {
                foodStored = new List<Thing>();
            }

            var builder = new StringBuilder();
            builder.Append(base.GetInspectString());
            if (builder.Length > 0)
            {
                builder.Append("\n");
            }

            builder.Append("stored:");
            foreach (var food in foodStored)
            {
                builder.Append($"\n{food.Label}");
            }

            return builder.ToString();
        }

        public void Empty()
        {
            foreach (var food in foodStored)
            {
                GenPlace.TryPlaceThing(food, Position, Map, ThingPlaceMode.Near);
            }

            foodStored.Clear();
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            Empty();
            base.Destroy(mode);
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (var floatMenuOption in base.GetFloatMenuOptions(selPawn))
            {
                yield return floatMenuOption;
            }

            if (foodCount() >= maxFoodStored)
            {
                yield return new FloatMenuOption("Fill (full)", null);
            }
            else if (WorkGiver_FillTube.shouldSkipStatic(selPawn))
            {
                yield return new FloatMenuOption("Fill (unwilling)", null);
            }
            else
            {
                yield return new FloatMenuOption("Fill", () =>
                {
                    var doFill = WorkGiver_FillTube.generateFillJob(selPawn, this);
                    if (doFill != null)
                    {
                        selPawn.jobs.TryTakeOrderedJob(doFill);
                    }
                });
            }
        }

        public override void Tick()
        {
            base.Tick();

            var bed = (Position + Rotation.FacingCell).GetFirstThing<Building_Bed>(Map);
            if (bed == null || !bed.CurOccupants.Any() || !bed.def.building.bed_defaultMedical)
            {
                return;
            }

            foreach (var victim in bed.CurOccupants)
            {
                if (foodCount() == 0)
                {
                    return;
                }

                var bestFood = foodStored.First();
                if (!FeedPatientUtility.IsHungry(victim))
                {
                    continue;
                }

                var needs = victim.needs.food.NutritionWanted;
                victim.needs.food.CurLevel += bestFood.Ingested(victim, needs);
                if (bestFood.Destroyed)
                {
                    foodStored.Remove(bestFood);
                }
            }
        }
    }
}