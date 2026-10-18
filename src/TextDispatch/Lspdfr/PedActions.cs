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
                    case ComplyAction.Kneel: return Kneel(ped, out detail);
                    case ComplyAction.TurnAround: return TurnAround(ped, out detail);
                    case ComplyAction.EngineOff: return EngineOff(ped, out detail);
                    case ComplyAction.WindowDown: return WindowDown(ped, out detail);
                    case ComplyAction.FollowMe: return FollowMe(ped, out detail);
                    case ComplyAction.GetInMyCar: return GetInMyCar(ped, out detail);
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
        /// TASK_HANDS_UP(ped, duration, facingPed, p3, p4). The ped to face is 0 for nobody: a (Ped)null
        /// there is what RPH cannot marshal, and it threw a NullReferenceException in TextCallouts every
        /// time. Five arguments is the native's real shape; six is kept as the fallback.
        /// </summary>
        private static bool HandsUp(Ped ped, out string detail)
        {
            try
            {
                NativeFunction.Natives.TaskHandsUp(ped, -1, 0, -1, true);
                detail = "TaskHandsUp(ped,-1,0,-1,true)";
                return true;
            }
            catch (Exception five)
            {
                detail = "five arguments failed (" + five.GetType().Name + ")";
            }

            try
            {
                NativeFunction.Natives.TaskHandsUp(ped, -1, 0, -1, true, false);
                detail = "TaskHandsUp(ped,-1,0,-1,true,false)";
                return true;
            }
            catch (Exception six)
            {
                detail = detail + "; six arguments failed too (" + six.GetType().Name + ")";
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

        /// <summary>On their knees, hands on the head - the game's own "busted" pose, looped.</summary>
        private static bool Kneel(Ped ped, out string detail)
        {
            if (ped.IsInAnyVehicle(false)) { detail = "in a vehicle"; return false; }
            var dictionary = new AnimationDictionary("random@arrests@busted");
            dictionary.LoadAndWait();
            ped.Tasks.Clear();
            ped.Tasks.PlayAnimation(dictionary, "idle_a", 4f, AnimationFlags.Loop);
            ped.BlockPermanentEvents = true;
            detail = "random@arrests@busted idle_a";
            return true;
        }

        /// <summary>Back to the officer: they face the way the officer faces.</summary>
        private static bool TurnAround(Ped ped, out string detail)
        {
            var player = Game.LocalPlayer.Character;
            if (player == null) { detail = "no player"; return false; }
            NativeFunction.Natives.TASK_ACHIEVE_HEADING(ped, player.Heading, 2000);
            detail = "TASK_ACHIEVE_HEADING " + player.Heading.ToString("0");
            return true;
        }

        private static Vehicle CarOf(Ped ped)
        {
            var vehicle = ped.CurrentVehicle;
            if (vehicle == null || !vehicle.Exists()) vehicle = ped.LastVehicle;
            return vehicle != null && vehicle.Exists() && vehicle.Position.DistanceTo(ped.Position) < 8f ? vehicle : null;
        }

        private static bool EngineOff(Ped ped, out string detail)
        {
            var vehicle = CarOf(ped);
            if (vehicle == null) { detail = "no car of theirs"; return false; }
            NativeFunction.Natives.SET_VEHICLE_ENGINE_ON(vehicle, false, true, true);
            detail = "SET_VEHICLE_ENGINE_ON false";
            return true;
        }

        private static bool WindowDown(Ped ped, out string detail)
        {
            var vehicle = CarOf(ped);
            if (vehicle == null) { detail = "no car of theirs"; return false; }
            NativeFunction.Natives.ROLL_DOWN_WINDOW(vehicle, 0);
            detail = "ROLL_DOWN_WINDOW 0";
            return true;
        }

        private static bool FollowMe(Ped ped, out string detail)
        {
            var player = Game.LocalPlayer.Character;
            if (player == null) { detail = "no player"; return false; }
            if (ped.IsInAnyVehicle(false)) { detail = "in a vehicle"; return false; }
            ped.Tasks.Clear();
            NativeFunction.Natives.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(ped, player, 0f, -1.4f, 0f, 1.4f, -1, 1.0f, true);
            ped.BlockPermanentEvents = true;
            detail = "TASK_FOLLOW_TO_OFFSET_OF_ENTITY";
            return true;
        }

        /// <summary>Into a free back seat of the officer's car - the one they last drove, if it is near.</summary>
        public static bool GetInMyCar(Ped ped, out string detail)
        {
            var player = Game.LocalPlayer.Character;
            Vehicle car = null;
            try { car = player.LastVehicle; } catch { }
            if (car == null || !car.Exists() || car.Position.DistanceTo(player.Position) > 25f) { detail = "no patrol car near"; return false; }

            var seat = -2;
            foreach (var s in new[] { 1, 2, 0 })
                if (car.IsSeatFree(s)) { seat = s; break; }
            if (seat == -2) { detail = "no free seat"; return false; }

            ped.Tasks.Clear();
            NativeFunction.Natives.TASK_ENTER_VEHICLE(ped, car, 20000, seat, 1.0f, 1, 0);
            ped.BlockPermanentEvents = true;
            detail = "TASK_ENTER_VEHICLE seat " + seat;
            return true;
        }

        // ------------------------------------------------------------------ the officer's side

        /// <summary>
        /// Something the officer does while the box says it - turning to them and playing the action for a few
        /// seconds, then letting go - so a pat-down or an ID check is seen, not only read.
        /// </summary>
        public static void OfficerAction(Ped target, string scenario, int milliseconds)
        {
            var player = Game.LocalPlayer.Character;
            if (player == null || !player.Exists() || player.IsInAnyVehicle(false)) return;
            try
            {
                if (target != null && target.Exists()) NativeFunction.Natives.TASK_TURN_PED_TO_FACE_ENTITY(player, target, 800);
                GameFiber.StartNew(() =>
                {
                    try
                    {
                        GameFiber.Sleep(700);
                        NativeFunction.Natives.TASK_START_SCENARIO_IN_PLACE(player, scenario, 0, true);
                        GameFiber.Sleep(milliseconds);
                        if (player.Exists()) player.Tasks.Clear();
                    }
                    catch (Exception ex) { Log.Error("officer action " + scenario, ex); }
                }, "TextDispatch officer action");
            }
            catch (Exception ex) { Log.Error("officer action", ex); }
        }

        /// <summary>Both play the hand-over: something passed from one to the other - an ID, keys, a ticket.</summary>
        public static void HandOver(Ped from, Ped to)
        {
            try
            {
                var dictionary = new AnimationDictionary("mp_common");
                dictionary.LoadAndWait();
                if (from != null && from.Exists() && !from.IsInAnyVehicle(false))
                    from.Tasks.PlayAnimation(dictionary, "givetake1_a", 4f, AnimationFlags.UpperBodyOnly | AnimationFlags.SecondaryTask);
                if (to != null && to.Exists() && !to.IsInAnyVehicle(false))
                    to.Tasks.PlayAnimation(dictionary, "givetake1_b", 4f, AnimationFlags.UpperBodyOnly | AnimationFlags.SecondaryTask);
            }
            catch (Exception ex) { Log.Error("hand over", ex); }
        }

        // ------------------------------------------------------------------ holding somebody

        /// <summary>
        /// Hold them by the arm: they are attached at the officer's side and go where the officer goes, the
        /// way StopThePed's grab does. LetGo puts them down.
        /// </summary>
        public static bool Grab(Ped ped, out string detail)
        {
            var player = Game.LocalPlayer.Character;
            if (player == null || ped == null || !ped.Exists()) { detail = "nobody"; return false; }
            if (ped.IsInAnyVehicle(false) || player.IsInAnyVehicle(false)) { detail = "somebody is in a vehicle"; return false; }
            ped.Tasks.Clear();
            ped.BlockPermanentEvents = true;
            int bone = NativeFunction.Natives.GET_PED_BONE_INDEX<int>(player, 11816);
            NativeFunction.Natives.ATTACH_ENTITY_TO_ENTITY(ped, player, bone, 0.27f, 0.45f, 0f, 0f, 0f, 0f, false, false, false, false, 2, true);
            detail = "attached at the pelvis, right side";
            return true;
        }

        public static bool LetGo(Ped ped, out string detail)
        {
            if (ped == null || !ped.Exists()) { detail = "nobody"; return false; }
            NativeFunction.Natives.DETACH_ENTITY(ped, true, false);
            detail = "detached";
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
