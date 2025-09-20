using Microsoft.AspNetCore.Mvc;
using ExamBookingSystem.Data;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;

namespace ExamBookingSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminController> _logger;

        public AdminController(ApplicationDbContext context, ILogger<AdminController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<ActionResult> Login([FromBody] AdminLoginDto dto)
        {
            try
            {
                _logger.LogInformation($"Login attempt for email: {dto.Email}");

                // Спрощена перевірка для тестування
                if (dto.Email == "admin@exambooking.com" && dto.Password == "Admin123!")
                {
                    var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

                    return Ok(new
                    {
                        success = true,
                        token = token,
                        admin = new
                        {
                            email = "admin@exambooking.com",
                            fullName = "System Administrator"
                        }
                    });
                }

                // Перевірка в БД
                var admin = await _context.Administrators
                    .FirstOrDefaultAsync(a => a.Email == dto.Email && a.IsActive);

                if (admin == null)
                {
                    _logger.LogWarning($"Admin not found: {dto.Email}");
                    return Unauthorized(new { message = "Invalid email or password" });
                }

                // Перевірка пароля
                if (!BCrypt.Net.BCrypt.Verify(dto.Password, admin.PasswordHash))
                {
                    _logger.LogWarning($"Invalid password for: {dto.Email}");
                    return Unauthorized(new { message = "Invalid email or password" });
                }

                admin.LastLoginAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                var dbToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

                return Ok(new
                {
                    success = true,
                    token = dbToken,
                    admin = new
                    {
                        email = admin.Email,
                        fullName = admin.FullName
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin login error");
                return StatusCode(500, new { message = "Login failed", error = ex.Message });
            }
        }

        [HttpGet("bookings")]
        public async Task<ActionResult> GetAllBookings()
        {
            try
            {
                var bookings = await _context.BookingRequests
                    .Include(b => b.AssignedExaminer)
                    .OrderByDescending(b => b.CreatedAt)
                    .Select(b => new
                    {
                        BookingId = $"BK{b.Id:D6}",
                        StudentName = $"{b.StudentFirstName} {b.StudentLastName}",
                        StudentEmail = b.StudentEmail,
                        StudentPhone = b.StudentPhone,
                        ExamType = b.ExamType,
                        Status = b.Status.ToString(),
                        IsPaid = b.IsPaid,
                        AssignedExaminerName = b.AssignedExaminer != null ? b.AssignedExaminer.Name : null,
                        CreatedAt = b.CreatedAt,
                        ScheduledDate = b.ScheduledDate
                    })
                    .ToListAsync();

                return Ok(bookings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all bookings");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("dashboard-stats")]
        public async Task<ActionResult> GetDashboardStats()
        {
            try
            {
                var stats = new
                {
                    TotalBookings = await _context.BookingRequests.CountAsync(),
                    AssignedBookings = await _context.BookingRequests
                        .CountAsync(b => b.AssignedExaminerId != null),
                    PendingBookings = await _context.BookingRequests
                        .CountAsync(b => b.Status == Models.BookingStatus.ExaminersContacted),
                    NoExaminerFound = await _context.BookingRequests
                        .CountAsync(b => b.Status == Models.BookingStatus.ExaminersContacted &&
                                   b.CreatedAt < DateTime.UtcNow.AddDays(-2)),
                    TotalRevenue = await _context.BookingRequests
                        .Where(b => b.IsPaid)
                        .SumAsync(b => b.Amount)
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard stats");
                return StatusCode(500, "Failed to load statistics");
            }
        }

        [HttpGet("examiner-responses")]
        public async Task<ActionResult> GetExaminerResponses()
        {
            try
            {
                var responses = await _context.ExaminerResponses
                    .Include(r => r.BookingRequest)
                    .OrderByDescending(r => r.ContactedAt)
                    .Take(50)
                    .Select(r => new
                    {
                        BookingId = $"BK{r.BookingRequestId:D6}",
                        ExaminerName = "Examiner", // Тимчасово, бо зв'язок з Examiner відключений
                        ExaminerEmail = "examiner@example.com",
                        Response = r.Response.ToString(),
                        ContactedAt = r.ContactedAt,
                        RespondedAt = r.RespondedAt,
                        ResponseMessage = r.ResponseMessage,
                        StudentName = $"{r.BookingRequest.StudentFirstName} {r.BookingRequest.StudentLastName}"
                    })
                    .ToListAsync();

                return Ok(responses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting examiner responses");
                return StatusCode(500, "Failed to load responses");
            }
        }

        [HttpPost("process-refund/{bookingId}")]
        public async Task<ActionResult> ProcessRefund(string bookingId)
        {
            try
            {
                if (!bookingId.StartsWith("BK") || !int.TryParse(bookingId.Substring(2), out int id))
                    return BadRequest("Invalid booking ID");

                var booking = await _context.BookingRequests
                    .FirstOrDefaultAsync(b => b.Id == id);

                if (booking == null)
                    return NotFound("Booking not found");

                if (!booking.IsPaid)
                    return BadRequest("Booking is not paid");

                if (booking.AssignedExaminerId != null)
                    return BadRequest("Cannot refund - examiner already assigned");

                booking.Status = Models.BookingStatus.Refunded;
                booking.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Refund processed for booking {bookingId}");

                return Ok(new { message = "Refund processed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing refund for {bookingId}");
                return StatusCode(500, "Failed to process refund");
            }
        }

        [HttpPost("create-admin")]
        public async Task<ActionResult> CreateAdmin([FromBody] CreateAdminDto dto)
        {
            try
            {
                if (await _context.Administrators.AnyAsync(a => a.Email == dto.Email))
                {
                    return BadRequest("Admin with this email already exists");
                }

                var admin = new Models.Administrator
                {
                    Email = dto.Email,
                    Phone = dto.Phone,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                    FullName = dto.FullName,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Administrators.Add(admin);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Admin created successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating admin");
                return StatusCode(500, "Failed to create admin");
            }
        }
    }

    public class AdminLoginDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class CreateAdminDto
    {
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string Password { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
    }
}