using System;
using System.Collections.Generic;

namespace ticketing_system_backend.Helpers
{
  public static class EmailTemplates
  {
    public static string BaseUrl { get; set; } = "http://localhost:4200";
    public static string ApiBaseUrl { get; set; } = "http://localhost:5166";
    public static string CompanyName { get; set; } = "Empower Logics";

    /// <summary>
    /// Generates a deterministic, RFC-compliant Message-ID for a ticket thread.
    /// All emails sent for the same ticket number will share this ID, enabling mail clients to thread them.
    /// </summary>
    public static string GetThreadId(string ticketNumber)
        => $"<ticket-{ticketNumber.ToLower().Replace(" ", "-")}@{CompanyName.Replace(" ", "").ToLower()}.support>";

    private static string BuildEmailHtml(string recipientName, string title, string headerColor, string contentHtml, string? actionText = null, string? actionUrl = null)
    {
      var accentLine = headerColor;
      var actionBtnHtml = "";
      if (!string.IsNullOrEmpty(actionText) && !string.IsNullOrEmpty(actionUrl))
      {
        actionBtnHtml = $@"
          <div style='text-align: center; margin: 25px 0 10px;'>
            <a href='{actionUrl}' target='_blank' style='background: {accentLine}; color: #ffffff; text-decoration: none; padding: 10px 24px; border-radius: 6px; font-weight: 600; font-size: 13px; display: inline-block; box-shadow: 0 4px 10px rgba(0,0,0,0.08);'>
              {actionText}
            </a>
          </div>";
      }

      var contentTableHtml = "";
      if (!string.IsNullOrEmpty(contentHtml))
      {
        contentTableHtml = $@"
            <table role='presentation' border='0' cellpadding='0' cellspacing='0' width='100%' style='background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; margin: 16px 0; font-size: 13px; line-height: 1.6; color: #334155;'>
              {contentHtml}
            </table>";
      }

      return $@"
  <!DOCTYPE html>
  <html>
  <head>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'/>
    <meta http-equiv='Content-Type' content='text/html; charset=UTF-8'/>
  </head>
  <body style='background-color: #f8fafc; font-family: ""Inter"", -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif; margin: 0; padding: 20px; -webkit-font-smoothing: antialiased;'>
    <table role='presentation' border='0' cellpadding='0' cellspacing='0' width='100%' style='max-width: 580px; margin: 0 auto;'>
      <tr>
        <td style='padding: 0;'>
          <!-- Header -->
          <div style='background: #0f172a; padding: 22px 20px; border-top-left-radius: 12px; border-top-right-radius: 12px; border-bottom: 3px solid {accentLine}; text-align: center;'>
            <h2 style='color: #ffffff; margin: 0; font-size: 18px; font-weight: 700; letter-spacing: -0.5px;'>
              {CompanyName} Support Center
            </h2>
          </div>

          <!-- Main Card -->
          <div style='background: #ffffff; padding: 26px 24px; border-bottom-left-radius: 12px; border-bottom-right-radius: 12px; border: 1px solid #e2e8f0; border-top: none; box-shadow: 0 4px 20px rgba(0,0,0,0.015);'>
            <h3 style='color: #0f172a; margin-top: 0; margin-bottom: 8px; font-size: 15px; font-weight: 700;'>
              Hello {recipientName},
            </h3>
            <p style='color: #475569; font-size: 13.5px; line-height: 1.5; margin-top: 0; margin-bottom: 16px;'>
              {title}
            </p>
            
            {contentTableHtml}

            {actionBtnHtml}
          </div>

          <!-- Footer -->
          <div style='text-align: center; padding: 20px 10px; font-size: 11px; color: #94a3b8; line-height: 1.5;'>
            <p style='margin: 0 0 5px;'>This is an automated notification from the {CompanyName} Support Team.</p>
            <p style='margin: 0 0 5px;'>&copy; {DateTime.UtcNow.AddMinutes(330):yyyy} {CompanyName} Inc. All rights reserved.</p>
            <p style='margin: 6px 0 0; font-size: 12px; font-weight: 600; color: #475569;'>Powered by Empower Logics</p>
          </div>
        </td>
      </tr>
    </table>
  </body>
  </html>";
    }

