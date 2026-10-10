using System;
using System.Collections.Generic;
using LSPD_First_Response;
using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using Rage;

namespace TextCallouts
{
    /// <summary>
    /// The plumbing every callout in this pack shares: where to put it, what to draw, how to spawn
    /// people without leaking them, how to make somebody flee or give up, and how to notice that the
    /// player has actually dealt with it.
    ///
    /// All of it is LSPDFR's own API or RAGE Plugin Hook's, which is what lets this pack run on a
    /// bare LSPDFR install. Where a behaviour does not exist in either - putting somebody's hands up -
    /// a GTA native is used, wrapped and reported, because RPH resolves natives by name at runtime and
    /// a wrong name must cost one line of speech, never a crash.
    /// </summary>
    public abstract class TextCallout : Callout
    {
        protected static readonly Random Rng = new Random();

        private readonly List<Entity> _spawned = new List<Entity>();
        private Blip _sceneBlip;
        private LHandle _pursuit;
        private int _startedAt;
        private bool _ended;
        private bool _cleanedUp;

        /// <summary>How long this callout may run before it closes itself.</summary>
        protected virtual int TimeoutMs { get { return 20 * 60 * 1000; } }

        /// <summary>One line, in dispatch's voice, when the callout is accepted.</summary>
        protected virtual string Briefing { get { return null; } }

        /// <summary>One line when it closes.</summary>
        protected virtual string SignOff { get { return "Show me 10-8 when you are clear."; } }

        protected Ped Suspect { get; set; }
        protected Vehicle SuspectVehicle { get; set; }

        // ------------------------------------------------------------------ dispatch

        /// <summary>Describe the callout to LSPDFR while it is being offered.</summary>
        protected void Offer(string friendlyName, string message, string advisory, Vector3 position, float radius)
        {
            FriendlyName = friendlyName;
            CalloutMessage = message;
            CalloutAdvisory = advisory;
            CalloutPosition = position;
            ShowCalloutAreaBlipBeforeAccepting(position, radius);
            AddMinimumDistanceCheck(150f, position);
        }

        public override bool OnCalloutAccepted()
        {
            _startedAt = Environment.TickCount;
            Mark();

            try
            {
                _sceneBlip = new Blip(CalloutPosition);
                _sceneBlip.Name = FriendlyName;
                _sceneBlip.Color = System.Drawing.Color.Yellow;
                _sceneBlip.Sprite = (BlipSprite)1;
                _sceneBlip.EnableRoute(System.Drawing.Color.Yellow);
            }
            catch (Exception ex) { Log.Error("scene blip", ex); }

            // A new scene means new people: whatever the last callout's suspects were going to say, they
            // are not saying it any more, and a reused handle must not inherit a script.
            try { Scripts.CalloutScript.Clear(); } catch { }

            Log.Line("accepted: " + FriendlyName + " at " + Where(CalloutPosition) +
                     " - the scene is built when you are within " + (int)BuildDistance + "m");
            if (!string.IsNullOrEmpty(Briefing)) Say(Briefing);

            // Not built here. The scene is hundreds of metres away at this moment, in a part of the map
            // the game has not loaded, and people spawned there have no ground under them: they drop
            // through the world, and the player arrives at an empty street. Building it as the player
            // closes in - Process, below - puts them on ground that is really there.
            _built = false;
            if (PlayerDistance() <= BuildDistance) BuildNow();

            return base.OnCalloutAccepted();
        }

        /// <summary>How close the player has to be before the scene is spawned: inside the loaded world.</summary>
        protected const float BuildDistance = 180f;

        private bool _built;

        private float PlayerDistance()
        {
            try { return Game.LocalPlayer.Character.Position.DistanceTo(CalloutPosition); }
            catch { return 0f; }
        }

        private void BuildNow()
        {
            _built = true;
            try
            {
                Build();
                Log.Line("scene built for " + FriendlyName + ", " + (int)PlayerDistance() + "m from the player");
            }
            catch (Exception ex)
            {
                Log.Error("building " + FriendlyName, ex);
                End();
            }
        }

        /// <summary>Build the scene. Called once, on acceptance.</summary>
        protected abstract void Build();

        /// <summary>Run it. Called every tick; false means the callout wants to close.</summary>
        protected abstract bool Tick();

