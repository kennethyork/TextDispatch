using System;
using System.Windows.Forms;
using GTA;
using GTA.UI;
using TextDispatch;
using TextDispatch.Chat;
using TextDispatch.Jobs;

namespace TextJobs
{
    /// <summary>
    /// A chat box for DriverJobs V, and nothing else.
    ///
    /// TextDispatch is the police side and is an LSPDFR plugin: it cannot load without LSPDFR, and it
    /// does not exist off duty. This is the other half of that idea - the same box, the same hiding of
    /// what you type from the other plugins, the same reading of the jobs mod's own file - written as
    /// a ScriptHookVDotNet script, which is what DriverJobs itself is. So it works with or without
    /// LSPDFR, on duty or off, and it needs nothing installed but the game and the jobs mod.
    ///
    /// What it deliberately is not: a second police plugin. No callouts, no traffic stops, no dispatch
    /// - those are LSPDFR's, and TextDispatch's.
    /// </summary>
    public class Main : Script
    {
        /// <summary>The key the self-check hides: F24, which no game and no menu uses.</summary>
        private const int ProbeKey = 0x87;

        private static Settings _settings;
        private static ChatBox _box;
        private static KeyCapture _capture;
        private static TypedInput _input;
        private static JobCommands _commands;
        private static bool _sawProbeKey;

        public Main()
        {
            try
            {
                // Qualified because ScriptHookVDotNet's own Script base class has a Settings property,
                // which otherwise shadows this plugin's settings type inside a script.
                _settings = TextJobs.Settings.Load();

                Corner corner;
                if (!JobCommands.TryCorner(_settings.ChatPosition, out corner))
                {
                    corner = Corner.TopRight;
                    Log.Line("ChatPosition '" + _settings.ChatPosition + "' is not a corner; using top-right");
                }

                _box = new ChatBox
                {
                    Position = corner,
                    Margin = _settings.ChatMargin,
                    FontSize = _settings.FontSize,
                    UiScale = _settings.UiScale,
                    Lines = _settings.Lines,
                    TranscriptSeconds = _settings.TranscriptSeconds
                };

                // The job list is DriverJobs' own file, found once here so /jobs answers instantly and
                // the log says where it looked.
                CivilianJobs.Locate(TextJobs.Settings.Folder());
                var jobs = CivilianJobs.All();
                Log.Line(jobs.Count > 0
                    ? "jobs: " + jobs.Count + " civilian jobs, from " + CivilianJobs.Location
                    : "jobs: no civilian job list - " + CivilianJobs.Problem);

                // The keyboard. Same code as TextDispatch's: a system-wide hook and a hook on the game
                // window, so what is typed here is hidden from everything else in the game.
                _capture = new KeyCapture(key => TakeProbeSeen());
                _capture.SetEnabled(_settings.BlockOtherModsKeys);
                _capture.SetHardware(_settings.HideHardwareKeys);
                _capture.Start();

                _commands = new JobCommands(_box, _settings, _capture);

                _input = new TypedInput(_box, _commands.Handle, _capture);

                // Which key opens this box. "auto" is the left arrow - the key the player already
                // knows - unless TextDispatch's box is loaded in this game, where the left arrow
                // belongs to it and this box uses F9 instead.
                var openKey = _settings.OpenKey == null ? "auto" : _settings.OpenKey.Trim();
                if (openKey.Length == 0 || openKey.Equals("auto", StringComparison.OrdinalIgnoreCase))
                {
                    var policeBox = PoliceBoxLoaded();
                    openKey = policeBox ? "F9" : "Left";
                    Log.Line(policeBox
                        ? "OpenKey is auto: TextDispatch's box is loaded, so the left arrow is its key and this box uses F9"
                        : "OpenKey is auto: no TextDispatch box in this session, so the left arrow opens this box");
                }

                if (!_input.SetOpenKey(openKey))
                {
                    // Said in the box as well as the log: "the box will not open" is otherwise
                    // indistinguishable from "the key is wrong", and the player is the one who knows.
                    Log.Line("OpenKey '" + openKey + "' is not a key name; sticking with the default");
                    _box.Error("OpenKey in TextJobs.ini is not a key name - using " + _input.OpenKeyDescription + ".");
                }

                _box.OpenHint = "Type a job name or /jobs. Esc closes.";

                // The one thing a command cannot do for itself: the key belongs to the input and the
                // ini, both of which live here.
                _commands.ChangeKey = delegate (string wanted)
                {
                    if (!_input.SetOpenKey(wanted))
                    {
                        _box.Error("'" + wanted + "' is not a key name - try F8, Right, Numpad0, OemQuestion.");
                        return;
                    }

                    _settings.OpenKey = wanted;
                    _settings.Save();
                    _box.OpenHint = "Type a job name or /jobs. Esc closes.";
                    _box.Notice("The box now opens with " + _input.OpenKeyDescription + ", saved to the ini.");
                };

                KeyDown += OnKeyDown;
                Tick += OnTick;
                Interval = 0;

                Log.Line("starting: driverjobs chat box, script location " + TextJobs.Settings.Folder());
                Log.Line("keys: " + _capture.Note);
                Log.Line("log: " + Log.Path + "  (game " + (TextJobs.Settings.GameFolder() ?? "not found from the process") + ")");

                // If the log is not where it should be, the box says where it is. That is one line
                // while something is wrong and none when nothing is, which is the right way round.
                var wanted = System.IO.Path.Combine(TextJobs.Settings.Folder(), "TextJobs.log");
                if (!string.Equals(Log.Path, wanted, StringComparison.OrdinalIgnoreCase))
                    _box.Notice("Log is at " + Log.Path + " (not the scripts folder).");

                _box.Notice("TextJobs ready. DriverJobs V's work, in text. " + _input.OpenKeyDescription + " opens the box.");
                if (jobs.Count > 0)
                    _box.Notice("/jobs lists all " + jobs.Count + " of them; /job <name> describes one and marks it.");
                else
                    _box.Error("No job list found: " + CivilianJobs.Problem + ".");

                try { GTA.UI.Notification.PostTicker("TextJobs loaded - press " + _input.OpenKeyDescription + " for the job list.", false, false); }
                catch { }
            }
            catch (Exception ex)
            {
                Log.Error("startup", ex);
            }
        }

