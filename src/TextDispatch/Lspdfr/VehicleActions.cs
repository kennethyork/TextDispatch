using System;
using Rage;
using Rage.Native;

namespace TextDispatch.Lspdfr
{
    /// <summary>
    /// Things you can do to a car.
    ///
    /// Same discipline as the pedestrian actions: these are GTA natives, resolved by name at run time
    /// with nothing to check them against, so every call is wrapped and reports what it did. A wrong
    /// name costs one command doing nothing, never a crash.
    /// </summary>
    internal static class VehicleActions
    {
        private const int DoorLocked = 2;
        private const int DoorUnlocked = 1;

        public static bool SetLocked(Vehicle vehicle, bool locked, out string detail)
        {
            detail = null;
            if (vehicle == null || !vehicle.Exists()) { detail = "no vehicle"; return false; }

            NativeFunction.Natives.SetVehicleDoorsLocked(vehicle, locked ? DoorLocked : DoorUnlocked);
            detail = locked ? "locked" : "unlocked";
            return true;
        }

        public static bool SetEngine(Vehicle vehicle, bool on, out string detail)
        {
            detail = null;
            if (vehicle == null || !vehicle.Exists()) { detail = "no vehicle"; return false; }

            NativeFunction.Natives.SetVehicleEngineOn(vehicle, on, true, false);
            detail = on ? "engine on" : "engine off";
            return true;
        }

        /// <summary>Door indices: 0 front-left, 1 front-right, 2 rear-left, 3 rear-right, 4 hood, 5 trunk.</summary>
        public static bool OpenDoor(Vehicle vehicle, int index, out string detail)
        {
            detail = null;
            if (vehicle == null || !vehicle.Exists()) { detail = "no vehicle"; return false; }

            NativeFunction.Natives.SetVehicleDoorOpen(vehicle, index, false, false);
            detail = "door " + index + " open";
            return true;
        }

        public static bool CloseDoors(Vehicle vehicle, out string detail)
        {
            detail = null;
            if (vehicle == null || !vehicle.Exists()) { detail = "no vehicle"; return false; }

            for (int i = 0; i <= 5; i++)
                NativeFunction.Natives.SetVehicleDoorShut(vehicle, i, false);

            detail = "all doors shut";
            return true;
        }

        public static bool Repair(Vehicle vehicle, out string detail)
        {
            detail = null;
            if (vehicle == null || !vehicle.Exists()) { detail = "no vehicle"; return false; }

            NativeFunction.Natives.SetVehicleFixed(vehicle);
            NativeFunction.Natives.SetVehicleDeformationFixed(vehicle);
            NativeFunction.Natives.SetVehicleDirtLevel(vehicle, 0f);

            detail = "repaired";
            return true;
        }

        /// <summary>
        /// How many liveries the game found for this car. Two kinds exist and a car has one or the other:
        /// numbered sign textures in its .ytd (the pack's cars, and the game's own police cars), or liveries in
        /// its mod kit (mod slot 48 - how most add-on police cars carry theirs). Sign liveries when there are
        /// any, the mod kit's otherwise; 0 when it has neither.
        /// </summary>
        public static int LiveryCount(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists()) return 0;
            int signs = SignCount(vehicle);
            return signs > 0 ? signs : ModCount(vehicle);
        }

        private const int LiveryModType = 48;

        private static int SignCount(Vehicle vehicle)
        {
            try { return Math.Max(0, (int)NativeFunction.Natives.GetVehicleLiveryCount<int>(vehicle)); }
            catch (Exception ex) { Log.Error("livery count", ex); return 0; }
        }

        private static int ModCount(Vehicle vehicle)
        {
            try
            {
                if (NativeFunction.Natives.GetVehicleModKit<int>(vehicle) < 0) NativeFunction.Natives.SetVehicleModKit(vehicle, 0);
                return Math.Max(0, (int)NativeFunction.Natives.GetNumVehicleMods<int>(vehicle, LiveryModType));
            }
            catch { return 0; }
        }

        /// <summary>Whether this car's liveries are in its mod kit rather than its sign textures.</summary>
        public static bool UsesModLiveries(Vehicle vehicle)
        {
            return vehicle != null && vehicle.Exists() && SignCount(vehicle) == 0 && ModCount(vehicle) > 0;
        }