        public override void Process()
        {
            base.Process();
            if (_ended) return;

            try
            {
                if (!Game.LocalPlayer.Character.IsAlive) { Close("the officer went down"); return; }
                if (Environment.TickCount - _startedAt > TimeoutMs) { Close("nothing came of it"); return; }

                // Nothing to run until there is a scene; it is built as the player arrives.
                if (!_built)
                {
                    if (PlayerDistance() <= BuildDistance) BuildNow();
                    if (!_built || _ended) return;
                }

                Tick();
            }
            catch (Exception ex) { Log.Error("tick " + FriendlyName, ex); }
        }

        public override void End()
        {
            _ended = true;
            CleanUp();
            base.End();
        }

        protected void Close(string reason)
        {
            Log.Line("closed: " + FriendlyName + " (" + reason + ")");
            if (!string.IsNullOrEmpty(SignOff)) Say(SignOff);
            End();
        }

        // ------------------------------------------------------------------ the scene

        /// <summary>A street position somewhere around the player: anywhere from the given distance out.</summary>
        protected static Vector3 StreetNear(Vector3 centre, float minimum, float maximum)
        {
            try
            {
                var angle = Rng.NextDouble() * Math.PI * 2;
                var distance = minimum + Rng.NextDouble() * (maximum - minimum);
                var point = new Vector3(centre.X + (float)Math.Cos(angle) * (float)distance,
                                        centre.Y + (float)Math.Sin(angle) * (float)distance,
                                        centre.Z);
                return World.GetNextPositionOnStreet(point);
            }
            catch (Exception ex)
            {
                Log.Error("finding a street", ex);
                return centre;
            }
        }

        protected Ped SpawnPed(string model, Vector3 position, float heading)
        {
            var ped = new Ped(model, position, heading);
            _spawned.Add(ped);
            ped.IsPersistent = true;
            ped.BlockPermanentEvents = true;
            return ped;
        }

        /// <summary>The same, for a model that has already been chosen and checked.</summary>
        protected Ped SpawnPed(Model model, Vector3 position, float heading)
        {
            var ped = new Ped(model, position, heading);
            _spawned.Add(ped);
            ped.IsPersistent = true;
            ped.BlockPermanentEvents = true;
            return ped;
        }

        protected Vehicle SpawnVehicle(string model, Vector3 position, float heading)
        {
            var vehicle = new Vehicle(model, position, heading);
            _spawned.Add(vehicle);
            vehicle.IsPersistent = true;
            return vehicle;
        }

        // ------------------------------------------------------------------ the ground, and what stands on it

        /// <summary>
        /// The nearest place a person can stand to a point: on the pavement rather than in a hedge, on the
        /// sand rather than in the sea, at the height the ground really is. Only meaningful once the area
        /// has loaded, which is why scenes are built as the player arrives. Falls back to the point.
        /// </summary>
        protected static Vector3 Ground(Vector3 point)
        {
            var result = point;

            try
            {
                Vector3 safe;
                if (Rage.Native.NativeFunction.Natives.GET_SAFE_COORD_FOR_PED<bool>(point.X, point.Y, point.Z + 1f, true, out safe, 16) &&
                    safe.DistanceTo(point) < 30f)
                    result = safe;
            }
            catch (Exception ex) { LogOnce("GET_SAFE_COORD_FOR_PED", ex); }

            try
            {
                var z = World.GetGroundZ(new Vector3(result.X, result.Y, result.Z + 15f), false, true);
                if (z.HasValue && Math.Abs(z.Value - result.Z) < 25f) result = new Vector3(result.X, result.Y, z.Value);
            }
            catch (Exception ex) { LogOnce("World.GetGroundZ", ex); }

            return result;
        }

        /// <summary>A point offset from the middle of a scene by X right and Y forward, turned to the scene's heading.</summary>
        protected static Vector3 Offset(Vector3 centre, float heading, float x, float y)
        {
            var radians = heading * Math.PI / 180.0;
            var cos = (float)Math.Cos(radians);
            var sin = (float)Math.Sin(radians);
            return new Vector3(centre.X + x * cos - y * sin, centre.Y + x * sin + y * cos, centre.Z);
        }

        /// <summary>A piece of scenery. Null, and one line in the log, if the game has no such model.</summary>
        protected Rage.Object SpawnProp(string model, Vector3 position, float heading)
        {
            try
            {
                var wanted = new Model(model);
                if (!wanted.IsValid) { Log.Line("the prop " + model + " is not in this game - left out of the scene"); return null; }

                var prop = new Rage.Object(wanted, position, heading);
                if (!prop.Exists()) return null;
                prop.IsPersistent = true;
                _spawned.Add(prop);
                try { Rage.Native.NativeFunction.Natives.PLACE_OBJECT_ON_GROUND_PROPERLY(prop); } catch { }
                return prop;
            }
            catch (Exception ex) { Log.Error("placing the prop " + model, ex); return null; }
        }

