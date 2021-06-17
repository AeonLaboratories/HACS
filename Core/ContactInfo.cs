using System.Collections.Generic;

namespace HACS.Core
{
	public class ContactInfo : BindableObject
	{
		public string SiteName
		{
			get => siteName;
			set => Ensure(ref siteName, value);
		}
		string siteName;


		public string PhoneNumber
		{
			get => phoneNumber;
			set => Ensure(ref phoneNumber, value);
		}
		string phoneNumber;


		// TODO: ObservableList?
		public List<string> AlertRecipients
		{
			get => alertRecipients;
			set => Ensure(ref alertRecipients, value);
		}
		List<string> alertRecipients;
	}
}
