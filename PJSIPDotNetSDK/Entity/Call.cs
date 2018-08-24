using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using PJSIP;
using PJSIPDotNetSDK.EventArgs;
using PJSIPDotNetSDK.Exceptions;
using PJSIPDotNetSDK.Helpers;
using PJSIPDotNetSDK.Utils;
using PJSIPDotNetSDK.Workers;

namespace PJSIPDotNetSDK.Entity
{
    public class Call : PJSIP.Call, INotifyObjectDisposing, IDispatchable<Call>
    {
        #region Enums
        public enum Type
        {
            Unknown,
            Outbound,
            Inbound,
            Missed,
            Declined
        }
        #endregion

        #region Fields
        private string _callingName;
        private string _callingNumber;
        private int _totalDuration = 0;
        private int _connectDuration = 0;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly DtmfDialerWorker _dtmfBackgroundWorker;
        private readonly Progress<int> _dtmfProgress = new Progress<int>();
        private static readonly Regex CallerIdRegEx = new Regex(@"(?:""(.*?)""\s\<)?sip\:(.*?)\@", RegexOptions.IgnoreCase);
        #endregion

        #region Properties
        public Dispatcher Dispatcher => Account.Endpoint.Dispatcher;

        public Account Account { get; }

        public readonly ConcurrentDictionary<int, pjsua_call_media_status> MediaStatuses = new ConcurrentDictionary<int, pjsua_call_media_status>();

        public int Id { get; private set; } = (int)pjsua_invalid_id_const_.PJSUA_INVALID_ID;
        public pjsip_inv_state LastState { get; private set; } = pjsip_inv_state.PJSIP_INV_STATE_NULL;
        public pjsip_inv_state State { get; private set; }
        public Type CallType { get; internal set; } = Type.Unknown;

        public Boolean HasAudioOut
        {
            get
            {
                if (!Validate())
                    return false;

                try
                {
                    var portId = GetAudioMedia().getPortId();

                    return Account.Endpoint.audDevManager().getPlaybackDevMedia().getPortInfo().listeners.Contains(portId);
                }
                catch (NullReferenceException ex)
                {
                    return false;
                }
            }
        }

        public Boolean HasAudioIn
        {
            get
            {
                if (!Validate())
                    return false;

                try
                {

                    var portId = Account.Endpoint.audDevManager().getCaptureDevMedia().getPortId();

                    return GetAudioMedia().getPortInfo().listeners.Contains(portId);
                }
                catch (NullReferenceException ex)
                {
                    return false;
                }
            }
        }

        public Boolean HasAudioMedia
        {
            get
            {
                if (!Validate())
                    return false;

                var ci = getInfo();

                return ci.media.Any(cmi => cmi != null && cmi.type == pjmedia_type.PJMEDIA_TYPE_AUDIO && cmi.status == pjsua_call_media_status.PJSUA_CALL_MEDIA_ACTIVE && getMedia(cmi.index) != null);
            }
        }

        public Boolean HasAudio => HasAudioIn && HasAudioOut;

        public IReadOnlyDictionary<int, Call> BridgedCalls
        {
            get
            {
                if (Validate() == false)
                    return new Dictionary<int, Call>();

                var am = GetAudioMedia();

                return Account.Calls.Where(c =>
                {
                    var a = c.Value.GetAudioMedia();

                    return am != null && a != null
                           && a.getPortInfo().listeners.Contains(am.getPortId())
                           && am.getPortInfo().listeners.Contains(a.getPortId());
                }).ToDictionary(x => x.Key, x => x.Value);
            }
        }

        public Boolean IsBridged => BridgedCalls.Any();

        public Boolean IsHeld
        {
            get
            {
                if (Validate() == false)
                    return false;

                var ci = getInfo();

                return ci != null && ci.media.Where(cmi => cmi != null && cmi.type == pjmedia_type.PJMEDIA_TYPE_AUDIO).Select(cmi => cmi.status).Any(
                    s => s.In(
                        pjsua_call_media_status.PJSUA_CALL_MEDIA_REMOTE_HOLD,
                        pjsua_call_media_status.PJSUA_CALL_MEDIA_LOCAL_HOLD
                    )
                );
            }
        }

        //"\"CITY, ST\" <sip:5555555555@127.0.0.1>"
        public String CallingName {
            get
            {
                if (Validate() == false)
                    return _callingName ?? "Unknown";

                if (_callingName != null)
                    return _callingName;

                _callingName = GetCallingName();

                return _callingName ?? "Unknown";
            }
        }

