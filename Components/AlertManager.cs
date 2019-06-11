using HACS.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Threading;
using System.Xml.Serialization;
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

	public class AlertManager : HacsComponent
	{
		#region Component Implementation

		public static readonly new List<AlertManager> List = new List<AlertManager>();
		public static new AlertManager Find(string name) { return List.Find(x => x?.Name == name); }

		protected void Start()
		{
			ShuttingDown = false;
			alertThread = new Thread(AlertHandler)
			{
				Name = $"{Name} AlertHandler",
				IsBackground = true
			};
			alertThread.Start();
		}

		protected void Stop()
		{
			ShuttingDown = true;
			while (Busy)
				Thread.Sleep(5);
		}

		public AlertManager()
		{
			List.Add(this);
			OnStart += Start;
			OnStop += Stop;
		}

		#endregion Component Implementation

		bool ShuttingDown;
		public bool Busy => alertThread != null && alertThread.IsAlive;
		[JsonProperty] public int MinutesToSupressSameMessage { get; set; } = 1440;
		[JsonProperty] public string LastAlertMessage { get; set; }
		[JsonProperty] public ContactInfo ContactInfo { get; set; }
		[JsonProperty] public SmtpInfo SmtpInfo { get; set; }

		// alert system
		protected Queue<AlertMessage> QAlertMessage = new Queue<AlertMessage>();
		protected Thread alertThread;
		protected AutoResetEvent alertSignal = new AutoResetEvent(false);
		protected Stopwatch AlertTimer = new Stopwatch();

		public void PlaySound() => Notice.Send("PlaySound", Notice.Type.Tell);
		[XmlIgnore] public HacsLog EventLog;

		public void Alert(string subject, string message)
		{
			if (message == LastAlertMessage && AlertTimer.IsRunning && 
                AlertTimer.Elapsed.TotalMinutes < MinutesToSupressSameMessage)
				return;

			string date = $"({DateTime.Now:MMMM dd, H:mm:ss}) ";
			EventLog.Record(subject + ": " + message);
			AlertMessage alert = new AlertMessage(date + subject, message);
			lock (QAlertMessage) QAlertMessage.Enqueue(alert);
			alertSignal.Set();

            PlaySound();
			LastAlertMessage = message;
			AlertTimer.Restart();
		}

		protected void AlertHandler()
		{
			try
			{
				AlertMessage alert;
				while (true)
				{
					if (ShuttingDown) break;
					if (alertSignal.WaitOne(500))
					{
						while (QAlertMessage.Count > 0)
						{
							lock (QAlertMessage) alert = QAlertMessage.Dequeue();
							SendMail(alert.Subject, alert.Message);
						}
					}
				}
			}
			catch (Exception e) { Notice.Send(e.ToString()); }
		}

		public void clearLastAlertMessage()
		{ LastAlertMessage = ""; AlertTimer.Stop(); }

		string getEmailAddress(string s)
		{
			int comment = s.IndexOf("//");
			return (comment < 0) ? s : s.Substring(0, comment).Trim();
		}

		protected void SendMail(string subject, string message)
		{
			try
			{
                var To = new List<MailAddress>();
                ContactInfo.AlertRecipients.ForEach(line =>
                {
                    var a = getEmailAddress(line);
                    if (a.Length > 0) To.Add(new MailAddress(a));
                });
                if (To.Count == 0) return;      // no recipients

                MailMessage mail = new MailMessage
				{
					From = new MailAddress(SmtpInfo.Username, Name),
                    Subject = subject,
                    Body = $"{message}\r\n\r\n{ContactInfo.SiteName}\r\n{ContactInfo.PhoneNumber}"
                };
                To.ForEach(a => mail.To.Add(a));

                // NOTE: System.Net.Mail can't do explicit SSL (port 465)
                SmtpClient SmtpServer = new SmtpClient()
                {
                    UseDefaultCredentials = false,
                    Credentials = new System.Net.NetworkCredential(SmtpInfo.Username, SmtpInfo.Password),
                    Host = SmtpInfo.Host,
                    Port = SmtpInfo.Port,
                    EnableSsl = true,
				};
				SmtpServer.Send(mail);
			}
			catch (Exception e) { Notice.Send(e.ToString()); }
		}
	}
}