        /// <summary>
        /// The name a mod-kit livery carries in the game's text - "LSPD Slicktop", "Sheriff" - or null. Sign
        /// liveries have no names; those come from the livery files, or from /livery name.
        /// </summary>
        public static string LiveryName(Vehicle vehicle, int number)
        {
            if (!UsesModLiveries(vehicle) || number < 1) return null;
            try
            {
                string label = NativeFunction.Natives.GetModTextLabel<string>(vehicle, LiveryModType, number - 1);
                if (string.IsNullOrEmpty(label)) return null;
                string text = NativeFunction.Natives.GetFilenameForAudioConversation<string>(label);
                return string.IsNullOrWhiteSpace(text) || text == "NULL" ? null : text.Trim();
            }
            catch { return null; }
        }

        /// <summary>
        /// The liveries on a car that should have at least `wanted`: a car just made can count none until its
        /// textures have streamed in, so it is asked again for up to `waitMs` before the answer is believed.
        /// A short answer is logged with what the game says about the car, for telling why.
        /// </summary>
        public static int LiveryCount(Vehicle vehicle, int wanted, int waitMs)
        {
            int count = LiveryCount(vehicle);
            int started = Environment.TickCount;
            while (count < wanted && Environment.TickCount - started < waitMs && vehicle != null && vehicle.Exists())
            {
                GameFiber.Yield();
                count = LiveryCount(vehicle);
            }

            if (count < wanted && vehicle != null && vehicle.Exists())
            {
                int raw = -2, mods = -2;
                try { raw = NativeFunction.Natives.GetVehicleLiveryCount<int>(vehicle); } catch { }
                try { NativeFunction.Natives.SetVehicleModKit(vehicle, 0); mods = NativeFunction.Natives.GetNumVehicleMods<int>(vehicle, 48); } catch { }
                Log.Line("livery count: " + vehicle.Model.Hash.ToString("X8") + " has " + raw + " sign liveries and " + mods +
                         " livery mods after " + (Environment.TickCount - started) + " ms, wanted " + wanted);
            }
            return count;
        }

        /// <summary>The livery the car is wearing, counted from 1 like the textures; 0 when none is set.</summary>
        public static int Livery(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists()) return 0;
            try
            {
                if (UsesModLiveries(vehicle)) return Math.Max(0, (int)NativeFunction.Natives.GetVehicleMod<int>(vehicle, LiveryModType) + 1);
                return Math.Max(0, (int)NativeFunction.Natives.GetVehicleLivery<int>(vehicle) + 1);
            }
            catch (Exception ex) { Log.Error("livery", ex); return 0; }
        }

        /// <summary>Paint a car with livery `number`, counted from 1: livery 1 is the texture ending _sign_1.</summary>
        public static bool SetLivery(Vehicle vehicle, int number, out string detail)
        {
            detail = null;
            if (vehicle == null || !vehicle.Exists()) { detail = "no vehicle"; return false; }

            if (UsesModLiveries(vehicle))
            {
                NativeFunction.Natives.SetVehicleMod(vehicle, LiveryModType, number - 1, false);
                detail = "livery " + number + " (mod kit)";
                return true;
            }

            NativeFunction.Natives.SetVehicleLivery(vehicle, number - 1);
            detail = "livery " + number;
            return true;
        }

        /// <summary>Whether the game has a vehicle by this name - "police3", "stanier" - as opposed to a department's.</summary>
        public static bool IsVehicleModel(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            try
            {
                var model = new Model(name.Trim());
                return model.IsValid && model.IsVehicle;
            }
            catch { return false; }
        }

        /// <summary>
        /// Bring a vehicle. The model is whatever the game calls it - "police2", "riot", or a name an
        /// installed vehicle mod registers - and a model the game does not know fails cleanly.
        /// </summary>
        public static bool Spawn(string model, out Vehicle spawned, out string detail)
        {
            spawned = null;
            detail = null;

            var player = Game.LocalPlayer.Character;
            if (player == null) { detail = "no player"; return false; }
            if (string.IsNullOrWhiteSpace(model)) { detail = "no model given"; return false; }

            try
            {
                var vehicle = new Vehicle(model.Trim(), player.GetOffsetPositionFront(6f), player.Heading);
                if (vehicle == null || !vehicle.Exists())
                {
                    detail = "the game does not know a model called '" + model.Trim() + "'";
                    return false;
                }

                spawned = vehicle;
                detail = "spawned " + model.Trim();
                return true;
            }
            catch (Exception ex)
            {
                detail = "spawn failed: " + ex.Message;
                return false;
            }
        }
    }
}