        //"\"CITY, ST\" <sip:5555555555@127.0.0.1>"
        public String CallingNumber {
            get
            {
                if (Validate() == false)
                    return _callingNumber ?? "";

                if (_callingNumber != null)
                    return _callingNumber;

                _callingNumber = GetCallingNumber();

                return _callingNumber ?? "";
            }
        }

        public string CallerDisplay => CallingName.Length > 0 && CallingName != "Unknown" ? CallingName : CallingNumber;

        public TimeSpan Duration
        {
            get
            {
                if (Validate())
                    _totalDuration = Invoke(c => getInfo().totalDuration.sec, DispatcherPriority.Send);
                return TimeSpan.FromSeconds(_totalDuration);
            }
        }

        public TimeSpan ConnectedDuration
        {
            get
            {
                if (Validate())
                    _connectDuration = Invoke(c => getInfo().connectDuration.sec, DispatcherPriority.Send);
                return TimeSpan.FromSeconds(_connectDuration);
            }
        }

        public Boolean IsDisposed { get; private set; }
        #endregion

        #region Constructor
        internal Call(Account account, int callId = (int)pjsua_invalid_id_const_.PJSUA_INVALID_ID, PreInitializedHandler preInitializedHandler = null, PostInitializedHandler postInitializedHandler = null) : base(account, callId)
        {
            DebugLog($"New call account:{account.Id}");
            Account = account;
            Id = callId;

            _dtmfBackgroundWorker = new DtmfDialerWorker(this);

            // this is tricky. since the targetCall could already be initialized we need to check the id to determine what handler to targetCall.
            // account.makecall will pass the pre
            // incoming targetCall will pass the post 
            if (Id == (int)pjsua_invalid_id_const_.PJSUA_INVALID_ID)
                preInitializedHandler?.Invoke(this);
            else
                postInitializedHandler?.Invoke(this);
        }

        internal static Call IncomingCall(Account account, int callId = (int)pjsua_invalid_id_const_.PJSUA_INVALID_ID, PreInitializedHandler preInitializedHandler = null, PostInitializedHandler postInitializedHandler = null)
        {
            return new Call(account, callId, preInitializedHandler, postInitializedHandler)
            {
                CallType = Type.Inbound,
                State = pjsip_inv_state.PJSIP_INV_STATE_INCOMING
            };
        }
        #endregion

        #region Overrides

        public override void Dispose()
        {
            NotifyObjectDisposing(this, this);
            //cancel anything observing this token
            _cancellationTokenSource.Cancel();

            DebugLog($"Disposing");
            if (State.In(pjsip_inv_state.PJSIP_INV_STATE_DISCONNECTED, pjsip_inv_state.PJSIP_INV_STATE_NULL) == false)
                hangup(new CallOpParam(true));
            IsDisposed = true;
            base.Dispose();
        }

        public static bool operator ==(Call a, Call b)
        {
            return a?.Id == b?.Id;
        }

        public static bool operator !=(Call a, Call b)
        {
            return a?.Id != b?.Id;
        }

        public override bool Equals(object obj)
        {
            var call = obj as Call;
            return this.Id == call?.Id;
        }

        public override void onCallState(OnCallStateParam prm)
        {
            base.onCallState(prm);

            _callingName = GetCallingName();
            _callingNumber = GetCallingNumber();

            DebugLog($"call state: {getInfo().stateText}");

            LastState = LastState != State ? State : LastState;
            State = getInfo().state;

            if (State == pjsip_inv_state.PJSIP_INV_STATE_DISCONNECTED && LastState == pjsip_inv_state.PJSIP_INV_STATE_INCOMING)
                CallType = Type.Missed;

            NotifyCallState(State);

            if (State == pjsip_inv_state.PJSIP_INV_STATE_DISCONNECTED)
                Dispose();
        }

