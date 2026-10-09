using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace TextDispatch.Voice
{
    /// <summary>
    /// Push to talk: hold a key, say it, let go, and what was said goes into the box as if typed.
    ///
    /// The microphone is recorded with Windows' own MCI wave recorder - nothing to ship, nothing to
    /// install - and the recording is sent to a speech-to-text server on this machine: whisper.cpp's
    /// server by default, or anything that answers OpenAI's /v1/audio/transcriptions. Like the
    /// language model, it is local or it is nothing; no audio leaves the machine.
    ///
    /// Every MCI call is made on one worker thread of its own. MCI devices belong to the thread that
    /// opened them, and the game fiber is not a place to wait on a microphone or an HTTP request.
    /// </summary>
    internal sealed class VoiceInput
    {
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        private static extern int mciSendString(string command, StringBuilder returned, int length, IntPtr callback);

        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        private static extern bool mciGetErrorString(int error, StringBuilder text, int length);

        [DllImport("winmm.dll")]
        private static extern int waveInGetNumDevs();

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int key);

        /// <summary>How many recording devices Windows has. Zero means no microphone at all.</summary>
        public static int Microphones()
        {
            try { return waveInGetNumDevs(); }
            catch { return -1; }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);

        private const string Alias = "textdispatchvoice";
        private const int MinimumMs = 350;
        private const int MaximumMs = 30000;

        private enum Phase { Idle, Recording, Transcribing }

        private readonly Settings _settings;
        private readonly Action<string> _heard;
        private readonly Action<string> _problem;
        private readonly BlockingCollection<Action> _work = new BlockingCollection<Action>();
        private readonly ConcurrentQueue<Action> _results = new ConcurrentQueue<Action>();
        private readonly uint _process = (uint)Process.GetCurrentProcess().Id;
        private Thread _thread;

        private volatile Phase _phase = Phase.Idle;
        private int _startedAt;
        private int _shownAt;
        private int _noMicAt = int.MinValue / 2;
        private Keys _key = Keys.N;

        public VoiceInput(Settings settings, Action<string> heard, Action<string> problem)
        {
            _settings = settings;
            _heard = heard;
            _problem = problem;
            SetKey(settings.VoiceKey);
        }

        public bool Enabled { get { return _settings.VoiceInput; } }
        public string KeyName { get { return _key.ToString(); } }
        public string Endpoint { get { return EndpointFor(_settings); } }
        public string LastError { get; private set; }
        public string LastHeard { get; private set; }

        public string Describe()
        {
            var microphones = Microphones();
            return "Voice is " + (Enabled ? "on: hold " + KeyName + " to talk" : "off") +
                   ". Speech goes to " + Endpoint + " (" + Provider(_settings) + ")" +
                   (_settings.VoiceSend ? ", and what is heard is sent at once." : ", and what is heard waits in the box for Enter.") +
                   (microphones == 0 ? " Windows has NO microphone right now." :
                    microphones > 0 ? " Microphones: " + microphones + "." : "");
        }

        public bool SetKey(string name)
        {
            Keys parsed;
            if (string.IsNullOrWhiteSpace(name) || !Enum.TryParse(name.Trim(), true, out parsed) || parsed == Keys.None)
                return false;
            _key = parsed;
            return true;
        }

        public static string Provider(Settings settings)
        {
            return string.Equals(settings.VoiceProvider, "openai", StringComparison.OrdinalIgnoreCase) ? "openai" : "whispercpp";
        }

        public static string EndpointFor(Settings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.VoiceEndpoint)) return settings.VoiceEndpoint.Trim();
            return Provider(settings) == "openai"
                ? "http://127.0.0.1:8000/v1/audio/transcriptions"
                : "http://127.0.0.1:8080/inference";
        }

        // ------------------------------------------------------------------ the tick

        /// <summary>
        /// Called every tick on the game fiber. `allowed` is false while the box is open - the key is a
        /// letter then - and off duty.
        /// </summary>
        public void Update(bool allowed)
        {
            Action delivered;
            while (_results.TryDequeue(out delivered))
            {
                try { delivered(); }
                catch (Exception ex) { Log.Error("voice result", ex); }
            }

            if (!Enabled) { if (_phase == Phase.Recording) Cancel(); return; }

            var down = allowed && GameInFront() && (GetAsyncKeyState((int)_key) & 0x8000) != 0;
            var now = Environment.TickCount;

            switch (_phase)
            {
                case Phase.Idle:
                    if (down) Begin();
                    break;

                case Phase.Recording:
                    if (now - _shownAt > 400)
                    {
                        _shownAt = now;
                        try { Rage.Game.DisplaySubtitle("~r~REC~s~  TextDispatch is listening - let go of " + KeyName + " to send", 600); }
                        catch { }
                    }

                    if (!down || now - _startedAt > MaximumMs)
                    {
                        if (now - _startedAt < MinimumMs) Cancel();
                        else Finish();
                    }
                    break;
            }
        }

        private bool GameInFront()
        {
            try
            {
                uint owner;
                GetWindowThreadProcessId(GetForegroundWindow(), out owner);
                return owner == _process;
            }
            catch { return true; }
        }

        private void Begin()
        {
            // Said once per press rather than letting MCI fail with "no wave device ... in the current
            // format", which is what Windows says when the real answer is that there is no microphone.
            if (Microphones() == 0)
            {
                if (Environment.TickCount - _noMicAt > 10000)
                {
                    _noMicAt = Environment.TickCount;
                    LastError = "Windows has no microphone - plug one in, or check Settings > Privacy > Microphone";
                    _problem("Voice: " + LastError + ".");
                }
                return;
            }

            EnsureThread();
            _phase = Phase.Recording;
            _startedAt = Environment.TickCount;
            _shownAt = 0;

            _work.Add(() =>
            {
                Mci("close " + Alias, false);
                if (!Mci("open new type waveaudio alias " + Alias, true) ||
                    !Mci("set " + Alias + " time format ms bitspersample 16 channels 1 samplespersec 16000 bytespersec 32000 alignment 2", true) ||
                    !Mci("record " + Alias, true))
                {
                    Mci("close " + Alias, false);
                    _phase = Phase.Idle;
                    Report("the microphone could not be opened: " + LastError);
                }
            });
        }

        private void Cancel()
        {
            _phase = Phase.Idle;
            _work.Add(() => Mci("close " + Alias, false));
        }

        private void Finish()
        {
            _phase = Phase.Transcribing;
            try { Rage.Game.DisplaySubtitle("TextDispatch: working out what you said...", 1500); } catch { }

            _work.Add(() =>
            {
                var file = Path.Combine(Path.GetTempPath(), "textdispatch-voice.wav");
                try
                {
                    Mci("stop " + Alias, false);
                    if (File.Exists(file)) File.Delete(file);
                    var saved = Mci("save " + Alias + " \"" + file + "\" wait", true);
                    Mci("close " + Alias, false);

                    if (!saved || !File.Exists(file))
                    {
                        Report("the recording could not be saved: " + LastError);
                        return;
                    }

                    string error;
                    var text = Transcribe(_settings, File.ReadAllBytes(file), out error);
                    if (text == null) { Report(error); return; }

                    var cleaned = VoiceText.Clean(text);
                    Log.Line("voice: heard \"" + text.Trim() + "\"" + (cleaned != text.Trim() ? " -> \"" + cleaned + "\"" : ""));

                    if (cleaned.Length == 0)
                    {
                        _results.Enqueue(() => _problem("Nothing was heard - hold " + KeyName + " while you speak."));
                        return;
                    }

                    LastHeard = cleaned;
                    _results.Enqueue(() => _heard(cleaned));
                }
                catch (Exception ex) { Report(ex.GetType().Name + ": " + ex.Message); }
                finally
                {
                    _phase = Phase.Idle;
                    try { if (File.Exists(file)) File.Delete(file); } catch { }
                }
            });
        }

        private void Report(string problem)
        {
            LastError = problem;
            Log.Line("voice: " + problem);
            _results.Enqueue(() => _problem("Voice: " + problem + ". /voice says how it is set up."));
        }

        private bool Mci(string command, bool needed)
        {
            var code = mciSendString(command, null, 0, IntPtr.Zero);
            if (code == 0) return true;

            var text = new StringBuilder(256);
            mciGetErrorString(code, text, text.Capacity);
            if (needed) LastError = text.ToString();
            return false;
        }

        private void EnsureThread()
        {
            if (_thread != null) return;

            _thread = new Thread(() =>
            {
                foreach (var work in _work.GetConsumingEnumerable())
                {
                    try { work(); }
                    catch (Exception ex) { Log.Error("voice worker", ex); }
                }
            });
            _thread.IsBackground = true;
            _thread.Name = "TextDispatch voice";
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        // ------------------------------------------------------------------ the server

        /// <summary>
        /// Send a WAV file to the speech server and return the text, or null with `error` saying why.
        /// whisper.cpp's server takes a multipart form at /inference; an OpenAI-style server takes the
        /// same form at /v1/audio/transcriptions with a model name. Both answer {"text": "..."}.
        /// </summary>
        public static string Transcribe(Settings settings, byte[] wav, out string error)
        {
            error = null;
            var endpoint = EndpointFor(settings);

            try
            {
                var boundary = "----TextDispatch" + DateTime.Now.Ticks.ToString("x");
                var body = new MemoryStream();

                Field(body, boundary, "response_format", "json");
                if (Provider(settings) == "openai")
                    Field(body, boundary, "model", string.IsNullOrWhiteSpace(settings.VoiceModel) ? "whisper-1" : settings.VoiceModel.Trim());
                else
                    Field(body, boundary, "temperature", "0.0");

                var head = Encoding.UTF8.GetBytes("--" + boundary + "\r\n" +
                    "Content-Disposition: form-data; name=\"file\"; filename=\"voice.wav\"\r\n" +
                    "Content-Type: audio/wav\r\n\r\n");
                body.Write(head, 0, head.Length);
                body.Write(wav, 0, wav.Length);
                var tail = Encoding.UTF8.GetBytes("\r\n--" + boundary + "--\r\n");
                body.Write(tail, 0, tail.Length);

                var request = (HttpWebRequest)WebRequest.Create(endpoint);
                request.Method = "POST";
                request.ContentType = "multipart/form-data; boundary=" + boundary;
                request.Timeout = Math.Max(3000, settings.VoiceTimeoutMs);
                request.ReadWriteTimeout = request.Timeout;
                request.ContentLength = body.Length;

                using (var stream = request.GetRequestStream()) body.WriteTo(stream);

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    var raw = reader.ReadToEnd();
                    var root = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(raw);
                    object text;
                    if (root != null && root.TryGetValue("text", out text) && text != null) return text.ToString();

                    error = "the speech server at " + endpoint + " answered without any text";
                    return null;
                }
            }
            catch (WebException ex) when (ex.Status == WebExceptionStatus.ConnectFailure)
            {
                error = "no speech server is running at " + endpoint;
                return null;
            }
            catch (Exception ex)
            {
                error = "the speech server at " + endpoint + " failed: " + ex.Message;
                return null;
            }
        }

        private static void Field(Stream body, string boundary, string name, string value)
        {
            var bytes = Encoding.UTF8.GetBytes("--" + boundary + "\r\n" +
                "Content-Disposition: form-data; name=\"" + name + "\"\r\n\r\n" + value + "\r\n");
            body.Write(bytes, 0, bytes.Length);
        }

        /// <summary>Whether anything answers at the endpoint - for /voice test.</summary>
        public static string Probe(Settings settings)
        {
            // A second of silence: the smallest real request, and an honest test of the whole path.
            var wav = VoiceText.Silence(16000);
            string error;
            var text = Transcribe(settings, wav, out error);
            return text != null ? null : error;
        }
    }

    /// <summary>
    /// What a transcript becomes before it is typed in. Kept apart from the recorder, with no
    /// reference to the game or the microphone, so it can be checked on its own.
    /// </summary>
    internal static class VoiceText
    {
        private static readonly Dictionary<string, string> Codes = new Dictionary<string, string>
        {
            { "ten eight", "10-8" }, { "10 8", "10-8" }, { "108", "10-8" },
            { "ten seven", "10-7" }, { "10 7", "10-7" }, { "107", "10-7" },
            { "ten six", "10-6" }, { "10 6", "10-6" }, { "106", "10-6" },
            { "ten ninety seven", "10-97" }, { "10 97", "10-97" }, { "1097", "10-97" }, { "ten 97", "10-97" },
            { "ten ninety eight", "10-98" }, { "10 98", "10-98" }, { "1098", "10-98" }, { "ten 98", "10-98" },
            { "ten thirteen", "10-13" }, { "10 13", "10-13" }, { "1013", "10-13" }, { "ten 13", "10-13" },
            { "code three", "code 3" }, { "code 3", "code 3" },
            { "code four", "code 4" }, { "code 4", "code 4" },
            { "accept", "accept" }, { "decline", "decline" },
        };

        /// <summary>
        /// Tidy a transcript: drop the markers a speech model adds for silence or noise, and turn a
        /// spoken status code into the code itself - "Ten ninety-seven." is what Whisper writes for
        /// 10-97, and the box only recognises the code. A sentence starting "radio" or "dispatch"
        /// goes on the radio, which is how somebody would say it with their hands on the wheel.
        /// </summary>
        public static string Clean(string text)
        {
            var t = (text ?? "").Trim();

            // [BLANK_AUDIO], (music), *coughs* - none of it is speech.
            t = System.Text.RegularExpressions.Regex.Replace(t, @"\[[^\]]*\]|\([^\)]*\)|\*[^\*]*\*", " ");
            t = System.Text.RegularExpressions.Regex.Replace(t, @"\s+", " ").Trim();
            if (t.Length == 0) return "";

            var key = System.Text.RegularExpressions.Regex.Replace(t.ToLowerInvariant(), @"[^a-z0-9 ]", " ");
            key = System.Text.RegularExpressions.Regex.Replace(key, @"\s+", " ").Trim();
            // "ten-ninety-seven" arrives as "ten ninety seven" here; "10-97" as "10 97".
            string code;
            if (Codes.TryGetValue(key, out code)) return code;

            foreach (var lead in new[] { "radio", "dispatch" })
            {
                if (!key.StartsWith(lead + " ")) continue;
                var rest = t.Substring(lead.Length).TrimStart(' ', ',', '.', ':', '-');
                return rest.Length == 0 ? "" : "/r " + rest;
            }

            return t;
        }

        /// <summary>A WAV file of silence, 16-bit mono.</summary>
        public static byte[] Silence(int samples)
        {
            var data = samples * 2;
            var wav = new MemoryStream();
            var w = new BinaryWriter(wav);
            w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + data); w.Write(Encoding.ASCII.GetBytes("WAVE"));
            w.Write(Encoding.ASCII.GetBytes("fmt ")); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(16000); w.Write(32000); w.Write((short)2); w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data")); w.Write(data); w.Write(new byte[data]);
            w.Flush();
            return wav.ToArray();
        }
    }
}
