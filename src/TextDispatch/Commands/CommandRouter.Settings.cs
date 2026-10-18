using System;
using System.Collections.Generic;
using System.Globalization;
using Rage;
using TextDispatch.Chat;
using TextDispatch.Dialogue;
using TextDispatch.Records;

namespace TextDispatch.Commands
{
    /// <summary>
    /// /settings, and EMS in text: the options on one screen, and the medical side of a call - vitals,
    /// treatment and a patient care report.
    /// </summary>
    internal sealed partial class CommandRouter
    {
        // ------------------------------------------------------------------ settings

        /// <summary>
        /// /settings: what the box is set to, on one screen, with how to change each - and /settings
        /// &lt;name&gt; &lt;value&gt; to change the simple ones without the ini. Saved to TextDispatch.ini.
        /// </summary>
        private void SettingsCommand(string argument)
        {
            var words = (argument ?? "").Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 2)
            {
                var name = words[0].ToLowerInvariant();
                var value = words[1].Trim().ToLowerInvariant();
                bool on = value == "on" || value == "1" || value == "yes" || value == "true";
                bool off = value == "off" || value == "0" || value == "no" || value == "false";
                int number;

                switch (name)
                {
                    case "autocalls": case "autocallouts":
                        if (!on && !off) break;
                        _settings.AutoCallouts = on; Saved("Auto calls", on ? "on" : "off"); return;
                    case "autoaccept":
                        if (!on && !off) break;
                        _settings.AutoAccept = on; Saved("Auto accept", on ? "on" : "off"); return;
                    case "callseconds": case "autocalloutseconds":
                        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) || number < 30 || number > 3600) break;
                        _settings.AutoCalloutSeconds = number; Saved("Seconds between auto calls", number.ToString()); return;
                    case "traffic": case "radiotraffic":
                        if (!on && !off) break;
                        _settings.RadioTraffic = on; Saved("Other units on the radio", on ? "on" : "off"); return;
                    case "warrants": case "warrantcalls":
                        if (!on && !off) break;
                        _settings.WarrantCalls = on; Saved("Warrant service calls", on ? "on" : "off"); return;
                    case "tips": case "phonetips":
                        if (!on && !off) break;
                        _settings.PhoneTips = on; Saved("Texts from contacts", on ? "on" : "off"); return;
                    case "certs": case "requirecertifications":
                        if (!on && !off) break;
                        _settings.RequireCertifications = on; Saved("Certifications required", on ? "on" : "off"); return;
                    case "chatter":
                        SetChatter(value); return;
                    case "key": case "openkey":
                        SetKey(words[1].Trim()); return;
                    case "typing":
                        SetBlock(value); return;
                }

