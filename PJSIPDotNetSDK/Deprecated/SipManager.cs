using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Channels;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using PJSIPDotNetSDK.EventArgs;
using PJSIPDotNetSDK.Entity;
using PJSIPDotNetSDK.Utils;
using PJSIP;
using Account = PJSIPDotNetSDK.Entity.Account;
using AccountConfig = PJSIPDotNetSDK.Entity.AccountConfig;
using Call = PJSIPDotNetSDK.Entity.Call;

namespace PJSIPDotNetSDK
{
    public class SipManager : IDisposable, IThreadInvokableGetter, ISafeInvokable
    {
        public delegate void AccountStateHandler(object sender, AccountStateEventArgs e);

        public delegate void CallMediaStateHandler(object sender, CallMediaStateEventArgs e);

        public delegate void CallStateHandler(object sender, CallStateEventArgs e);

        public delegate void IncomingCallHandler(object sender, IncomingCallEventArgs e);

        private readonly List<Account> _accounts = new List<Account>();

        public Entity.Endpoint Endpoint { get; private set; }

        public InvokableThread InvokableThread { get; private set; }
        public InvokableThread GetInvokableThread() => InvokableThread;

        private CancellationTokenSource _cts = new CancellationTokenSource();
        public CancellationToken GetCancellationToken() => _cts.Token;

        public SipManager(Thread thread, EpConfig config = null,
            pjsip_transport_type_e tType = pjsip_transport_type_e.PJSIP_TRANSPORT_UDP)
        {
            Logger.GetLogger("SIP MANAGER").Debug($"creating manager");

            Logger.GetLogger("SIP MANAGER").Debug($"loading calllogger");

            // Load the logger
            CallLogPath = CallLogPath ?? "calllog.json";
            CallLogManager = CallLogManager ?? new CallLogManager();
            if (File.Exists(CallLogPath))
                CallLogManager.LoadFromFile(CallLogPath);

            Logger.GetLogger("SIP MANAGER").Debug($"calllogger loaded");

            var initTaskCompletedWaitHandle = new ManualResetEventSlim(false);


            Logger.GetLogger("SIP MANAGER").Debug($"init task started");

            // InvokableThread = new InvokableThread($"SipManager_{Guid.NewGuid()}", _cts.Token);

            // InvokableThread.BeginInvoke(() => { 
                Endpoint = new PJSIPDotNetSDK.Entity.Endpoint(thread, config, tType); //InvokableThread.Thread

                // Logger.GetLogger("SIP MANAGER").Debug($"creating pjsip library");
                // // Create Library
                // Endpoint.libCreate();

                // Logger.GetLogger("SIP MANAGER").Debug($"initializing endpoint");
                // // Initialize endpoint
                // Endpoint.libInit(config ?? initEndpoint());
                // 
                // Logger.GetLogger("SIP MANAGER").Debug($"creating transport");
                // // Create SIP transport.
                // initTransportMode(tType);
                // 
                // Logger.GetLogger("SIP MANAGER").Debug($"starting pjsip library");
                // // Start the library
                // Endpoint.libStart();
                // 
                // Logger.GetLogger("SIP MANAGER").Debug($"registering thread {InvokableThread.Thread.Name}");
                // // Register Thread
                // Endpoint.libRegisterThread(InvokableThread.Thread.Name);

                Logger.GetLogger("SIP MANAGER").Debug($"init finished");
                initTaskCompletedWaitHandle.Set();
            // });

            while (_cts.IsCancellationRequested == false && initTaskCompletedWaitHandle.IsSet == false)
            {
                initTaskCompletedWaitHandle.Wait(_cts.Token);
            }

            Logger.GetLogger("SIP MANAGER").Debug($"init finished");
        }

        public List<Account> Accounts => _accounts;

        public Account DefaultAccount
        {
            get { return _accounts.FirstOrDefault(account => account.isDefault()); }
        }

        public Dictionary<int, Call> Calls
        {
            get
            {
                var calls = new Dictionary<int, Call>();
                return _accounts.ToList()
                    .Where(acc => acc.Calls.Count > 0)
                    .Aggregate(calls,
                        (current, acc) =>
                            current.Concat(acc.Calls.ToList())
                                .GroupBy(d => d.Key)
                                .ToDictionary(d => d.Key, d => d.First().Value));
            }
        }

        //public String SipLogPath { get; private set; }
        public String CallLogPath { get; set; }
        public CallLogManager CallLogManager { get; set; }

