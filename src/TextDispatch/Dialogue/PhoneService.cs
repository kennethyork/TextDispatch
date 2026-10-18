using System;
using System.Collections.Generic;
using TextDispatch.Ai;
using TextDispatch.Chat;
using TextDispatch.Records;

namespace TextDispatch.Dialogue
{
    /// <summary>
    /// The officer's phone: texts and calls with the people they work with and the people they have met.
    ///
    /// On GTA World the phone is how a case moves between shifts - a detective asking for the report, an
    /// informant with a name, a witness remembering something. Here the department's own contacts answer
    /// from what is on file (the sergeant knows which reports came back, the DA knows what is waiting),
    /// people you have met answer as who they are, and on a shift some of them text first. Text only: a
    /// "call" is the same conversation written as one.
    /// </summary>
    internal sealed class PhoneService
    {
        private readonly ChatBox _chat;
        private readonly Settings _settings;
        private readonly RecordsLedger _records;
        private readonly ReplyPump _pump;
        private int _nextTipAt = Environment.TickCount + 10 * 60000;

        /// <summary>The department's own numbers. Always in the phone.</summary>
        public static readonly string[] Department =
        {
            "Sgt. Kowalski", "Det. Ramirez", "DA's Office", "Tow Yard", "Lucky"
        };

        private static readonly Dictionary<string, string> Roles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Sgt. Kowalski", "your watch sergeant" },
            { "Det. Ramirez", "a detective, Robbery-Homicide" },
            { "DA's Office", "the deputy DA on your cases" },
            { "Tow Yard", "the police tow and impound yard" },
            { "Lucky", "a street informant - knows people, wants paying" }
        };

        public PhoneService(ChatBox chat, Settings settings, RecordsLedger records)
        {
            _chat = chat;
            _settings = settings;
            _records = records;
            _pump = new ReplyPump(settings);
        }

        public void Update(bool onShift)
        {
            _pump.Update();
            if (!onShift || !_settings.PhoneTips || Environment.TickCount - _nextTipAt < 0) return;

            _nextTipAt = Environment.TickCount + (12 + new Random(Environment.TickCount).Next(14)) * 60000;
            try { Tip(); }
            catch (Exception ex) { Log.Error("phone tip", ex); }
        }

        // ------------------------------------------------------------------ contacts

        /// <summary>The department, then the people you saved, then anybody else you have met.</summary>
        public List<string> Contacts()
        {
            var list = new List<string>(Department);
            foreach (var name in _records.Contacts) if (!list.Contains(name)) list.Add(name);
            return list;
        }

        /// <summary>A contact by what was typed: the whole name, its start, or any part of it.</summary>
        public string Resolve(string typed, out PersonRecord person)
        {
            person = null;
            var t = (typed ?? "").Trim();
            if (t.Length == 0) return null;

            foreach (var name in Contacts())
                if (name.Equals(t, StringComparison.OrdinalIgnoreCase)) return Found(name, out person);
            foreach (var name in Contacts())
                if (name.StartsWith(t, StringComparison.OrdinalIgnoreCase) ||
                    name.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0) return Found(name, out person);

            // Anybody you have met gave you their number - and is saved once you text them.
            var met = _records.FindPerson(t);
            if (met != null && met.Met) return Found(met.Name, out person);
            return null;
        }

        private string Found(string name, out PersonRecord person)
        {
            person = Array.IndexOf(Department, name) >= 0 ? null : _records.FindPerson(name);
            return name;
        }

        public void Save(string name)
        {
            if (string.IsNullOrEmpty(name) || Array.IndexOf(Department, name) >= 0 || _records.Contacts.Contains(name)) return;
            _records.Contacts.Add(name);
            _records.Dirty = true;
        }

        public static string RoleOf(string contact)
        {
            string role;
            return Roles.TryGetValue(contact ?? "", out role) ? role : null;
        }

        // ------------------------------------------------------------------ texts and calls

