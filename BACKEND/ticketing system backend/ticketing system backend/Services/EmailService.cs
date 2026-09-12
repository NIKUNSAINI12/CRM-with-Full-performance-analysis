using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

/// <summary>
/// Defines the contract for an email sending service.
/// </summary>
public interface IEmailService
{
  /// <summary>
  /// Sends an email asynchronously.
  /// </summary>
  /// <param name="toEmail">The recipient's email address.</param>
  /// <param name="subject">The subject of the email.</param>
  /// <param name="body">The HTML body of the email.</param>
  /// <param name="threadMessageId">
  /// Optional. The root Message-ID of the ticket thread (e.g. "&lt;ticket-TKT-001@empowerlogics.support&gt;").
  /// When provided, this email is set as a reply to that thread via In-Reply-To + References headers.
  /// When null, this email starts a new thread and uses threadMessageId as its own Message-ID.
  /// </param>
  /// <param name="isThreadRoot">
  /// When true, the email IS the root of the thread (ticket creation email).
  /// Its own Message-ID will be set to threadMessageId.
  /// </param>
  Task SendEmailAsync(string toEmail, string subject, string body, string? threadMessageId = null, bool isThreadRoot = false);
}

/// <summary>
/// SMTP implementation of IEmailService.
/// Supports email threading via Message-ID / In-Reply-To / References headers.
/// </summary>
public class SmtpEmailService : IEmailService
{
  private readonly string _host;
  private readonly int _port;
  private readonly string _fromAddress;
  private readonly string _password;
  private readonly string _companyName;

  public SmtpEmailService(string host, int port, string fromAddress, string password, string companyName = "Empower Logics")
  {
    _host = host;
    _port = port;
    _fromAddress = fromAddress;
    _password = password;
    _companyName = companyName;
  }

  public async Task SendEmailAsync(string toEmail, string subject, string body, string? threadMessageId = null, bool isThreadRoot = false)
  {
    if (string.IsNullOrWhiteSpace(toEmail))
    {
      return;
    }

    var client = new SmtpClient(_host, _port)
    {
      Credentials = new NetworkCredential(_fromAddress, _password),
      EnableSsl = true
    };

    var mailMessage = new MailMessage
    {
      From    = new MailAddress(_fromAddress, $"{_companyName} Support"),
      Subject = subject,
      Body    = body,
      IsBodyHtml = true,
    };
    mailMessage.To.Add(toEmail);

    // ── Email Threading Headers ──────────────────────────────────────────────
    // Gmail replaces custom Message-IDs, so injecting a fake In-Reply-To breaks 
    // threading instead of helping it. By relying strictly on identical Subject lines, 
    // Gmail and Outlook will thread the messages naturally.
    /*
    if (!string.IsNullOrEmpty(threadMessageId))
    {
      if (isThreadRoot)
      {
        mailMessage.Headers.Add("Message-ID", threadMessageId);
      }
      else
      {
        mailMessage.Headers.Add("In-Reply-To", threadMessageId);
        mailMessage.Headers.Add("References",  threadMessageId);
      }
    }
    */
    // ────────────────────────────────────────────────────────────────────────

    await client.SendMailAsync(mailMessage);
  }
}
