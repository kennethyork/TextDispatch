using System;
using System.Collections.Generic;
using Rage;
using Rage.Native;
using TextDispatch.Chat;

namespace TextDispatch.Lspdfr
{
    /// <summary>
    /// What Ultimate Backup put on the road, done by TextDispatch: spike strips that burst the tyres of whatever
    /// crosses them, road blocks of two cars and their officers across the road ahead, and a K9 unit whose
    /// dog can be put round a car. Everything placed is cleaned up by itself, or by /dismiss.
    /// </summary>
    internal sealed class Tactics
    {
        private sealed class Strip
        {
            public Rage.Object Prop;
            public int Until;
            public HashSet<uint> Burst = new HashSet<uint>();
        }

        private sealed class Block
        {
            public List<Vehicle> Cars = new List<Vehicle>();
            public List<Ped> Officers = new List<Ped>();
            public int Until;
        }

        private readonly ChatBox _chat;
        private readonly LspdfrApi _api;
        private readonly List<Strip> _strips = new List<Strip>();
        private readonly List<Block> _blocks = new List<Block>();
        private int _nextCheckAt;

        /// <summary>The K9 unit, while there is one: its car, its handler and its dog.</summary>
        public Vehicle K9Car;
        public Ped K9Dog;

        public Tactics(ChatBox chat, LspdfrApi api)
        {
            _chat = chat;
            _api = api;
        }

        // ------------------------------------------------------------------ where to put it

        /// <summary>
        /// Where a stinger or a block goes: ahead of whatever is running - the nearest moving car in front of you
        /// that is not police - on the road, 150 m in front of it; with nothing running, 100 m ahead of you.
        /// </summary>
        public bool Ahead(out Vector3 position, out float heading, out string what)
        {
            position = Vector3.Zero;
            heading = 0f;
            what = "ahead of you";
            var player = Game.LocalPlayer.Character;
            if (player == null || !player.Exists()) return false;

            var origin = player.Position;
            var forward = player.IsInAnyVehicle(false) ? player.CurrentVehicle.ForwardVector : player.ForwardVector;
            Vehicle target = null;
            var best = float.MaxValue;
            try
            {
                foreach (var entity in World.GetEntities(origin, 300f, GetEntitiesFlags.ConsiderAllVehicles | GetEntitiesFlags.ExcludePlayerVehicle))
                {
                    var car = entity as Vehicle;
                    if (car == null || !car.Exists() || car.Speed < 7f) continue;
                    var offset = car.Position - origin;
                    var metres = offset.Length();
                    if (metres < 5f || Vector3.Dot(offset / metres, forward) < 0.5f) continue;
                    Ped driver = null;
                    try { driver = car.Driver; } catch { }
                    if (driver != null && driver.Exists() && _api.PedIsCop(driver)) continue;
                    if (metres < best) { best = metres; target = car; }
                }
            }
            catch { }

            Vector3 guess;
            if (target != null)
            {
                guess = target.Position + target.ForwardVector * 150f;
                heading = target.Heading;
                string model = "vehicle";
                try { model = target.Model.Name; } catch { }
                what = "ahead of the fleeing " + model;
            }
            else
            {
                guess = origin + forward * 100f;
                heading = player.IsInAnyVehicle(false) ? player.CurrentVehicle.Heading : player.Heading;
            }

            try { position = World.GetNextPositionOnStreet(guess); }
            catch { position = guess; }
            return position != Vector3.Zero;
        }

        // ------------------------------------------------------------------ spike strips

        public bool Spikes(out string where)
        {
            Vector3 at;
            float heading;
            if (!Ahead(out at, out heading, out where)) return false;

            var prop = new Rage.Object("p_ld_stinger_s", at, heading + 90f);
            if (prop == null || !prop.Exists()) return false;
            try { NativeFunction.Natives.PLACE_OBJECT_ON_GROUND_PROPERLY(prop); } catch { }
            prop.IsPersistent = true;
            _strips.Add(new Strip { Prop = prop, Until = Environment.TickCount + 120000 });
            Log.Line("tactics: spike strip at " + at);
            return true;
        }

        // ------------------------------------------------------------------ road blocks

        public bool Roadblock(out string where)
        {
            Vector3 at;
            float heading;
            if (!Ahead(out at, out heading, out where)) return false;

            var block = new Block { Until = Environment.TickCount + 180000 };
            var side = new Vector3((float)Math.Cos(heading * Math.PI / 180f), (float)Math.Sin(heading * Math.PI / 180f), 0f);
            foreach (var offset in new[] { -2.8f, 2.8f })
            {
                try
                {
                    var car = new Vehicle(offset < 0 ? "police3" : "police", at + side * offset, heading + 90f);
                    if (car == null || !car.Exists()) continue;
                    car.IsPersistent = true;
                    car.IsSirenOn = true;
                    car.IsSirenSilent = true;
                    block.Cars.Add(car);

                    var officer = new Ped("s_m_y_cop_01", car.Position + car.ForwardVector * -2.2f, heading);
                    if (officer != null && officer.Exists())
                    {
                        officer.IsPersistent = true;
                        officer.BlockPermanentEvents = true;
                        officer.Inventory.GiveNewWeapon(WeaponHash.Pistol, 60, true);
                        // Facing back down the road, the way the car they are stopping comes from.
                        NativeFunction.Natives.TASK_TURN_PED_TO_FACE_COORD(officer, at.X + side.Y * 50f, at.Y - side.X * 50f, at.Z, -1);
                        block.Officers.Add(officer);
                    }
                }
                catch (Exception ex) { Log.Error("roadblock car", ex); }
            }
            if (block.Cars.Count == 0) return false;
            _blocks.Add(block);
            Log.Line("tactics: road block at " + at);
            return true;
        }