        public void Text(string contact, PersonRecord person, string message, bool call)
        {
            Save(contact);
            Store(contact, true, message);
            _chat.Phone(call ? "[CALL]" : "[SMS]", (call ? "You: " : "To " + contact + ": ") + message);

            var scripted = Scripted(contact, person, message);
            if (scripted == null)
            {
                _pump.Schedule(call ? "(( It rings out. Nobody answers. ))" : "(( Delivered. No reply. ))", 1500,
                               line => _chat.Phone(call ? "[CALL]" : "[SMS]", line));
                return;
            }

            Action<string> deliver = line =>
            {
                Store(contact, false, line);
                _chat.Phone(call ? "[CALL]" : "[SMS]", contact + ": " + line);
            };

            if (!_settings.UseModel)
            {
                _pump.Schedule(scripted, call ? _settings.TypingMs : _settings.TypingMs * 2, deliver);
                return;
            }

            // The department answers from the records - the model only words it, and a version that drops a
            // number or a name in capitals is thrown away for the records' own.
            if (person == null)
            {
                var role = RoleOf(contact) ?? "somebody the officer works with";
                var facts = scripted;
                _pump.Ask(() => Phrasing.Reword(_settings, contact + ", " + role,
                                                (call ? "You are on the phone" : "You are replying by text message") + " to a police officer you work with. The officer said: " + message,
                                                facts),
                          scripted, deliver);
                return;
            }

            var who = person.Name + ", " + person.CharacterLine();
            _pump.Ask(() =>
            {
                string error;
                var system = "You are " + who + " You are replying to a text message from a police officer you have met. " +
                             "Reply in one or two short sentences, as a text message, in character. No quotation marks, no narration.";
                var reply = LocalModel.Chat(_settings, system, message, out error);
                if (reply == null && error != null) Log.Line("phone model: " + error);
                return reply == null ? null : reply.Trim().Trim('"');
            }, scripted, deliver);
        }

        public void Inbox(string contact)
        {
            var found = new List<PhoneMessage>();
            foreach (var message in _records.Messages)
                if (contact == null || message.Contact.Equals(contact, StringComparison.OrdinalIgnoreCase)) found.Add(message);
            if (found.Count == 0)
            {
                _chat.Notice(contact == null ? "No messages. '/text <contact> <message>' sends one." : "No messages with " + contact + ".");
                return;
            }

            // The last ten, oldest first, the way a phone shows a thread.
            _chat.Notice((contact == null ? "Messages" : "Messages with " + contact) + ", the last " + Math.Min(10, found.Count) + ":");
            for (var i = Math.Max(0, found.Count - 10); i < found.Count; i++)
            {
                var message = found[i];
                _chat.Phone("", "  " + message.When.Substring(Math.Max(0, message.When.Length - 5)) + "  " +
                                (message.Outgoing ? "you -> " + message.Contact : message.Contact) + ": " + message.Text);
            }
        }

        private void Store(string contact, bool outgoing, string text)
        {
            _records.Messages.Add(new PhoneMessage
            {
                Contact = contact,
                Outgoing = outgoing,
                Text = text,
                When = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            });
            if (_records.Messages.Count > 300) _records.Messages.RemoveAt(0);
            _records.Dirty = true;
        }

        /// <summary>A text that arrives by itself, in the sender's own words when the model is running. Kept with the rest.</summary>
        private void Incoming(string contact, string text)
        {
            Action<string> deliver = line =>
            {
                Store(contact, false, line);
                _chat.Phone("[SMS]", contact + ": " + line);
                Log.Line("phone: " + contact + " - " + line);
            };
            if (!_settings.UseModel) { deliver(text); return; }
            var role = RoleOf(contact) ?? "somebody the officer knows";
            _pump.Ask(() => Phrasing.Reword(_settings, contact + ", " + role, "You are sending a text message to a police officer, unprompted.", text),
                      text, deliver);
        }

        // ------------------------------------------------------------------ what they say

