using System;
using Rage;
using TextDispatch.Chat;
using TextDispatch.Dispatch;
using TextDispatch.Lspdfr;
using TextDispatch.Records;

namespace TextDispatch.Commands
{
    /// <summary>
    /// Ultimate Backup's work, done by TextDispatch: a K9 unit with a dog that sniffs a car, spike strips,
    /// road blocks, and sending every unit home. Spikes and road blocks still go to Ultimate Backup or
    /// Policing Redefined first when one is installed; without them, TextDispatch places its own.
    /// </summary>
    internal sealed partial class CommandRouter
    {
        /// <summary>Spike strips, road blocks and the K9. Set by the plugin after it is built.</summary>
        internal Tactics Tactics;

        /// <summary>
        /// /k9: send a K9 unit - a patrol car with a dog in the back. /k9 sniff (or /k9 search) puts the dog
        /// round the car you stopped: it sits at the door when there is something in it, and that alert is
        /// probable cause for '/search car'.
        /// </summary>
        private void K9(string argument)
        {
            if (Tactics == null) { _chat.Error("The K9 is not running."); return; }
            var verb = (argument ?? "").Trim().ToLowerInvariant();

            if (verb == "sniff" || verb == "search" || verb == "car")
            {
                if (!Tactics.K9OnScene(60f)) { _chat.Error("No K9 on scene - '/k9' sends one, then wait for it to arrive."); return; }
                var car = _api.PulloverVehicle() ?? _api.NearestOtherVehicle(15f);
                if (car == null || !car.Exists() || car == Tactics.K9Car) { _chat.Error("No car to put the dog round - the one you stopped, or one beside you."); return; }

                string plate = null, model = null;
                try { plate = car.LicensePlate.Trim(); model = car.Model.Name; } catch { }
                PersonRecord driver = null;
                Ped driverPed = _api.PulloverSuspect();
                try { if (driverPed == null) driverPed = car.Driver; } catch { }
                if (driverPed != null && driverPed.Exists())
                {
                    var handle = unchecked((int)driverPed.Handle.Value);
                    driver = _records.EnsurePerson(handle, Dialogue.Identities.NameFor(handle));
                }

                // What a search would find decides it - the dog smells what is really there.
                bool illegal;
                _records.WouldFind(plate, model, driver, out illegal);
                var drugs = illegal;
                _chat.Write(ChatChannel.Radio, "[K9]", "Handler: sending the dog round the " + (model ?? "vehicle") + " now.");
                var call = _dispatch.ReportCallName;
                Tactics.Sniff(car, drugs, alerted =>
                {
                    _chat.Write(ChatChannel.Radio, "[K9]", alerted ? "Handler: positive alert - he's sat down at the driver's door. You've got your probable cause."
                                                                   : "Handler: nothing. He's not interested in it.");
                    if (driver != null) _records.Act(driver, call, "had the K9 sniff the vehicle (" + plate + "), which " + (alerted ? "gave a positive alert" : "did not alert"));
                    if (alerted) _chat.Notice("  The alert is probable cause - '/search car' searches it without consent.");
                });
                return;
            }

            if (Tactics.K9OnScene(400f)) { _chat.Notice("The K9 is already here. '/k9 sniff' puts the dog round the car."); return; }

            var vehicle = _api.RequestBackup("Code3", "LocalUnit");
            if (vehicle == null) { _chat.Error("No K9 unit available - LSPDFR did not send one."); return; }
            Tactics.MakeK9(vehicle);
            _dispatch.Track("a K9 unit", vehicle);
            _dispatch.Report(DispatcherIntent.Backup, "10-13, K9 unit responding.");
            _chat.Notice("  K9 en route. When it is on scene, '/k9 sniff' sends the dog round the car.");
        }

        /// <summary>/spikes: a stinger across the road ahead of what you are chasing - Ultimate Backup's, or TextDispatch's own.</summary>
        private void Spikes()
        {
            if (Plugin.Bridge != null)
            {
                Plugin.Bridge.Ensure();
                var who = Plugin.Bridge.Backup("spikes");
                if (who != null) { _chat.Notice(who + " is putting spike strips down ahead."); return; }
            }
            if (Tactics == null) { _chat.Error("Spike strips are not running."); return; }

            string where;
            if (!Tactics.Spikes(out where)) { _chat.Error("There is no road ahead to put a strip on."); return; }
            _chat.Write(ChatChannel.Radio, "[RADIO]", "Unit deploying stop sticks " + where + ". Back off and let them hit it.");
            _records.IncidentNote("spike strips deployed");
            _records.Act(_records.LastSubject, _dispatch.ReportCallName, "requested spike strips, deployed " + where);
        }

        /// <summary>/roadblock: two cars and their officers across the road ahead - Ultimate Backup's, or TextDispatch's own.</summary>
        private void RoadBlock()
        {
            if (Plugin.Bridge != null)
            {
                Plugin.Bridge.Ensure();
                var who = Plugin.Bridge.Backup("roadblock");
                if (who != null) { _chat.Notice(who + " is setting up a road block ahead."); return; }
            }
            if (Tactics == null) { _chat.Error("Road blocks are not running."); return; }

            string where;
            if (!Tactics.Roadblock(out where)) { _chat.Error("There is no road ahead to block."); return; }
            _chat.Write(ChatChannel.Radio, "[RADIO]", "Two units setting up a road block " + where + ".");
            _records.IncidentNote("road block set up");
            _records.Act(_records.LastSubject, _dispatch.ReportCallName, "requested a road block, set up " + where);
        }

        /// <summary>/dismiss: every unit sent to you goes back to patrol, and what was put on the road comes up.</summary>
        private void Dismiss()
        {
            var units = _dispatch.DismissUnits();
            var placed = Tactics == null ? 0 : Tactics.Clear();
            string bridge = null;
            if (Plugin.Bridge != null) { Plugin.Bridge.Ensure(); bridge = Plugin.Bridge.DismissAll(); }

            _chat.Radio("You: " + _dispatch.Unit + ", code 4, all units can clear.");
            _chat.Write(ChatChannel.Dispatch, "[DISPATCH]", "Copy, code 4. " + (units > 0 ? units + " unit(s) back in service" : "Units clearing") +
                        (placed > 0 ? ", " + placed + " strip(s), block(s) and the K9 picked up" : "") + (bridge != null ? ", " + bridge + " stood down too" : "") + ".");
        }
    }
}
