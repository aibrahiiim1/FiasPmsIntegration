using FiasPmsIntegration.Models;
using System.Text;

namespace FiasPmsIntegration.Services
{
    public class FiasProtocolService
    {
        private const char STX = (char)0x02; // Start of Text
        private const char ETX = (char)0x03; // End of Text
        private const char FIELD_SEPARATOR = '|';

        private readonly GuestDataStore _guestStore;
        private readonly LogService _logService;

        public FiasProtocolService(GuestDataStore guestStore, LogService logService)
        {
            _guestStore = guestStore;
            _logService = logService;
        }

        // Parse incoming FIAS message
        public FiasMessage? ParseMessage(string rawMessage)
        {
            try
            {
                // Remove STX and ETX
                var cleanMessage = rawMessage.Trim(STX, ETX, '\r', '\n', ' ');

                if (string.IsNullOrWhiteSpace(cleanMessage))
                    return null;

                // Check for LRC (last character)
                if (cleanMessage.Length > 1)
                {
                    var lrc = cleanMessage[^1];
                    var dataWithoutLrc = cleanMessage[..^1];

                    // Validate LRC
                    if (!ValidateLRC(dataWithoutLrc, lrc))
                    {
                        _logService.Log("WARN", "LRC validation failed");
                    }

                    cleanMessage = dataWithoutLrc;
                }

                var fields = cleanMessage.Split(FIELD_SEPARATOR);
                if (fields.Length < 1) return null;

                var message = new FiasMessage
                {
                    RecordId = fields[0],
                    Timestamp = DateTime.Now
                };

                // Parse field pairs (field ID + value)
                for (int i = 1; i < fields.Length; i++)
                {
                    if (string.IsNullOrEmpty(fields[i])) continue;

                    if (fields[i].Length >= 2)
                    {
                        var fieldId = fields[i].Substring(0, 2);
                        var fieldValue = fields[i].Length > 2 ? fields[i].Substring(2) : string.Empty;
                        message.Fields[fieldId] = fieldValue;
                    }
                }

                _logService.Log("INFO", $"Parsed {message.RecordId} message with {message.Fields.Count} fields");
                return message;
            }
            catch (Exception ex)
            {
                _logService.Log("ERROR", $"Parse error: {ex.Message}");
                return null;
            }
        }

        // Build FIAS response message
        public string BuildMessage(string recordId, Dictionary<string, string> fields)
        {
            var sb = new StringBuilder();
            sb.Append(recordId);
            sb.Append(FIELD_SEPARATOR);

            foreach (var field in fields)
            {
                sb.Append(field.Key);
                sb.Append(field.Value);
                sb.Append(FIELD_SEPARATOR);
            }

            var message = sb.ToString();
            var lrc = CalculateLRC(message);

            var fullMessage = $"{STX}{message}{ETX}{lrc}";
            return fullMessage;
        }

        // Calculate LRC (Longitudinal Redundancy Check)
        private char CalculateLRC(string data)
        {
            byte lrc = 0;
            foreach (char c in data)
            {
                lrc ^= (byte)c;
            }
            return (char)lrc;
        }

        private bool ValidateLRC(string data, char receivedLrc)
        {
            var calculatedLrc = CalculateLRC(data);
            return calculatedLrc == receivedLrc;
        }

        // Process message and generate response
        public string? ProcessMessage(FiasMessage message)
        {
            return message.RecordId switch
            {
                "LS" => HandleLinkStart(message),
                "LA" => HandleLinkAlive(message),
                "LE" => HandleLinkEnd(message),
                "LD" => null, // LD is sent by us, no response needed
                "LR" => null, // LR is sent by us, no response needed
                "DR" => HandleDatabaseResync(message),
                "GI" => HandleGuestCheckIn(message),
                "GO" => HandleGuestCheckOut(message),
                "GC" => HandleGuestChange(message),
                "PS" => HandlePostingSimple(message),
                "PR" => HandlePostingRequest(message),
                _ => null
            };
        }

