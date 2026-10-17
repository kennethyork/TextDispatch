using System;
using System.Collections.Generic;
using Rage;
using Rage.Native;

namespace TextDispatch.Lspdfr
{
    /// <summary>
    /// What livery every police car wears: one of the pack's own until /veh names a department, and then
    /// that department's.
    ///
    /// The game and LSPDFR bring the base cars - police, police3, sheriff - and pick their livery at random
    /// from everything on the car's texture, which since the livery builder is mostly real cities' designs.
    /// So a session starts with every police car in one that is no real city's: one the car came with, or
    /// one of the San Andreas departments (LIVERIES, not LIVERIES - REAL). A car already in one is left
    /// so, and one in a real city's design is repainted in one of them at random.
    ///
    /// Once /veh has named a department, every police car the game has - parked, on patrol, backup on
    /// its way, the one you are sitting in - and every one that turns up afterwards is put in its
    /// livery instead. A car whose own texture has the design is simply repainted. One whose texture
    /// does not - a police3 for Atlanta, which only lvinterceptor2 carries - is swapped for the copy that
    /// does: the same place, heading and speed, the same crew in the same seats, the same plate, siren
    /// and blips. A car with no design for the department at all - a fire truck, for a police
    /// department - is left as it is. /veh off goes back to the pack's own, swapping a copy that carries
    /// only real cities for its base car.
    ///
    /// A swap is a new car, so whatever held the old one - LSPDFR's backup, a callout - is holding a car
    /// that has gone. Only cars crewed by the player and the emergency services are swapped, never the
    /// car being stopped and never one on a tow truck; a copy made from a car somebody held is let go
    /// when they let its crew go, as their own car would have been.
    ///
    /// The department lasts the session: the next one starts in the pack's own again.
    /// </summary>
    internal sealed class Fleet
    {
        // How often the world is looked over, and how many cars are swapped each time: a swap loads a
        // model and makes a car, and a street of them at once is a stutter.
        private const int ScanMs = 500;
        private const int SwapsPerScan = 3;
        private const int LoadMs = 3000;

        private readonly LspdfrApi _api;
        private readonly Random _random = new Random();
        private GameFiber _fiber;
        private bool _running;
        private string _department;

        // The cars already in the livery wanted, or left alone on purpose: handle -> the model it was
        // then. A handle the game hands to a different model is a different car.
        private readonly Dictionary<uint, uint> _settled = new Dictionary<uint, uint>();
        private string _settledFor = "";

        // Copies made from a car somebody else held, with the crew that came with it.
        private readonly List<KeyValuePair<Vehicle, List<Ped>>> _held = new List<KeyValuePair<Vehicle, List<Ped>>>();

        // What has been said in the log already, so a missing copy is one line and not one a scan.
        private readonly HashSet<string> _said = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public Fleet(LspdfrApi api)
        {
            _api = api;
        }

        /// <summary>The department every police car is put in, or null for the pack's own designs.</summary>
        public string Department { get { return _department; } }

        /// <summary>Put the whole fleet in a department, or back in the pack's own designs with null.</summary>
        public void Set(string department)
        {
            var now = string.IsNullOrWhiteSpace(department) ? null : department.Trim();
            if (string.Equals(now, _department, StringComparison.OrdinalIgnoreCase)) return;
            _department = now;

            // Every car is looked at again - here rather than at the next scan, so a car settled straight
            // after this (the one /veh brings) stays settled.
            _settled.Clear();
            _settledFor = now ?? "";
            Log.Line("fleet: " + (now == null ? "back to the pack's own designs - no real city's" : "every police car in " + now));
        }

        /// <summary>Leave this car as it is: one /veh brought plain, or one somebody repainted with /livery.</summary>
        public void Settle(Vehicle vehicle)
        {
            try { if (vehicle != null && vehicle.Exists()) _settled[vehicle.Handle] = vehicle.Model.Hash; }
            catch { }
        }

        public void Start()
        {
            if (_running) return;
            _running = true;
            _fiber = GameFiber.StartNew(Loop, "TextDispatch fleet");
            Log.Line("fleet: police cars start in the pack's own designs, no real city's; /veh <department> puts them all in one");
        }

        public void Stop()
        {
            _running = false;
        }

