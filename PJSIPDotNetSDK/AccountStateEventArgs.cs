using System;
using PJSIP;

namespace pjsipDotNetSDK
{
    public class AccountStateEventArgs : EventArgs
    {
        public AccountStateEventArgs(Account account, pjsip_status_code state) : this(account, state, "")
        {
        }

        public AccountStateEventArgs(Account account, pjsip_status_code state, String reason)
        {
            Account = account;
            State = state;
            Reason = reason;
        }

        public Account Account { get; private set; }
        public pjsip_status_code State { get; private set; }
        public String Reason { get; private set; }
    }
}