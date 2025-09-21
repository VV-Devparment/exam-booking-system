using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace ExamBookingSystem.Services
{
    public interface ISmsService
    {
        Task<bool> SendSmsAsync(string to, string message);
    }

    public class SmsService : ISmsService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SmsService> _logger;
        private readonly bool _isDemoMode;

        public SmsService(IConfiguration configuration, ILogger<SmsService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            var accountSid = _configuration["Twilio:AccountSid"];
            var authToken = _configuration["Twilio:AuthToken"];
            
            // Demo mode якщо credentials тестові, пусті або неправильні
            _isDemoMode = string.IsNullOrEmpty(accountSid) ||
                         string.IsNullOrEmpty(authToken) ||
                         accountSid.StartsWith("AC40a8dcb17a692efd72883b3f2baa14e8") ||
                         authToken == "c5b35cbc4d501dfb27fdbbc1a0069a29" ||
                         accountSid.Contains("YOUR_");

            if (_isDemoMode)
            {
                _logger.LogWarning("📱 SMS Service running in DEMO MODE - messages will be simulated");
            }
            else
            {
                TwilioClient.Init(accountSid, authToken);
            }
        }

        public async Task<bool> SendSmsAsync(string to, string message)
        {
            if (_isDemoMode)
            {
                _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                _logger.LogInformation("📱 SMS SIMULATION (Demo Mode)");
                _logger.LogInformation($"📞 To: {to}");
                _logger.LogInformation($"💬 Message: {message}");
                _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                _logger.LogInformation("✅ SMS sent successfully! (simulated)");
                
                return await Task.FromResult(true);
            }

            try
            {
                var fromNumber = _configuration["Twilio:FromNumber"];

                if (string.IsNullOrEmpty(fromNumber))
                {
                    _logger.LogWarning("SMS FROM number not configured, treating as demo mode");
                    return true;
                }

                var sms = await MessageResource.CreateAsync(
                    body: message,
                    from: new Twilio.Types.PhoneNumber(fromNumber),
                    to: new Twilio.Types.PhoneNumber(to)
                );

                _logger.LogInformation($"✅ SMS sent successfully: {sms.Sid}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to send SMS");
                return false;
            }
        }
    }
}