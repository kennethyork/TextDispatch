using System;
using System.Collections.Generic;
using LSPD_First_Response;
using LSPD_First_Response.Mod.API;
using Rage;

namespace TextCallouts.Custom
{
    /// <summary>
    /// A callout built from a file rather than from C#.
    ///
    /// The eight callouts that ship with the pack are written out longhand because their behaviour is
    /// particular. This one covers the shape almost every callout actually has - go somewhere, find
    /// people, they react when you arrive, it ends when you have dealt with them - and takes every
    /// part of that from an XML recipe, so a new callout is a text file.
    ///
    /// Which recipe it is belongs to is answered by its own type name: the loader emits one subclass
    /// per file and binds it here. That is why the lookup is by name rather than a field.
    /// </summary>
    public abstract class RecipeCallout : TextCallout
    {
        private static readonly Dictionary<string, CalloutRecipe> Bindings =
            new Dictionary<string, CalloutRecipe>(StringComparer.OrdinalIgnoreCase);

        internal static void Bind(string typeName, CalloutRecipe recipe)
        {
            if (string.IsNullOrEmpty(typeName) || recipe == null) return;
            Bindings[typeName] = recipe;
        }

        private CalloutRecipe _recipe;
        private readonly List<Ped> _suspects = new List<Ped>();
        private readonly List<Ped> _bystanders = new List<Ped>();
        private bool _approached;
        private bool _supportCalled;

        private CalloutRecipe Recipe
        {
            get
            {
                if (_recipe != null) return _recipe;

                CalloutRecipe found;
                if (Bindings.TryGetValue(GetType().Name, out found)) _recipe = found;
                return _recipe;
            }
        }

        protected override int TimeoutMs
        {
            get { return (Recipe == null ? 20 : Recipe.TimeoutMinutes) * 60 * 1000; }
        }

        protected override string Briefing { get { return Recipe == null ? null : Recipe.Briefing; } }

        protected override string SignOff
        {
            get
            {
                if (Recipe == null) return null;
                return string.IsNullOrEmpty(Recipe.SignOff) ? Recipe.ResolvedLine : Recipe.SignOff;
            }
        }

        public override bool OnBeforeCalloutDisplayed()
        {
            var recipe = Recipe;
            if (recipe == null)
            {
                // Nothing to offer. Declining is better than offering a callout that cannot happen.
                Log.Line("a custom callout has no recipe bound to " + GetType().Name + " - not offering it");
                return false;
            }

            var player = Game.LocalPlayer.Character;
            Offer(recipe.Name, recipe.Message, recipe.Advisory,
                  StreetNear(player.Position, recipe.DistanceMin, recipe.DistanceMax),
                  recipe.BlipRadius);
            return base.OnBeforeCalloutDisplayed();
        }

        protected override void Build()
        {
            var recipe = Recipe;
            var position = CalloutPosition;
            var heading = Rng.Next(360);

            foreach (var actor in recipe.Actors)
            {
                Vehicle vehicle = null;
                if (!string.IsNullOrEmpty(actor.Vehicle)) vehicle = SpawnVehicle(actor.Vehicle, position, heading);

                for (var i = 0; i < actor.Count; i++)
                {
                    // Spread a group out a little, so three suspects are not one suspect standing in
                    // a puddle of its own geometry.
                    var offset = i == 0 ? new Vector3(0f, 0f, 0f) : new Vector3(1.6f * i, 1.1f, 0f);
                    var ped = SpawnPed(actor.Model, position + offset, heading);

                    if (vehicle != null) PutInVehicle(ped, vehicle, i == 0 ? -1 : i);

                    Arm(ped, actor);

                    if (actor.IsSuspect)
                    {
                        _suspects.Add(ped);
                        if (string.IsNullOrEmpty(actor.Vehicle)) MarkStoppable(ped);
                    }
                    else
                    {
                        _bystanders.Add(ped);
                    }

                    if (actor.Cower) Try(ped, p => p.Tasks.Cower(-1), "Cower");
                }
            }

            Say("Dispatch shows " + _suspects.Count + " suspect(s) and " + _bystanders.Count +
                " other(s) at " + Where(position) + ".");
        }

        protected override bool Tick()
        {
            var recipe = Recipe;
            if (recipe == null) { Close("the recipe went missing"); return false; }

            if (!_approached &&
                Game.LocalPlayer.Character.Position.DistanceTo(CalloutPosition) < recipe.ApproachDistance)
            {
                _approached = true;

                if (!string.IsNullOrEmpty(recipe.ApproachLine)) Say(recipe.ApproachLine);

                if (!_supportCalled)
                {
                    _supportCalled = true;
                    if (recipe.RequestAmbulance) RequestAmbulance(CalloutPosition);
                    if (recipe.RequestBackup)
                    {
                        try
                        {
                            Functions.RequestBackup(CalloutPosition, EBackupResponseType.Code3, EBackupUnitType.LocalUnit);
                            Log.Line("backup requested for " + FriendlyName);
                        }
                        catch (Exception ex) { Log.Error("requesting backup", ex); }
                    }
                }

                foreach (var ped in _suspects)
                {
                    if (ped == null || !ped.Exists()) continue;

                    if (recipe.ApproachHostile) MakeHostile(ped, null, 0);
                    if (recipe.ApproachHandsUp) HandsUp(ped);
                    if (recipe.ApproachFleeInVehicle) FleeInVehicle(ped);
                    else if (recipe.ApproachFlee) FleeOnFoot(ped);
                }

                if (recipe.ApproachCower)
                    foreach (var ped in _bystanders) Try(ped, p => p.Tasks.Cower(-1), "Cower");
            }

            switch (recipe.Resolution)
            {
                case Resolution.Manual:
                    // Nothing to catch: it closes on the timer, or when the player leaves it alone.
                    return true;

                case Resolution.AnyArrest:
                    foreach (var ped in _suspects)
                    {
                        if (!Dealt(ped)) continue;
                        Close("the suspect is dealt with");
                        return false;
                    }
                    return true;

                default:
                    foreach (var ped in _suspects) if (!Dealt(ped)) return true;
                    Close("every suspect is dealt with");
                    return false;
            }
        }

        private static void Arm(Ped ped, ActorRecipe actor)
        {
            if (ped == null || !ped.Exists()) return;

            try
            {
                ped.Accuracy = actor.Accuracy;
                ped.Armor = actor.Armor;
                ped.CanAttackFriendlies = true;
                if (actor.Armed)
                {
                    ped.Inventory.GiveNewWeapon(actor.Weapon, (short)actor.Ammo, true);
                }
            }
            catch (Exception ex) { Log.Error("arming a custom ped", ex); }

            if (actor.Hostile)
            {
                MakeHostile(ped, null, 0);
                Try(ped, p => p.Tasks.FightAgainst(Game.LocalPlayer.Character), "FightAgainst");
            }
        }

        private static void Try(Ped ped, Action<Ped> action, string what)
        {
            if (ped == null || !ped.Exists()) return;
            try { action(ped); }
            catch (Exception ex) { Log.Error(what + " for a custom callout", ex); }
        }
    }
}