        private string Scripted(string contact, PersonRecord person, string message)
        {
            var m = (message ?? "").ToLowerInvariant();
            var rng = new Random(Environment.TickCount);

            switch (contact)
            {
                case "Sgt. Kowalski":
                {
                    var returned = _records.ReturnedReports();
                    if (m.Contains("report") || returned.Count > 0)
                        return returned.Count == 0
                            ? "Your paperwork is square. Nothing waiting on you."
                            : "I've got " + returned.Count + " of yours sent back - #" + returned[0].Number + " first. " +
                              (returned[0].ReviewNotes ?? "") + " '/report fix " + returned[0].Number + " <more>' and I'll look again.";
                    if (m.Contains("rank") || m.Contains("promot"))
                    {
                        var rank = Career.RankOf(_records);
                        return rank.Next == null ? "You're as high as patrol goes. Don't get comfortable."
                                                 : "You're " + rank.Title + ". Keep it up - " + rank.ToNext + " more points and we talk about " + rank.Next + ".";
                    }
                    return Pick(rng, "Copy. Keep me posted.", "Understood. Watch your six out there.", "Got it. Make sure it's on paper before end of watch.");
                }
                case "Det. Ramirez":
                {
                    var file = _records.FindCase(_records.ActiveCase);
                    if (file != null)
                        return "Case #" + file.Number + ", " + file.Title + ": " + file.Reports.Count + " report(s), " + file.People.Count +
                               " name(s) on it. " + (file.Reports.Count == 0 ? "I need your report before I can do anything with it." : "Keep feeding it - I'll work the names.");
                    var wanted = _records.KnownWanted();
                    if (m.Contains("warrant") && wanted.Count > 0)
                        return wanted[0].Name + " is still out there on " + wanted[0].WarrantFor + ". Last known around " + wanted[0].HomeZone + ".";
                    return Pick(rng, "Send me what you've got and open a case on it - '/case open <title>'.", "Nothing on my desk from you. Busy shift?", "I'll look into it. Write it up.");
                }
                case "DA's Office":
                {
                    if (_records.FollowUps.Count == 0) return "We have nothing pending from you, officer.";
                    var followUp = _records.FollowUps[0];
                    return "On " + followUp.Name + ", " + followUp.Offence + ": " +
                           (followUp.Kind != "arrest" ? "it's a citation, it'll sort itself out."
                            : (followUp.ReportFiled ? "we have the report" : "we still need a report that holds up") + ", " +
                              (followUp.Evidence == null ? 0 : followUp.Evidence.Count) + " piece(s) of evidence. Call it " +
                              RecordsLedger.ChargeChance(followUp) + " percent we file.");
                }
                case "Tow Yard":
                    return Pick(rng, "Yard. If you need a truck, call it in on scene - /tow.", "We've got space. Bring it in.", "Truck's out on another job, give it twenty.");
                case "Lucky":
                    return LuckyTip(rng) ?? Pick(rng, "Streets are quiet, man. Ask me later.", "I don't know nothing about nothing.", "You buying lunch or what?");
            }

            if (person == null) return null;

            // People you have met answer as who they are, and how they feel about you.
            if (person.Wanted) return Pick(rng, "Who is this?", "Wrong number.", "Lose my number.");
            if (person.Arrests > 0) return Pick(rng, "Why are you texting me? Talk to my lawyer.", "Leave me alone.", "Seriously? After what you did?");
            if (person.Citations > 0) return Pick(rng, "I paid it, okay?", "What now, officer?", "Is this about the ticket?");
            if (m.Contains("?")) return Pick(rng, "I told you everything I saw.", "Not that I remember, sorry.", "Maybe? I'd have to think about it.");
            return Pick(rng, "Okay, thanks officer.", "Got it.", "Thanks for checking in.");
        }

        private string LuckyTip(Random rng)
        {
            var wanted = _records.KnownWanted();
            if (wanted.Count > 0)
            {
                var person = wanted[rng.Next(wanted.Count)];
                return "Word is " + person.Name + " been hanging round " + person.HomeZone + ". Didn't hear it from me.";
            }
            return null;
        }

        /// <summary>
        /// Somebody texts first. What they say follows the records: the DA chasing a report, the sergeant
        /// with work sent back, the detective on an open case, an informant with a name or a plate - which
        /// becomes a BOLO, because that is what a tip is for.
        /// </summary>
        private void Tip()
        {
            var rng = new Random(Environment.TickCount);

            var returned = _records.ReturnedReports();
            if (returned.Count > 0 && rng.Next(3) == 0)
            {
                Incoming("Sgt. Kowalski", "Report #" + returned[0].Number + " is still sitting on my desk. " + (returned[0].ReviewNotes ?? "") +
                                          " Fix it with '/report fix " + returned[0].Number + " <more>'.");
                return;
            }

            foreach (var followUp in _records.FollowUps)
                if (followUp.Kind == "arrest" && !followUp.ReportFiled && rng.Next(2) == 0)
                {
                    Incoming("DA's Office", "We have " + followUp.Name + " in custody on " + followUp.Offence + " and no report from you. Without one we can't file.");
                    return;
                }

            var file = _records.FindCase(_records.ActiveCase);
            if (file != null && rng.Next(3) == 0)
            {
                Incoming("Det. Ramirez", "Any movement on case #" + file.Number + " (" + file.Title + ")? Put what you find under it - '/case note " + file.Number + " <text>'.");
                return;
            }

            var tip = LuckyTip(rng);
            if (tip != null && rng.Next(2) == 0) { Incoming("Lucky", tip); return; }

            // A plate, and what it is about. Entered as a BOLO so a stop on it says so.
            var plates = new List<VehicleRecord>(_records.Vehicles);
            if (plates.Count == 0) return;
            var vehicle = plates[rng.Next(plates.Count)];
            var what = Pick(rng, "been casing the liquor stores", "dropping off packages in the alley behind the laundromat",
                            "picking up kids outside the high school and it ain't their dad", "doing donuts in the mall car park at 3am",
                            "parked outside the same house every night this week");
            _records.AddBolo(vehicle.Plate, vehicle.Model + " - informant says " + what);
            Incoming("Lucky", "Got one for you. " + vehicle.Model + ", plate " + vehicle.Plate + ", " + what + ". We square now?");
            _chat.Notice("  BOLO entered for " + vehicle.Plate + ". /bolo lists them; a stop on it will flag.");
        }

        private static string Pick(Random rng, params string[] lines)
        {
            return lines[rng.Next(lines.Length)];
        }
    }
}
