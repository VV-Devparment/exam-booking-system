using Microsoft.AspNetCore.Mvc;
using ExamBookingSystem.Services;

namespace ExamBookingSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly IEmailService _emailService;
        private readonly ISlackService _slackService;
        private readonly ILocationService _locationService;

        public TestController(
            IEmailService emailService,
            ISlackService slackService,
            ILocationService locationService)
        {
            _emailService = emailService;
            _slackService = slackService;
            _locationService = locationService;
        }

        [HttpPost("email")]
        public async Task<ActionResult> TestEmail([FromQuery] string to, [FromQuery] string subject = "Test Email")
        {
            var result = await _emailService.SendEmailAsync(to, subject, "This is a test email from Exam Booking System");
            return Ok(new { success = result, message = result ? "Email sent" : "Email failed" });
        }

        [HttpPost("slack")]
        public async Task<ActionResult> TestSlack([FromQuery] string message = "Test notification")
        {
            var result = await _slackService.SendNotificationAsync(message);
            return Ok(new { success = result, message = result ? "Slack sent" : "Slack failed" });
        }

        [HttpGet("location/distance")]
        public ActionResult TestDistance(
            [FromQuery] double lat1, [FromQuery] double lon1,
            [FromQuery] double lat2, [FromQuery] double lon2)
        {
            var distance = _locationService.CalculateDistance(lat1, lon1, lat2, lon2);
            return Ok(new { distanceKm = Math.Round(distance, 2) });
        }

        [HttpGet("location/examiners")]
        public async Task<ActionResult> TestNearbyExaminers(
            [FromQuery] double latitude = 50.4501,
            [FromQuery] double longitude = 30.5234)
        {
            var examiners = await _locationService.FindNearbyExaminersAsync(latitude, longitude);
            return Ok(examiners);
        }
    }
}