        public void Dispose()
        {
            //nvokableThread.Invoke(() =>
            //
            //   Endpoint.hangupAllCalls();
            //
            //   /* Explicitly delete the account.
            //      * This is to avoid GC to delete the endpoint first before deleting
            //      * the account.
            //      */
            //   foreach (var acc in _accounts)
            //   {
            //       acc.Dispose();
            //   }
            //
            //   // Explicitly destroy and delete endpoint
            //   Endpoint.libDestroy();
            //   Endpoint.Dispose();


                _cts.Cancel(); //kill loop
            //}, InvokableThread.Priority.Procastinate);

        }

        public event AccountStateHandler AccountStateChange;

        private void NotifyAccountState(Account account, pjsip_status_code state, string reason)
        {
            AccountStateChange?.Invoke(null, new AccountStateEventArgs(account, state, reason));
        }

        public event IncomingCallHandler IncomingCall;

        private void NotifyIncomingCall(Call call)
        {
            IncomingCall?.Invoke(null, new IncomingCallEventArgs(call));
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
            
            CallStateChange?.Invoke(null, new CallStateEventArgs(call, state));
        }

        public event CallMediaStateHandler CallMediaStateChange;

        private void NotifyCallMediaState(Call call, pjsua_call_media_status state)
        {
            CallMediaStateChange?.Invoke(null, new CallMediaStateEventArgs(call, state));
        }

        public List<Account> addAccount(string username, string password, string host, int port = 5060)
        {
            Logger.GetLogger("SIP MANAGER").Debug($"adding account");
            _accounts.Add(initAccount(getBasicAccountConfig(username, password, host, port)));
            return _accounts;
        }

        public List<Account> addAccount(Account account)
        {
            Logger.GetLogger("SIP MANAGER").Debug($"adding account");
            _accounts.Add(account);
            return _accounts;
        }

        public List<Account> removeAccount(Account account)
        {
            Logger.GetLogger("SIP MANAGER").Debug($"removing account");
            if (_accounts.Contains(account))
                _accounts.Remove(account);
            return _accounts;
        }

        public static EpConfig initEndpoint()
        {
            var epConfig = new EpConfig
            {
                uaConfig = {maxCalls = 6},
                medConfig = {
                    sndClockRate = 16000,
                    //noVad = true,
                    ecTailLen = 0,
                    hasIoqueue = true
                }
            };
            //epConfig.logConfig.filename = Utils.UserAppDataPath + "pjsip.log";
            //epConfig.logConfig.consoleLevel = 6;
            //epConfig.logConfig.msgLogging = 6;
            //epConfig.logConfig.level = 6;
            return epConfig;
        }

        public int initTransportMode(pjsip_transport_type_e tType)
        {
            //if (transportid >= 0)
            //    ep.transportClose(transportid);

            var sipTpConfig = new TransportConfig();
            var random = new Random();
            var randomPort = random.Next(1025, 65534);

            sipTpConfig.port = (uint) randomPort; //5060;
            return Endpoint.transportCreate(tType, sipTpConfig);
        }

        public static AccountConfig getBasicAccountConfig(string username, string password, string host, int port = 5060)
        {
            var acfg = new AccountConfig(username, password, host, port)
            {
                idUri = "sip:" + username + "@" + host,
                regConfig = {registrarUri = "sip:" + host + ":" + port}
            };
            acfg.sipConfig.authCreds.Add(new AuthCredInfo("digest", "*", username, 0, password));
            acfg.callConfig.timerSessExpiresSec = 90; //required or pjsip complains on feature code calls
            acfg.callConfig.timerMinSESec = 90;  //required or pjsip complains on feature code calls

            return acfg;
        }

        public Account initAccount(AccountConfig acfg)
        {
            Logger.GetLogger("SIP MANAGER").Debug($"initializing account");
            // Create the account
            var account = new Account(Endpoint, acfg);

            account.IncomingCall += (sender, e) => { NotifyIncomingCall(e.Call); };
            account.AccountStateChange += (sender, e) => { NotifyAccountState(e.Account, e.State, e.Reason); };
            account.CallStateChange += (sender, e) => { NotifyCallState(e.Call, e.State); };
            account.CallMediaStateChange += (sender, e) => { NotifyCallMediaState(e.Call, e.State); };

            return account;
        }

        public Call answerCallExclusive(Call call)
        {
            foreach (var entry in Calls.Where(entry => entry.Key != call.Id && entry.Value.HasAudio))
            {
                entry.Value.Hold();
            }

            call.Answer();
            return call;
        }

        public Dictionary<int, Call> holdAllCalls(bool force = false, Dictionary<int, Call> excludingCalls = null)
        {
            foreach (var entry in Calls)
            {
                if (excludingCalls != null)
                    if (excludingCalls.ContainsKey(entry.Key))
                        continue;
                if (entry.Value.HasAudioOut || force)
                    entry.Value.Hold();
            }

            return Calls;
        }
    }
}