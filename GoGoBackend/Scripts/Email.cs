using System.Threading.Tasks;
using SendGrid;
using SendGrid.Helpers.Mail;
using SecretKeys;
using GoGoBackend.Controllers;
using System;

namespace Emails
{
	public class Server
	{
		// static bool mailSent = false;
		const string smtpServer = "Smtp.gmail.com";
		const string smtpAddress = "gogobackend@gmail.com";
		static readonly string smtpPassword = SecretsController.SecretKey("smtpPassword");
        static readonly string apiKey = SecretsController.SecretKey("apiKey");

		public static async Task SendValidationMail(string recipient, string validationLink)
		{
			// var msg = new SendGridMessage();

			var from = new EmailAddress("noreply@jsgo.com", "User Confirm");
			var subject = "New Account Confirmation";
			var to = new EmailAddress(recipient, "New User");
			var plainTextContent = "Please click the following link to confirm your account.";
			var htmlContent = string.Format("<a href = \"{0}\" target = \"_blank\">Confirm Account</a>", validationLink);
			var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);

			var client = new SendGridClient(apiKey);
			// send the damn email.
			var response = await client.SendEmailAsync(msg);
            Console.WriteLine("sending validation email...  " + response.StatusCode);
			return;

		}

		public static void SendNotificationEmail(string recipient, string message)
		{
			var from = new EmailAddress("noreply@omisego.com", "notification system");
			var subject = "Move Notification";
			var to = new EmailAddress(recipient, "User");
			var plainTextContent = message;
			var msg = MailHelper.CreateSingleEmail(from, to, subject, message, "");

			var client = new SendGridClient(apiKey);
			// send the email
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