        public override void onCallMediaState(OnCallMediaStateParam prm)
        {
            base.onCallMediaState(prm);

            DebugLog("media state change");

            var callInfo = getInfo();

            foreach (var callMediaInfo in callInfo.media.Where(cmi => cmi != null && cmi.type == pjmedia_type.PJMEDIA_TYPE_AUDIO))
            {
                //get cached version
                pjsua_call_media_status status;
                MediaStatuses.TryGetValue((int)callMediaInfo.index, out status);

                //if the media status hasn't changed
                if (status == callMediaInfo.status) continue;

                //update the cached version and notify
                MediaStatuses.TryUpdate((int)callMediaInfo.index, callMediaInfo.status, status);
                NotifyCallMediaState(callMediaInfo.status);

                //if the media has already been active then we probably want to skip it
                if (status == pjsua_call_media_status.PJSUA_CALL_MEDIA_ACTIVE || callMediaInfo.status != pjsua_call_media_status.PJSUA_CALL_MEDIA_ACTIVE) continue;

                try
                {
                    //connect the targetCall to the audio device
                    ConnectAudioDevices(callMediaInfo); //TODO: System.NullReferenceException
                }
                catch (AudioDeviceException ex)
                {
                    NotifyAudioDeviceException(ex);
                } 
            }
        }

        public override string ToString()
        {
            return $"{Id}:{Duration}:{CallingName}<{CallingNumber}>";
        }

        #endregion

        #region Delegates
        public delegate void PreInitializedHandler(Call call);
        public delegate void PostInitializedHandler(Call call);
        public delegate void AudioDeviceExceptionHandler(object sender, AudioDeviceExceptionEventArgs e);
        public delegate void MediaStateHandler(object sender, CallMediaStateEventArgs e);
        public delegate void StateHandler(object sender, CallStateEventArgs e);
        public delegate void BridgeStateHandler(object sender, CallBridgeStateEventArgs args);
        #endregion

        #region Events
        public event AudioDeviceExceptionHandler AudioDeviceException;
        public event MediaStateHandler MediaStateChange;
        public event StateHandler StateChange;
        public event BridgeStateHandler BridgeStateChange;
        public event GenericHandlers.ObjectDisposingHandler ObjectDisposing;
        #endregion

        #region Methods

        #region Static
        public static Call MakeCall(Account account, string number) => account.MakeCall(number);
        #endregion

        #region Internal

        #region New
        internal new void makeCall(string destination, CallOpParam opParam = null)
        {
            if (Validate() || IsDisposed)
                throw new Exception("attempt to make call on already connected call object");

            CallType = Type.Outbound;
            _callingNumber = Regex.Match(destination, @"sip\:(.*?)\@").Groups[1]?.Value;
            base.makeCall(destination, opParam ?? new CallOpParam(true));
            Id = getId();
        }
        #endregion

        #endregion

        #region Public

        public Boolean Validate()
        {
            if (Id == (int)pjsua_invalid_id_const_.PJSUA_INVALID_ID)
                return false;

            if (State.In(pjsip_inv_state.PJSIP_INV_STATE_NULL, pjsip_inv_state.PJSIP_INV_STATE_DISCONNECTED))
                return false;

            if (IsDisposed)
                return false;

            if (Dispatcher.CheckAccess() == false && Account.Endpoint.libIsThreadRegistered() == false)
                Account.Endpoint.libRegisterThread(Thread.CurrentThread.ManagedThreadId.ToString());

            return true;
        }

        public String GetCallDurationString() => Duration.ToString(@"hh\:mm\:ss");

        public String GetConnectDurationString() => ConnectedDuration.ToString(@"hh\:mm\:ss");

        public AudioMedia GetAudioMedia()
        {
            var callMediaInfo = getInfo().media.FirstOrDefault(m => m != null && m.type == pjmedia_type.PJMEDIA_TYPE_AUDIO);

            return callMediaInfo == null ? null : AudioMedia.typecastFromMedia(getMedia(callMediaInfo.index));
        }

        public void ConnectAudioDevices(CallMediaInfo callMediaInfo = null)
        {
            DebugLog($"connecting audio devices");


            if (callMediaInfo == null)
            {
                try
                {
                    callMediaInfo = getInfo().media.First(cmi => cmi != null && cmi.type == pjmedia_type.PJMEDIA_TYPE_AUDIO);

                    if (callMediaInfo == null) //TODO: System.NullReferenceException
                    {
                        DebugLog($"Call does not have a valid media [audio] object");
                        throw new InvalidCallMediaInfoException("Error connecting audio device to call");
                    }
                }
                catch (Exception e) {
                    DebugLog($"Call ConnectAudioDevices threw error: {e.Message}");
                    throw e;
                }
            }

            var audioMedia = AudioMedia.typecastFromMedia(getMedia(callMediaInfo.index));
            var audioDeviceManager = Account.Endpoint.audDevManager();
            try
            {
                audioMedia.startTransmit(audioDeviceManager.getPlaybackDevMedia());                
            }
            catch (Exception ex)
            {
                Logger.GetLogger("Call").Error($"[ConnectAudioDevices] Failed to connect playback device ({ex.GetType().Namespace} {ex.GetType().Name})");
                Logger.GetLogger("Call").Error(ex);
                throw new AudioDeviceException("Could not access output device, please check settings and/or physical connection.", ex);
            }

            try
            {
                audioDeviceManager.getCaptureDevMedia().startTransmit(audioMedia);
            }
            catch (Exception ex)
            {
                Logger.GetLogger("Call").Error($"[ConnectAudioDevices] Failed to connect capture device ({ex.GetType().Namespace} {ex.GetType().Name})");
                Logger.GetLogger("Call").Error(ex);
                throw new AudioDeviceException("Could not access input device, please check settings and/or physical connection.", ex);
            }
        }

