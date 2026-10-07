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

        /// <summary>Set while the player is on duty: the jobs box is not there then.</summary>
        private static bool _suppressed;

        /// <summary>When the duty state was last read - once a second is plenty, and a file read a frame
        /// is not.</summary>
        private static int _stateCheckedAt;

        /// <summary>Set when the heartbeat could not be read, so that is said once and not once a
        /// second.</summary>
        private static bool _stateReadFailed;

        /// <summary>True when OpenKey is auto, so this plugin may choose the key itself.</summary>
        private static bool _autoKey;

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
                _autoKey = openKey.Length == 0 || openKey.Equals("auto", StringComparison.OrdinalIgnoreCase);
                if (_autoKey)
                {
                    bool policeBox, policeOnDuty;
                    PoliceBoxState(out policeBox, out policeOnDuty);
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
        /// What the police box is doing, read from the file it refreshes while it runs.
        ///
        /// It cannot be asked - LSPDFR keeps its plugins in an AppDomain of their own - so it says so
        /// itself, twice a second, in a file beside its log: "on" or "off" and the time. Fresh means
        /// the police box is loaded in this session; the word means whether the player is on duty,
        /// which is the part that decides whether this box should be there at all.
        ///
        /// The freshness window is a minute rather than two seconds because this is read once, at
        /// startup, and a heartbeat that has just started should not be missed by a hair. Stale means
        /// a leftover from a session that has ended - which must not count, or the wrong key would be
        /// bound for the whole of the next one.
        /// </summary>
        private static void PoliceBoxState(out bool present, out bool onDuty)
        {
            present = false;
            onDuty = false;

            try
            {
                var game = TextJobs.Settings.GameFolder();
                if (game == null) return;

                var path = System.IO.Path.Combine(game, "Plugins", "LSPDFR", TextJobs.Settings.PoliceAliveFile);
                if (!System.IO.File.Exists(path)) return;
                if (DateTime.UtcNow - System.IO.File.GetLastWriteTimeUtc(path) >= TimeSpan.FromSeconds(60)) return;

                present = true;
                onDuty = System.IO.File.ReadAllText(path).Trim()
                             .StartsWith("on", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                // Once, not once a second: this is asked every second, and a heartbeat that cannot be
                // read is one line in the log rather than a transcript of them. It used to be
                // swallowed - which is the worst of both, because the box then stays up on duty and
                // nothing anywhere says why.
                if (_stateReadFailed) return;
                _stateReadFailed = true;
                Log.Line("could not read the police box's heartbeat: " + ex.Message +
                         " - the jobs box will stay up and keep the left arrow");
            }
        }

        /// <summary>
        /// Stand down while the player is on duty, and come back when they are not.
        ///
        /// The civilian jobs box is for off duty: on duty the police box is the interface, and it has
        /// /jobs and /job in it already. Leaving both up means two panels and two sets of keystrokes
        /// for the same errand, which is what the player pointed at.
        ///
        /// Standing down means all three things, not just closing: nothing drawn, nothing typed into,
        /// and nothing hidden from the rest of the game - a box that is invisible but still swallowing
        /// keystrokes would be worse than one that is simply there.
        /// </summary>
        private static void UpdateDutyState()
        {
            var now = Environment.TickCount;
            if (now - _stateCheckedAt < 1000) return;
            _stateCheckedAt = now;

            bool present, onDuty;
            PoliceBoxState(out present, out onDuty);

            if (onDuty && !_suppressed)
            {
                _suppressed = true;
                _box.IsOpen = false;

                // And stop hiding the keyboard from the rest of the game. Nothing else in this box is
                // asked to update while it is stood down, so a box that was open at the moment the
                // player went on duty would otherwise keep every keystroke to itself for the whole
                // patrol - invisible, and the one failure this design says is worse than being seen.
                if (_capture != null) _capture.Update(false);

                Log.Line("on duty: the jobs box stands down - the police box is the interface, and it has /jobs");
                try { GTA.UI.Notification.PostTicker("Jobs box off while on duty - the police box has /jobs.", false, false); }
                catch { }
                return;
            }

            if (!onDuty && _suppressed)
            {
                _suppressed = false;

                // The key is decided again, because what it depends on has changed: with the police
                // box loaded its left arrow is still its own, so this box takes F9; without it, the
                // left arrow is free and is the obvious key. Only when the setting is auto - a key the
                // player named themselves is not overruled.
                if (_autoKey && _input != null)
                {
                    var key = present ? "F9" : "Left";
                    if (_input.SetOpenKey(key)) Log.Line("OpenKey is auto: off duty, so this box is on " + _input.OpenKeyDescription);
                }

                _box.Notice("Jobs box up - off duty. " + (_input == null ? "" : _input.OpenKeyDescription + " opens it."));
                Log.Line("off duty: the jobs box is back");
                try { GTA.UI.Notification.PostTicker("Jobs box is up - off duty.", false, false); }
                catch { }
            }
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
                UpdateDutyState();

                // On duty the box is not here at all: not drawn, not typed into, and - the part that
                // would be easy to get wrong - not hiding keystrokes from the rest of the game either.
                if (_suppressed) return;

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
