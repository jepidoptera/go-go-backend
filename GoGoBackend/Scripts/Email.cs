using System;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Threading;
using System.ComponentModel;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Emails
{
	public class Server
	{
		static bool mailSent = false;
		const string smtpServer = "Smtp.gmail.com";
		const string smtpAddress = "gogobackend@gmail.com";
		const string smtpPassword = "cat!!a3%malomor10tta761,,";

		private static void SendCompletedCallback(object sender, AsyncCompletedEventArgs e)
		{
			// Get the unique identifier for this asynchronous operation.
			String token = (string)e.UserState;

			if (e.Cancelled)
			{
				Console.WriteLine("[{0}] Send canceled.", token);
			}
			if (e.Error != null)
			{
				Console.WriteLine("[{0}] {1}", token, e.Error.ToString());
			}
			else
			{
				Console.WriteLine("Message sent.");
			}
			mailSent = true;
		}
		public static void SendValidationMail(string recipient, string validationLink)
		{
			var msg = new SendGridMessage();

			msg.SetFrom(new EmailAddress("dx@example.com", "SendGrid DX Team"));

			msg.AddTo(recipient);

			msg.SetSubject("New account confirmation");

			msg.AddContent(MimeType.Text, "Please click the following link to confirm your account.");
			msg.AddContent(MimeType.Text, validationLink);
			msg.

			return;

			// set up the SMTP host.
			SmtpClient client = new SmtpClient(smtpServer);
			// log in
			client.Credentials = new NetworkCredential(smtpAddress, smtpPassword);

			// Specify the e-mail sender.
			// Create a mailing address that includes a UTF8 character
			// in the display name.
			MailAddress from = new MailAddress("noreply@omg.com",
			   "GoGo " + (char)0xD8 + " Registration",
			System.Text.Encoding.UTF8);
			
			// Set destinations for the e-mail message.
			MailAddress to = new MailAddress(recipient);
			
			// Specify the message content.
			MailMessage message = new MailMessage(from, to);
			message.Body = string.Format("Please click the following link to complete your registration: \n {0}", validationLink);
			// Include some non-ASCII characters in body and subject.
			string someArrows = new string(new char[] { '\u2190', '\u2191', '\u2192', '\u2193' });
			message.Body += Environment.NewLine + someArrows;
			message.BodyEncoding = System.Text.Encoding.UTF8;
			message.Subject = "email validiation";
			message.SubjectEncoding = System.Text.Encoding.UTF8;
			// Set the method that is called back when the send operation ends.
			client.SendCompleted += new
			SendCompletedEventHandler(SendCompletedCallback);
			// The userState can be any object that allows your callback 
			// method to identify this send operation.
			// For this example, the userToken is a string constant.
			string userState = "test message1";
			client.SendAsync(message, userState);
			// Clean up.
			message.Dispose();
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
					if (dotCharacter[dotCharacter.Length - 1].Length == 0)
					{
						return false;
					}
					else
					{
						return true;
					}
				}
				else return false;
			}
			else return false;
		}
	}
}