    /// <summary>
    /// Generates welcome email for a new PM or Developer (internal staff only — NOT sent to customers).
    /// </summary>
    public static (string Subject, string Body) UserCreation(string fullName, string userNumber, string password, string role)
    {
      string subject = $"Welcome to {CompanyName} Support Portal!";
      string themeColor = "#8b5cf6"; // Purple (internal staff)

      string contentHtml = $@"
        <tr>
          <td style='padding: 4px 0;'><strong>Portal Role:</strong></td>
          <td style='padding: 4px 0; color: {themeColor}; font-weight: 600;'>{role}</td>
        </tr>
        <tr>
          <td style='padding: 4px 0;'><strong>Login User ID:</strong></td>
          <td style='padding: 4px 0; font-family: monospace; font-size: 14px; font-weight: bold;'>{userNumber}</td>
        </tr>
        <tr>
          <td style='padding: 4px 0;'><strong>Temp Password:</strong></td>
          <td style='padding: 4px 0; font-family: monospace; font-size: 14px; font-weight: bold;'>{password}</td>
        </tr>";

      string portalUrl = $"{BaseUrl.TrimEnd('/')}/login";
      string body = BuildEmailHtml(fullName, $"Your account for the {CompanyName} ticketing portal has been set up. Please log in using the credentials below and change your password after first login:", themeColor, contentHtml, "Access Portal Now", portalUrl);
      return (subject, body);
    }

