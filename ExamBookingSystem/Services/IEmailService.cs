namespace ExamBookingSystem.Services
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string to, string subject, string body, string? fromName = null);
        Task<bool> SendExaminerContactEmailAsync(string examinerEmail, string examinerName, string studentName, string examType, DateTime preferredDate);
        Task<bool> SendStudentConfirmationEmailAsync(string studentEmail, string studentName, string examinerName, DateTime scheduledDate);
    }
}