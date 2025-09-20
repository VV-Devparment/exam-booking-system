using ExamBookingSystem.Data;
using ExamBookingSystem.DTOs;
using ExamBookingSystem.Models;
using ExamBookingSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookingStatus = ExamBookingSystem.Models.BookingStatus;

namespace ExamBookingSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BookingController : ControllerBase
    {
        private readonly IEmailService _emailService;
        private readonly ISlackService _slackService;
        private readonly ILocationService _locationService;
        private readonly IBookingService _bookingService;
        private readonly ISmsService? _smsService;
        private readonly ICalendarService? _calendarService;
        private readonly ILogger<BookingController> _logger;
        private readonly IConfiguration _configuration;

        public BookingController(
            IEmailService emailService,
            ISlackService slackService,
            ILocationService locationService,
            IBookingService bookingService,
            ILogger<BookingController> logger,
            IConfiguration configuration,
            ISmsService? smsService = null,
            ICalendarService? calendarService = null)
        {
            _emailService = emailService;
            _slackService = slackService;
            _locationService = locationService;
            _bookingService = bookingService;
            _logger = logger;
            _configuration = configuration;
            _smsService = smsService;
            _calendarService = calendarService;
        }

        [HttpPost("create")]
        public async Task<ActionResult<BookingResponseDto>> CreateBooking([FromBody] CreateBookingDto request)
        {
            try
            {
                _logger.LogInformation($"Creating booking for {request.StudentFirstName} {request.StudentLastName} - {request.CheckRideType}");

                // 1. Створити бронювання
                var bookingId = await _bookingService.CreateBookingAsync(request);

                // 2. Geocode student preferred airport/address
                var coordinates = await _locationService.GeocodeAddressAsync(request.PreferredAirport);
                if (!coordinates.HasValue)
                {
                    _logger.LogWarning($"Unable to geocode airport: {request.PreferredAirport}");
                    await _bookingService.CancelBookingAsync(bookingId, "Unable to geocode preferred airport location");
                    return BadRequest($"Unable to find location for airport: {request.PreferredAirport}");
                }

                _logger.LogInformation($"Geocoded {request.PreferredAirport} to ({coordinates.Value.Latitude}, {coordinates.Value.Longitude})");

                // 3. Find nearby examiners
                var radiusKm = request.SearchRadius * 1.852; // Перетворюємо nautical miles в km
                var nearbyExaminers = await _locationService.FindNearbyExaminersAsync(
                    coordinates.Value.Latitude,
                    coordinates.Value.Longitude,
                    radiusKm,
                    request.CheckRideType);

                if (!nearbyExaminers.Any())
                {
                    _logger.LogWarning($"No examiners found for {request.CheckRideType} within {request.SearchRadius}nm of {request.PreferredAirport}");
                    await _bookingService.CancelBookingAsync(bookingId, $"No qualified examiners found within {request.SearchRadius} nautical miles");
                    return BadRequest($"No qualified examiners found within {request.SearchRadius} nautical miles of {request.PreferredAirport}");
                }

                // 4. Update booking status
                await _bookingService.UpdateBookingStatusAsync(bookingId, Services.BookingStatus.ExaminersContacted);

                // 5. Send Slack notification about new booking
                await _slackService.NotifyNewBookingAsync(
                    $"{request.StudentFirstName} {request.StudentLastName}",
                    request.CheckRideType,
                    request.StartDate ?? DateTime.UtcNow.AddDays(7));

                // 6. Contact examiners in parallel
                var maxExaminers = _configuration.GetValue("ApplicationSettings:MaxExaminersToContact", 3);
                var examinersToContact = nearbyExaminers.Take(maxExaminers).ToList();

                _logger.LogInformation($"Contacting {examinersToContact.Count} examiners for booking {bookingId}");

                var contactTasks = examinersToContact.Select(examiner =>
                    ContactExaminerAsync(examiner, request, bookingId));

                await Task.WhenAll(contactTasks);

                return Ok(new BookingResponseDto
                {
                    BookingId = bookingId,
                    Message = $"Booking request sent to {examinersToContact.Count} qualified examiners",
                    ExaminersContacted = examinersToContact.Select(e => e.Name).ToList(),
                    Status = "ExaminersContacted"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating booking");
                await _slackService.NotifyErrorAsync("Booking creation failed", ex.Message);
                return StatusCode(500, "Internal server error while processing booking request");
            }
        }

        [HttpPost("examiner/respond")]
        public async Task<ActionResult> ExaminerResponse([FromBody] ExaminerResponseDto response)
        {
            try
            {
                _logger.LogInformation($"Processing examiner response: {response.Response} from {response.ExaminerEmail} for booking {response.BookingId}");

                // Validate request
                if (string.IsNullOrEmpty(response.BookingId) ||
                    string.IsNullOrEmpty(response.ExaminerEmail) ||
                    string.IsNullOrEmpty(response.Response))
                {
                    return BadRequest("Missing required fields");
                }

                // Get booking
                var booking = await _bookingService.GetBookingAsync(response.BookingId);
                if (booking == null)
                {
                    _logger.LogWarning($"Booking not found: {response.BookingId}");
                    return NotFound("Booking not found");
                }

                // Check if booking is still available for assignment
                var isAvailable = await _bookingService.IsBookingAvailableAsync(response.BookingId);
                if (!isAvailable)
                {
                    _logger.LogInformation($"Booking {response.BookingId} is no longer available. Current status: {booking.Status}");

                    return Ok(new
                    {
                        message = "Sorry, this booking is no longer available. Another examiner may have already been assigned.",
                        assigned = false,
                        currentStatus = booking.Status.ToString()
                    });
                }

                if (response.Response.Equals("Accepted", StringComparison.OrdinalIgnoreCase))
                {
                    // Try to assign examiner (first YES wins logic)
                    var assigned = await _bookingService.TryAssignExaminerAsync(
                        response.BookingId,
                        response.ExaminerEmail,
                        response.ExaminerName);

                    if (assigned)
                    {
                        // Send confirmation email to student
                        await _emailService.SendStudentConfirmationEmailAsync(
                            response.StudentEmail,
                            response.StudentName,
                            response.ExaminerName,
                            response.ProposedDateTime ?? DateTime.UtcNow.AddDays(7));

                        // Send calendar invitation if available
                        if (_calendarService != null && response.ProposedDateTime.HasValue)
                        {
                            await SendCalendarInvitation(
                                response.StudentEmail,
                                response.StudentName,
                                response.ExaminerName,
                                response.ProposedDateTime.Value);
                        }

                        // Notify Slack
                        await _slackService.NotifyExaminerResponseAsync(
                            response.ExaminerName,
                            "ACCEPTED (ASSIGNED!) 🎉",
                            response.StudentName);

                        _logger.LogInformation($"Examiner {response.ExaminerName} successfully assigned to booking {response.BookingId}");

                        return Ok(new
                        {
                            message = "Congratulations! You have been assigned to this booking. The student has been notified.",
                            assigned = true,
                            bookingId = response.BookingId,
                            studentName = response.StudentName,
                            scheduledDateTime = response.ProposedDateTime
                        });
                    }
                    else
                    {
                        // Another examiner was faster
                        await _slackService.NotifyExaminerResponseAsync(
                            response.ExaminerName,
                            "ACCEPTED (too late) ⏰",
                            response.StudentName);

                        return Ok(new
                        {
                            message = "Sorry, another examiner responded first and has been assigned to this booking.",
                            assigned = false
                        });
                    }
                }
                else if (response.Response.Equals("Declined", StringComparison.OrdinalIgnoreCase))
                {
                    // Log decline
                    await _slackService.NotifyExaminerResponseAsync(
                        response.ExaminerName,
                        "DECLINED ❌",
                        response.StudentName);

                    _logger.LogInformation($"Examiner {response.ExaminerName} declined booking {response.BookingId}");

                    return Ok(new
                    {
                        message = "Thank you for your response. Your decline has been recorded.",
                        assigned = false
                    });
                }
                else
                {
                    return BadRequest("Invalid response. Must be 'Accepted' or 'Declined'");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing examiner response for booking {response.BookingId}");
                await _slackService.NotifyErrorAsync("Examiner response processing failed", ex.Message);
                return StatusCode(500, "Internal server error");
            }
        }
        // Додайте ці методи в BookingController.cs

        [HttpGet("debug/{bookingId}")]
        public async Task<ActionResult> DebugBooking(string bookingId)
        {
            try
            {
                var booking = await _bookingService.GetBookingAsync(bookingId);
                if (booking == null)
                    return NotFound($"Booking {bookingId} not found");

                var isAvailable = await _bookingService.IsBookingAvailableAsync(bookingId);

                return Ok(new
                {
                    BookingId = booking.BookingId,
                    StudentName = booking.StudentName,
                    Status = booking.Status.ToString(),
                    AssignedExaminerEmail = booking.AssignedExaminerEmail,
                    AssignedExaminerName = booking.AssignedExaminerName,
                    IsAvailable = isAvailable,
                    IsPaid = booking.IsPaid,
                    CreatedAt = booking.CreatedAt,
                    ScheduledDateTime = booking.ScheduledDateTime
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error debugging booking {bookingId}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("reset/{bookingId}")]
        public async Task<ActionResult> ResetBooking(string bookingId)
        {
            try
            {
                // Цей метод скидає бронювання в початковий стан для тестування
                if (!bookingId.StartsWith("BK") || !int.TryParse(bookingId.Substring(2), out int id))
                    return BadRequest("Invalid booking ID format");

                // Тільки для EntityFrameworkBookingService
                if (_bookingService is EntityFrameworkBookingService efService)
                {
                    // Тут потрібно додати метод ResetBookingForTesting в EntityFrameworkBookingService
                    await efService.ResetBookingForTestingAsync(bookingId);
                    return Ok(new { message = $"Booking {bookingId} reset successfully" });
                }

                return BadRequest("Reset not supported for this booking service");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error resetting booking {bookingId}");
                return StatusCode(500, ex.Message);
            }
        }
        [HttpGet("{bookingId}")]
        public async Task<ActionResult<BookingInfo>> GetBooking(string bookingId)
        {
            try
            {
                var booking = await _bookingService.GetBookingAsync(bookingId);
                if (booking == null)
                    return NotFound($"Booking {bookingId} not found");

                return Ok(booking);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving booking {bookingId}");
                return StatusCode(500, "Internal server error");
            }
        }
        [HttpGet("diagnose/{bookingId}")]
        public async Task<ActionResult> DiagnoseBooking(string bookingId)
        {
            try
            {
                if (_bookingService is EntityFrameworkBookingService efService)
                {
                    var diagnosticInfo = await efService.GetBookingDiagnosticInfoAsync(bookingId);
                    // Перевіряємо доступність
                    var isAvailable = await _bookingService.IsBookingAvailableAsync(bookingId);

                    return Ok(new
                    {
                        diagnostic = diagnosticInfo,
                        isAvailable = isAvailable,
                        canBeAssigned = diagnosticInfo.AssignedExaminerId == null && isAvailable
                    });
                }

                return BadRequest("Diagnostic not available for this service type");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error diagnosing booking {bookingId}");
                return StatusCode(500, ex.Message);
            }
        }
            [HttpGet("active")]
            public async Task<ActionResult<List<BookingInfo>>> GetActiveBookings()
            {
                try
                {
                    var bookings = await _bookingService.GetActiveBookingsAsync();
                    return Ok(bookings);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving active bookings");
                    return StatusCode(500, "Internal server error");
                }
            }
        [HttpPost("fix/{bookingId}")]
        public async Task<ActionResult> FixBooking(string bookingId)
        {
            try
            {
                if (!bookingId.StartsWith("BK") || !int.TryParse(bookingId.Substring(2), out int id))
                    return BadRequest("Invalid booking ID");

                using (var scope = HttpContext.RequestServices.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    var booking = await context.BookingRequests.FirstOrDefaultAsync(b => b.Id == id);
                    if (booking == null)
                        return NotFound("Booking not found");

                    // Використовуємо повний namespace
                    booking.AssignedExaminerId = null;
                    booking.Status = ExamBookingSystem.Models.BookingStatus.ExaminersContacted;
                    booking.ScheduledDate = null;
                    booking.ScheduledTime = null;
                    booking.UpdatedAt = DateTime.UtcNow;

                    await context.SaveChangesAsync();

                    return Ok(new
                    {
                        message = "Booking reset successfully",
                        status = booking.Status.ToString(),
                        assignedExaminerId = booking.AssignedExaminerId
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fixing booking {bookingId}");
                return StatusCode(500, ex.Message);
            }
        }

        private async Task ContactExaminerAsync(ExaminerLocation examiner, CreateBookingDto request, string bookingId)
        {
            try
            {
                _logger.LogInformation($"Contacting examiner {examiner.Name} ({examiner.Email}) for booking {bookingId}");

                var success = await _emailService.SendExaminerContactEmailAsync(
                    examiner.Email,
                    examiner.Name,
                    $"{request.StudentFirstName} {request.StudentLastName}",
                    request.CheckRideType,
                    request.StartDate ?? DateTime.UtcNow.AddDays(7));

                if (success)
                {
                    _logger.LogInformation($"Successfully contacted examiner {examiner.Name} for booking {bookingId}");
                }
                else
                {
                    _logger.LogWarning($"Failed to contact examiner {examiner.Name} for booking {bookingId}");
                    await _slackService.NotifyErrorAsync(
                        $"Failed to contact examiner",
                        $"Could not send email to {examiner.Name} ({examiner.Email})");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error contacting examiner {examiner.Name} for booking {bookingId}");
            }
        }

        private async Task SendCalendarInvitation(string studentEmail, string studentName, string examinerName, DateTime scheduledDateTime)
        {
            try
            {
                if (_calendarService == null)
                    return;

                var icsContent = _calendarService.GenerateIcsFile(
                    $"Aviation Checkride with {examinerName}",
                    scheduledDateTime,
                    scheduledDateTime.AddHours(_configuration.GetValue("ApplicationSettings:DefaultExamDurationHours", 2)),
                    "TBD - Examiner will provide location details",
                    $"Checkride examination with designated examiner {examinerName}. " +
                    $"Please confirm location and bring required documents.");

                _logger.LogInformation($"Calendar invitation generated for {studentName} - {scheduledDateTime}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating calendar invitation");
            }
        }
    }
}