        public void StopAll()
        {
            DebugLog($"stop all");

            StopTransmit();
            StopRecieve();
        }

        public void StopTransmit()
        {
            DebugLog($"stop transmit");

            var am = GetAudioMedia();

            if (am == null)
            {
                DebugLog($"audio media null");
                return;
            }

            try
            {
                // This will disconnect the sound device/mic to the targetCall audio media
                Account.Endpoint.audDevManager().getCaptureDevMedia().stopTransmit(am);
            }
            catch (Exception ex)
            {
                Logger.GetLogger("Call").Error($"[StopTransmit] Failed to connect capture device ({ex.GetType().Namespace} {ex.GetType().Name})");
                Logger.GetLogger("Call").Error(ex);
                throw new AudioDeviceException("Could not access input device, please check settings and/or physical connection.", ex);
            }
        }

        public void StopRecieve()
        {
            DebugLog($"stop recieve");

            var am = GetAudioMedia();

            if (am == null)
            {
                DebugLog($"audio media null");
                return;
            }

            try
            {
                // And this will disconnect the targetCall audio media to the sound device/speaker
                am.stopTransmit(Account.Endpoint.audDevManager().getPlaybackDevMedia());
            }
            catch (Exception ex)
            {
                Logger.GetLogger("Call").Error($"[StopRecieve] Error accessing output device ({ex.GetType().Namespace} {ex.GetType().Name})");
                Logger.GetLogger("Call").Error(ex);
                throw new AudioDeviceException("Could not access output device, please check settings and/or physical connection.", ex);
            }
        }

        public void StartAll()
        {
            DebugLog($"start all");

            ConnectAudioDevices();
        }

        public void StartTransmit()
        {
            DebugLog($"start transmit");

            var am = GetAudioMedia();

            if (am == null)
            {
                DebugLog($"audio media null");
                return;
            }

            try
            {
                // This will disconnect the sound device/mic to the targetCall audio media
                Account.Endpoint.audDevManager().getCaptureDevMedia().startTransmit(am);
            }
            catch (Exception ex)
            {
                Logger.GetLogger("Call").Error($"[StopTransmit] Error accessing input device ({ex.GetType().Namespace} {ex.GetType().Name})");
                Logger.GetLogger("Call").Error(ex);
                throw new AudioDeviceException("Could not access input device, please check settings and/or physical connection.", ex);
            }
        }

        public void StartRecieve()
        {
            DebugLog($"start recieve");

            var am = GetAudioMedia();

            if (am == null)
            {
                DebugLog($"audio media null");
                return;
            }

            try
            {
                // And this will disconnect the targetCall audio media to the sound device/speaker
                am.startTransmit(Account.Endpoint.audDevManager().getPlaybackDevMedia());
            }
            catch (Exception ex)
            {
                Logger.GetLogger("Call").Error($"[StopRecieve] Error accessing output device ({ex.GetType().Namespace} {ex.GetType().Name})");
                Logger.GetLogger("Call").Error(ex);
                throw new AudioDeviceException("Could not access output device, please check settings and/or physical connection.", ex);
            }
        }

        public void Transfer(String number)
        {
            DebugLog($"blind transfer -> {number}");

            xfer(Account.GetSipAddress(number), new CallOpParam(true));
        }

        public void transferAttended(String number)
        {
            DebugLog($"attended transfer -> {number}");

            transferAttended(Account.MakeCall(number));
        }

        public void transferAttended(Call call)
        {
            DebugLog($"warm transfer -> {call}");

            xferReplaces(call, new CallOpParam(true));
        }
        #endregion

        #region Private