        private void Loop()
        {
            while (_running)
            {
                GameFiber.Sleep(ScanMs);
                try { Scan(); }
                catch (Exception ex)
                {
                    Log.Error("fleet", ex);
                    GameFiber.Sleep(5000);
                }
            }
        }

        private void Scan()
        {
            var department = _department;
            if (!Plugin.OnDuty) return;

            if (!string.Equals(department ?? "", _settledFor, StringComparison.OrdinalIgnoreCase))
            {
                _settled.Clear();
                _settledFor = department ?? "";
            }

            var map = LiveryMap.Current();
            if (!map.Found) return;
            if (department != null && !map.Knows(department))
            {
                Once("unknown " + department, "fleet: " + department + " is not in " + LiveryMap.FileName + " - no car repainted. /veh another department, or /veh off.");
                return;
            }

            Release();

            // The cars as they are this moment. Settled cars the game has since let go are forgotten here,
            // before anything below gives the game a frame to make new ones.
            var all = World.GetAllVehicles();
            var present = new HashSet<uint>();
            foreach (var vehicle in all)
                if (vehicle != null && vehicle.Exists()) present.Add(vehicle.Handle);
            foreach (var handle in new List<uint>(_settled.Keys))
                if (!present.Contains(handle)) _settled.Remove(handle);

            int swaps = 0;
            foreach (var vehicle in all)
            {
                if (vehicle == null || !vehicle.Exists()) continue;

                uint was;
                if (_settled.TryGetValue(vehicle.Handle, out was) && was == vehicle.Model.Hash) continue;

                var done = Bring(vehicle, map, department, ref swaps);

                // A swap waits on the model, and /veh may have named another department meanwhile: the
                // rest is for the next scan, which starts over in that one.
                if (!string.Equals(_department, department, StringComparison.OrdinalIgnoreCase)) return;
                if (done != null && done.Exists()) _settled[done.Handle] = done.Model.Hash;
            }
        }

        /// <summary>
        /// Put one car in the department, or with none, in one of the pack's own designs. Returns the car
        /// to leave alone from now on - this one, or the copy that replaced it - or null to look at it
        /// again next scan.
        /// </summary>
        private Vehicle Bring(Vehicle vehicle, LiveryMap map, string department, ref int swaps)
        {
            var model = map.ModelOf(vehicle);
            if (model == null || vehicle.IsDead) return vehicle;

            var carrier = department == null ? map.PlainCarrier(model) : map.Carrier(model, department);
            if (carrier == null) return vehicle;

            if (carrier.Equals(model, StringComparison.OrdinalIgnoreCase))
            {
                Paint(vehicle, map, model, department);
                return vehicle;
            }

            if (swaps >= SwapsPerScan || !Swappable(vehicle)) return null;
            swaps++;
            return Swap(vehicle, map, model, carrier, department);
        }

        /// <summary>
        /// Whether a car can be replaced without taking it from somebody: crewed by nobody but the player
        /// and the emergency services, not the car being stopped, not on a tow truck, not being got into.
        /// A stolen cruiser with a suspect at the wheel belongs to whatever put them there, and a car whose
        /// crew has stepped out of it at a scene waits until they are back in - they could not get into
        /// a car that is not there any more.
        /// </summary>
        private bool Swappable(Vehicle vehicle)
        {
            var stopped = _api.PulloverVehicle();
            if (stopped != null && stopped.Exists() && stopped == vehicle) return false;

            try { if (NativeFunction.Natives.IsEntityAttached<bool>(vehicle)) return false; }
            catch { return false; }

            var player = Game.LocalPlayer.Character;
            if (player != null && player.Exists())
            {
                var entering = player.VehicleTryingToEnter;
                if ((object)entering != null && entering.Exists() && entering == vehicle) return false;
            }

            foreach (var ped in vehicle.Occupants)
            {
                if (ped == null || !ped.Exists() || ped.IsPlayer) continue;
                var group = ped.RelationshipGroup;
                if (group == RelationshipGroup.Cop || group == RelationshipGroup.Medic || group == RelationshipGroup.Fireman) continue;
                if (_api.PedIsCop(ped)) continue;
                return false;
            }
            return !SteppedOut(vehicle);
        }

