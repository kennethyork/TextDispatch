using System;
using System.Collections.Generic;
using LSPD_First_Response;
using LSPD_First_Response.Mod.API;
using Rage;
using TextCallouts.Callouts;

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

        // Who the scene's people turned out to be, for the log. The same callout twice running is two
        // different men, and the log is the only place that can be seen from outside the game.
        private readonly List<string> _people = new List<string>();
        private readonly List<string> _vehicles = new List<string>();
        private readonly List<Ped> _bystanders = new List<Ped>();
        private bool _approached;
        private bool _supportCalled;
        private bool _declined;

        // the medical path
        private Ped _patient;
        private bool _treating;
        private bool _treated;
        private int _workedMs;
        private int _nextHelp;
        private int _handoverAt;
        private int _lastTick;

        // the fire recipes
        private bool _fireLit;
        private bool _fireCleared;
        private Vector3 _fireAt;

        // the stages: what the scene does after the player has been there a while
        private int _arrivedAt;
        private readonly List<StageRecipe> _played = new List<StageRecipe>();

        // the "nothing to catch" recipes: be at the scene, then be done with it
        private bool _beenThere;
        private int _onSceneAt;

        /// <summary>How long the player has to be at a scene with nothing to catch before it is
        /// plainly dealt with. Shorter and a report is not taken; longer and they are stood about.</summary>
        private const int OnSceneSeconds = 75;

        /// <summary>How far away counts as having left the scene.</summary>
        private const float LeftTheScene = 150f;

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

            // Which duty this belongs to. A recipe says `police`, `medical`, `fire` or `any`, and the
            // rule lives in Callouts.Agency so that it is the same rule the hand-written callouts use.
            if (!Agency.AllowsFor(recipe.For))
            {
                if (!_declined)
                {
                    _declined = true;
                    Log.Line("recipe " + recipe.Id + " is " + recipe.For + " work; this patrol is '" +
                             (Agency.Current() ?? "an agency nobody recognises") + "' - not offering it");
                }
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
                var arrivingIn = PickVehicle(actor);
                if (arrivingIn != null)
                {
                    vehicle = SpawnVehicle(arrivingIn, position, heading);
                    if (actor.Vehicles.Length > 1) _vehicles.Add(arrivingIn);
                }

                for (var i = 0; i < actor.Count; i++)
                {
                    // Spread a group out a little, so three suspects are not one suspect standing in
                    // a puddle of its own geometry.
                    var offset = i == 0 ? new Vector3(0f, 0f, 0f) : new Vector3(1.6f * i, 1.1f, 0f);
                    var model = PickModel(actor);
                    var ped = SpawnPed(model, position + offset, heading);
                    if (actor.Models.Length > 1) _people.Add(model.Name);

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

            // The patient, for the medical recipes: on the ground and alive, with nothing to arrest.
            if (recipe.Patient != null)
            {
                _patient = SpawnPed(recipe.Patient.Model, position, heading);
                _patient.Health = 70;
                try { _patient.BlockPermanentEvents = true; } catch { }
                try { Rage.Native.NativeFunction.Natives.SetPedToRagdoll(_patient, -1, -1, 0, false, false, false); }
                catch (Exception ex) { Log.Error("putting a recipe's patient on the ground", ex); }
            }

            // What they will say when they are spoken to: the first suspect, or the patient, or whoever is
            // there. One speaker per scene, because a scene with two conversations in it is two callouts.
            if (recipe.Script.Count > 0)
            {
                var speaker = _suspects.Count > 0 ? _suspects[0] : (_patient ?? (_bystanders.Count > 0 ? _bystanders[0] : null));
                if (speaker != null)
                {
                    Scripts.CalloutScript.Register(speaker, recipe.Name, recipe.Script.ToArray());
                    Log.Line("script for " + recipe.Id + ": " + recipe.Script.Count + " line(s), to be spoken by the " +
                             (recipe.Patient != null ? "patient" : "suspect") + " when the player talks to them");
                }
            }

            // The count goes to the log, not the box: it is bookkeeping, and a recipe that says it
            // three times over a callout is how a chat box stops being readable.
            Log.Line("custom callout scene: " + _suspects.Count + " suspect(s), " + _bystanders.Count +
                     " other(s)" + (recipe.Patient != null ? ", one patient" : "") +
                     (recipe.Stages.Count > 0 ? ", " + recipe.Stages.Count + " stage(s)" : "") +
                     (_people.Count > 0 ? ", drawn from the pool: " + string.Join(", ", _people.ToArray()) : "") +
                     (_vehicles.Count > 0 ? ", driving: " + string.Join(", ", _vehicles.ToArray()) : "") +
                     " at " + Where(position));
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

                if (recipe.ApproachFire) LightFire(CalloutPosition);
            }

            // What happens after the player has been there a while: the stages. This is what turns a scene
            // into a sequence - they hold the door for twenty seconds and then come out, the second car
            // arrives a minute in, the fire starts once they have had a chance to talk.
            if (recipe.Stages.Count > 0 && !RunStages(recipe)) return false;

            // A recipe with a patient has nobody to catch: it ends when the player has spent long enough
            // on them. That is the whole of the medical path, and it is in one method because a recipe's
            // patient is data rather than code.
            if (recipe.Patient != null) return PatientTick(recipe.Patient);

            switch (recipe.Resolution)
            {
                case Resolution.Manual:
                    return NothingToCatch(recipe);

                case Resolution.AnyArrest:
                    foreach (var ped in _suspects)
                    {
                        if (!Dealt(ped)) continue;
                        ClearFire();
                        Close("the suspect is dealt with");
                        return false;
                    }
                    return true;

                default:
                    foreach (var ped in _suspects) if (!Dealt(ped)) return true;
                    ClearFire();
                    Close("every suspect is dealt with");
                    return false;
            }
        }

        /// <summary>
        /// What this actor arrives in, on the same terms as who they are: a pool drawn from at random,
        /// each candidate checked against the game, and a model that certainly exists as the fallback.
        /// A vehicle that will not spawn is worse than a plain one - three suspects standing about
        /// where a car chase was meant to be.
        /// </summary>
        private static string PickVehicle(ActorRecipe actor)
        {
            var pool = actor.Vehicles;

            if (pool == null || pool.Length == 0)
            {
                if (string.IsNullOrEmpty(actor.Vehicle)) return null;
                try { return new Model(actor.Vehicle).IsValid ? actor.Vehicle : null; }
                catch (Exception ex) { Log.Error("checking the vehicle " + actor.Vehicle, ex); return null; }
            }

            var start = Rng.Next(pool.Length);
            for (var i = 0; i < pool.Length; i++)
            {
                var name = pool[(start + i) % pool.Length];
                try
                {
                    if (new Model(name).IsValid) return name;
                    Log.Line("the vehicle " + name + " is not in this game - the next one in the pool is used");
                }
                catch (Exception ex) { Log.Error("checking the vehicle " + name, ex); }
            }

            Log.Line("none of the " + pool.Length + " vehicles in this recipe's pool exist here - the actor arrives on foot");
            return null;
        }

        /// <summary>
        /// Who this actor is this time.
        ///
        /// A pool is drawn from at random, starting at a random place in it so the same few names are
        /// not always tried first, and the first candidate that exists in the game wins. That check is
        /// the point: a pool with a typo in it, or a model a player's game does not have, must not cost
        /// the callout its suspect - a scene with nobody in it closes the moment it starts, and that is
        /// a far worse thing than a familiar face. If nothing in the pool exists, the actor falls back
        /// to the model the recipe named - and a woman is not a fallback, so the pool carries both and
        /// the recipe's own Model is a man only because a_m_y_business_01 is the one model certain to
        /// exist in every install.
        /// </summary>
        private static Model PickModel(ActorRecipe actor)
        {
            var pool = actor.Models;
            if (pool == null || pool.Length == 0) return new Model(actor.Model);

            var start = Rng.Next(pool.Length);
            for (var i = 0; i < pool.Length; i++)
            {
                var name = pool[(start + i) % pool.Length];
                try
                {
                    var candidate = new Model(name);
                    if (candidate.IsValid) return candidate;
                    Log.Line("the model " + name + " is not in this game - the next one in the pool is used");
                }
                catch (Exception ex) { Log.Error("checking the model " + name, ex); }
            }

            Log.Line("none of the " + pool.Length + " models in this recipe's pool exist here - falling back to " + actor.Model);
            return new Model(actor.Model);
        }

        /// <summary>
        /// A recipe with nothing to catch: a report, a check on somebody, a scene that is over when it
        /// is over. It is finished by the player being there and then being done - the shape the
        /// hand-written ones have, where you deal with the scene and it closes behind you.
        ///
        /// It used to end only by timing out, which meant the hundred and fourteen recipes like this
        /// could not be finished by anything the player did: you arrived, dispatch said its line, and
        /// the call stayed open for twenty minutes. The timeout is still there, now only as the
        /// backstop for a player who never arrives at all.
        /// </summary>
        private bool NothingToCatch(CalloutRecipe recipe)
        {
            var player = Game.LocalPlayer.Character;
            if (player == null) return true;

            var distance = player.Position.DistanceTo(CalloutPosition);

            if (!_beenThere)
            {
                // Still on the way. Nothing is timed until they are actually at the scene.
                if (distance > recipe.ApproachDistance) return true;
                _beenThere = true;
                _onSceneAt = Environment.TickCount;
                return true;
            }

            var onScene = (Environment.TickCount - _onSceneAt) / 1000;

            // Left after being there: the report is taken, the door has been knocked on, the scene has
            // been looked at. That is the whole of a call with nothing to catch.
            if (distance > LeftTheScene && onScene >= 10)
            {
                ClearFire();
                Close("you have been to the scene and moved on");
                return false;
            }

            // Or they have stayed, in which case it is equally done - being made to drive off to end a
            // call would be a strange thing to ask of a player standing in the middle of it.
            if (onScene >= OnSceneSeconds)
            {
                ClearFire();
                Close("the scene is dealt with");
                return false;
            }

            if (Environment.TickCount >= _nextHelp)
            {
                _nextHelp = Environment.TickCount + 2000;
                try { Hud.Help("Nothing to arrest here. Deal with it and move on - /endcall ends it now."); }
                catch (Exception ex) { Log.Error("the help line for a call with nothing to catch", ex); }
            }

            return true;
        }

        /// <summary>
        /// Play any stage whose time has come. False means a stage ended the callout.
        /// </summary>
        private bool RunStages(CalloutRecipe recipe)
        {
            // Nothing is timed before the player is at the scene, or a stage would play to an empty
            // street while they were still driving to it.
            if (!_approached) return true;

            if (_arrivedAt == 0)
            {
                _arrivedAt = Environment.TickCount;
                Log.Line("stages for " + recipe.Id + " start now: " + recipe.Stages.Count + " of them");
            }

            var elapsed = (Environment.TickCount - _arrivedAt) / 1000;

            foreach (var stage in recipe.Stages)
            {
                if (_played.Contains(stage) || elapsed < stage.At) continue;
                _played.Add(stage);
                if (!Play(stage, recipe)) return false;
            }

            return true;
        }

        /// <summary>One stage. False means it ended the callout.</summary>
        private bool Play(StageRecipe stage, CalloutRecipe recipe)
        {
            Log.Line("stage " + stage.At + "s in " + recipe.Id + ": " + stage.Do +
                     (string.IsNullOrEmpty(stage.Text) ? "" : "  -  " + stage.Text));

            if (!string.IsNullOrEmpty(stage.Text)) Say(stage.Text);

            switch (stage.Do)
            {
                case "flee":
                    foreach (var ped in _suspects) if (ped != null && ped.Exists()) FleeOnFoot(ped);
                    break;

                case "fleeinvehicle":
                    foreach (var ped in _suspects) if (ped != null && ped.Exists()) FleeInVehicle(ped);
                    break;

                case "hostile":
                    foreach (var ped in _suspects) if (ped != null && ped.Exists()) MakeHostile(ped, null, 0);
                    break;

                case "handsup":
                    foreach (var ped in _suspects) if (ped != null && ped.Exists()) HandsUp(ped);
                    break;

                case "cower":
                    foreach (var ped in _bystanders) Try(ped, p => p.Tasks.Cower(-1), "Cower");
                    break;

                case "backup":
                    try
                    {
                        Functions.RequestBackup(CalloutPosition, EBackupResponseType.Code3, EBackupUnitType.LocalUnit);
                        Log.Line("backup requested by a stage in " + recipe.Id);
                    }
                    catch (Exception ex) { Log.Error("a stage requesting backup", ex); }
                    break;

                case "ambulance":
                    RequestAmbulance(CalloutPosition);
                    break;

                case "fire":
                    LightFire(CalloutPosition);
                    break;

                case "end":
                    ClearFire();
                    Close("the scene reached its end");
                    return false;
            }

            return true;
        }

        /// <summary>
        /// The medical path: somebody on the ground, a count of seconds next to them, and an ambulance
        /// at the end of it. Deliberately the same shape as the hand-written medical callouts.
        /// </summary>
        private bool PatientTick(PatientRecipe want)
        {
            var now = Environment.TickCount;
            var elapsed = _lastTick == 0 ? 0 : now - _lastTick;
            _lastTick = now;

            if (_patient == null || !_patient.Exists()) return true;

            if (!_patient.IsAlive)
            {
                ClearFire();
                Close("the patient did not make it");
                return false;
            }

            if (!_approached) return true;

            if (_treated)
            {
                ClearFire();
                if (_handoverAt != 0 && now - _handoverAt > 25000)
                {
                    Close("handed over to the ambulance crew");
                    return false;
                }
                return true;
            }

            var player = Game.LocalPlayer.Character;
            var close = false;
            try
            {
                close = player.Position.DistanceTo(_patient.Position) <= want.Range &&
                        !player.IsInAnyVehicle(false);
            }
            catch { }

            if (close)
            {
                if (!_treating)
                {
                    _treating = true;
                    _workedMs = 0;
                    _nextHelp = 0;
                    Say("You are with the patient. Stay with them.");
                }

                _workedMs += elapsed;

                if (now >= _nextHelp)
                {
                    _nextHelp = now + 1000;
                    var left = Math.Max(0, want.TreatmentSeconds - _workedMs / 1000);
                    try { Hud.Help(want.Working + "  " + left + "s"); } catch { }
                }

                if (_workedMs >= want.TreatmentSeconds * 1000)
                {
                    _treated = true;
                    _treating = false;

                    if (!string.IsNullOrEmpty(want.Line)) Say(want.Line);
                    Say("The patient is stable. Get the ambulance to us.");

                    try
                    {
                        Functions.RequestBackup(CalloutPosition, EBackupResponseType.Code3, EBackupUnitType.Ambulance);
                        Log.Line("ambulance requested for " + FriendlyName);
                    }
                    catch (Exception ex) { Log.Error("requesting an ambulance", ex); }

                    _handoverAt = now;
                }
            }
            else if (_treating)
            {
                _treating = false;
                _workedMs = 0;
                Say("You have stepped away - the patient is still waiting.");
            }

            return true;
        }

        /// <summary>A native fire, because RPH has no managed one. Cleared by ClearFire, and never
        /// left to burn: a script fire is not an entity, so nothing else would tidy it away.</summary>
        private void LightFire(Vector3 position)
        {
            if (_fireLit) return;

            try
            {
                Rage.Native.NativeFunction.Natives.StartScriptFire(position.X, position.Y, position.Z, 1, true);
                _fireLit = true;
                _fireAt = position;
                Log.Line("a fire was lit at " + Where(position));
            }
            catch (Exception ex) { Log.Error("lighting a fire", ex); }
        }

        private void ClearFire()
        {
            if (!_fireLit || _fireCleared) return;
            _fireCleared = true;

            try
            {
                Rage.Native.NativeFunction.Natives.StopFireInRange(_fireAt.X, _fireAt.Y, _fireAt.Z, 25f);
                Log.Line("the fire was put out at " + Where(_fireAt));
            }
            catch (Exception ex) { Log.Error("putting a fire out", ex); }
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
