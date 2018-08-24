using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using PJSIP;
using PJSIPDotNetSDK.EventArgs;
using PJSIPDotNetSDK.Helpers;
using PJSIPDotNetSDK.Utils;

namespace PJSIPDotNetSDK.Entity
{
    public class Account : PJSIP.Account, INotifyObjectDisposing, IDispatchable<Account>
    {
        #region Enums

        #endregion

        #region Fields
        private readonly AccountConfig _accountConfig;
        #endregion

        #region Properties
        public Dispatcher Dispatcher => Endpoint.Dispatcher;

        public Endpoint Endpoint { get; }

        public readonly ConcurrentDictionary<int, Call> Calls = new ConcurrentDictionary<int, Call>();

        public int Id { get; } = (int)pjsua_invalid_id_const_.PJSUA_INVALID_ID;

        public Boolean IsRegistered { get; private set; }

        public Boolean IsDisposed { get; private set; }

        public Boolean IsDefault => !IsDisposed && isDefault();

        public AccountConfig AccountConfig => _accountConfig;

        public Boolean DND { get; set; }
        #endregion

        #region Constructor
        internal Account(Endpoint endpoint, AccountConfig accountConfig, Boolean isDefault = false, PreInitializedHandler preInitializedHandler = null, PostInitializedHandler postInitializedHandler = null)
        {
            Logger.GetLogger("Account").Debug($"New account");
            Endpoint = endpoint;
            preInitializedHandler?.Invoke(this);
            //Endpoint.HookAccount(this);
            this._accountConfig = accountConfig;
            create(accountConfig, isDefault);
            Id = getId();
            postInitializedHandler?.Invoke(this);
            base.setRegistration(true); //TODO: some weird bug where registration gets hung up and needs to be refreshed. possibly from error thrown with VAD media
        }
        #endregion

        #region Overrides
        public override void Dispose()
        {
            Logger.GetLogger("Account").Debug($"Disposing");
            IsDisposed = true;
            NotifyObjectDisposing(this, this);
            foreach (var call in Calls.Values)
                call.Dispose();
            base.Dispose();
        }

        public override void onRegState(OnRegStateParam prm)
        {
            base.onRegState(prm);

            Logger.GetLogger("Account").Debug($"Reg state: {prm.reason}");

            NotifyAccountState(prm.code, prm.reason);
        }

        public override void onIncomingCall(OnIncomingCallParam iprm)
        {
            base.onIncomingCall(iprm);

            var call = Call.IncomingCall(this, iprm.callId, null, HookCall);

            Logger.GetLogger("Account").Debug($"Incoming Call: {call.Id}");

            Calls.AddOrUpdate(call.Id, call, (id, c) => call);

            NotifyIncomingCall(call);
        }

        public override void onRegStarted(OnRegStartedParam prm)
        {
            base.onRegStarted(prm);

            Logger.GetLogger("Account").Debug($"Reg: {prm.renew}");

            IsRegistered = prm.renew;

            NotifyAccountRegisteredState(prm.renew);
        }
        #endregion
        
        #region Delegates
        public delegate void PreInitializedHandler(Account account);
        public delegate void PostInitializedHandler(Account account);
        public delegate void RegisteredStateHandler(object sender, AccountRegisteredStateEventArgs e);
        public delegate void StateHandler(object sender, AccountStateEventArgs e);
        public delegate void IncomingCallHandler(object sender, IncomingCallEventArgs e);
        #endregion

        #region Events
        public event GenericHandlers.ObjectDisposingHandler ObjectDisposing;
        public event RegisteredStateHandler AccountRegisteredStateChange;
        public event IncomingCallHandler IncomingCall;
        public event StateHandler AccountStateChange;
        public event Call.AudioDeviceExceptionHandler AudioDeviceException;
        public event Call.StateHandler CallStateChange;
        public event Call.MediaStateHandler CallMediaStateChange;
        #endregion
        
        #region Methods

        #region Static

        #endregion
        
        #region Internal
        internal void HookCall(Call call)
        {
            Logger.GetLogger("Account").Debug($"Hooking call");
            call.StateChange += OnOnStateChange;
            call.MediaStateChange += OnMediaStateChange;
            call.ObjectDisposing += OnCallOnObjectDisposing;
            call.AudioDeviceException += OnAudioDeviceException;
        }