        /// <summary>Whether somebody other than the player got out of this car and is still beside it.</summary>
        private static bool SteppedOut(Vehicle vehicle)
        {
            var at = vehicle.Position;
            foreach (var ped in World.GetAllPeds())
            {
                if (ped == null || !ped.Exists() || ped.IsPlayer || ped.IsDead) continue;
                if (ped.DistanceTo(at) > 60f || ped.IsInAnyVehicle(false)) continue;

                var last = ped.LastVehicle;
                if ((object)last != null && last.Exists() && last == vehicle) return true;
            }
            return false;
        }

        /// <summary>
        /// Replace a car with the copy of it that carries the department, all in one frame: the copy is
        /// made where the car is, the crew moved across, the car deleted, and the copy sent on at the
        /// speed the car was going.
        /// </summary>
        private Vehicle Swap(Vehicle vehicle, LiveryMap map, string model, string carrier, string department)
        {
            var wanted = new Model(carrier);
            if (!wanted.IsValid)
            {
                Once("model " + carrier, "fleet: the game has no " + carrier + ", so a " + model + " stays out of " + (department ?? "the pack's own designs") +
                                         " - is the pack's livery copies add-on installed?");
                return vehicle;
            }

            if (!wanted.IsLoaded)
            {
                wanted.Load();
                var started = Environment.TickCount;
                while (!wanted.IsLoaded && Environment.TickCount - started < LoadMs) GameFiber.Yield();
                if (!wanted.IsLoaded)
                {
                    Once("load " + carrier, "fleet: " + carrier + " did not load in " + (LoadMs / 1000) + "s - trying again");
                    return null;
                }

                // Loading took frames: the car may have gone, or somebody got in.
                if (!vehicle.Exists() || vehicle.IsDead || !Swappable(vehicle)) return null;
            }

            // What the car is doing, before it goes.
            var position = vehicle.Position;
            var heading = vehicle.Heading;
            var forward = Vector3.Dot(vehicle.Velocity, vehicle.ForwardVector);
            var engine = vehicle.IsEngineOn;
            var siren = vehicle.HasSiren && vehicle.IsSirenOn;
            var silent = vehicle.IsSirenSilent;
            var plate = vehicle.LicensePlate;
            var dirt = vehicle.DirtLevel;
            var held = vehicle.IsPersistent;

            var crew = new List<KeyValuePair<Ped, int>>();
            foreach (var ped in vehicle.Occupants)
                if (ped != null && ped.Exists()) crew.Add(new KeyValuePair<Ped, int>(ped, ped.SeatIndex));
            crew.Sort((a, b) => a.Value.CompareTo(b.Value));

            var blips = new List<KeyValuePair<BlipSprite, System.Drawing.Color>>();
            try
            {
                foreach (var blip in vehicle.GetAttachedBlips())
                    if (blip != null && blip.Exists()) blips.Add(new KeyValuePair<BlipSprite, System.Drawing.Color>(blip.Sprite, blip.Color));
            }
            catch { }

            Vehicle copy;
            try { copy = new Vehicle(wanted, position, heading); }
            catch (Exception ex) { Log.Error("fleet: making a " + carrier, ex); return null; }
            finally { wanted.Dismiss(); }
            if (copy == null || !copy.Exists()) return null;

            try { copy.LicensePlate = plate; } catch { }
            try { copy.DirtLevel = dirt; } catch { }

            foreach (var seat in crew)
                if (seat.Key.Exists()) seat.Key.WarpIntoVehicle(copy, seat.Value);

            vehicle.Delete();

            if (engine) NativeFunction.Natives.SetVehicleEngineOn(copy, true, true, false);
            if (siren)
            {
                copy.IsSirenOn = true;
                copy.IsSirenSilent = silent;
            }
            if (Math.Abs(forward) > 0.5f) NativeFunction.Natives.SetVehicleForwardSpeed(copy, forward);

            foreach (var mark in blips)
            {
                try
                {
                    var blip = copy.AttachBlip();
                    blip.Sprite = mark.Key;
                    blip.Color = mark.Value;
                }
                catch { }
            }

            Paint(copy, map, carrier, department, true);

            // The game's own cars go back to the game, driving on as they were. A car somebody held stays
            // held - its driver is theirs to send on - until they let its crew go.
            var ai = new List<Ped>();
            foreach (var seat in crew)
                if (seat.Key.Exists() && !seat.Key.IsPlayer) ai.Add(seat.Key);

            if (!held)
            {
                var driver = copy.Driver;
                if (driver != null && driver.Exists() && !driver.IsPlayer && !driver.IsPersistent)
                    driver.Tasks.CruiseWithVehicle(copy, Math.Max(forward, 15f), VehicleDrivingFlags.Normal);
                copy.Dismiss();
            }
            else if (ai.Count > 0) _held.Add(new KeyValuePair<Vehicle, List<Ped>>(copy, ai));

            Log.Line("fleet: " + model + " -> " + carrier + " in " + (department ?? "one of the pack's own designs") +
                     (crew.Count == 0 ? ", empty" : ", " + crew.Count + " aboard") + (held ? ", held by a script" : "") +
                     (Math.Abs(forward) > 0.5f ? ", at " + forward.ToString("0") + " m/s" : ""));
            return copy;
        }