        /// <summary>
        /// Whether TextDispatch's chat box is loaded in this game.
        ///
        /// It cannot be asked, so it says so itself: TextDispatch refreshes a file beside its log
        /// about twice a second while it is running, and only a session that is going on now has a
        /// fresh one. The window is a minute rather than two seconds because this is read once, at
        /// startup, and a heartbeat that has just started should not be missed by a hair.
        /// </summary>
        private static bool PoliceBoxLoaded()
        {
            try
            {
                var game = TextJobs.Settings.GameFolder();
                if (game == null) return false;

                var path = System.IO.Path.Combine(game, "Plugins", "LSPDFR", TextJobs.Settings.PoliceAliveFile);
                if (!System.IO.File.Exists(path)) return false;

                return DateTime.UtcNow - System.IO.File.GetLastWriteTimeUtc(path) < TimeSpan.FromSeconds(60);
            }
            catch { return false; }
        }

        /// <summary>
        /// The self-check's eyes, and the reason it is this event rather than a keyboard read:
        /// ScriptHookVDotNet is what every other script in this game listens through, so "does
        /// ScriptHookVDotNet still see a key we hid" is the question worth asking.
        /// </summary>
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if ((int)e.KeyCode == ProbeKey) _sawProbeKey = true;
        }

        private static bool TakeProbeSeen()
        {
            var seen = _sawProbeKey;
            _sawProbeKey = false;
            return seen;
        }

        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                _capture.Update(_box.IsOpen);
                _input.Update();
                _box.Render();
            }
            catch (Exception ex)
            {
                Log.Error("tick", ex);
            }
        }
    }
}
