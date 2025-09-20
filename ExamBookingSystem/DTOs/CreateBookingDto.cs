using System.ComponentModel.DataAnnotations;

namespace ExamBookingSystem.DTOs
{
    public class CreateBookingDto
    {
        [Required]
        [StringLength(100)]
        public string StudentFirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string StudentLastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string StudentEmail { get; set; } = string.Empty;

        [Required]
        [Phone]
        public string StudentPhone { get; set; } = string.Empty;

        // Авіаційні поля
        [Required]
        public string AircraftType { get; set; } = string.Empty;

        [Required]
        public string CheckRideType { get; set; } = string.Empty;

        [Required]
        public string PreferredAirport { get; set; } = string.Empty;

        public int SearchRadius { get; set; } = 50;

        public bool WillingToFly { get; set; }

        public string DateOption { get; set; } = "ASAP";

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public string? PreferredExaminer { get; set; }

        public bool AdditionalRating { get; set; }

        public bool IsRecheck { get; set; }

        public string? AdditionalNotes { get; set; }

        // Поля після match
        public string? CertificateNumber { get; set; }

        public string? FtnNumber { get; set; }

        public string? WrittenLast4 { get; set; }

        // Старі поля для сумісності
        public string StudentAddress { get; set; } = string.Empty;

        public string ExamType { get; set; } = string.Empty;

        public DateTime PreferredDate { get; set; }

        public string? PreferredTime { get; set; }

        public string? SpecialRequirements { get; set; }
    }
}