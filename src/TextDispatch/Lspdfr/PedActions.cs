using System;
using Rage;
using Rage.Native;
using TextDispatch.Dialogue;

namespace TextDispatch.Lspdfr
{
    /// <summary>
    /// Making a pedestrian actually do what they were told.
    ///
    /// These are GTA natives, which RPH resolves **by name at run time** - there is no compile-time
    /// table to check them against, and a handful changed shape between game builds. So every call is
    /// wrapped, tried in more than one shape where the argument count is known to have moved, and
    /// reported through the log with the shape that worked. A wrong guess costs one line of dialogue
    /// being backed by nothing, never a crash.
    /// </summary>
    internal static class PedActions
    {
        public static bool Perform(LspdfrApi api, Ped ped, ComplyAction action, out string detail)
        {
            detail = null;
            if (ped == null) { detail = "no ped"; return false; }

            try
            {
                switch (action)
                {
                    case ComplyAction.HandsUp: return HandsUp(ped, out detail);
                    case ComplyAction.LeaveVehicle: return LeaveVehicle(ped, out detail);
                    case ComplyAction.LookAtMe: return LookAtMe(ped, out detail);
                    case ComplyAction.HoldStill: return HoldStill(api, ped, out detail);
                    case ComplyAction.StandDown: return StandDown(ped, out detail);
                }

                detail = "nothing to do";
                return false;
            }
            catch (Exception ex)
            {
                detail = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// TASK_HANDS_UP. This is the one that carries the risk: six arguments in later game builds,
        /// five in earlier ones. Try the longer signature, then the shorter.
        /// </summary>
        private static bool HandsUp(Ped ped, out string detail)
        {
            try
            {
                NativeFunction.Natives.TaskHandsUp(ped, -1, (Ped)null, -1, true, false);
                detail = "TaskHandsUp(ped,-1,null,-1,true,false)";
                return true;
            }
            catch (Exception six)
            {
                detail = "six arguments failed (" + six.GetType().Name + ")";
            }

            try
            {
                NativeFunction.Natives.TaskHandsUp(ped, -1, (Ped)null, -1, true);
                detail = "TaskHandsUp(ped,-1,null,-1,true)";
                return true;
            }
            catch (Exception five)
            {
                detail = detail + "; five arguments failed too (" + five.GetType().Name + ")";
                return false;
            }
        }

        private static bool LeaveVehicle(Ped ped, out string detail)
        {
            var vehicle = ped.CurrentVehicle;
            if (vehicle == null || !vehicle.Exists())
            {
                detail = "not in a vehicle";
                return false;
            }

            NativeFunction.Natives.TaskLeaveVehicle(ped, vehicle, 0);
            detail = "TaskLeaveVehicle";
            return true;
        }

        private static bool LookAtMe(Ped ped, out string detail)
        {
            var player = Game.LocalPlayer.Character;
            if (player == null) { detail = "no player"; return false; }

            NativeFunction.Natives.TaskTurnPedToFaceEntity(ped, player, 3000);
            detail = "TaskTurnPedToFaceEntity";
            return true;
        }

        /// <summary>
        /// LSPDFR's own stopped state, which is why it is preferred here: it is the same thing the
        /// modification does when a pedestrian is detained, so it fits rather than fights.
        /// </summary>
        private static bool HoldStill(LspdfrApi api, Ped ped, out string detail)
        {
            if (api == null || !api.Available) { detail = "LSPDFR not available"; return false; }

            api.StopPed(ped);
            detail = "LSPDFR SetPedAsStopped";
            return true;
        }

        private static bool StandDown(Ped ped, out string detail)
        {
            NativeFunction.Natives.ClearPedTasks(ped);
            NativeFunction.Natives.TaskWanderStandard(ped, 10f, 10);
            detail = "ClearPedTasks + TaskWanderStandard";
            return true;
        }
    }
}
