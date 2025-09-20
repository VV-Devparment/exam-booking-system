using System.Text;

namespace ExamBookingSystem.Services
{
    public interface ICalendarService
    {
        string GenerateIcsFile(string title, DateTime start, DateTime end, string location, string description);
    }

    public class CalendarService : ICalendarService
    {
        public string GenerateIcsFile(string title, DateTime start, DateTime end, string location, string description)
        {
            var sb = new StringBuilder();

            sb.AppendLine("BEGIN:VCALENDAR");
            sb.AppendLine("VERSION:2.0");
            sb.AppendLine("PRODID:-//Exam Booking System//EN");
            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine($"UID:{Guid.NewGuid()}@exambooking.com");
            sb.AppendLine($"DTSTART:{start:yyyyMMddTHHmmss}");
            sb.AppendLine($"DTEND:{end:yyyyMMddTHHmmss}");
            sb.AppendLine($"SUMMARY:{title}");
            sb.AppendLine($"DESCRIPTION:{description}");
            sb.AppendLine($"LOCATION:{location}");
            sb.AppendLine("END:VEVENT");
            sb.AppendLine("END:VCALENDAR");

            return sb.ToString();
        }
    }
}