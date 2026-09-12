using System;
using System.Collections.Generic;

namespace ticketing_system_backend.Helpers
{
  public static class EmailTemplates
  {
    public static string BaseUrl { get; set; } = "http://localhost:4200";

    /// <summary>
    /// Generates a deterministic, RFC-compliant Message-ID for a ticket thread.
    /// All emails sent for the same ticket number will share this ID, enabling mail clients to thread them.
    /// </summary>
    public static string GetThreadId(string ticketNumber)
        => $"<ticket-{ticketNumber.ToLower().Replace(" ", "-")}@empowerlogics.support>";

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
              Empower Logics Support Center
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
            
            <table role='presentation' border='0' cellpadding='0' cellspacing='0' width='100%' style='background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; margin: 16px 0; font-size: 13px; line-height: 1.6; color: #334155;'>
              {contentHtml}
            </table>

            {actionBtnHtml}
          </div>

          <!-- Footer -->
          <div style='text-align: center; padding: 20px 10px; font-size: 11px; color: #94a3b8; line-height: 1.5;'>
            <p style='margin: 0 0 5px;'>This is an automated notification from the Empower Logics Support Team.</p>
            <p style='margin: 0;'>&copy; {DateTime.UtcNow.AddMinutes(330):yyyy} Empower Logics Inc. All rights reserved.</p>
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
      string subject = "Welcome to Empower Logics Support Portal!";
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
      string body = BuildEmailHtml(fullName, "Your account for the Empower Logics ticketing portal has been set up. Please log in using the credentials below and change your password after first login:", themeColor, contentHtml, "Access Portal Now", portalUrl);
      return (subject, body);
    }