        public async Task RetrieveCallAsync()
        {
            if (Validate() == false)
            {
                DebugLog($"retrieve call async failed: call not valid. Aborted!");
                throw new Exception("Call not valid");
            }

            var call = this;

            DebugLog($"retrieve call task: started");

            await Task.Run(() =>
            {
                DebugLog($"retrieve call task: starting inner retrieve task");

                var waitHandle = new ManualResetEvent(false);

                if (call.IsHeld == false)
                {
                    DebugLog($"retrieve call task: call is not held. Aborting.");
                    return;
                };

                MediaStateHandler handler = (s, e) =>
                {
                    DebugLog($"retrieve call task state change: {e.State}");
                    waitHandle.Set();
                };

                call.MediaStateChange += handler;

                DebugLog($"retrieve call task: retrive");

                call.Retrieve();

                DebugLog($"retrieve call task: waiting");
                waitHandle.WaitOne();
                DebugLog($"retrieve call task: done");

                call.MediaStateChange -= handler;
            }, _cancellationTokenSource.Token);
        }

        private GroupCollection GetCaller()
        {
            return Validate() ? CallerIdRegEx.Match(getInfo().remoteUri).Groups : null;
        }

        private string GetCallingName()
        {
            var result = GetCaller();
            return result != null && result.Count >= 2 ? result[1].Value : "";
        }

        private string GetCallingNumber()
        {
            var result = GetCaller();
            return result != null && result.Count >= 2 ? result[2].Value : _callingNumber + "";
        }

        private void DebugLog(string message) {
            Logger.GetLogger("Call").Debug($"{this}: {message}");
        }

        private new void hangup(CallOpParam op = null)
        {
            DebugLog($"hangup");

            if (Validate() == false) return;

            base.hangup(op);
        }

        public void Answer(bool exclusive = false)
        {
            DebugLog($"answer");

            if (Validate() == false || State != pjsip_inv_state.PJSIP_INV_STATE_INCOMING) return;

            foreach (var call in Account.Endpoint.Accounts.Values.SelectMany(account => account.Calls.Values.Where(call => call.Id != Id && call.Validate() && !call.IsHeld)))
            {
                call.Hold();
            }

            var op = new CallOpParam(true) { statusCode = pjsip_status_code.PJSIP_SC_OK };

            base.answer(op);
        }

        public void Decline()
        {
            DebugLog($"decline");

            if (Validate() == false || (State != pjsip_inv_state.PJSIP_INV_STATE_INCOMING)) return;

            CallType = Type.Declined;

            var op = new CallOpParam(true) { statusCode = pjsip_status_code.PJSIP_SC_DECLINE };

            hangup(op);
        }

        public void Hold()
        {
            DebugLog($"hold");

            if (Validate() && State == pjsip_inv_state.PJSIP_INV_STATE_CONFIRMED && IsHeld == false)
                setHold(new CallOpParam(true));
        }

        public void Retrieve()
        {
            DebugLog($"retrieve");

            if (Validate() == false || State != pjsip_inv_state.PJSIP_INV_STATE_CONFIRMED || IsHeld == false) return;

            var op = new CallOpParam(true);
            op.opt.flag |= (uint)pjsua_call_flag.PJSUA_CALL_UNHOLD;

            base.reinvite(op);
        }

        public async Task<bool> SafeBridge(Call targetCall)
        {
            return await Task.Run(() =>
            {
                DebugLog($"attempting to safe bridge -> {targetCall}");

                if (Validate() == false || targetCall.Validate() == false)
                    return false;

                Task.WaitAll(new Task[]
                {
                    //pjsip gets angry if we retrieve both at the same time so do one at a time
                    RetrieveCallAsync().ContinueWith((task) => targetCall.RetrieveCallAsync())
                }, _cancellationTokenSource.Token);

                DebugLog($"safe bridge tasks returned");

                return Bridge(targetCall);
            });
        }

