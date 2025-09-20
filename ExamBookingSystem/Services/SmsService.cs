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

        public SmsService(IConfiguration configuration, ILogger<SmsService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            var accountSid = _configuration["Twilio:AccountSid"];
            var authToken = _configuration["Twilio:AuthToken"];

            if (!string.IsNullOrEmpty(accountSid) && !string.IsNullOrEmpty(authToken))
            {
                TwilioClient.Init(accountSid, authToken);
            }
        }

        public async Task<bool> SendSmsAsync(string to, string message)
        {
            try
            {
                var fromNumber = _configuration["Twilio:FromNumber"];

                if (string.IsNullOrEmpty(fromNumber))
                {
                    _logger.LogWarning("SMS DEMO MODE: Would send to {To}: {Message}", to, message);
                    return true;
                }

                var sms = await MessageResource.CreateAsync(
                    body: message,
                    from: new Twilio.Types.PhoneNumber(fromNumber),
                    to: new Twilio.Types.PhoneNumber(to)
                );

                _logger.LogInformation($"SMS sent: {sms.Sid}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send SMS");
                return false;
            }
        }
    }
}