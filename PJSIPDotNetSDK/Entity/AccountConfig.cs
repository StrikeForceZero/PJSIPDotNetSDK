using PJSIP;

namespace PJSIPDotNetSDK.Entity
{
    public class AccountConfig : PJSIP.AccountConfig
    {
        public string Username { get; }
        //public string Password { get; set; }
        public string Host { get; }
        public int Port { get; }
        public string DisplayName { get; set; }

        internal AccountConfig(string username, string password, string host, int port = 5060)
        {
            Username = username;
            //Password = password;
            Host = host;
            Port = port;

            idUri = $"sip:{username}@{host}";
            regConfig = new AccountRegConfig {registrarUri = $"sip:{host}:{port}"};
            sipConfig.authCreds.Add(new AuthCredInfo("digest", "*", username, 0, password));
            callConfig.timerSessExpiresSec = 90; //required or pjsip complains on feature code calls
            callConfig.timerMinSESec = 90;  //required or pjsip complains on feature code calls
        }


        public static AccountConfig BasicConfig(string username, string password, string host, int port = 5060)
        {
            return new AccountConfig(username, password, host, port);
        }
    }
}
