using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace Eventizo.Helper
{
    public static class EmailHelper
    {
        public static async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            // ⚠️ Đặt email & mật khẩu ứng dụng của bạn tại đây
            var fromEmail = "honglamtoan0124@gmail.com";
            var password = "uubycfmwclgsuegv"; // không phải mật khẩu Gmail thường!

            var message = new MailMessage();
            message.From = new MailAddress(fromEmail, "Eventizo Support");
            message.To.Add(new MailAddress(toEmail));
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = true;

            using (var smtp = new SmtpClient("smtp.gmail.com", 587))
            {
                smtp.Credentials = new NetworkCredential(fromEmail, password);
                smtp.EnableSsl = true;
                await smtp.SendMailAsync(message);
            }
        }
    }
}