    /// <summary>
    /// Generates welcome email for a newly created Customer account.
    /// No portal link or password — customer interacts only via email.
    /// </summary>
    public static (string Subject, string Body) CustomerAccountCreated(string fullName, string contactPerson)
    {
      string subject = $"Welcome to {CompanyName} Support!";
      string themeColor = "#10b981"; // Emerald (customer)

      string contentHtml = $@"
        <tr>
          <td colspan='2' style='padding: 8px 0; color: #475569; font-size: 13px; line-height: 1.6;'>
            We have created your support account with {CompanyName}. Whenever you raise an issue or query, 
            our team will log a support ticket on your behalf and you will receive all updates — including ticket details, 
            progress, and resolution — directly to this email address.
          </td>
        </tr>
        <tr>
          <td style='padding: 4px 0; width: 130px;'><strong>Account Name:</strong></td>
          <td style='padding: 4px 0; font-weight: 600;'>{fullName}</td>
        </tr>
        {(!string.IsNullOrEmpty(contactPerson) ? $@"
        <tr>
          <td style='padding: 4px 0;'><strong>Contact Person:</strong></td>
          <td style='padding: 4px 0;'>{contactPerson}</td>
        </tr>" : "")}
        <tr>
          <td colspan='2' style='padding: 12px 0 4px; font-size: 12px; color: #94a3b8; border-top: 1px solid #e2e8f0; margin-top: 8px;'>
            To raise a support request, simply contact your {CompanyName} representative or send an email to our support team.
          </td>
        </tr>";

      string body = BuildEmailHtml(fullName, "Your support account has been set up successfully. You do not need to log in anywhere — all ticket updates will be sent to you directly via email.", themeColor, contentHtml);
      return (subject, body);
    }

    /// <summary>
    /// Generates email to PMs for a newly logged ticket (internal).
    /// </summary>
    public static (string Subject, string Body) NewTicket(string recipientName, string customerName, string ticketNumber, string ticketSubject)
    {
      string subject = $"New Ticket Created: [{ticketNumber}] {ticketSubject}";
      string themeColor = "#8b5cf6"; // Purple (internal)

      string contentHtml = $@"
        <tr>
          <td style='padding: 4px 0; width: 110px;'><strong>Ticket Number:</strong></td>
          <td style='padding: 4px 0; font-weight: 600; color: #0f172a;'>{ticketNumber}</td>
        </tr>
        <tr>
          <td style='padding: 4px 0;'><strong>Customer:</strong></td>
          <td style='padding: 4px 0;'>{customerName}</td>
        </tr>
        <tr>
          <td style='padding: 4px 0;'><strong>Subject:</strong></td>
          <td style='padding: 4px 0; color: #475569;'>{ticketSubject}</td>
        </tr>";

      string portalUrl = $"{BaseUrl.TrimEnd('/')}/pm";
      string body = BuildEmailHtml(recipientName, "A new support ticket has been created and requires your attention. Details are summarized below:", themeColor, contentHtml, "Review PM Dashboard", portalUrl);
      return (subject, body);
    }

    /// <summary>
    /// Sends a full ticket confirmation to the customer — no portal link, includes full description and attachment list.
    /// This is the customer's primary record of their ticket since they do not use the portal.
    /// </summary>
    public static (string Subject, string Body) NewTicketConfirmation(
        string customerName,
        string ticketNumber,
        string? docketNumber,
        DateTime? deadlineDate)
    {
      string subject = $"Ticket [{ticketNumber}]";
      string themeColor = "#10b981"; // Emerald/green (customer facing)
      
      string slaResponse = "";
      if (deadlineDate.HasValue)
      {
          slaResponse = deadlineDate.Value.ToString("dd MMM yyyy, hh:mm tt") + " IST";
      }
      else
      {
          slaResponse = "Next Working day";
      }

      string docketText = !string.IsNullOrWhiteSpace(docketNumber) ? docketNumber : "N/A";
      string originalMessage = $"We have received your query about shipment {docketText}. Your ticket id is {ticketNumber}. Our team will respond by {slaResponse}.";

      string body = BuildEmailHtml(customerName, originalMessage, themeColor, "");
      return (subject, body);
    }

    /// <summary>
    /// Generates email notification when a developer is assigned a ticket (internal).
    /// </summary>
    public static (string Subject, string Body) TicketAssigned(string assigneeName, string ticketNumber, string assignedBy, string ticketSubject)
    {
      string subject = $"Task Assignment: Ticket [{ticketNumber}]";
      string themeColor = "#8b5cf6"; // Purple (internal)

      string contentHtml = $@"
        <tr>
          <td style='padding: 4px 0; width: 110px;'><strong>Ticket Number:</strong></td>
          <td style='padding: 4px 0; font-weight: 600; color: #0f172a;'>{ticketNumber}</td>
        </tr>
        <tr>
          <td style='padding: 4px 0;'><strong>Assigned By:</strong></td>
          <td style='padding: 4px 0;'>{assignedBy}</td>
        </tr>
        <tr>
          <td style='padding: 4px 0;'><strong>Subject:</strong></td>
          <td style='padding: 4px 0; color: #475569;'>{ticketSubject}</td>
        </tr>";

      string portalUrl = $"{BaseUrl.TrimEnd('/')}/developer";
      string body = BuildEmailHtml(assigneeName, "A new support ticket task has been assigned to you. Please review and proceed with investigation:", themeColor, contentHtml, "View Dev Dashboard", portalUrl);
      return (subject, body);
    }

    /// <summary>
    /// Notifies customer that a specialist has been assigned. No portal link.
    /// </summary>
    public static (string Subject, string Body) TicketAssignedToCustomer(string customerName, string ticketNumber, string assigneeName, string ticketSubject)
    {
      string subject = $"Update: A specialist has been assigned to your ticket [{ticketNumber}]";
      string themeColor = "#10b981"; // Emerald (customer)

      string contentHtml = $@"
        <tr>
          <td style='padding: 4px 0; width: 130px;'><strong>Ticket Number:</strong></td>
          <td style='padding: 4px 0; font-weight: 600; color: #10b981;'>{ticketNumber}</td>
        </tr>
        <tr>
          <td style='padding: 4px 0;'><strong>Subject:</strong></td>
          <td style='padding: 4px 0;'>{ticketSubject}</td>
        </tr>
        <tr>
          <td style='padding: 4px 0;'><strong>Assigned Agent:</strong></td>
          <td style='padding: 4px 0; font-weight: 600;'>{assigneeName}</td>
        </tr>
        <tr>
          <td colspan='2' style='padding: 10px 0 4px; border-top: 1px solid #e2e8f0; font-size: 12px; color: #94a3b8;'>
            Our team is actively working on your issue. You will receive a further update once there is progress or resolution. 
            No action is required from your side at this time.
          </td>
        </tr>";

      // No action button — customer doesn't use the portal
      string body = BuildEmailHtml(customerName, "Good news! A dedicated support specialist has been assigned to your ticket and is actively working on it.", themeColor, contentHtml);
      return (subject, body);
    }

    /// <summary>
    /// Generates email for ticket status updates. No portal link for customers.
    /// </summary>
    public static (string Subject, string Body) TicketStatusUpdate(string recipientName, string ticketNumber, string newStatus, string updatedBy, string ticketSubject, bool isCustomer = true, int ticketId = 0, string? remark = null)
    {
      string subject = $"Support Ticket Logged: [{ticketNumber}] – {ticketSubject}";
      string themeColor = isCustomer ? "#10b981" : "#8b5cf6";

      var statusColor = newStatus?.ToLower() switch
      {
        "closed"      => "#10b981",
        "resolved"    => "#10b981",
        "in progress" => "#8b5cf6",
        "open"        => "#f59e0b",
        _             => "#64748b"
      };

      bool isClosed = newStatus?.ToLower() == "closed" || newStatus?.ToLower() == "close";
      string ratingHtml = "";

      if (isClosed && isCustomer)
      {
        int targetId = ticketId;
        if (targetId <= 0 && !string.IsNullOrEmpty(ticketNumber))
        {
          var parts = ticketNumber.Trim().Split('-');
          if (parts.Length > 0 && int.TryParse(parts[parts.Length - 1].Trim(), out int parsedId))
          {
            targetId = parsedId;
          }
          else
          {
            var match = System.Text.RegularExpressions.Regex.Match(ticketNumber.Trim(), @"\d+$");
            if (match.Success)
            {
              int.TryParse(match.Value, out targetId);
            }
          }
        }

        ratingHtml = $@"
        <tr>
          <td colspan='2' style='padding: 18px 0 4px; border-top: 1px solid #e2e8f0; text-align: center;'>
            <strong style='color: #0f172a; font-size: 14px;'>How would you rate our service for this ticket?</strong>
            <div style='margin-top: 16px;'>
              <a href='{ApiBaseUrl.TrimEnd('/')}/api/Tickets/{targetId}/rate?rating=1' target='_blank' style='text-decoration: none; display: inline-block; width: 32px; height: 32px; line-height: 32px; border-radius: 50%; border: 1.5px solid #ef4444; color: #ef4444; font-weight: bold; text-align: center; margin: 0 5px; font-size: 14px;' title='1 - Poor'>1</a>
              <a href='{ApiBaseUrl.TrimEnd('/')}/api/Tickets/{targetId}/rate?rating=2' target='_blank' style='text-decoration: none; display: inline-block; width: 32px; height: 32px; line-height: 32px; border-radius: 50%; border: 1.5px solid #f97316; color: #f97316; font-weight: bold; text-align: center; margin: 0 5px; font-size: 14px;' title='2 - Fair'>2</a>
              <a href='{ApiBaseUrl.TrimEnd('/')}/api/Tickets/{targetId}/rate?rating=3' target='_blank' style='text-decoration: none; display: inline-block; width: 32px; height: 32px; line-height: 32px; border-radius: 50%; border: 1.5px solid #f59e0b; color: #f59e0b; font-weight: bold; text-align: center; margin: 0 5px; font-size: 14px;' title='3 - Good'>3</a>
              <a href='{ApiBaseUrl.TrimEnd('/')}/api/Tickets/{targetId}/rate?rating=4' target='_blank' style='text-decoration: none; display: inline-block; width: 32px; height: 32px; line-height: 32px; border-radius: 50%; border: 1.5px solid #10b981; color: #10b981; font-weight: bold; text-align: center; margin: 0 5px; font-size: 14px;' title='4 - Very Good'>4</a>
              <a href='{ApiBaseUrl.TrimEnd('/')}/api/Tickets/{targetId}/rate?rating=5' target='_blank' style='text-decoration: none; display: inline-block; width: 32px; height: 32px; line-height: 32px; border-radius: 50%; border: 1.5px solid #10b981; color: #ffffff; background-color: #10b981; font-weight: bold; text-align: center; margin: 0 5px; font-size: 14px;' title='5 - Excellent'>5</a>
            </div>
            <p style='margin: 8px 0 0; font-size: 11px; color: #64748b;'>Click on a number to submit your rating directly.</p>
          </td>
        </tr>";
      }

      string remarkHtml = "";
      if (!string.IsNullOrWhiteSpace(remark))
      {
        remarkHtml = $@"
        <tr>
          <td style='padding: 8px 0 4px; vertical-align: top;'><strong>Remark / Note:</strong></td>
          <td style='padding: 8px 0 4px;'>
            <div style='background-color: #f8fafc; border-left: 3px solid {statusColor}; padding: 8px 12px; color: #1e293b; font-size: 13px; font-style: italic; border-radius: 0 4px 4px 0;'>
              {System.Net.WebUtility.HtmlEncode(remark)}
            </div>
          </td>
        </tr>";
      }

      string contentHtml = $@"
        <tr>
          <td style='padding: 4px 0; width: 110px;'><strong>Ticket Number:</strong></td>
          <td style='padding: 4px 0; font-weight: 600; color: #0f172a;'>{ticketNumber}</td>
        </tr>
        <tr>
          <td style='padding: 4px 0;'><strong>Subject:</strong></td>
          <td style='padding: 4px 0; pointer-events: none;'>{ticketSubject}</td>
        </tr>
        <tr>
          <td style='padding: 4px 0;'><strong>New Status:</strong></td>
          <td style='padding: 4px 0; font-weight: 700; color: {statusColor};'>{newStatus}</td>
        </tr>
        <tr>
          <td style='padding: 4px 0;'><strong>Updated By:</strong></td>
          <td style='padding: 4px 0;'>{updatedBy}</td>
        </tr>
        {remarkHtml}
        {ratingHtml}
        {(isCustomer && !isClosed ? $@"
        <tr>
          <td colspan='2' style='padding: 10px 0 4px; border-top: 1px solid #e2e8f0; font-size: 12px; color: #94a3b8;'>
            No action is required from your side. If you have queries, contact your {CompanyName} representative.
          </td>
        </tr>" : "")}";

      // Internal staff get portal button; customers do not
      string? actionText = isCustomer ? null : "Open Support Portal";
      string? actionUrl  = isCustomer ? null : $"{BaseUrl.TrimEnd('/')}/pm";

      string body = BuildEmailHtml(recipientName, "The status of your support ticket has been updated. Please see the latest details below:", themeColor, contentHtml, actionText, actionUrl);
      return (subject, body);
    }

    /// <summary>
    /// Generates email for ticket priority updates.
    /// </summary>
    public static (string Subject, string Body) TicketPriorityUpdate(string recipientName, string ticketNumber, string oldPriority, string newPriority, string changedBy, string ticketSubject)
    {
      string subject = $"Update: Priority for [{ticketNumber}] changed to {newPriority}";
      string themeColor = "#8b5cf6";

      string contentHtml = $@"
        <tr>
          <td style='padding: 4px 0; width: 110px;'><strong>Ticket Number:</strong></td>
          <td style='padding: 4px 0; font-weight: 600; color: #0f172a;'>{ticketNumber}</td>
        </tr>
        <tr>
          <td style='padding: 4px 0;'><strong>Old Priority:</strong></td>
          <td style='padding: 4px 0; text-decoration: line-through;'>{oldPriority}</td>
        </tr>
        <tr>
          <td style='padding: 4px 0;'><strong>New Priority:</strong></td>
          <td style='padding: 4px 0; font-weight: 600; color: #f97316;'>{newPriority}</td>
        </tr>
        <tr>
          <td style='padding: 4px 0;'><strong>Updated By:</strong></td>
          <td style='padding: 4px 0;'>{changedBy}</td>
        </tr>
        <tr>
          <td style='padding: 4px 0;'><strong>Subject:</strong></td>
          <td style='padding: 4px 0;'>{ticketSubject}</td>
        </tr>";

      string portalUrl = $"{BaseUrl.TrimEnd('/')}/pm";
      string body = BuildEmailHtml(recipientName, "The priority level of the support ticket has been updated:", themeColor, contentHtml, "Open in Dashboard", portalUrl);
      return (subject, body);
    }

    /// <summary>
    /// Generates email to PMs when a developer submits a ticket for review (internal).
    /// </summary>
    public static (string Subject, string Body) TicketReviewRequest(string pmName, string developerName, string ticketNumber, string ticketSubject)
    {
      string subject = $"Awaiting Review: Ticket [{ticketNumber}] submitted by Dev";
      string themeColor = "#8b5cf6"; // Purple (internal)

      string contentHtml = $@"
        <tr>
          <td style='padding: 4px 0; width: 110px;'><strong>Ticket Number:</strong></td>
          <td style='padding: 4px 0; font-weight: 600; color: #0f172a;'>{ticketNumber}</td>
        </tr>
        <tr>
          <td style='padding: 4px 0;'><strong>Developer:</strong></td>
          <td style='padding: 4px 0; font-weight: 600;'>{developerName}</td>
        </tr>
        <tr>
          <td style='padding: 4px 0;'><strong>Subject:</strong></td>
          <td style='padding: 4px 0;'>{ticketSubject}</td>
        </tr>";

      string portalUrl = $"{BaseUrl.TrimEnd('/')}/pm";
      string body = BuildEmailHtml(pmName, "An assignee has marked their work completed and submitted the ticket for formal review. Please inspect and verify resolution status:", themeColor, contentHtml, "Review in PM Dashboard", portalUrl);
      return (subject, body);
    }
  }
}
