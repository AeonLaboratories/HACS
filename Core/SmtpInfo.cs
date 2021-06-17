namespace HACS.Core
{
	public class SmtpInfo : BindableObject
	{
		public string Host
		{
			get => host;
			set => Ensure(ref host, value);
		}
		string host;

		public int Port
		{
			get => port;
			set => Ensure(ref port, value);
		}
		int port;

		public string Username
		{
			get => username;
			set => Ensure(ref username, value);
		}
		string username;

		public string Password
		{
			get => password;
			set => Ensure(ref password, value);
		}
		string password;

	}
}