        // Handle Link Start
        private string HandleLinkStart(FiasMessage message)
        {
            _logService.Log("INFO", "Link Start received - sending Link Description");

            // Respond with Link Start
            var lsFields = new Dictionary<string, string>
            {
                { "DA", DateTime.Now.ToString("yyMMdd") },
                { "TI", DateTime.Now.ToString("HHmmss") }
            };

            return BuildMessage("LS", lsFields);
        }

        // Handle Link Alive
        private string HandleLinkAlive(FiasMessage message)
        {
            _logService.Log("INFO", "Link Alive received");

            var laFields = new Dictionary<string, string>
            {
                { "DA", DateTime.Now.ToString("yyMMdd") },
                { "TI", DateTime.Now.ToString("HHmmss") }
            };

            return BuildMessage("LA", laFields);
        }

        // Handle Link End
        private string HandleLinkEnd(FiasMessage message)
        {
            _logService.Log("INFO", "Link End received");

            var leFields = new Dictionary<string, string>
            {
                { "DA", DateTime.Now.ToString("yyMMdd") },
                { "TI", DateTime.Now.ToString("HHmmss") }
            };

            return BuildMessage("LE", leFields);
        }

        // Send Link Description (LD)
        public string BuildLinkDescription()
        {
            var fields = new Dictionary<string, string>
            {
                { "DA", DateTime.Now.ToString("yyMMdd") },
                { "TI", DateTime.Now.ToString("HHmmss") },
                { "V#", "1.0" },
                { "IF", "WW" }, // In-Room Internet Systems
                { "RT", "1" }  // Request room payment methods
            };

            return BuildMessage("LD", fields);
        }

        // Send Link Records (LR)
        public List<string> BuildLinkRecords()
        {
            var records = new List<string>();

            // Define supported record types with their fields
            var recordDefinitions = new Dictionary<string, string>
            {
                { "GI", "RNG#GNGLGAGDGSGV" },      // Guest Check-in
                { "GO", "RNG#GS" },                 // Guest Check-out
                { "GC", "RNG#GNGLGAGDGSROGV" },    // Guest Change
                { "PS", "RNTATIDATISOPM" },        // Posting Simple
                { "PR", "RNG#PMPITADATIWS" },      // Posting Request
                { "PL", "RNG#GNGLDATIWS" },        // Posting List
                { "PA", "RNASCTDATIWS" }           // Posting Answer
            };

            foreach (var definition in recordDefinitions)
            {
                var fields = new Dictionary<string, string>
                {
                    { "RI", definition.Key },
                    { "FL", definition.Value }
                };
                records.Add(BuildMessage("LR", fields));
            }

            return records;
        }

        // Handle Database Resync Request
        private string HandleDatabaseResync(FiasMessage message)
        {
            _logService.Log("INFO", "Database resync requested");
            // This would trigger sending DS, all GI records, then DE
            return string.Empty; // Handled separately
        }

        // Handle Guest Check-in
        private string? HandleGuestCheckIn(FiasMessage message)
        {
            var guest = new GuestData
            {
                RoomNumber = message.GetField("RN"),
                ReservationNumber = message.GetField("G#"),
                GuestName = message.GetField("GN"),
                Language = message.GetField("GL"),
                Status = "checked-in"
            };

            // Parse dates
            if (DateTime.TryParseExact(message.GetField("GA"), "yyMMdd",
                null, System.Globalization.DateTimeStyles.None, out var arrivalDate))
            {
                guest.ArrivalDate = arrivalDate;
            }

            if (DateTime.TryParseExact(message.GetField("GD"), "yyMMdd",
                null, System.Globalization.DateTimeStyles.None, out var departureDate))
            {
                guest.DepartureDate = departureDate;
            }

            _guestStore.AddOrUpdateGuest(guest);
            _logService.Log("INFO", $"Guest checked in: {guest.GuestName} - Room {guest.RoomNumber}");

            return null; // No response required
        }

