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
                    Scale = _settings.FontScale,
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
                if (!_input.SetOpenKey(_settings.OpenKey))
                    Log.Line("OpenKey '" + _settings.OpenKey + "' is not a key name; sticking with F8");

                _box.OpenHint = "press " + _input.OpenKeyDescription + " for the job list";

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
                    _box.OpenHint = "press " + _input.OpenKeyDescription + " for the job list";
                    _box.Notice("The box now opens with " + _input.OpenKeyDescription + ", saved to the ini.");
                };

                KeyDown += OnKeyDown;
                Tick += OnTick;
                Interval = 0;

                Log.Line("starting: driverjobs chat box, script location " + TextJobs.Settings.Folder());
                Log.Line("keys: " + _capture.Note);

                _box.Notice("TextJobs ready. DriverJobs V's work, in text.");
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
