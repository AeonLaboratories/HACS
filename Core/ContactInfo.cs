using System.Collections.Generic;

namespace HACS.Core
{
	public class ContactInfo
	{
		public string SiteName { get; set; }
		
		public string PhoneNumber { get; set; }
		
		public List<string> AlertRecipients { get; set; }
	}
}
