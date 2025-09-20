using Newtonsoft.Json;

namespace ExamBookingSystem.Services
{
    public class SlackService : ISlackService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SlackService> _logger;
        private readonly string? _webhookUrl;

        public SlackService(HttpClient httpClient, IConfiguration configuration, ILogger<SlackService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _webhookUrl = _configuration["Slack:WebhookUrl"];
        }

        public async Task<bool> SendNotificationAsync(string message, string? channel = null)
        {
            if (string.IsNullOrEmpty(_webhookUrl))
            {
                _logger.LogWarning("Slack webhook URL not configured");
                return false;
            }

            try
            {
                var payload = new
                {
                    text = message,
                    channel = channel ?? "#exam-bookings",
                    username = "Exam Booking Bot",
                    icon_emoji = ":calendar:"
                };

                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(_webhookUrl, content);

                _logger.LogInformation($"Slack notification sent, Status: {response.StatusCode}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send Slack notification");
                return false;
            }
        }

        public async Task<bool> NotifyNewBookingAsync(string studentName, string examType, DateTime preferredDate)
        {
            var message = $"🆕 *New Exam Booking*\n" +
                         $"Student: {studentName}\n" +
                         $"Exam Type: {examType}\n" +
                         $"Preferred Date: {preferredDate:dddd, MMMM dd, yyyy}";

            return await SendNotificationAsync(message);
        }

        public async Task<bool> NotifyExaminerResponseAsync(string examinerName, string response, string studentName)
        {
            var emoji = response.ToLower() == "accepted" ? "✅" : "❌";
            var message = $"{emoji} *Examiner Response*\n" +
                         $"Examiner: {examinerName}\n" +
                         $"Response: {response}\n" +
                         $"Student: {studentName}";

            return await SendNotificationAsync(message);
        }

        public async Task<bool> NotifyErrorAsync(string error, string? details = null)
        {
            var message = $"🚨 *System Error*\n{error}";
            if (!string.IsNullOrEmpty(details))
            {
                message += $"\nDetails: {details}";
            }

            return await SendNotificationAsync(message, "#alerts");
        }
    }
}