        private bool Bridge(Call targetCall)
        {
            DebugLog($"attempting to bridge -> {targetCall}");

            //cant bridge self
            if (targetCall == this) return false;

            if (Validate() == false || targetCall.Validate() == false)
                return false;

            if (BridgedCalls.ContainsKey(targetCall.Id) || targetCall.BridgedCalls.ContainsKey(Id))
            {
                DebugLog($"failed to bridge {targetCall}: already bridged. Aborted!");
                return false;
            }

            var audioMedia = GetAudioMedia();
            var targetAudioMedia = targetCall.GetAudioMedia();

            if (audioMedia == null || targetAudioMedia == null)
            {
                DebugLog($"failed to bridge {targetCall}: call does not have media [audio]");
                //throw new Exception("Can't bridge targetCall that has no media!");
                return false;
            }

            try
            {
                audioMedia.startTransmit(targetAudioMedia);
                targetAudioMedia.startTransmit(audioMedia);

                DebugLog($"bridged -> {targetCall}");

                NotifyCallBridgeState(targetCall, CallBridgeStateEventArgs.BridgeState.Bridged);

                return true;
            }
            catch (AudioDeviceException ex)
            {
                NotifyAudioDeviceException(ex);
            }

            return false;

        }

        public bool Unbridge(Call targetCall)
        {
            DebugLog($"attempting to unbridge -> {targetCall}");

            //cant unbridge self
            if (targetCall == this) return false;

            if (!BridgedCalls.ContainsKey(targetCall.Id) || !targetCall.BridgedCalls.ContainsKey(Id))
            {
               DebugLog($"failed to unbridge {targetCall}: calls aren't bridged. Aborted!");
               return false;
            }

            var audioMedia = GetAudioMedia();
            var targetAudioMedia = targetCall.GetAudioMedia();

            if (audioMedia == null || targetAudioMedia == null)
            {
                DebugLog($"failed to unbridge {targetCall}: call does not have media [audio]");
                //throw new Exception("Can't unbridge call that does not have media [audio]!");
                return false;
            }


            try
            {
                audioMedia.stopTransmit(targetAudioMedia);
                targetAudioMedia.stopTransmit(audioMedia);

                DebugLog($"unbridged -> {targetCall}");

                NotifyCallBridgeState(targetCall, CallBridgeStateEventArgs.BridgeState.Unbridged);

                return true;
            }
            catch (AudioDeviceException ex)
            {
                NotifyAudioDeviceException(ex);
            }

            return false;
        }

        public void hangup() => hangup(new CallOpParam(true));

        public void DialDtmf(string digits, bool queue = true)
        {
            if (Validate() == false)
            {
                DebugLog($"failed to dial dtmf: call is not valid!");
                throw new Exception("failed to dial dtmf: call is not valid!");
            }

            if (queue == false)
            {
                DebugLog($"DTMF Sending: {digits}");
                base.dialDtmf(digits);
                return;
            }

            var running = _dtmfBackgroundWorker.Running; //get current stae before we add to the queue
            _dtmfBackgroundWorker.DtmfQueue.Enqueue(digits);

            DebugLog($"DTMF Queued: {digits}");

            //make sure we aren't already running
            if (running)
            {
                DebugLog($"dtmf worker already running");
            }

            _dtmfBackgroundWorker.Process(_dtmfProgress, _cancellationTokenSource.Token);
        }

        public void Invoke(GenericHandlers.InvokableDelegate<Call> action, DispatcherPriority priorty = DispatcherPriority.Normal) => Extensions.Invoke(this, action, priorty);
        public TR Invoke<TR>(GenericHandlers.InvokableDelegate<Call, TR> action, DispatcherPriority priorty = DispatcherPriority.Normal) => Extensions.Invoke(this, action, priorty);
        public void BeginInvoke(GenericHandlers.InvokableDelegate<Call> action, DispatcherPriority priorty = DispatcherPriority.Normal) => Extensions.BeginInvoke(this, action, priorty);
        public Task<TR> BeginInvoke<TR>(GenericHandlers.InvokableDelegate<Call, TR> action, DispatcherPriority priorty = DispatcherPriority.Normal) => Extensions.BeginInvoke(this, action, priorty);

        #region EventNotifiers
        private void NotifyAudioDeviceException(AudioDeviceException exception) => AudioDeviceException?.Invoke(this, new AudioDeviceExceptionEventArgs(this, exception));
        private void NotifyCallMediaState(pjsua_call_media_status state) => MediaStateChange?.Invoke(this, new CallMediaStateEventArgs(this, state));
        private void NotifyObjectDisposing(object sender, object obj) => ObjectDisposing?.Invoke(this, obj);
        private void NotifyCallState(pjsip_inv_state state) => StateChange?.Invoke(this, new CallStateEventArgs(this, state));
        private void NotifyCallBridgeState(Call targetCall, CallBridgeStateEventArgs.BridgeState state) => BridgeStateChange?.Invoke(this, new CallBridgeStateEventArgs(this, targetCall, state));
        #endregion

        #endregion

        #endregion
    }
}
