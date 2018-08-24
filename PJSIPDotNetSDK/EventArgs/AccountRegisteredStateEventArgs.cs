using Account = PJSIPDotNetSDK.Entity.Account;

namespace PJSIPDotNetSDK.EventArgs
{
    public class AccountRegisteredStateEventArgs
    {
        public Account Account;
        public bool IsRegistered;

        public AccountRegisteredStateEventArgs(Account account, bool isRegistered)
        {
            this.Account = account;
            this.IsRegistered = isRegistered;
        }
    }
}