        internal void UnhookCall(Call call)
        {
            Logger.GetLogger("Account").Debug($"Unhooking call: {call.Id}");
            Call deletedCall;
            Calls.TryRemove(call.Id, out deletedCall);
            call.StateChange -= OnOnStateChange;
            call.MediaStateChange -= OnMediaStateChange;
            call.ObjectDisposing -= OnCallOnObjectDisposing;
            call.AudioDeviceException -= OnAudioDeviceException;
        }
        #region New

        #endregion

        #endregion

        #region Public

        public String GetSipAddress(string number)
        {
            var host = String.Join(":", _accountConfig.regConfig.registrarUri.Split(':').Skip(1).Take(2).ToArray());
            return $"sip:{number}@{host}";
        }

        public Call MakeCall(string number, Call.PreInitializedHandler preInitializedHandler = null, Call.PostInitializedHandler postInitializedHandler = null)
        {
            Logger.GetLogger("Account").Debug($"Making call to: {number}");

            var call = new Call(this, -1, (Call c) =>
            {
                HookCall(c);
                preInitializedHandler?.Invoke(c);
            });

            call.makeCall(GetSipAddress(number), new CallOpParam(true));

            Calls.AddOrUpdate(call.Id, call, (id, c) => call);
            postInitializedHandler?.Invoke(call);

            return call;
        }


        public void Invoke(GenericHandlers.InvokableDelegate<Account> action, DispatcherPriority priorty = DispatcherPriority.Normal) => Extensions.Invoke(this, action, priorty);
        public TR Invoke<TR>(GenericHandlers.InvokableDelegate<Account, TR> action, DispatcherPriority priorty = DispatcherPriority.Normal) => Extensions.Invoke(this, action, priorty);
        public void BeginInvoke(GenericHandlers.InvokableDelegate<Account> action, DispatcherPriority priorty = DispatcherPriority.Normal) => Extensions.BeginInvoke(this, action, priorty);
        public Task<TR> BeginInvoke<TR>(GenericHandlers.InvokableDelegate<Account, TR> action, DispatcherPriority priorty = DispatcherPriority.Normal) => Extensions.BeginInvoke(this, action, priorty);
        #endregion

        #region Private

        #region EventNotifiers
        private void NotifyAudioDeviceException(Call call, Exceptions.AudioDeviceException exception) => AudioDeviceException?.Invoke(this, new AudioDeviceExceptionEventArgs(call, exception));
        private void NotifyAccountRegisteredState(bool renew) => AccountRegisteredStateChange?.Invoke(this, new AccountRegisteredStateEventArgs(this, renew));
        private void NotifyAccountState(pjsip_status_code state, string reason) => AccountStateChange?.Invoke(this, new AccountStateEventArgs(this, state, reason));
        private void NotifyIncomingCall(Call call) => IncomingCall?.Invoke(this, new IncomingCallEventArgs(call));
        private void NotifyCallState(Call call, pjsip_inv_state state) => CallStateChange?.Invoke(this, new CallStateEventArgs(call, state));
        private void NotifyCallMediaState(Call call, pjsua_call_media_status state) => CallMediaStateChange?.Invoke(this, new CallMediaStateEventArgs(call, state));
        private void NotifyObjectDisposing(object sender, object obj)
        {
            if (obj is Call)
                UnhookCall((Call)obj);
            ObjectDisposing?.Invoke(this, obj);
        }
        #endregion

        #region Event Handlers
        private void OnAudioDeviceException(object senmder, AudioDeviceExceptionEventArgs e) => NotifyAudioDeviceException(e.Call, e.Exception);
        private void OnOnStateChange(object sender, CallStateEventArgs e) => NotifyCallState(e.Call, e.State);
        private void OnMediaStateChange(object sender, CallMediaStateEventArgs e) => NotifyCallMediaState(e.Call, e.State);
        private void OnCallOnObjectDisposing(object sender, object obj) => NotifyObjectDisposing(this, obj);
        #endregion

        #endregion

        #endregion
    }
}
