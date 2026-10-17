using System;
using System.Collections.Generic;

namespace TextDispatch.Records
{
    /// <summary>One call, as a dispatch screen keeps it: a number, a priority, when it opened and how it closed.</summary>
    public sealed class IncidentRecord
    {
        public string Number;      // "2026-0412"
        public string Call;
        public string Priority;    // P1, P2, P3
        public string Zone;
        public string Opened;      // local time
        public string OnScene;     // local time, or null
        public string Closed;      // local time, or null while open
        public string Disposition;
        public List<string> Log = new List<string>();
    }

    /// <summary>The station paperwork for an arrest: a booking number, what they had on them, and the cell.</summary>
    public sealed class BookingRecord
    {
        public string Number;      // "B26-04117"
        public string Name;
        public string Station;
        public string When;
        public List<string> Charges = new List<string>();
        public List<string> Property = new List<string>();
        public string Cell;
    }

    /// <summary>The LSPDFR side of the records: incidents with call numbers, and bookings.</summary>
    public sealed partial class RecordsLedger
    {
        private readonly List<IncidentRecord> _incidents = new List<IncidentRecord>();
        private readonly List<BookingRecord> _bookings = new List<BookingRecord>();

        public List<IncidentRecord> Incidents { get { return _incidents; } }
        public List<BookingRecord> Bookings { get { return _bookings; } }

        public int NextIncident { get; internal set; } = 401;
        public int NextBooking { get; internal set; } = 4101;

        /// <summary>The incident in progress, or null.</summary>
        public IncidentRecord CurrentIncident
        {
            get
            {
                for (var i = _incidents.Count - 1; i >= 0; i--)
                    if (_incidents[i].Closed == null) return _incidents[i];
                return null;
            }
        }

        public static string PriorityOf(string call)
        {
            var c = (call ?? "").ToLowerInvariant();
            foreach (var word in new[] { "shot", "shoot", "gun", "armed", "robbery", "stab", "hostage", "officer", "pursuit", "homicide",
                                         "murder", "fire", "kidnap", "assault", "cardiac", "overdose", "collision with injur", "siege", "riot" })
                if (c.Contains(word)) return "P1";
            foreach (var word in new[] { "noise", "parking", "loiter", "welfare", "abandon", "litter", "report", "found property", "complaint", "animal" })
                if (c.Contains(word)) return "P3";
            return "P2";
        }

        /// <summary>Open an incident for a call, closing any still open. Returns it.</summary>
        public IncidentRecord OpenIncident(string call, string zone)
        {
            var open = CurrentIncident;
            if (open != null && string.Equals(open.Call, call, StringComparison.OrdinalIgnoreCase)) return open;
            if (open != null) CloseIncident("superseded by another call");

            var incident = new IncidentRecord
            {
                Number = DateTime.Now.ToString("yyyy") + "-" + (NextIncident++).ToString("0000"),
                Call = call,
                Priority = PriorityOf(call),
                Zone = zone,
                Opened = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            };
            incident.Log.Add(DateTime.Now.ToString("HH:mm") + " received - " + incident.Priority);
            _incidents.Add(incident);
            if (_incidents.Count > 300) _incidents.RemoveAt(0);
            Dirty = true;
            return incident;
        }

        public void IncidentNote(string text)
        {
            var open = CurrentIncident;
            if (open == null || string.IsNullOrEmpty(text)) return;
            open.Log.Add(DateTime.Now.ToString("HH:mm") + " " + text);
            if (open.Log.Count > 40) open.Log.RemoveAt(1);
            Dirty = true;
        }

        public void IncidentOnScene()
        {
            var open = CurrentIncident;
            if (open == null || open.OnScene != null) return;
            open.OnScene = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            IncidentNote("on scene (10-97)");
        }

        public IncidentRecord CloseIncident(string disposition)
        {
            var open = CurrentIncident;
            if (open == null) return null;
            open.Closed = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            open.Disposition = string.IsNullOrEmpty(disposition) ? "closed" : disposition;
            open.Log.Add(DateTime.Now.ToString("HH:mm") + " closed - " + open.Disposition);
            Dirty = true;
            return open;
        }

        public IncidentRecord FindIncident(string number)
        {
            var n = (number ?? "").Trim().TrimStart('#');
            foreach (var incident in _incidents)
                if (incident.Number == n || incident.Number.EndsWith("-" + n.PadLeft(4, '0'))) return incident;
            return null;
        }

        // ------------------------------------------------------------------ booking

        private static readonly string[] Pockets =
        {
            "a wallet with $" + "{0} cash", "a phone with a cracked screen", "a set of house keys", "car keys",
            "a lighter and a pack of cigarettes", "a bus pass", "earbuds", "a receipt from a 24/7", "a belt", "a watch",
            "loose change", "a pack of gum", "a bank card"
        };

        /// <summary>The arrest still waiting to be booked for this person, or null.</summary>
        public FollowUp Unbooked(PersonRecord person)
        {
            var open = OpenCase(person);
            return open != null && !open.Booked ? open : null;
        }

        /// <summary>Everybody arrested and not booked yet.</summary>
        public List<FollowUp> UnbookedArrests()
        {
            var list = new List<FollowUp>();
            foreach (var followUp in FollowUps) if (followUp.Kind == "arrest" && !followUp.Booked) list.Add(followUp);
            return list;
        }

        /// <summary>
        /// Book an arrest at a station: a booking number, the charges, their property bagged (the evidence is
        /// already the DA's), prints and photographs, a cell. Kept, and on the case.
        /// </summary>
        public BookingRecord Book(PersonRecord person, FollowUp arrest, string station, string call)
        {
            var rng = new Random(unchecked(person.Id.GetHashCode() * 29 + NextBooking));
            var booking = new BookingRecord
            {
                Number = "B" + DateTime.Now.ToString("yy") + "-" + (NextBooking++).ToString("00000"),
                Name = person.Name,
                Station = station,
                When = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                Cell = "holding cell " + (1 + rng.Next(8))
            };
            if (arrest.Charges != null && arrest.Charges.Count > 0) booking.Charges.AddRange(arrest.Charges);
            else booking.Charges.Add(arrest.Offence);

            var count = 2 + rng.Next(3);
            var taken = new HashSet<int>();
            while (booking.Property.Count < count)
            {
                var i = rng.Next(Pockets.Length);
                if (!taken.Add(i)) continue;
                booking.Property.Add(string.Format(Pockets[i], 5 + rng.Next(180)));
            }

            arrest.Booked = true;
            arrest.BookingNumber = booking.Number;
            _bookings.Add(booking);
            if (_bookings.Count > 300) _bookings.RemoveAt(0);
            Act(person, call, "booked them at " + station + " under " + booking.Number + " - prints and photographs taken, property bagged");
            Dirty = true;
            return booking;
        }
    }
}