        /// <summary>A parked vehicle that is part of the scene - dented, hazards on, if it was in a collision.</summary>
        protected Vehicle SpawnSceneVehicle(string model, Vector3 position, float heading, bool damaged)
        {
            try
            {
                var wanted = new Model(model);
                if (!wanted.IsValid) { Log.Line("the vehicle " + model + " is not in this game - left out of the scene"); return null; }

                var vehicle = new Vehicle(wanted, position, heading);
                if (!vehicle.Exists()) return null;
                vehicle.IsPersistent = true;
                _spawned.Add(vehicle);
                try { Rage.Native.NativeFunction.Natives.SET_VEHICLE_ON_GROUND_PROPERLY(vehicle, 5f); } catch { }

                if (damaged)
                {
                    try { vehicle.Deform(new Vector3(0f, 2f, 0.2f), 1.6f, 140f); } catch { }
                    try { vehicle.EngineHealth = 250f; } catch { }
                    try { vehicle.IndicatorLightsStatus = VehicleIndicatorLightsStatus.Both; } catch { }
                }
                return vehicle;
            }
            catch (Exception ex) { Log.Error("placing the vehicle " + model, ex); return null; }
        }

        private static readonly HashSet<string> Logged = new HashSet<string>();

        private static void LogOnce(string what, Exception ex)
        {
            if (Logged.Add(what)) Log.Error(what + " (said once; the scene falls back to the plain point)", ex);
        }

        protected static void PutInVehicle(Ped ped, Vehicle vehicle, int seat)
        {
            if (ped == null || vehicle == null || !ped.Exists() || !vehicle.Exists()) return;
            ped.WarpIntoVehicle(vehicle, seat);
        }

        /// <summary>Armed and willing: used for the callouts that escalate.</summary>
        protected static void MakeHostile(Ped ped, string weapon = "WEAPON_PISTOL", int ammo = 150)
        {
            if (ped == null || !ped.Exists()) return;

            try
            {
                ped.Accuracy = 45;
                ped.Armor = 40;
                ped.CanAttackFriendlies = true;
                // ammo is a short in RPH's signature: a literal converts, a variable does not.
                if (!string.IsNullOrEmpty(weapon)) ped.Inventory.GiveNewWeapon(weapon, (short)ammo, true);

                // Natives, because there is no managed way to say "fight and do not back down".
                Rage.Native.NativeFunction.Natives.SetPedCombatAttributes(ped, 46, true);      // always fight
                Rage.Native.NativeFunction.Natives.SetPedFleeAttributes(ped, (short)0, false); // never flee
            }
            catch (Exception ex) { Log.Error("making a ped hostile", ex); }

            try { ped.Tasks.FightAgainst(Game.LocalPlayer.Character); }
            catch (Exception ex) { Log.Error("FightAgainst", ex); }
        }

        /// <summary>Runs away on foot - the foot pursuit.</summary>
        protected void FleeOnFoot(Ped ped)
        {
            if (ped == null || !ped.Exists()) return;

            try { ped.Tasks.Flee(Game.LocalPlayer.Character, 150f, -1); }
            catch (Exception ex) { Log.Error("Tasks.Flee", ex); }

            try { Rage.Native.NativeFunction.Natives.SetPedFleeAttributes(ped, (short)0, false); }
            catch { }
        }

        /// <summary>
        /// Runs away in a vehicle. LSPDFR does the driving: adding the ped to a pursuit is its own
        /// "flee and do not stop" AI, which is far better than anything written here would be.
        /// </summary>
        protected void FleeInVehicle(Ped driver)
        {
            if (driver == null || !driver.Exists()) return;

            try
            {
                if (_pursuit == null) _pursuit = Functions.CreatePursuit();
                Functions.AddPedToPursuit(_pursuit, driver);
                Functions.SetPursuitIsActiveForPlayer(_pursuit, true);
                Log.Line("pursuit started for " + FriendlyName);
            }
            catch (Exception ex) { Log.Error("starting a pursuit", ex); }
        }

        protected bool PursuitRunning()
        {
            if (_pursuit == null) return false;
            try { return Functions.IsPursuitStillRunning(_pursuit); }
            catch { return false; }
        }

