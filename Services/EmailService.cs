using Hangfire.Server;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MimeKit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WSTKNG.Models;

namespace WSTKNG.Services
{
    public interface IEmailService
    {
        Task Send(string filename, Stream file);
    }

    public class EmailService : IEmailService
    {

        private readonly ApplicationContext _context;

        public EmailService(ApplicationContext context)
        {
            _context = context;
        }

        public async Task Send(string filename, Stream file)
        {
            Setting setting = await _context.Settings.FindAsync(1);
            if (setting == null) return;

            // create message
            var email = new MimeMessage();
            email.Sender = MailboxAddress.Parse(setting.EmailFrom);
            email.To.Add(MailboxAddress.Parse(setting.KindleEmail));
            email.Subject = "convert";

            var builder = new BodyBuilder { HtmlBody = "convert" };
            builder.Attachments.Add(filename, file);

            email.Body = builder.ToMessageBody();

            // send email
            using var smtp = new SmtpClient();
            smtp.Connect(setting.SMTPHost, setting.SMTPPort, SecureSocketOptions.StartTls);
            smtp.Authenticate(setting.SMTPUser, setting.SMTPPassword);
            smtp.Send(email);
            smtp.Disconnect(true);
        }
    }
}
