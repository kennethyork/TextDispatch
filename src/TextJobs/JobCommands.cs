using System;
using System.Collections.Generic;
using System.Globalization;
using GTA;
using GTA.Math;
using GTA.Native;
using TextDispatch;
using TextDispatch.Chat;
using TextDispatch.Jobs;

namespace TextJobs
{
    /// <summary>
    /// The commands this box understands.
    ///
    /// Deliberately short: it exists for one mod's jobs, and everything a police plugin would offer -
    /// callouts, stops, dispatch, records - belongs to LSPDFR and to TextDispatch. What is here is the
    /// list, one job in detail, where it starts, and the few settings worth changing without leaving
    /// the game.
    /// </summary>
    internal sealed class JobCommands
    {
        private readonly ChatBox _box;
        private readonly Settings _settings;
        private readonly KeyCapture _capture;

        /// <summary>The one job marker this box has on the map, so asking for another does not leave a trail.</summary>
        private static Blip _blip;

        /// <summary>Set by Main, which owns the key and the ini: how to change the key that opens the box.</summary>
        public Action<string> ChangeKey;

        public JobCommands(ChatBox box, Settings settings, KeyCapture capture)
        {
            _box = box;
            _settings = settings;
            _capture = capture;
        }

        public static bool TryCorner(string text, out Corner corner)
        {
            switch ((text ?? "").Trim().ToLowerInvariant())
            {
                case "top-left": corner = Corner.TopLeft; return true;
                case "top-right": corner = Corner.TopRight; return true;
                case "bottom-left": corner = Corner.BottomLeft; return true;
                case "bottom-right": corner = Corner.BottomRight; return true;
                default: corner = Corner.TopRight; return false;
            }
        }

        public void Handle(string line)
        {
            var text = (line ?? "").Trim();
            if (text.Length == 0) return;

            try
            {
                if (text[0] != '/')
                {
                    _box.Notice("This box is for DriverJobs V's jobs: /jobs lists them, /job <name> describes one.");
                    return;
                }

                var body = text.Substring(1);
                var split = body.IndexOf(' ');
                var name = (split < 0 ? body : body.Substring(0, split)).Trim().ToLowerInvariant();
                var argument = split < 0 ? "" : body.Substring(split + 1).Trim();

                switch (name)
                {
                    case "jobs": ListJobs(argument); return;
                    case "job": DescribeJob(argument); return;
                    case "help": Help(); return;
                    case "clear":
                    case "cls": _box.Clear(); return;
                    case "key": SetKey(argument); return;
                    case "pos":
                    case "corner": SetPosition(argument); return;
                    case "hide":
                    case "typing": Hide(argument); return;
                    case "hardware": Hardware(argument); return;
                    default:
                        _box.Error("No such command: /" + name + "  -  /help lists them.");
                        return;
                }
            }
            catch (Exception ex)
            {
                Log.Error("command", ex);
                _box.Error("That did not work - the log has the detail.");
            }
        }

        private void Help()
        {
            _box.Notice("TextJobs - DriverJobs V's jobs, in text.");
            _box.Notice("  /jobs [filter]          every job, with what each pays");
            _box.Notice("  /job <name or number>   what the work is, what you drive, and where it starts");
            _box.Notice("  /key <key>              which key opens this box (currently " + _settings.OpenKey + ")");
            _box.Notice("  /pos <corner>           top-left, top-right, bottom-left, bottom-right");
            _box.Notice("  /hide on|off            whether what you type is hidden from the other plugins");
            _box.Notice("  /hardware on|off        whether a hidden key is released in the hardware state too");
            _box.Notice("  /clear                  empty the box");
            _box.Notice("Taking a job is DriverJobs' own: be at its place, or use its menu (Shift+J).");
        }

        private void ListJobs(string filter)
        {
            var jobs = CivilianJobs.All();
            if (jobs.Count == 0)
            {
                _box.Error("No civilian jobs to list: " + CivilianJobs.Problem + ".");
                _box.Notice("They come with DriverJobs V - scripts\\DriverJobsData\\Missions\\Jobs.xml.");
                return;
            }

            var matching = new List<CivilianJob>();
            foreach (var job in jobs)
                if (filter.Length == 0 || job.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) matching.Add(job);

            if (matching.Count == 0)
            {
                _box.Error("No job matches '" + filter + "'.");
                return;
            }

            const int page = 12;
            _box.Notice("Civilian jobs" + (filter.Length == 0 ? "" : " matching '" + filter + "'") +
                        ": " + matching.Count + "  (DriverJobs V)");

            for (int i = 0; i < matching.Count && i < page; i++)
                _box.Say("  " + (i + 1) + ". " + matching[i].Name + "  -  " + matching[i].PayText());

            if (matching.Count > page)
                _box.Say("  ... " + (matching.Count - page) + " more - narrow it with '/jobs <text>'.");

            _box.Say("Mark one with '/job <name or number>'. The whole list is in the log.");

            foreach (var job in matching) Log.Line("job: " + job.LogLine());
        }

