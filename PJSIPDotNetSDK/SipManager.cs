using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using PJSIP;

namespace pjsipDotNetSDK
{
    public class SipManager : IDisposable
    {
        public delegate void AccountStateHandler(object sender, AccountStateEventArgs e);

        public delegate void CallMediaStateHandler(object sender, CallMediaStateEventArgs e);

        public delegate void CallStateHandler(object sender, CallStateEventArgs e);

        public delegate void IncomingCallHandler(object sender, IncomingCallEventArgs e);

        private readonly List<Account> accounts = new List<Account>();
        private readonly Endpoint ep = new Endpoint();
        private int transportid;

        public SipManager(Thread thread, EpConfig config = null,
            pjsip_transport_type_e tType = pjsip_transport_type_e.PJSIP_TRANSPORT_UDP)
        {
            if (thread == null)
                throw new ArgumentNullException("Thread cannot be null!");

            // Create Library
            ep.libCreate();

            // Initialize endpoint
            ep.libInit(config == null ? initEndpoint() : config);

            // Create SIP transport.
            initTransportMode(tType);

            // Start the library
            ep.libStart();

            // Load the logger
            CallLogPath = Utils.UserAppDataPath + "calllog.json";
            CallLogManager = new CallLogManager();
            if (File.Exists(CallLogPath))
                CallLogManager.LoadFromFile(CallLogPath);

            // Register Thread
            Endpoint.instance()
                .libRegisterThread(thread.Name ?? Assembly.GetEntryAssembly().FullName);
            //Console.WriteLine("Thread registered: " + ep.libIsThreadRegistered());
        }

        public Endpoint Endpoint
        {
            get { return ep; }
        }

        public List<Account> Accounts
        {
            get { return accounts; }
        }

        public Account DefaultAccount
        {
            get { return accounts.FirstOrDefault(account => account.isDefault()); }
        }

        public Dictionary<int, Call> Calls
        {
            get
            {
                var calls = new Dictionary<int, Call>();
                return accounts.ToList()
                    .Where(acc => acc.Calls.Count > 0)
                    .Aggregate(calls,
                        (current, acc) =>
                            current.Concat(acc.Calls.ToList())
                                .GroupBy(d => d.Key)
                                .ToDictionary(d => d.Key, d => d.First().Value));
            }
        }

        //public String SipLogPath { get; private set; }
        public String CallLogPath { get; private set; }
        public CallLogManager CallLogManager { get; private set; }

        public void Dispose()
        {
            ep.hangupAllCalls();

            /* Explicitly delete the account.
               * This is to avoid GC to delete the endpoint first before deleting
               * the account.
               */
            foreach (var acc in accounts)
            {
                acc.Dispose();
            }

            // Explicitly destroy and delete endpoint
            ep.libDestroy();
            ep.Dispose();
        }

        public event AccountStateHandler AccountStateChange;

        private void NotifyAccountState(Account account, pjsip_status_code state, string reason)
        {
            // Make sure someone is listening to event
            if (AccountStateChange == null) return;
            AccountStateChange(null, new AccountStateEventArgs(account, state, reason));
        }

        public event IncomingCallHandler IncomingCall;

        private void NotifyIncomingCall(Call call)
        {
            // Make sure someone is listening to event
            if (IncomingCall == null) return;
            IncomingCall(null, new IncomingCallEventArgs(call));
        }

        public event CallStateHandler CallStateChange;

        private void NotifyCallState(Call call, pjsip_inv_state state)
        {
            if (call.State == pjsip_inv_state.PJSIP_INV_STATE_DISCONNECTED)
            {
                // Update Call Log
                CallLogManager.CallLogs.Add(new CallLog(call).EndLog());
                if (CallLogPath != null)
                    CallLogManager.SaveToFile(CallLogPath);
            }

            /*if(
                (call.State.In(pjsip_inv_state.PJSIP_INV_STATE_CONNECTING,pjsip_inv_state.PJSIP_INV_STATE_CONFIRMED) && call.LastState == pjsip_inv_state.PJSIP_INV_STATE_INCOMING)
                || 
                call.State == pjsip_inv_state.PJSIP_INV_STATE_CALLING)
            {
                // Hold all active (non 'spying' calls)
                holdAllCalls(false, new Dictionary<int,Call>() { {call.ID, call} } );
            }*/

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

        public List<Account> addAccount(string username, string password, string host, int port = 5060)
        {
            accounts.Add(initAccount(getBasicAccountConfig(username, password, host, port)));
            return accounts;
        }

        public List<Account> addAccount(Account account)
        {
            accounts.Add(account);
            return accounts;
        }

        public List<Account> removeAccount(Account account)
        {
            if (accounts.Contains(account))
                accounts.Remove(account);
            return accounts;
        }

        public static EpConfig initEndpoint()
        {
            var epConfig = new EpConfig();
            //epConfig.logConfig.filename = Utils.UserAppDataPath + "pjsip.log";
            //epConfig.logConfig.consoleLevel = 6;
            //epConfig.logConfig.msgLogging = 6;
            //epConfig.logConfig.level = 6;
            epConfig.uaConfig.maxCalls = 6;
            epConfig.medConfig.sndClockRate = 16000;
            epConfig.medConfig.noVad = true;
            epConfig.medConfig.ecTailLen = 0;
            epConfig.medConfig.hasIoqueue = true;
            return epConfig;
        }

        public void initTransportMode(pjsip_transport_type_e tType)
        {
            //if (transportid >= 0)
            //    ep.transportClose(transportid);

            var sipTpConfig = new TransportConfig();
            var random = new Random();
            var randomPort = random.Next(1025, 65534);

            sipTpConfig.port = (uint) randomPort; //5060;
            transportid = ep.transportCreate(tType, sipTpConfig);
        }

        public static AccountConfig getBasicAccountConfig(string username, string password, string host, int port = 5060)
        {
            var acfg = new AccountConfig();
            acfg.idUri = "sip:" + username + "@" + host;
            acfg.regConfig.registrarUri = "sip:" + host + ":" + port;
            var cred = new AuthCredInfo("digest", "*", username, 0, password);
            acfg.sipConfig.authCreds.Add(cred);

            return acfg;
        }

        public Account initAccount(AccountConfig acfg)
        {
            // Create the account
            var account = new Account(acfg);

            account.IncomingCall += (sender, e) => { NotifyIncomingCall(e.Call); };
            account.AccountStateChange += (sender, e) => { NotifyAccountState(e.Account, e.State, e.Reason); };
            account.CallStateChange += (sender, e) => { NotifyCallState(e.Call, e.State); };
            account.CallMediaStateChange += (sender, e) => { NotifyCallMediaState(e.Call, e.State); };

            return account.Register();
        }

        public Call answerCallExclusive(Call call)
        {
            foreach (var entry in Calls)
            {
                if (entry.Key != call.ID && entry.Value.HasAudio)
                    entry.Value.hold();
            }

            return call.answer();
        }

        public Dictionary<int, Call> holdAllCalls(bool force = false, Dictionary<int, Call> excludingCalls = null)
        {
            foreach (var entry in Calls)
            {
                if (excludingCalls != null)
                    if (excludingCalls.ContainsKey(entry.Key))
                        continue;
                if (entry.Value.HasAudioOut || force)
                    entry.Value.hold();
            }

            return Calls;
        }
    }
}