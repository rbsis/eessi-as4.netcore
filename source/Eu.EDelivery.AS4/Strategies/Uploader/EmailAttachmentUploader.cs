using System.Net.Mail;
using Eu.EDelivery.AS4.Common;
using Eu.EDelivery.AS4.Model.Common;
using Eu.EDelivery.AS4.Model.PMode;
using Microsoft.Extensions.Logging;
using MimeKit;
using Attachment = Eu.EDelivery.AS4.Model.Core.Attachment;

namespace Eu.EDelivery.AS4.Strategies.Uploader;

/// <summary>
/// <see cref="Attachment"/> Uploader to send E-Mail messages 
/// with these <see cref="Attachment"/> Models
/// </summary>
[NotConfigurable]
public class EmailAttachmentUploader : IAttachmentUploader
{
    public const string Key = "EMAIL";

    private Method? _method;

    private readonly ILogger<EmailAttachmentUploader> _logger;

    private readonly IConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailAttachmentUploader"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="config"></param>
    public EmailAttachmentUploader(ILogger<EmailAttachmentUploader> logger, IConfig config)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Configure the <see cref="IAttachmentUploader"/>
    /// with a given <paramref name="payloadReferenceMethod"/>
    /// </summary>
    /// <param name="payloadReferenceMethod"></param>
    public void Configure(Method payloadReferenceMethod)
    {
        _method = payloadReferenceMethod;
    }

    /// <inheritdoc />
    public Task<UploadResult?> UploadAsync(Attachment attachment, MessageInfo referringUserMessage, CancellationToken cancellation)
    {
        SendAttachmentAsMail(attachment);

        return Task.FromResult<UploadResult?>(UploadResult.SuccessWithId(payloadId: attachment.Id));
    }

    /// <summary>
    /// Start uploading <see cref="Attachment"/>
    /// </summary>
    /// <param name="attachment"></param>
    private void SendAttachmentAsMail(Attachment attachment)
    {
        var mail = new MailMessage();
        var smtpServer = new SmtpClient(_config.GetSetting("smtpserver"));

        AddCommonInfoToMailMessage(mail);
        AddEMailAttachmentToMail(attachment, mail);
        AddSecurityToSmtpServer(smtpServer);

        smtpServer.Send(mail);

        LogUploadInformation(attachment);
    }

    private void AddCommonInfoToMailMessage(MailMessage mail)
    {
        mail.From = new MailAddress(_config.GetSetting("smtpusername"));

        AssignIfNotNull("body", body => mail.Body = body);
        AssignIfNotNull("subject", subject => mail.Subject = subject);
        AssignIfNotNull("to", to => mail.To.Add(to));
    }

    private void AssignIfNotNull(string key, Action<string> targetAction)
    {
        var parameter = _method?[key];
        if (parameter?.Value != null)
        {
            targetAction(parameter.Value);
        }
        else
        {
            _logger.LogDebug("Following key is not defined in Paylaod Reference Method: {Key}", key);
        }
    }

    private void AddSecurityToSmtpServer(SmtpClient smtpServer)
    {
        _ = int.TryParse(_config.GetSetting("smtpport"), out var smtpServerPort);
        smtpServer.Port = smtpServerPort;

        SetNetWorkCredentials(smtpServer);
        smtpServer.EnableSsl = true;
    }

    private void SetNetWorkCredentials(SmtpClient smtpServer)
    {
        smtpServer.Credentials = new System.Net.NetworkCredential(
            _config.GetSetting("smtpusername"), _config.GetSetting("smtppassword"));
    }

    private static void AddEMailAttachmentToMail(Attachment attachment, MailMessage mail)
    {
        _ = MimeTypes.TryGetExtension(attachment.ContentType, out var extension);
        var emailAttachment = new System.Net.Mail.Attachment(attachment.Content, attachment.Id + extension);

        mail.Attachments.Add(emailAttachment);
    }

    private void LogUploadInformation(Attachment attachment)
    {
        var toEmailAddress = _method?["to"]?.Value;
        _logger.LogInformation("Attachment {AttachmentId} is send as Mail Attachment to {ToEmailAddress}", attachment.Id, toEmailAddress);
    }
}