        private void DescribeJob(string argument)
        {
            if (argument.Length == 0)
            {
                _box.Notice("Usage: /job <name or number>  -  /jobs lists them.");
                return;
            }

            string problem;
            var job = CivilianJobs.Find(argument, out problem);
            if (job == null)
            {
                _box.Error("No single job matches '" + argument + "'" + (problem == null ? "" : ": " + problem) + ".");
                _box.Notice("Try /jobs to see the list.");
                return;
            }

            _box.Notice(job.Name + "  -  " + job.KindText());

            for (int i = 0; i < job.Lines.Length && i < 4; i++)
                if (job.Lines[i].Length > 0) _box.Say("  " + job.Lines[i]);
            if (job.Lines.Length > 4) _box.Say("  ...");

            _box.Say("  " + job.PayText() + ".  You drive " + job.VehicleText() + ".");
            if (job.HasUniform) _box.Say("  This one still puts you in its own work clothes.");

            if (job.HasStart) Mark(job);
            else _box.Say("  No start point is given in the file - take it from the job menu (Shift+J).");
        }

        /// <summary>
        /// Put a job's start on the map, replacing the last one this box marked: a blip with its name,
        /// and the sat-nav route to it.
        /// </summary>
        private void Mark(CivilianJob job)
        {
            try
            {
                if (_blip != null)
                {
                    try { _blip.Delete(); } catch { }
                    _blip = null;
                }

                var position = new Vector3(job.X, job.Y, job.Z);
                _blip = Blip.Create(position);
                _blip.Name = job.Name;
                _blip.Color = BlipColor.Yellow;
                _blip.ShowRoute = true;

                try { Function.Call(Hash.SET_NEW_WAYPOINT, job.X, job.Y); }
                catch (Exception ex) { Log.Error("waypoint", ex); }

                float away = 0f;
                try { away = Game.Player.Character.Position.DistanceTo(position); } catch { }

                _box.Say("  Marked on the map" + (away > 1f
                    ? " - " + Math.Round(away / 1000f, 1).ToString(CultureInfo.InvariantCulture) + " km away"
                    : "") + ". Drive there to take it" + (job.Remote
                    ? ", or open the job menu (Shift+J) anywhere in a suitable vehicle."
                    : "."));
            }
            catch (Exception ex)
            {
                Log.Error("blip", ex);
                _box.Say("  Its start is at " +
                         string.Format(CultureInfo.InvariantCulture, "{0:0.#}, {1:0.#}", job.X, job.Y) + ".");
            }
        }

        private void SetKey(string argument)
        {
            if (argument.Length == 0)
            {
                _box.Notice("The box opens with " + _settings.OpenKey + ".");
                _box.Notice("Usage: /key F8   -   any key name: F8, Right, Numpad0, OemQuestion.");
                return;
            }

            if (ChangeKey != null) ChangeKey(argument);
        }

        private void SetPosition(string argument)
        {
            Corner corner;
            if (!TryCorner(argument, out corner))
            {
                _box.Notice("Usage: /pos top-left | top-right | bottom-left | bottom-right");
                return;
            }

            _box.Position = corner;
            _settings.ChatPosition = (argument ?? "").Trim().ToLowerInvariant();
            _settings.Save();
            _box.Notice("The box now sits " + _settings.ChatPosition + ", saved to the ini.");
        }

        private void Hide(string argument)
        {
            bool on;
            if (!Parse(argument, _capture.Enabled, out on))
            {
                _box.Notice("Hiding what you type from the other plugins is " + (_capture.Enabled ? "on" : "off") + ".");
                _box.Notice("Usage: /hide on | off");
                return;
            }

            _capture.SetEnabled(on);
            _settings.BlockOtherModsKeys = on;
            _settings.Save();
            _box.Notice(on
                ? "Typing is hidden from the other plugins again, and that is saved to the ini."
                : "Typing is no longer hidden: the other plugins see it, as they did before.");
        }

        private void Hardware(string argument)
        {
            bool on;
            if (!Parse(argument, _capture.Hardware, out on))
            {
                _box.Notice("Hidden keys are " + (_capture.Hardware ? "also" : "not") + " released in the hardware state.");
                _box.Notice("Usage: /hardware on | off  -  this is what stops a plugin that reads the keyboard directly.");
                return;
            }

            _capture.SetHardware(on);
            _settings.HideHardwareKeys = on;
            _settings.Save();
            _box.Notice(on
                ? "Hidden keys now release in the hardware state too, saved to the ini."
                : "Hidden keys are no longer released in the hardware state, saved to the ini.");
        }

        private static bool Parse(string argument, bool current, out bool value)
        {
            switch ((argument ?? "").Trim().ToLowerInvariant())
            {
                case "on": case "1": case "true": value = true; return true;
                case "off": case "0": case "false": value = false; return true;
                case "": value = current; return false;
                default: value = current; return false;
            }
        }
    }
}