        // ------------------------------------------------------------------ K9

        /// <summary>Put a dog in the back of a unit that has just been sent - that unit is the K9.</summary>
        public bool MakeK9(Vehicle car)
        {
            if (car == null || !car.Exists()) return false;
            try
            {
                if (K9Dog != null && K9Dog.Exists()) K9Dog.Dismiss();
                var dog = new Ped("a_c_shepherd", car.Position + car.ForwardVector * -4f, car.Heading);
                if (dog == null || !dog.Exists()) return false;
                dog.IsPersistent = true;
                dog.BlockPermanentEvents = true;
                dog.WarpIntoVehicle(car, 1);
                K9Car = car;
                K9Dog = dog;
                return true;
            }
            catch (Exception ex) { Log.Error("k9 dog", ex); return false; }
        }

        /// <summary>
        /// The dog round a car: out of the K9 car, nose along the panels, and back. `alerts` decides whether it
        /// sits; the box says so a few seconds in, when the dog would have reached the door.
        /// </summary>
        public void Sniff(Vehicle car, bool alerts, Action<bool> done)
        {
            var dog = K9Dog;
            GameFiber.StartNew(() =>
            {
                try
                {
                    if (dog.IsInAnyVehicle(false)) { NativeFunction.Natives.TASK_LEAVE_VEHICLE(dog, dog.CurrentVehicle, 16); GameFiber.Sleep(1500); }
                    foreach (var corner in new[] { new Vector3(2.2f, 0f, 0f), new Vector3(0f, 3f, 0f), new Vector3(-2.2f, 0f, 0f), new Vector3(0f, -3f, 0f) })
                    {
                        if (!dog.Exists() || !car.Exists()) break;
                        var point = car.GetOffsetPosition(corner);
                        NativeFunction.Natives.TASK_GO_STRAIGHT_TO_COORD(dog, point.X, point.Y, point.Z, 1.2f, 4000, 0f, 0.5f);
                        GameFiber.Sleep(2200);
                    }
                    if (alerts && dog.Exists()) NativeFunction.Natives.TASK_STAND_STILL(dog, 4000);
                    GameFiber.Sleep(1200);
                    done(alerts);
                    if (dog.Exists() && K9Car != null && K9Car.Exists())
                    {
                        GameFiber.Sleep(4000);
                        NativeFunction.Natives.TASK_ENTER_VEHICLE(dog, K9Car, 10000, 1, 1.5f, 1, 0);
                    }
                }
                catch (Exception ex) { Log.Error("k9 sniff", ex); done(alerts); }
            }, "TextDispatch K9");
        }

        public bool K9OnScene(float metres)
        {
            try
            {
                return K9Dog != null && K9Dog.Exists() && K9Car != null && K9Car.Exists() &&
                       K9Car.Position.DistanceTo(Game.LocalPlayer.Character.Position) < metres;
            }
            catch { return false; }
        }

        // ------------------------------------------------------------------ the tick, and clearing up

        /// <summary>Called on the plugin's fiber: tyres over a strip burst, and what has served its time goes.</summary>
        public void Update()
        {
            if ((_strips.Count == 0 && _blocks.Count == 0) || Environment.TickCount - _nextCheckAt < 0) return;
            _nextCheckAt = Environment.TickCount + 100;

            Vehicle mine = null;
            try { mine = Game.LocalPlayer.Character.CurrentVehicle; } catch { }

            for (var i = _strips.Count - 1; i >= 0; i--)
            {
                var strip = _strips[i];
                if (!strip.Prop.Exists() || Environment.TickCount - strip.Until > 0) { Remove(strip); _strips.RemoveAt(i); continue; }
                try
                {
                    foreach (var entity in World.GetEntities(strip.Prop.Position, 3.5f, GetEntitiesFlags.ConsiderAllVehicles))
                    {
                        var car = entity as Vehicle;
                        if (car == null || !car.Exists() || car == mine || !strip.Burst.Add(car.Handle.Value)) continue;
                        for (var wheel = 0; wheel < 8; wheel++) NativeFunction.Natives.SET_VEHICLE_TYRE_BURST(car, wheel, true, 1000f);
                        string model = "a vehicle";
                        try { model = car.Model.Name; } catch { }
                        _chat.Write(ChatChannel.Radio, "[RADIO]", "Spikes, spikes, spikes - " + model + " hit the strip, tyres are out.");
                    }
                }
                catch { }
            }

            for (var i = _blocks.Count - 1; i >= 0; i--)
                if (Environment.TickCount - _blocks[i].Until > 0) { Remove(_blocks[i]); _blocks.RemoveAt(i); }
        }

        private static void Remove(Strip strip)
        {
            try { if (strip.Prop.Exists()) strip.Prop.Delete(); } catch { }
        }

        private static void Remove(Block block)
        {
            foreach (var officer in block.Officers) try { if (officer.Exists()) officer.Dismiss(); } catch { }
            foreach (var car in block.Cars) try { if (car.Exists()) car.Dismiss(); } catch { }
        }

        /// <summary>Take everything down: strips, blocks and the K9. Returns how many things went.</summary>
        public int Clear()
        {
            var count = _strips.Count + _blocks.Count;
            foreach (var strip in _strips) Remove(strip);
            foreach (var block in _blocks) Remove(block);
            _strips.Clear();
            _blocks.Clear();
            try { if (K9Dog != null && K9Dog.Exists()) { K9Dog.Dismiss(); count++; } } catch { }
            K9Dog = null;
            K9Car = null;
            return count;
        }
    }
}
