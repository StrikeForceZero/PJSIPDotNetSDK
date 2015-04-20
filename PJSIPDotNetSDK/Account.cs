using System;
using System.Collections.Generic;
using PJSIP;

namespace pjsipDotNetSDK
{
    public class Account : PJSIP.Account
    {
        public delegate void AccountStateHandler(object sender, AccountStateEventArgs e);

        public delegate void CallMediaStateHandler(object sender, CallMediaStateEventArgs e);

        public delegate void CallStateHandler(object sender, CallStateEventArgs e);

        public delegate void IncomingCallHandler(object sender, IncomingCallEventArgs e);

        private readonly AccountConfig acfg;
        private readonly Dictionary<int, Call> calls = new Dictionary<int, Call>();

        public Account(AccountConfig acfg)
        {
            DND = false;
            this.acfg = acfg;
        }

        public String DisplayName
        {
            get { return acfg.DisplayName; }
            private set { acfg.DisplayName = value; }
        }

        public String Username
        {
            get { return acfg.sipConfig.authCreds[0].username; }
        }

        public Boolean DND { get; set; }

        public String RegURI
        {
            get { return acfg.regConfig.registrarUri; }
        }

        public Dictionary<int, Call> Calls
        {
            get { return calls; }
        }

        public Account Register()
        {
            create(acfg);
            return this;
        }

        public override void onRegState(OnRegStateParam prm)
        {
            base.onRegState(prm);
            //Console.WriteLine("Account registration state: " + prm.code + " : " + prm.reason);
            NotifyAccountState(prm.code, prm.reason);
        }

        public event AccountStateHandler AccountStateChange;

        private void NotifyAccountState(pjsip_status_code state, string reason)
        {
            // Make sure someone is listening to event
            if (AccountStateChange == null) return;
            AccountStateChange(null, new AccountStateEventArgs(this, state, reason));
        }

        public event IncomingCallHandler IncomingCall;

        private void NotifyIncomingCall(Call call)
        {
            if (!calls.ContainsKey(call.ID))
                calls.Add(call.ID, call);

            // Make sure someone is listening to event
            if (IncomingCall == null) return;
            IncomingCall(null, new IncomingCallEventArgs(call));
        }

        public event CallStateHandler CallStateChange;

        private void NotifyCallState(Call call, pjsip_inv_state state)
        {
            if (!calls.ContainsKey(call.ID))
                calls.Add(call.ID, call);
            if (state == pjsip_inv_state.PJSIP_INV_STATE_DISCONNECTED)
                calls.Remove(call.ID);

            // Make sure someone is listening to event
            if (CallStateChange == null) return;
            CallStateChange(null, new CallStateEventArgs(call, state));
        }

        public event CallMediaStateHandler CallMediaStateChange;

        private void NotifyCallMediaState(Call call, pjsua_call_media_status state)
        {
            // Make sure someone is listening to event
            if (CallMediaStateChange == null) return;
            CallMediaStateChange(null, new CallMediaStateEventArgs(call, state));
        }

        public override void onIncomingCall(OnIncomingCallParam iprm)
        {
            base.onIncomingCall(iprm);

            var call = new Call(this, iprm.callId, Call.Type.INBOUND);

            // If Do not disturb
            if (DND)
            {
                // hangup;
                var op = new CallOpParam(true);
                op.statusCode = pjsip_status_code.PJSIP_SC_DECLINE;
                call.decline();

                // And delete the call
                //call.Dispose();
                return;
            }

            // Hook into call state
            hookCall(call);

            // Notify stack of the call
            NotifyIncomingCall(call);
        }

        private void hookCall(Call call)
        {
            call.CallStateChange += (sender, e) => { NotifyCallState(e.Call, e.State); };
            call.CallMediaStateChange += (sender, e) => { NotifyCallMediaState(e.Call, e.State); };
        }

        public Call makeCall(string number)
        {
            var call = new Call(this);

            // Hook into call state
            hookCall(call);

            return call.makeCall(number);
        }
    }
}