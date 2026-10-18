using System;
using System.Collections.Generic;
using Rage;
using Rage.Native;
using TextDispatch.Lspdfr;

namespace TextDispatch.Commands
{
    /// <summary>
    /// The officer's body and kit: /anim, so the character does what /me says, and /gear and /armory, the
    /// duty belt and what is signed out of the station.
    /// </summary>
    internal sealed partial class CommandRouter
    {
        // ------------------------------------------------------------------ animations

        // name -> (scenario, or "dict|clip" for an animation; what it looks like, for the list)
        private static readonly Dictionary<string, string[]> Anims = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "notepad", new[] { "CODE_HUMAN_MEDIC_TIME_OF_DEATH", "write in a notepad" } },
            { "clipboard", new[] { "WORLD_HUMAN_CLIPBOARD", "hold a clipboard" } },
            { "radio", new[] { "random@arrests|generic_radio_chatter", "talk on the shoulder radio" } },
            { "crossarms", new[] { "amb@world_human_hang_out_street@female_arms_crossed@base|base", "cross your arms" } },
            { "lean", new[] { "WORLD_HUMAN_LEANING", "lean against what is behind you" } },
            { "kneel", new[] { "CODE_HUMAN_MEDIC_KNEEL", "kneel down" } },
            { "investigate", new[] { "CODE_HUMAN_POLICE_INVESTIGATE", "crouch and look at something closely" } },
            { "traffic", new[] { "WORLD_HUMAN_CAR_PARK_ATTENDANT", "direct traffic" } },
            { "guard", new[] { "WORLD_HUMAN_COP_IDLES", "stand watch, hands on the belt" } },
            { "crowd", new[] { "CODE_HUMAN_POLICE_CROWD_CONTROL", "hold a crowd back" } },
            { "coffee", new[] { "WORLD_HUMAN_AA_COFFEE", "drink a coffee" } },
            { "phone", new[] { "WORLD_HUMAN_STAND_MOBILE", "look at your phone" } },
            { "binoculars", new[] { "WORLD_HUMAN_BINOCULARS", "look through binoculars" } },
            { "camera", new[] { "WORLD_HUMAN_PAPARAZZI", "photograph the scene" } },
            { "point", new[] { "gestures@m@standing@casual|gesture_point", "point" } },
            { "wave", new[] { "friends@frj@ig_1|wave_a", "wave" } },
        };

        /// <summary>
        /// /anim &lt;name&gt;: the character does it - writes in a notepad, talks on the radio, leans, kneels,
        /// directs traffic. /anim stop (or moving) ends it; /anim on its own lists them.
        /// </summary>
        private void Anim(string argument)
        {
            var name = (argument ?? "").Trim().ToLowerInvariant();
            var player = Game.LocalPlayer.Character;
            if (player == null || !player.Exists()) return;

            if (name.Length == 0 || name == "list")
            {
                _chat.Notice("Animations - '/anim <name>', '/anim stop' or just move to end one:");
                var line = new System.Text.StringBuilder();
                foreach (var pair in Anims)
                {
                    var entry = pair.Key + " (" + pair.Value[1] + ")";
                    if (line.Length > 0 && line.Length + entry.Length > 95) { _chat.Notice("  " + line); line.Length = 0; }
                    line.Append(line.Length > 0 ? ",  " : "").Append(entry);
                }
                if (line.Length > 0) _chat.Notice("  " + line);
                return;
            }

            if (name == "stop" || name == "cancel" || name == "end")
            {
                try { player.Tasks.Clear(); } catch { }
                return;
            }

            string[] anim;
            if (!Anims.TryGetValue(name, out anim))
            {
                foreach (var pair in Anims) if (pair.Key.StartsWith(name)) { anim = pair.Value; name = pair.Key; break; }
            }
            if (anim == null) { _chat.Error("No animation called '" + name + "'. /anim lists them."); return; }

            try
            {
                if (player.IsInAnyVehicle(false) && name != "radio" && name != "phone")
                {
                    _chat.Error("Get out of the car first - only 'radio' and 'phone' work seated.");
                    return;
                }

                var bar = anim[0].IndexOf('|');
                if (bar < 0)
                {
                    NativeFunction.Natives.TASK_START_SCENARIO_IN_PLACE(player, anim[0], 0, true);
                }
                else
                {
                    var dictionary = new AnimationDictionary(anim[0].Substring(0, bar));
                    dictionary.LoadAndWait();
                    var once = name == "point" || name == "wave";
                    player.Tasks.PlayAnimation(dictionary, anim[0].Substring(bar + 1), 4f,
                        once ? AnimationFlags.UpperBodyOnly | AnimationFlags.SecondaryTask
                             : AnimationFlags.Loop | AnimationFlags.UpperBodyOnly | AnimationFlags.SecondaryTask);
                }
                Log.Line("anim: " + name);
            }
            catch (Exception ex)
            {
                Log.Error("animation " + name, ex);
                _chat.Error("That animation did not play - the log has why.");
            }
        }

        // ------------------------------------------------------------------ equipment

        // key -> (the weapon, ammunition, what it is called)
        private static readonly Dictionary<string, Tuple<WeaponHash, short, string>> Armory = new Dictionary<string, Tuple<WeaponHash, short, string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "pistol", Tuple.Create(WeaponHash.CombatPistol, (short)120, "combat pistol") },
            { "taser", Tuple.Create(WeaponHash.StunGun, (short)1, "taser") },
            { "baton", Tuple.Create(WeaponHash.Nightstick, (short)1, "baton") },
            { "flashlight", Tuple.Create(WeaponHash.Flashlight, (short)1, "flashlight") },
            { "shotgun", Tuple.Create(WeaponHash.PumpShotgun, (short)48, "pump shotgun") },
            { "rifle", Tuple.Create(WeaponHash.CarbineRifle, (short)180, "patrol rifle") },
            { "flares", Tuple.Create(WeaponHash.Flare, (short)10, "road flares") },
            { "extinguisher", Tuple.Create(WeaponHash.FireExtinguisher, (short)1000, "fire extinguisher") },
        };

        private static readonly string[] Kit =
        {
            "handcuffs", "radio", "body camera", "notebook and pen", "first aid pouch", "breath tester", "gloves"
        };

        /// <summary>/gear: what you are carrying - the duty belt, the weapons on you, and what is signed out.</summary>
        private void Gear()
        {
            var player = Game.LocalPlayer.Character;
            var carried = new List<string>();
            try
            {
                foreach (var pair in Armory)
                    if (player.Inventory.Weapons.Contains(pair.Value.Item1)) carried.Add(pair.Value.Item3 + (_records.SignedOut.Contains(pair.Key) ? " (signed out)" : ""));
            }
            catch (Exception ex) { Log.Error("gear", ex); }

            _chat.Notice("On you: " + string.Join(", ", Kit) + ".");
            _chat.Notice("  Weapons: " + (carried.Count == 0 ? "none" : string.Join(", ", carried.ToArray())) + ".");
            if (_records.SignedOut.Count > 0)
            {
                var names = new List<string>();
                foreach (var key in _records.SignedOut) names.Add(Armory.ContainsKey(key) ? Armory[key].Item3 : key);
                _chat.Notice("  Signed out of the armory: " + string.Join(", ", names.ToArray()) + " - '/armory in' at a station returns it.");
            }
            _chat.Notice("  '/armory' at a station signs weapons out and back in.");
        }

        /// <summary>
        /// /armory [out|in] [item]: at a station, sign a weapon out - it is given to you - or back in. Kept in a
        /// log, and the sergeant asks at the end of the shift about anything still out.
        /// </summary>
        private void ArmoryCommand(string argument)
        {
            var words = (argument ?? "").Trim().ToLowerInvariant().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            var verb = words.Length > 0 ? words[0] : "";
            var item = words.Length > 1 ? words[1].Trim() : "";

            if (verb != "out" && verb != "in")
            {
                _chat.Notice("Armory - '/armory out <item>' signs one out, '/armory in <item>' (or 'all') returns it:");
                var list = new List<string>();
                foreach (var pair in Armory) list.Add(pair.Key + (_records.SignedOut.Contains(pair.Key) ? " [out]" : ""));
                _chat.Notice("  " + string.Join(", ", list.ToArray()));
                for (var i = Math.Max(0, _records.ArmoryLog.Count - 4); i < _records.ArmoryLog.Count; i++) _chat.Notice("  log: " + _records.ArmoryLog[i]);
                return;
            }

            var station = DispatchService.StationHere();
            if (station == null) { _chat.Error("The armory is at a station - Mission Row, Vespucci, Davis, Sandy Shores, Paleto Bay and the rest."); return; }

            var player = Game.LocalPlayer.Character;
            var keys = new List<string>();
            if (verb == "in" && (item == "all" || item == "")) keys.AddRange(_records.SignedOut);
            else
            {
                string found = null;
                foreach (var key in Armory.Keys) if (key.StartsWith(item) || Armory[key].Item3.Contains(item)) { found = key; break; }
                if (found == null) { _chat.Error("The armory has no '" + item + "'. '/armory' lists it."); return; }
                keys.Add(found);
            }
            if (keys.Count == 0) { _chat.Notice("Nothing signed out."); return; }

            foreach (var key in keys)
            {
                var weapon = Armory[key];
                try
                {
                    if (verb == "out")
                    {
                        if (_records.SignedOut.Contains(key)) { _chat.Notice("The " + weapon.Item3 + " is already signed out to you."); continue; }
                        player.Inventory.GiveNewWeapon(weapon.Item1, weapon.Item2, false);
                        _records.SignedOut.Add(key);
                    }
                    else
                    {
                        if (player.Inventory.Weapons.Contains(weapon.Item1)) player.Inventory.Weapons.Remove(weapon.Item1);
                        _records.SignedOut.Remove(key);
                    }
                }
                catch (Exception ex) { Log.Error("armory " + key, ex); _chat.Error("The armory could not do that - the log has why."); continue; }

                var entry = DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " " + verb + ": " + weapon.Item3 + " (" + station + ")";
                _records.ArmoryLog.Add(entry);
                if (_records.ArmoryLog.Count > 100) _records.ArmoryLog.RemoveAt(0);
                _records.Dirty = true;
                _chat.Me("You", verb == "out" ? "sign the " + weapon.Item3 + " out at the armory window and check it." : "clear the " + weapon.Item3 + " and hand it back in at the armory window.");
            }
        }
    }
}
