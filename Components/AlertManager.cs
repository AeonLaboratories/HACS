using HACS.Core;
using MailKit.Net.Smtp;
using MimeKit;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using Utilities;

namespace HACS.Components
{
	public struct AlertMessage
	{
		public string Subject;
		public string Message;
		public AlertMessage(string subject, string message)
		{ Subject = subject; Message = message; }
	}

	public static class Alert
    {
		public static IAlertManager DefaultAlertManager { get; set; } = new AlertManager();
		public static void Send(string subject, string message) =>
			DefaultAlertManager?.Send(subject, message);

		/// <summary>
		/// Dispatch a message to the remote operator and to the local user interface.
		/// The process is not paused.
		/// </summary>
		public static void Announce(string subject, string message) =>
			DefaultAlertManager?.Announce(subject, message);

		/// <summary>
		/// Pause and give the local operator the option to continue.
		/// </summary>
		public static void Pause(string subject, string message) =>
			DefaultAlertManager?.Pause(subject, message);

		/// <summary>
		/// Make an entry in the EventLog, pause and give the local operator 
		/// the option to continue. The notice is transmitted as a Warning.
		/// </summary>
		public static void Warn(string subject, string message) =>
			DefaultAlertManager?.Warn(subject, message);
	}

	// TODO: should this class derive from StateManager?
	public class AlertManager : HacsComponent, IAlertManager
	{
		#region HacsComponent

		[HacsStart]
		protected virtual void Start()
		{
			Stopping = false;
			alertThread = new Thread(AlertHandler) { Name = $"{Name} AlertHandler", IsBackground = true };
			alertThread.Start();
		}

		[HacsStop]
		protected virtual void Stop()
		{
			Stopping = true;
			alertSignal.Set();
			stoppedSignal.WaitOne();
		}

		ManualResetEvent stoppedSignal = new ManualResetEvent(true);
		public new bool Stopped => stoppedSignal.WaitOne(0);
		protected bool Stopping { get; set; }

		#endregion HacsComponent

		[JsonProperty, DefaultValue(1440)] public int MinutesToSuppressSameMessage
		{
			get => minutesToSuppressSameMessage;
			set => Ensure(ref minutesToSuppressSameMessage, value);
		}
		int minutesToSuppressSameMessage = 1440;

		[JsonProperty] public string PriorAlertMessage
		{
			get => lastAlertMessage;
			set => Ensure(ref lastAlertMessage, value);
		}
		string lastAlertMessage;

		[JsonProperty] public ContactInfo ContactInfo
		{
			get => contactInfo;
			set => Ensure(ref contactInfo, value, OnPropertyChanged);
		}
		ContactInfo contactInfo;

		[JsonProperty] public SmtpInfo SmtpInfo
		{
			get => smtpInfo;
			set => Ensure(ref smtpInfo, value, OnPropertyChanged);
		}
		SmtpInfo smtpInfo;


		// alert system
		protected Queue<AlertMessage> QAlertMessage = new Queue<AlertMessage>();
		protected Thread alertThread;
		protected AutoResetEvent alertSignal = new AutoResetEvent(false);
		protected Stopwatch AlertTimer = new Stopwatch();

		void PlaySound() => Notice.Send("PlaySound", Notice.Type.Tell);
		public IHacsLog EventLog => Hacs.EventLog;

		public enum AlertType { Alert, Announce, Pause, Warn }

		/// <summary>
		/// Send a message to the remote operator.
		/// </summary>
		public void Send(string subject, string message)
		{
			if (message == PriorAlertMessage && AlertTimer.IsRunning && 
                AlertTimer.Elapsed.TotalMinutes < MinutesToSuppressSameMessage)
				return;

			string date = $"({DateTime.Now:MMMM dd, H:mm:ss}) ";
			AlertMessage alert = new AlertMessage(date + subject, message);
			lock (QAlertMessage) QAlertMessage.Enqueue(alert);
			alertSignal.Set();

            PlaySound();
			PriorAlertMessage = message;
			AlertTimer.Restart();
		}

		/// <summary>
		/// Dispatch a message to the remote operator and to the local user interface.
		/// The process is not paused.
		/// </summary>
		public virtual void Announce(string subject, string message)
		{
			Send(subject, message);
			Notice.Send(subject, message, Notice.Type.Tell);
		}

		/// <summary>
		/// Pause and give the local operator the option to continue.
		/// </summary>
		public virtual void Pause(string subject, string message)
		{
			Send(subject, message);
			Notice.Send(subject, message + "\r\nPress Ok to continue");
		}

		/// <summary>
		/// Make an entry in the EventLog, pause and give the local operator 
		/// the option to continue. The notice is transmitted as a Warning.
		/// </summary>
		public virtual void Warn(string subject, string message)
		{
			EventLog.Record(subject + ": " + message);
			Send(subject, message);
			Notice.Send(subject, message, Notice.Type.Warn);
		}

		protected void AlertHandler()
		{
            stoppedSignal.Reset();
			try
			{
				AlertMessage alert;
				while (!Stopping)
				{
                    while (QAlertMessage.Count > 0)
                    {
                        lock (QAlertMessage) alert = QAlertMessage.Dequeue();
                        SendMail(alert.Subject, alert.Message);
                    }
                    alertSignal.WaitOne(500);
				}
			}
			catch (Exception e) { Notice.Send(e.ToString()); }
            stoppedSignal.Set();
		}

		public void ClearLastAlertMessage()
		{ PriorAlertMessage = ""; AlertTimer.Stop(); }

		string getEmailAddress(string s)
		{
			int comment = s.IndexOf("//");
			return (comment < 0) ? s : s.Substring(0, comment).Trim();
		}

		protected void SendMail(string subject, string message)
		{
			try
			{
                var To = new List<MailboxAddress>();
                ContactInfo?.AlertRecipients?.ForEach(line =>
                {
                    var a = getEmailAddress(line);
                    if (a.Length > 0) To.Add(new MailboxAddress(a, a));
                });
                if (To.Count == 0) return;      // no recipients

				var mail = new MimeMessage();
				mail.From.Add(new MailboxAddress(Name, SmtpInfo.Username));
				mail.Subject = subject;
				mail.Body = new TextPart(MimeKit.Text.TextFormat.Plain)
				{
					Text = $"{message}\r\n\r\n{ContactInfo.SiteName}\r\n{ContactInfo.PhoneNumber}"
				};
				To.ForEach(a => mail.To.Add(a));

				using (var client = new SmtpClient())
				{
					client.Connect(SmtpInfo.Host, SmtpInfo.Port, false);
					// TODO: Consider using OAuth2, to prevent the need to enable
					// the gmail account's "Allow less secure apps" setting:
					// https://github.com/jstedfast/MailKit/blob/master/GMailOAuth2.md
					// Another alternative would be to enable 2-factor authentication
					// (instead of Allow less secure apps), then generate app-specific 
					// passwords on the account and use them for authentication.
					client.AuthenticationMechanisms.Remove("XOAUTH2");
					client.Authenticate(SmtpInfo.Username, SmtpInfo.Password);
					client.Send(mail);
					client.Disconnect(true);
				}
			}
			catch (Exception e) { Notice.Send(e.ToString()); }
		}

		protected virtual void OnPropertyChanged(object sender = null, PropertyChangedEventArgs e = null)
		{
			if (sender == ContactInfo)
				NotifyPropertyChanged(nameof(ContactInfo));
			else if (sender == SmtpInfo)
				NotifyPropertyChanged(nameof(SmtpInfo));
		}
	}
}
