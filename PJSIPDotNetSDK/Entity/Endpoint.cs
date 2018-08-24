using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using PJSIP;
using PJSIPDotNetSDK.EventArgs;
using PJSIPDotNetSDK.Helpers;
using PJSIPDotNetSDK.Utils;

namespace PJSIPDotNetSDK.Entity
{
    public class Endpoint : PJSIP.Endpoint, INotifyObjectDisposing, IDispatchable<Endpoint>
    {
        #region Enums

        #endregion

        #region Fields
        private Dispatcher _dispatcher;
        private bool? _hasDispatcher;
        #endregion

        #region Properties
        public readonly ConcurrentDictionary<int, Account> Accounts = new ConcurrentDictionary<int, Account>();
        public IEnumerable<Call> Calls => Accounts.Values.SelectMany(a => a.Calls.Values);
        public Boolean IsDisposed { get; private set; }
        public Thread Thread { get; }

        public Dispatcher Dispatcher
        {
            get
            {
                if (_hasDispatcher.HasValue)
                    return _dispatcher;
                return _dispatcher = Dispatcher.FromThread(Thread);
            }
            private set
            {
                _dispatcher = value;
                _hasDispatcher = value != null;
            }
        }

        public Boolean HasDispatcher
        {
            get
            {
                if (_hasDispatcher.HasValue)
                    return _hasDispatcher.Value;
                _dispatcher = Dispatcher.FromThread(Thread);
                return (_hasDispatcher = _dispatcher != null).Value;
            }
        }

        public Account DefaultAccount => Accounts.Values.FirstOrDefault(a => a.IsDefault);

        #endregion

        #region Constructor
        public Endpoint(Thread thread, EpConfig epConfig = null, pjsip_transport_type_e transportType = pjsip_transport_type_e.PJSIP_TRANSPORT_UDP)
        {
            PJSIPDotNetSDK.Utils.Logger.LogCallingThread("Endpoint");

            Logger.GetLogger("Endpoint").Debug($"creating lib");
            libCreate();

            Logger.GetLogger("Endpoint").Debug($"lib init");
            libInit(new EpConfig
            {
                uaConfig = { maxCalls = 6 },
                medConfig =
                        {
                            sndClockRate = 16000,
                            //noVad = true,
                            ecTailLen = 0,
                            hasIoqueue = true
                        }
            });

            Logger.GetLogger("Endpoint").Debug($"creating transport");
            var randomPort = new Random().Next(1025, 65534);
            transportCreate(transportType, new TransportConfig() { port = (uint)randomPort });

            Logger.GetLogger("Endpoint").Debug($"lib start");
            libStart();

            Logger.GetLogger("Endpoint").Debug($"Thread: {thread.ManagedThreadId}");
            libRegisterThread(thread.ManagedThreadId.ToString());

            Dispatcher = Dispatcher.FromThread(thread);
            if(Dispatcher == null)
                throw new Exception("Thread must have a dispatcher");
        }
        #endregion

        #region Overrides
        public override void Dispose()
        {
            Logger.GetLogger("Endpoint").Debug("Disposing");

            IsDisposed = true;
            NotifyObjectDisposing(this, this);

            hangupAllCalls();

            /* Explicitly delete the Account.
               * This is to avoid GC to delete the endpoint first before deleting
               * the Account.
               */
            foreach (var account in Accounts.Values)
            {
                account.Dispose();
            }

            // Explicitly destroy and delete endpoint
            libDestroy();

            base.Dispose();
        }
        #endregion

        #region Delegates

        #endregion

        #region Events
        public event Account.StateHandler AccountStateChange;
        public event Account.IncomingCallHandler IncomingCall;
        public event Call.AudioDeviceExceptionHandler AudioDeviceException;
        public event Call.StateHandler CallStateChange;
        public event Call.MediaStateHandler CallMediaStateChange;
        public event GenericHandlers.ObjectDisposingHandler ObjectDisposing;
        #endregion

        #region Methods

        #region Static

        #endregion

        #region Internal
        internal void HookAccount(Account account)
        {
            Logger.GetLogger("Endpoint").Debug($"Hook account: {account.Id}");

            //Accounts.AddOrUpdate(account.Id, account, (id, a) => a);
            account.IncomingCall += OnAccountOnIncomingCall;
            account.AccountStateChange += OnAccountOnAccountStateChange;
            account.CallStateChange += OnAccountOnCallStateChange;
            account.CallMediaStateChange += OnAccountOnCallMediaStateChange;
            account.AudioDeviceException += OnAudioDeviceException;
        }