        // Handle Guest Check-out
        private string? HandleGuestCheckOut(FiasMessage message)
        {
            var roomNumber = message.GetField("RN");
            var reservationNumber = message.GetField("G#");

            _guestStore.RemoveGuest(reservationNumber);
            _logService.Log("INFO", $"Guest checked out: Reservation {reservationNumber}");

            return null;
        }

        // Handle Guest Change
        private string? HandleGuestChange(FiasMessage message)
        {
            var reservationNumber = message.GetField("G#");
            var guest = _guestStore.GetGuest(reservationNumber);

            if (guest != null)
            {
                // Update guest data
                if (message.Fields.ContainsKey("RN"))
                    guest.RoomNumber = message.GetField("RN");
                if (message.Fields.ContainsKey("GN"))
                    guest.GuestName = message.GetField("GN");

                guest.LastUpdate = DateTime.Now;
                _logService.Log("INFO", $"Guest updated: {guest.GuestName}");
            }

            return null;
        }

        // Handle Posting Simple
        private string HandlePostingSimple(FiasMessage message)
        {
            var roomNumber = message.GetField("RN");
            var amount = message.GetField("TA");

            _logService.Log("INFO", $"Posting: Room {roomNumber}, Amount {amount}");

            // Send Posting Answer
            var paFields = new Dictionary<string, string>
            {
                { "RN", roomNumber },
                { "AS", "OK" },
                { "DA", DateTime.Now.ToString("yyMMdd") },
                { "TI", DateTime.Now.ToString("HHmmss") }
            };

            return BuildMessage("PA", paFields);
        }

        // Handle Posting Request (Inquiry or Posting)
        private string HandlePostingRequest(FiasMessage message)
        {
            var hasAmount = message.Fields.ContainsKey("TA") &&
                           !string.IsNullOrEmpty(message.GetField("TA"));

            if (hasAmount)
            {
                // This is a posting
                return HandlePosting(message);
            }
            else
            {
                // This is an inquiry
                return HandleInquiry(message);
            }
        }

        private string HandleInquiry(FiasMessage message)
        {
            var inquiry = message.GetField("PI");
            var guests = _guestStore.GetGuestsByRoomOrName(inquiry);

            if (guests.Count == 0)
            {
                // No guests found
                var paFields = new Dictionary<string, string>
                {
                    { "AS", "NG" },
                    { "CT", "GUEST NOT FOUND" },
                    { "DA", DateTime.Now.ToString("yyMMdd") },
                    { "TI", DateTime.Now.ToString("HHmmss") }
                };
                return BuildMessage("PA", paFields);
            }

            if (guests.Count == 1)
            {
                // Single guest - send PL
                var guest = guests[0];
                var plFields = new Dictionary<string, string>
                {
                    { "RN", guest.RoomNumber },
                    { "G#", guest.ReservationNumber },
                    { "GN", guest.GuestName },
                    { "DA", DateTime.Now.ToString("yyMMdd") },
                    { "TI", DateTime.Now.ToString("HHmmss") }
                };
                return BuildMessage("PL", plFields);
            }

            // Multiple guests - send PL with multiple entries
            // Implementation simplified for brevity
            return BuildMessage("PL", new Dictionary<string, string>());
        }

        private string HandlePosting(FiasMessage message)
        {
            var roomNumber = message.GetField("RN");
            var amount = message.GetField("TA");
            var reservationNumber = message.GetField("G#");

            _logService.Log("INFO", $"Posting to room {roomNumber}: {amount}");

            var guest = _guestStore.GetGuest(reservationNumber);
            if (guest != null)
            {
                // Update balance (simplified)
                if (decimal.TryParse(amount, out var amt))
                {
                    guest.Balance += amt / 100; // FIAS sends without decimal point
                }
            }

            var paFields = new Dictionary<string, string>
            {
                { "RN", roomNumber },
                { "G#", reservationNumber },
                { "AS", "OK" },
                { "DA", DateTime.Now.ToString("yyMMdd") },
                { "TI", DateTime.Now.ToString("HHmmss") }
            };

            return BuildMessage("PA", paFields);
        }


    }
}
