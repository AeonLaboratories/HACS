using System.Collections.Generic;
using System.ComponentModel;

namespace HACS.Core
{
	public class ContactInfo
	{
		public string SiteName { get; set; }
		
		public string PhoneNumber { get; set; }
		
		public List<string> PermanentAlertRecipients { get; set; }
		
		public List<string> AlertRecipients { get; set; }
	}
}