    /// <summary>
    /// Generates welcome email for a newly created Customer account.
    /// No portal link or password — customer interacts only via email.
    /// </summary>
    public static (string Subject, string Body) CustomerAccountCreated(string fullName, string contactPerson)
    {
      string subject = "Welcome to Empower Logics Support!";
      string themeColor = "#10b981"; // Emerald (customer)

      string contentHtml = $@"
        <tr>
          <td colspan='2' style='padding: 8px 0; color: #475569; font-size: 13px; line-height: 1.6;'>
            We have created your support account with Empower Logics. Whenever you raise an issue or query, 
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
            To raise a support request, simply contact your Empower Logics representative or send an email to our support team.
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
        string ticketSubject,
        string ticketDescription,
        string priority,
        string? product = null,
        string? docketNumber = null,
        List<string>? attachmentNames = null)
    {
      string subject = $"Support Ticket Logged: [{ticketNumber}] – {ticketSubject}";
      string themeColor = "#10b981"; // Emerald (customer)

      var priorityColor = priority?.ToLower() switch
      {
        "critical" => "#ef4444",
        "high"     => "#f97316",
        "medium"   => "#f59e0b",
        "low"      => "#10b981",
        _          => "#64748b"
      };

      // Truncate description for email if very long
      var descPreview = !string.IsNullOrWhiteSpace(ticketDescription) && ticketDescription.Length > 1000
          ? ticketDescription.Substring(0, 1000) + "..."
          : ticketDescription ?? "";

      var productRow = !string.IsNullOrEmpty(product) ? $@"
        <tr>
          <td style='padding: 4px 0; width: 130px;'><strong>Product / Module:</strong></td>
          <td style='padding: 4px 0;'>{product}</td>
        </tr>" : "";

      var docketRow = !string.IsNullOrEmpty(docketNumber) ? $@"
        <tr>
          <td style='padding: 4px 0;'><strong>Docket Number:</strong></td>
          <td style='padding: 4px 0; font-family: monospace; font-weight: 600;'>{docketNumber}</td>
        </tr>" : "";

      var attachmentHtml = "";
      if (attachmentNames != null && attachmentNames.Count > 0)
      {
        var fileList = string.Join("", attachmentNames.ConvertAll(f =>
            $"<li style='margin: 2px 0; color: #475569;'>{f}</li>"));
        attachmentHtml = $@"
        <tr>
          <td colspan='2' style='padding: 8px 0 4px;'>
            <strong>Attachments ({attachmentNames.Count}):</strong>
            <ul style='margin: 4px 0 0 16px; padding: 0;'>{fileList}</ul>
          </td>
        </tr>";
      }

      string contentHtml = $@"
        <tr>
          <td style='padding: 4px 0; width: 130px;'><strong>Ticket Number:</strong></td>
          <td style='padding: 4px 0; font-weight: 700; color: #10b981; font-size: 14px;'>{ticketNumber}</td>
        </tr>
        <tr>
          <td style='padding: 4px 0;'><strong>Subject:</strong></td>
          <td style='padding: 4px 0; font-weight: 600;'>{ticketSubject}</td>
        </tr>
        <tr>
          <td style='padding: 4px 0;'><strong>Priority:</strong></td>
          <td style='padding: 4px 0; font-weight: 600; color: {priorityColor};'>{priority}</td>
        </tr>
        {productRow}
        {docketRow}
        <tr>
          <td style='padding: 4px 0;'><strong>Date Logged:</strong></td>
          <td style='padding: 4px 0;'>{DateTime.UtcNow.AddMinutes(330):dd MMM yyyy, hh:mm tt} IST</td>
        </tr>
        <tr>
          <td colspan='2' style='padding: 12px 0 4px; border-top: 1px solid #e2e8f0;'>
            <strong>Description:</strong>
            <div style='margin-top: 6px; padding: 10px 12px; background: #f1f5f9; border-radius: 6px; font-size: 13px; color: #334155; line-height: 1.6; white-space: pre-wrap;'>{System.Net.WebUtility.HtmlEncode(descPreview)}</div>
          </td>
        </tr>
        {attachmentHtml}
        <tr>
          <td colspan='2' style='padding: 12px 0 4px; border-top: 1px solid #e2e8f0; font-size: 12px; color: #94a3b8;'>
            Our support team is reviewing your request. You will receive email updates as the ticket progresses. 
            Please keep this email as your reference. If you need to provide additional information, contact your Empower Logics representative.
          </td>
        </tr>";

      string body = BuildEmailHtml(customerName, "Your support request has been successfully logged. Below is the complete record of your ticket — please keep this email for your reference:", themeColor, contentHtml);
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
    public static (string Subject, string Body) TicketStatusUpdate(string recipientName, string ticketNumber, string newStatus, string updatedBy, string ticketSubject, bool isCustomer = true)
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

      string contentHtml = $@"
        <tr>
          <td style='padding: 4px 0; width: 110px;'><strong>Ticket Number:</strong></td>
          <td style='padding: 4px 0; font-weight: 600; color: #0f172a;'>{ticketNumber}</td>
        </tr>
        <tr>
          <td style='padding: 4px 0;'><strong>Subject:</strong></td>
          <td style='padding: 4px 0;'>{ticketSubject}</td>
        </tr>
        <tr>
          <td style='padding: 4px 0;'><strong>New Status:</strong></td>
          <td style='padding: 4px 0; font-weight: 700; color: {statusColor};'>{newStatus}</td>
        </tr>
        <tr>
          <td style='padding: 4px 0;'><strong>Updated By:</strong></td>
          <td style='padding: 4px 0;'>{updatedBy}</td>
        </tr>
        {(isCustomer ? @"
        <tr>
          <td colspan='2' style='padding: 10px 0 4px; border-top: 1px solid #e2e8f0; font-size: 12px; color: #94a3b8;'>
            No action is required from your side. If you have queries, contact your Empower Logics representative.
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

    /// <summary>
    /// Generates email for ticket status updates with transition buttons for customer actions.
    /// </summary>
    public static (string Subject, string Body) TicketStatusUpdateWithButtons(
        string customerName,
        string ticketNumber,
        string newStatus,
        string updatedBy,
        string ticketSubject,
        int ticketId,
        List<string> nextStatuses)
    {
      string subject = $"Action Required: [{ticketNumber}] Status Updated to {newStatus}";
      string themeColor = "#10b981"; // Emerald for customer

      var statusColor = newStatus?.ToLower() switch
      {
        "closed"      => "#10b981",
        "resolved"    => "#10b981",
        "in progress" => "#8b5cf6",
        "open"        => "#f59e0b",
        _             => "#64748b"
      };

      // Generate buttons HTML
      var buttonsHtml = "";
      if (nextStatuses != null && nextStatuses.Count > 0)
      {
        buttonsHtml = "<div style='margin-top: 20px; text-align: center;'>";
        buttonsHtml += "<p style='color: #475569; font-size: 13.5px; font-weight: 600; margin-bottom: 12px;'>Choose an action below to update this ticket status:</p>";
        foreach (var status in nextStatuses)
        {
          var payload = EncryptionHelper.Encrypt($"{ticketId}|{status}");
          var actionUrl = $"{BaseUrl.TrimEnd('/')}/public/ticket-action?payload={Uri.EscapeDataString(payload)}";
          var btnColor = status.ToLower() switch
          {
            "rework" => "#ef4444",    // Red
            "closed" => "#10b981",    // Green
            "close"  => "#10b981",    // Green
            "open"   => "#f59e0b",    // Orange
            _        => "#3b82f6"     // Blue
          };
          buttonsHtml += $@"
            <a href='{actionUrl}' target='_blank' style='background: {btnColor}; color: #ffffff; text-decoration: none; padding: 10px 20px; margin: 5px 8px; border-radius: 6px; font-weight: bold; font-size: 12px; display: inline-block; box-shadow: 0 4px 6px rgba(0,0,0,0.05);'>
              {status}
            </a>";
        }
        buttonsHtml += "</div>";
      }

      string contentHtml = $@"
        <tr>
          <td style='padding: 4px 0; width: 110px;'><strong>Ticket Number:</strong></td>
          <td style='padding: 4px 0; font-weight: 600; color: #0f172a;'>{ticketNumber}</td>
        </tr>
        <tr>
          <td style='padding: 4px 0;'><strong>Subject:</strong></td>
          <td style='padding: 4px 0;'>{ticketSubject}</td>
        </tr>
        <tr>
          <td style='padding: 4px 0;'><strong>New Status:</strong></td>
          <td style='padding: 4px 0; font-weight: 700; color: {statusColor};'>{newStatus}</td>
        </tr>
        <tr>
          <td style='padding: 4px 0;'><strong>Updated By:</strong></td>
          <td style='padding: 4px 0;'>{updatedBy}</td>
        </tr>
        <tr>
          <td colspan='2' style='padding: 12px 0 4px; border-top: 1px solid #e2e8f0;'>
            {buttonsHtml}
          </td>
        </tr>";

      string body = BuildEmailHtml(customerName, "The status of your support ticket has been updated. Please choose one of the options below to approve or request rework:", themeColor, contentHtml);
      return (subject, body);
    }
  }
}
