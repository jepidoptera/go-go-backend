using System;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Threading.Tasks;
using System.ComponentModel;
using SendGrid;
using SendGrid.Helpers.Mail;
using System.Net.Http;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace Emails
{
	public class Server
	{
		static bool mailSent = false;
		const string smtpServer = "Smtp.gmail.com";
		const string smtpAddress = "gogobackend@gmail.com";
		const string smtpPassword = "cat!!a3%malomor10tta761,,";

		public static async Task SendValidationMail(string recipient, string validationLink)
		{
			// var msg = new SendGridMessage();

			var from = new EmailAddress("noreply@omisego.com", "User Confirm");
			var subject = "New Account Confirmation";
			var to = new EmailAddress(recipient, "New User");
			var plainTextContent = "Please click the following link to confirm your account.";
			var htmlContent = string.Format("<a href = \"{0}\" target = \"_blank\">Confirm Account</a>", validationLink);
			var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);

			//msg.SetFrom(new EmailAddress("dx@example.com", "SendGrid DX Team"));

			//msg.AddTo(recipient);

			//msg.SetSubject("New account confirmation");

			//msg.AddContent(MimeType.Text, "Please click the following link to confirm your account.");
			//msg.AddContent(MimeType.Text, validationLink);

			var apiKey = System.Environment.GetEnvironmentVariable("SENDGRID_API_KEY");
			// this is definitely not how this is supposed to be done
			// TODO: puzzle through documentation on azure key vaults
			// or web config settings, or something better than this.
			apiKey = "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";
			var client = new SendGridClient(apiKey);
			// send the damn email. clearly, the most important part.
			var response = await client.SendEmailAsync(msg);

			return;

		}

		public static void SendNotificationEmail(string recipient, string message)
		{
			var from = new EmailAddress("noreply@omisego.com", "notification system");
			var subject = "Move Notification";
			var to = new EmailAddress(recipient, "User");
			var plainTextContent = message;
			var msg = MailHelper.CreateSingleEmail(from, to, subject, message, "");

			var apiKey = System.Environment.GetEnvironmentVariable("SENDGRID_API_KEY");
			// this is definitely not how this is supposed to be done
			// TODO: puzzle through documentation on azure key vaults
			// or web config settings, or something better than this.
			apiKey = "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";
			var client = new SendGridClient(apiKey);
			// send the damn email. clearly, the most important part.
			var response = client.SendEmailAsync(msg);

			return;
		}

		public static bool VerifyEmailAddress(string address)
		{
			string[] atCharacter;
			string[] dotCharacter;
			atCharacter = address.Split("@"[0]);
			if (atCharacter.Length == 2)
			{
				dotCharacter = atCharacter[1].Split("."[0]);
				if (dotCharacter.Length >= 2)
				{
					foreach (string part in dotCharacter)
					{
						if (part.Length == 0)
						{
							return false;
						}
					}
				}
				else return false;

				if (atCharacter[0].Length == 0)
				{
					return false;
				}
				return true;
			}
			else return false;
		}
	}
}