                _chat.Error("'/settings " + words[0] + " " + words[1] + "' is not one I can change. /settings lists them.");
                return;
            }

            _chat.Notice("TextDispatch settings - '/settings <name> <value>' changes one, and it is saved to TextDispatch.ini:");
            Row("autocalls", _settings.AutoCallouts ? "on" : "off", "dispatch raises calls by itself");
            Row("callseconds", _settings.AutoCalloutSeconds.ToString(CultureInfo.InvariantCulture), "seconds between those calls (30-3600)");
            Row("autoaccept", _settings.AutoAccept ? "on" : "off", "take a call without typing 'accept'");
            Row("chatter", _settings.Chatter, "how much radio you see: quiet, brief, full");
            Row("traffic", _settings.RadioTraffic ? "on" : "off", "other units heard on the radio");
            Row("warrants", _settings.WarrantCalls ? "on" : "off", "dispatch sends you after people from your records");
            Row("tips", _settings.PhoneTips ? "on" : "off", "contacts text you on a shift");
            Row("certs", _settings.RequireCertifications ? "on" : "off", "K9 and air support need their certification");
            Row("key", Plugin.OpenKeyDescription, "the key that opens the box - change it if another mod uses it");
            Row("typing", _settings.BlockOtherModsKeys ? "hidden" : "visible", "whether other plugins see what you type (/typing)");
            _chat.Notice("  The rest - the model, ranges, the box's look - is in Plugins\\LSPDFR\\TextDispatch.ini, or /pos /ui /font /lines.");
        }

        private void Row(string name, string value, string what)
        {
            _chat.Notice("  " + name.PadRight(12) + value.PadRight(10) + what);
        }

        private void Saved(string what, string value)
        {
            _settings.Save();
            _chat.Notice(what + ": " + value + ". Saved to TextDispatch.ini.");
        }

        // ------------------------------------------------------------------ EMS

        /// <summary>
        /// /vitals: the patient in front of you - pulse, blood pressure, breathing, oxygen, how responsive,
        /// pupils, skin - from how hurt they really are and how much they have had. Kept for the report.
        /// </summary>
        private void Vitals()
        {
            var ped = Subject(4f);
            if (ped == null) { _chat.Error("No patient close enough - kneel beside them."); return; }
            var handle = unchecked((int)ped.Handle.Value);
            var person = PersonInFront();
            var rng = new Random(unchecked(handle * 113 + Environment.TickCount / 120000));

            bool dead = false;
            float health = 1f;
            try
            {
                dead = ped.IsDead;
                health = Math.Max(0f, Math.Min(1f, (ped.Health - 100f) / Math.Max(1f, ped.MaxHealth - 100f)));
            }
            catch { }
            var bac = Sobriety.Bac(handle);

            string line;
            if (dead)
                line = "No pulse. Not breathing. Pupils fixed and dilated. GCS 3. Skin cool and pale.";
            else
            {
                var hurt = 1f - health;
                var pulse = 72 + (int)(hurt * 55) + rng.Next(12) + (bac > 0.1 ? 10 : 0);
                var systolic = 124 - (int)(hurt * 45) + rng.Next(10);
                var diastolic = 78 - (int)(hurt * 25) + rng.Next(6);
                var breaths = 14 + (int)(hurt * 12) + rng.Next(3);
                var spo2 = 99 - (int)(hurt * 12) - rng.Next(2);
                var gcs = hurt > 0.8f ? 8 + rng.Next(3) : hurt > 0.5f ? 12 + rng.Next(2) : bac >= Sobriety.Limit ? 13 + rng.Next(2) : 15;
                line = "Pulse " + pulse + ", BP " + systolic + "/" + diastolic + ", resp " + breaths + ", SpO2 " + spo2 + "%, GCS " + gcs +
                       ". Pupils " + (bac >= Sobriety.Limit ? "sluggish" : "equal and reactive") + ". Skin " +
                       (hurt > 0.5f ? "pale and clammy" : "warm and dry") + "." +
                       (hurt > 0.3f ? " Visible injuries - bleeding" + (hurt > 0.6f ? " heavily" : "") + "." : "") +
                       (bac > 0 ? " Smell of alcohol." : "");
            }

            _chat.Me("You", "kneel, check the airway, take a pulse and put the cuff and the oximeter on.");
            _chat.Do(line);
            _records.Act(person, _dispatch.ReportCallName, "took a set of vitals: " + line.TrimEnd('.'));
            if (!dead && health < 0.5f) _chat.Notice("  Serious. '/treat bleeding', '/treat oxygen', then '/ems hospital' or '/ems call' for an ambulance.");
        }

        private static readonly Dictionary<string, string[]> Treatments = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "bleeding", new[] { "pack the wound and hold direct pressure, then a pressure bandage.", "controlled the bleeding with direct pressure and a pressure dressing" } },
            { "tourniquet", new[] { "put a tourniquet high and tight above the wound and note the time.", "applied a tourniquet" } },
            { "oxygen", new[] { "put a non-rebreather mask on at fifteen litres.", "gave high-flow oxygen" } },
            { "splint", new[] { "splint the limb above and below the injury.", "splinted a suspected fracture" } },
            { "cpr", new[] { "start compressions - thirty, then two breaths, and keep going.", "performed CPR" } },
            { "aed", new[] { "put the pads on and stand clear while the AED analyses.", "applied the AED" } },
            { "narcan", new[] { "give naloxone, four milligrams in the nose.", "administered naloxone" } },
            { "collar", new[] { "hold the head still and fit a cervical collar.", "applied a cervical collar" } },
            { "recovery", new[] { "roll them into the recovery position and keep the airway open.", "placed them in the recovery position" } },
            { "burns", new[] { "cool the burn with water and cover it with cling film.", "cooled and dressed burns" } },
        };

        /// <summary>
        /// /treat &lt;what&gt;: first aid on the patient in front of you - bleeding, a tourniquet, oxygen, a splint,
        /// CPR, the AED, naloxone, a collar, the recovery position, burns. Stops bleeding where it can; kept
        /// for the report.
        /// </summary>
        private void Treat(string argument)
        {
            var what = (argument ?? "").Trim().ToLowerInvariant();
            string[] treatment = null;
            foreach (var pair in Treatments)
                if (what.Length > 0 && (pair.Key.StartsWith(what) || what.Contains(pair.Key))) { treatment = pair.Value; what = pair.Key; break; }
            if (treatment == null)
            {
                _chat.Error("Usage: /treat <" + string.Join("|", new List<string>(Treatments.Keys).ToArray()) + ">");
                return;
            }

            var ped = Subject(4f);
            if (ped == null) { _chat.Error("No patient close enough - kneel beside them."); return; }
            var person = PersonInFront();

            _chat.Me("You", treatment[0]);
            try
            {
                // Stopping the bleeding is the one thing the game can show: they stop losing health and get a little back.
                if (!ped.IsDead && (what == "bleeding" || what == "tourniquet") && ped.Health < ped.MaxHealth)
                    ped.Health = Math.Min(ped.MaxHealth, ped.Health + 25);
                if (ped.IsDead && what != "cpr" && what != "aed") _chat.Do("There is no response.");
            }
            catch { }
            _records.Act(person, _dispatch.ReportCallName, treatment[1]);
        }

        /// <summary>/pcr: a patient care report drafted from the vitals and treatment you gave, into the box to finish.</summary>
        private void PatientReport()
        {
            var person = _records.LastSubject;
            if (person == null) { _chat.Error("No patient yet - '/vitals' on somebody first."); return; }

            var text = new System.Text.StringBuilder("Patient care report. ");
            text.Append("I attended ").Append(person.Name).Append(person.Age > 0 ? ", " + person.Age : "").Append(". ");
            var any = false;
            foreach (var action in _records.Actions)
            {
                if (action.PersonId != person.Id || DateTime.Now.Ticks - action.Ticks > TimeSpan.FromHours(1).Ticks) continue;
                if (!action.Text.StartsWith("took a set of vitals") && !Treated(action.Text)) continue;
                var at = new DateTime(action.Ticks).ToString("HH:mm");
                text.Append(at).Append(" - I ").Append(action.Text).Append(". ");
                any = true;
            }
            if (!any) { _chat.Error("Nothing done for " + person.Name + " to report yet - '/vitals' and '/treat'."); return; }
            text.Append("Care handed over to EMS for transport.");

            _chat.IsOpen = true;
            _chat.Input = "/report " + person.Name + ": " + text;
            _chat.Scroll = 0;
            _chat.Notice("Patient care report drafted in the box - add what you saw, then Enter files it.");
        }

        private static bool Treated(string action)
        {
            foreach (var treatment in Treatments.Values) if (action == treatment[1]) return true;
            return false;
        }
    }
}