        /// <summary>
        /// Hands up. There is no managed call for this in RPH, so it is the native - and the argument
        /// count moved between game builds (six in later ones, five in earlier), so both are tried.
        /// </summary>
        protected void HandsUp(Ped ped)
        {
            if (ped == null || !ped.Exists()) return;

            // The "face this ped" argument is 0 for nobody. A (Ped)null there is what RPH cannot marshal -
            // it threw a NullReferenceException in both shapes, every time - so nobody ever put their
            // hands up.
            try
            {
                Rage.Native.NativeFunction.Natives.TaskHandsUp(ped, -1, 0, -1, true);
                return;
            }
            catch (Exception five) { Log.Line("TaskHandsUp with five arguments failed: " + five.GetType().Name); }

            try { Rage.Native.NativeFunction.Natives.TaskHandsUp(ped, -1, 0, -1, true, false); }
            catch (Exception six) { Log.Error("TaskHandsUp with six arguments", six); }
        }

        /// <summary>
        /// Hurt, not dead. GTA counts a ped at or below 100 health as dead - the scale runs from 100 to
        /// their maximum, usually 200 - so "Health = 70" killed every patient the moment they spawned.
        /// `percent` is how much of their life they have left.
        /// </summary>
        protected static void Hurt(Ped ped, int percent)
        {
            if (ped == null || !ped.Exists()) return;
            try
            {
                var max = ped.MaxHealth > 100 ? ped.MaxHealth : 200;
                var health = 100 + (max - 100) * Math.Max(1, Math.Min(100, percent)) / 100;
                ped.Health = Math.Max(101, health);
            }
            catch (Exception ex) { Log.Error("setting a ped's health", ex); }
        }

        /// <summary>Out of the car, hands up, and stop running - the "make them give up" button.</summary>
        protected void Surrender(Ped ped)
        {
            if (ped == null || !ped.Exists()) return;

            try { Rage.Native.NativeFunction.Natives.ClearPedTasks(ped); } catch { }
            try { ped.Tasks.StandStill(-1); } catch { }
            HandsUp(ped);
        }

        protected void RequestAmbulance(Vector3 position)
        {
            try
            {
                Functions.RequestBackup(position, EBackupResponseType.Code3, EBackupUnitType.Ambulance);
                Log.Line("ambulance requested for " + FriendlyName);
            }
            catch (Exception ex) { Log.Error("requesting an ambulance", ex); }
        }

        /// <summary>Arrested, or dead - either way, the player has dealt with them.</summary>
        protected static bool Dealt(Ped ped)
        {
            if (ped == null) return false;

            try
            {
                if (!ped.Exists()) return true;
                if (!ped.IsAlive) return true;
                return Functions.IsPedArrested(ped);
            }
            catch { return false; }
        }

        /// <summary>Say something: notification, chat box if there is one, and the log.</summary>
        protected void Say(string line)
        {
            Log.Line("say: " + line);
            Hud.Say(line);
        }

        /// <summary>"Sandy Shores", or the coordinates if the game will not say.</summary>
        protected static string Where(Vector3 position)
        {
            try
            {
                var street = World.GetStreetName(position);

                // GetZoneAtPosition hands back a WorldZone, not a string - ToString() is the name the
                // game knows the area by, and it is what a dispatcher would say.
                string zone = null;
                var worldZone = Functions.GetZoneAtPosition(position);
                if (worldZone != null) zone = worldZone.ToString();

                if (!string.IsNullOrEmpty(street) && !string.IsNullOrEmpty(zone)) return street + ", " + zone;
                if (!string.IsNullOrEmpty(zone)) return zone;
                if (!string.IsNullOrEmpty(street)) return street;
            }
            catch { }

            return "(" + (int)position.X + ", " + (int)position.Y + ")";
        }

        /// <summary>
        /// Mark a ped as one of ours for LSPDFR's own systems - a stopped suspect can be interacted
        /// with the way the player expects, and can be arrested.
        /// </summary>
        protected static void MarkStoppable(Ped ped)
        {
            try { Functions.SetPedAsStopped(ped, false); }
            catch (Exception ex) { Log.Error("SetPedAsStopped", ex); }
        }

        private void Mark() { }

        private void CleanUp()
        {
            if (_cleanedUp) return;
            _cleanedUp = true;

            try { if (_sceneBlip != null && _sceneBlip.Exists()) _sceneBlip.Delete(); }
            catch { }
            _sceneBlip = null;

            // The pursuit is left running on purpose: the player still has to catch somebody.
            _pursuit = null;

            foreach (var entity in _spawned)
            {
                try
                {
                    if (entity == null || !entity.Exists()) continue;
                    if (Suspect != null) { try { if (ReferenceEquals(entity, Suspect) && Functions.IsPedArrested(Suspect)) continue; } catch { } }
                    entity.Delete();
                }
                catch { }
            }

            _spawned.Clear();
        }
    }
}
