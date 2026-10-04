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