        /// <summary>
        /// A car's livery set to the department's number on it, if the game sees that texture - or, with
        /// no department, to one of its own that is no real city's. `fresh` is a car just made, whose
        /// livery is the first by default rather than anybody's choice.
        /// </summary>
        private void Paint(Vehicle vehicle, LiveryMap map, string model, string department, bool fresh = false)
        {
            // A [skins] car already is the department, by its body texture.
            if (map.IsSkin(model)) return;

            if (department == null)
            {
                PaintPlain(vehicle, map, model, fresh);
                return;
            }

            int number = map.NumberFor(model, department);
            if (number < 1) return;

            int count = VehicleActions.LiveryCount(vehicle, number, fresh ? 1000 : 0);
            if (number > count)
            {
                // Next scan, a copy of the car that does have it is brought in its place.
                LiveryMap.Short(model, count);
                Once("short " + model, "fleet: " + department + " is livery " + number + " on the " + model + ", but the game sees only " +
                                       count + " on it - rebuild and reinstall the pack");
                return;
            }
            if (VehicleActions.Livery(vehicle) == number) return;

            string detail;
            VehicleActions.SetLivery(vehicle, number, out detail);
        }

        /// <summary>
        /// One of the car's own designs that is no real city's: left as it is when it already wears one,
        /// and otherwise one of them at random, so a street of police cars is not all the same.
        /// </summary>
        private void PaintPlain(Vehicle vehicle, LiveryMap map, string model, bool fresh)
        {
            int count = VehicleActions.LiveryCount(vehicle);
            var plain = map.PlainLiveries(model);
            plain.RemoveAll(number => number > count);
            if (plain.Count == 0)
            {
                Once("short " + model, "fleet: the " + model + "'s own designs are not among the " + count +
                                       " liveries the game sees on it - rebuild and reinstall the pack");
                return;
            }
            if (!fresh && plain.Contains(VehicleActions.Livery(vehicle))) return;

            string detail;
            VehicleActions.SetLivery(vehicle, plain[_random.Next(plain.Count)], out detail);
        }

        /// <summary>
        /// Let go of the copies whose crew has been let go: when backup is sent home its officers are
        /// dismissed, and the car they came in - which LSPDFR would have dismissed with them, had it still
        /// been the one LSPDFR made - goes back to the game. A copy the player is in is kept.
        /// </summary>
        private void Release()
        {
            var player = Game.LocalPlayer.Character;
            for (int i = _held.Count - 1; i >= 0; i--)
            {
                var copy = _held[i].Key;
                if (!copy.Exists()) { _held.RemoveAt(i); continue; }

                bool kept = false;
                foreach (var ped in _held[i].Value)
                    if (ped.Exists() && ped.IsPersistent) { kept = true; break; }
                if (kept) continue;
                if (player != null && player.Exists() && player.IsInVehicle(copy, false)) continue;

                try { foreach (var blip in copy.GetAttachedBlips()) if (blip != null && blip.Exists()) blip.Delete(); }
                catch { }
                copy.Dismiss();
                _held.RemoveAt(i);
            }
        }

        private void Once(string key, string line)
        {
            if (_said.Add(key)) Log.Line(line);
        }
    }
}
