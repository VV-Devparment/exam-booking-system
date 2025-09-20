using SendGrid;
using SendGrid.Helpers.Mail;

namespace ExamBookingSystem.Services
{
    public class EmailService : IEmailService
    {
        private readonly ISendGridClient _sendGridClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly bool _isDemoMode;

        public EmailService(ISendGridClient sendGridClient, IConfiguration configuration, ILogger<EmailService> logger)
        {
            _sendGridClient = sendGridClient;
            _configuration = configuration;
            _logger = logger;

            
            var apiKey = _configuration["SendGrid:ApiKey"];
            _isDemoMode = string.IsNullOrEmpty(apiKey) ||
                         apiKey.StartsWith("your-") ||
                         apiKey.Length < 20;

            if (_isDemoMode)
            {
                _logger.LogWarning("📧 Email Service running in DEMO MODE - emails will be simulated");
            }
        }

        public async Task<bool> SendEmailAsync(string to, string subject, string body, string? fromName = null)
        {
           
            if (_isDemoMode)
            {
                _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                _logger.LogInformation("📧 EMAIL SIMULATION (Demo Mode)");
                _logger.LogInformation($"📨 To: {to}");
                _logger.LogInformation($"📝 Subject: {subject}");
                _logger.LogInformation($"👤 From: {fromName ?? "Exam Booking System"}");
                _logger.LogInformation("📄 Body Preview:");

                
                var plainBody = body.Replace("<br>", "\n")
                                   .Replace("</p>", "\n")
                                   .Replace("</li>", "\n")
                                   .Replace("<[^>]*>", "");

                var preview = plainBody.Length > 300
                    ? plainBody.Substring(0, 300) + "..."
                    : plainBody;

                _logger.LogInformation(preview);
                _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                _logger.LogInformation("✅ Email simulated successfully!");

                return await Task.FromResult(true);
            }

            
            try
            {
                var from = new EmailAddress("noreply@examwoodwood.com", fromName ?? "Exam Booking System");
                var toEmail = new EmailAddress(to);
                var msg = MailHelper.CreateSingleEmail(from, toEmail, subject, body, body);

                var response = await _sendGridClient.SendEmailAsync(msg);

                if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
                {
                    _logger.LogInformation($"✅ Email sent successfully to {to}");
                    return true;
                }
                else
                {
                    _logger.LogError($"❌ Failed to send email. Status: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error sending email to {to}");
                return false;
            }
        }

        public async Task<bool> SendExaminerContactEmailAsync(
            string examinerEmail,
            string examinerName,
            string studentName,
            string examType,
            DateTime preferredDate)
        {
            var subject = $"🎓 New Exam Request - {examType}";

            var body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 20px; color: white; border-radius: 10px 10px 0 0;'>
                        <h2 style='margin: 0;'>New Exam Request</h2>
                    </div>
                    
                    <div style='padding: 20px; background: #f8f9fa; border: 1px solid #dee2e6; border-top: none;'>
                        <p>Hello <strong>{examinerName}</strong>,</p>
                        
                        <p>You have received a new exam request:</p>
                        
                        <div style='background: white; padding: 15px; border-radius: 8px; margin: 20px 0;'>
                            <table style='width: 100%;'>
                                <tr>
                                    <td style='padding: 8px 0;'><strong>👤 Student:</strong></td>
                                    <td>{studentName}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 8px 0;'><strong>📚 Exam Type:</strong></td>
                                    <td>{examType}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 8px 0;'><strong>📅 Preferred Date:</strong></td>
                                    <td>{preferredDate:dddd, MMMM dd, yyyy}</td>
                                </tr>
                            </table>
                        </div>
                        
                        <div style='background: #fff3cd; border: 1px solid #ffc107; padding: 15px; border-radius: 8px; margin: 20px 0;'>
                            <p style='margin: 0; color: #856404;'>
                                <strong>⚡ IMPORTANT:</strong> First examiner to accept wins! Please respond quickly if you're available.
                            </p>
                        </div>
                        
                        <p>Please use the API endpoint to respond with your availability.</p>
                        
                        <div style='margin-top: 30px; padding-top: 20px; border-top: 1px solid #dee2e6; color: #6c757d; font-size: 12px;'>
                            <p>This is an automated message from Exam Booking System.</p>
                        </div>
                    </div>
                </div>";

            _logger.LogInformation($"📤 Sending examiner contact email to {examinerName} ({examinerEmail})");
            return await SendEmailAsync(examinerEmail, subject, body, "Exam Booking System");
        }

        public async Task<bool> SendStudentConfirmationEmailAsync(
            string studentEmail,
            string studentName,
            string examinerName,
            DateTime scheduledDate)
        {
            var subject = "✅ Exam Scheduled - Confirmation";

            var body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <div style='background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%); padding: 20px; color: white; border-radius: 10px 10px 0 0;'>
                        <h2 style='margin: 0;'>✅ Exam Confirmed!</h2>
                    </div>
                    
                    <div style='padding: 20px; background: #f8f9fa; border: 1px solid #dee2e6; border-top: none;'>
                        <p>Hello <strong>{studentName}</strong>,</p>
                        
                        <p>Great news! Your exam has been successfully scheduled.</p>
                        
                        <div style='background: white; padding: 20px; border-radius: 8px; margin: 20px 0; border-left: 4px solid #28a745;'>
                            <h3 style='margin-top: 0; color: #28a745;'>📋 Exam Details</h3>
                            <table style='width: 100%;'>
                                <tr>
                                    <td style='padding: 8px 0;'><strong>👨‍🏫 Examiner:</strong></td>
                                    <td>{examinerName}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 8px 0;'><strong>📅 Date:</strong></td>
                                    <td>{scheduledDate:dddd, MMMM dd, yyyy}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 8px 0;'><strong>⏰ Time:</strong></td>
                                    <td>{scheduledDate:HH:mm}</td>
                                </tr>
                            </table>
                        </div>
                        
                        <div style='background: #d4edda; border: 1px solid #c3e6cb; padding: 15px; border-radius: 8px; margin: 20px 0;'>
                            <p style='margin: 0; color: #155724;'>
                                📧 You will receive a calendar invitation shortly with all the details.
                            </p>
                        </div>
                        
                        <div style='background: #f0f0f0; padding: 15px; border-radius: 8px; margin: 20px 0;'>
                            <h4 style='margin-top: 0;'>📝 What to bring:</h4>
                            <ul style='margin: 0; padding-left: 20px;'>
                                <li>Valid ID document</li>
                                <li>Any required paperwork</li>
                                <li>Pen and paper for notes</li>
                            </ul>
                        </div>
                        
                        <p>If you need to reschedule or have any questions, please contact us as soon as possible.</p>
                        
                        <p>Good luck with your exam!</p>
                        
                        <div style='margin-top: 30px; padding-top: 20px; border-top: 1px solid #dee2e6; color: #6c757d; font-size: 12px;'>
                            <p>Best regards,<br>Exam Booking System Team</p>
                        </div>
                    </div>
                </div>";

            _logger.LogInformation($"📤 Sending confirmation email to {studentName} ({studentEmail})");
            return await SendEmailAsync(studentEmail, subject, body, "Exam Booking System");
        }
    }
}