        internal void UnhookAccount(Account account)
        {
            Logger.GetLogger("Endpoint").Debug($"Unhook account: {account.Id}");

            Account removedAccount;
            Accounts.TryRemove(account.Id, out removedAccount);
            account.IncomingCall -= OnAccountOnIncomingCall;
            account.AccountStateChange -= OnAccountOnAccountStateChange;
            account.CallStateChange -= OnAccountOnCallStateChange;
            account.CallMediaStateChange -= OnAccountOnCallMediaStateChange;
            account.AudioDeviceException -= OnAudioDeviceException;
        }
        #region New

        #endregion

        #endregion

        #region Public
        public Account CreateAccount(AccountConfig accountConfig, bool isDefault = false, Account.PreInitializedHandler preInitializedHandler = null, Account.PostInitializedHandler postInitializedHandler = null)
        {
            return new Account(
                this,
                accountConfig,
                isDefault,
                (Account account) =>
                {
                    HookAccount(account);
                    preInitializedHandler?.Invoke(account);
                },
                (Account account) =>
                {
                    Accounts.AddOrUpdate(account.Id, account, (id, a) => a);
                    postInitializedHandler?.Invoke(account);
                }
            );
        }

        public void HoldAllCalls()
        {
            foreach (var c in Accounts.Values.SelectMany(a => a.Calls.Values.Where(c => !c.IsHeld)))
            {
                c.Hold();
            }
        }


        public void Invoke(GenericHandlers.InvokableDelegate<Endpoint> action, DispatcherPriority priorty = DispatcherPriority.Normal) => Extensions.Invoke(this, action, priorty);
        public TR Invoke<TR>(GenericHandlers.InvokableDelegate<Endpoint, TR> action, DispatcherPriority priorty = DispatcherPriority.Normal) => Extensions.Invoke(this, action, priorty);
        public void BeginInvoke(GenericHandlers.InvokableDelegate<Endpoint> action, DispatcherPriority priorty = DispatcherPriority.Normal) => Extensions.BeginInvoke(this, action, priorty);
        public Task<TR> BeginInvoke<TR>(GenericHandlers.InvokableDelegate<Endpoint, TR> action, DispatcherPriority priorty = DispatcherPriority.Normal) => Extensions.BeginInvoke(this, action, priorty);
        #endregion

        #region Private

        #region EventNotifiers
        private void NotifyAudioDeviceException(Call call, Exceptions.AudioDeviceException exception) => AudioDeviceException?.Invoke(this, new AudioDeviceExceptionEventArgs(call, exception));
        private void NotifyAccountState(Account account, pjsip_status_code state, string reason) => AccountStateChange?.Invoke(this, new AccountStateEventArgs(account, state, reason));
        private void NotifyIncomingCall(Call call) => IncomingCall?.Invoke(this, new IncomingCallEventArgs(call));
        private void NotifyCallState(Call call, pjsip_inv_state state) => CallStateChange?.Invoke(this, new CallStateEventArgs(call, state));
        private void NotifyCallMediaState(Call call, pjsua_call_media_status state) => CallMediaStateChange?.Invoke(this, new CallMediaStateEventArgs(call, state));
        private void NotifyObjectDisposing(object sender, object obj)
        {
            if (obj is Account)
            {
                UnhookAccount((Account)obj);
            }
            ObjectDisposing?.Invoke(this, obj);
        }
        #endregion

        #region Event Handlers
        private void OnAudioDeviceException(object senmder, AudioDeviceExceptionEventArgs e) => NotifyAudioDeviceException(e.Call, e.Exception);
        private void OnAccountOnCallMediaStateChange(object sender, CallMediaStateEventArgs e) => NotifyCallMediaState(e.Call, e.State);
        private void OnAccountOnCallStateChange(object sender, CallStateEventArgs e) => NotifyCallState(e.Call, e.State);
        private void OnAccountOnAccountStateChange(object sender, AccountStateEventArgs e) => NotifyAccountState(e.Account, e.State, e.Reason);
        private void OnAccountOnIncomingCall(object sender, IncomingCallEventArgs e) => NotifyIncomingCall(e.Call);
        #endregion

        #endregion



        #endregion
    }
}
