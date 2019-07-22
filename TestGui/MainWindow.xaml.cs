using PJSIPDotNetSDK;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using PJSIP;
using PJSIPDotNetSDK.EventArgs;
using Call = PJSIPDotNetSDK.Entity.Call;

namespace TestGui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        PJSIPDotNetSDK.SipManager sm;
        System.Timers.Timer timer = new System.Timers.Timer(250);

        private bool _isRegistered = false;
        public bool isRegistered
        {
            get { return _isRegistered; }
            set {
                _isRegistered = value;
                OnPropertyChanged("isRegistered");
            }
        }

        private bool _hasActiveCall = false;
        public bool hasActiveCall
        {
            get { return _hasActiveCall; }
            set
            {
                _hasActiveCall = value;
                OnPropertyChanged("hasActiveCall");
            }
        }

        private bool _hasIncomingCall = false;
        public bool hasIncomingCall
        {
            get { return _hasIncomingCall; }
            set
            {
                _hasIncomingCall = value;
                OnPropertyChanged("hasIncomingCall");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                sm = new SipManager(this.Dispatcher.Thread);

                Refresh();

                sm.AccountStateChange += sm_AccountStateChange;
                sm.CallStateChange += sm_CallStateChange;
                sm.CallMediaStateChange += sm_CallMediaStateChange;
                sm.IncomingCall += sm_IncomingCall;
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            timer.Elapsed += (a, b) =>
            {
                Application.Current.Dispatcher.BeginInvoke(
                    DispatcherPriority.Normal,
                    (ThreadStart) delegate()
                    {
                        foreach (Call call in sm.Calls.Values.ToList())
                        {

                            /*for (int i = 0; i < call.getInfo().media.Count; ++i)
                    {
                        //if (call.getInfo().media[i].type == PJSIP.pjmedia_type.PJMEDIA_TYPE_AUDIO)
                        //{
                        Console.WriteLine(call.getInfo().media[i].type + " : " + call.getInfo().media[i].status.ToString() + " : " + PJSIP.AudioMedia.typecastFromMedia(call.getMedia((uint)i)).getPortInfo().listeners.Count);
                        //}
                    }

                    Console.WriteLine(call.hasMedia());

                    if (call.getAudioMedia() == null) continue;
                    Console.WriteLine("RX:" + call.getAudioMedia().getRxLevel() + " TX:" + call.getAudioMedia().getTxLevel());
                    PJSIP.AudioMedia ep = PJSIP.Endpoint.instance().audDevManager().getCaptureDevMedia();
                    PJSIP.IntVector iv = ep.getPortInfo().listeners;
                    Console.WriteLine(iv.Contains(call.getAudioMedia().getPortInfo().portId));
                    foreach (int v in iv)
                    {
                        Console.Write(v + " ");
                    }
                    Console.WriteLine("port:"+call.getAudioMedia().getPortInfo().portId);*/
                            PJSIP.AudioMedia ep = call.GetAudioMedia();
                            //PJSIP.Endpoint.instance().audDevManager().getCaptureDevMedia();
                            if (ep != null)
                                Console.WriteLine("RX:" + ep.getRxLevel() + " TX:" + ep.getTxLevel());
                        }
                    });
            };
            timer.Start();
        }

        void sm_CallMediaStateChange(object sender, CallMediaStateEventArgs e)
        {
            /*PJSIP.CallInfo ci = e.Call.getInfo();
            Console.WriteLine("Call Media State:");
            Console.WriteLine(ci.callIdString);
            Console.WriteLine(e.State);*/

            Refresh();
        }

        void sm_IncomingCall(object sender, IncomingCallEventArgs e)
        {
            /*PJSIP.CallInfo ci = e.Call.getInfo();
            Console.WriteLine("Incoming Call:");
            Console.WriteLine(ci.localContact);
            Console.WriteLine(ci.stateText);*/

            foreach (Call call in sm.Calls.Values.ToList())
            {
                Console.WriteLine("State: " + call.State + " : " + call.LastState);
            }

            Refresh();
        }

        void sm_CallStateChange(object sender, CallStateEventArgs e)
        {
            /*PJSIP.CallInfo ci = e.Call.getInfo();
            Console.WriteLine("Call State:");
            Console.WriteLine(ci.callIdString);
            Console.WriteLine(ci.stateText);*/

            hasActiveCall = sm.Calls.Values.ToList().Any(call => call.State == pjsip_inv_state.PJSIP_INV_STATE_CONFIRMED);
            hasIncomingCall = sm.Calls.Values.ToList().Any(call => call.State == pjsip_inv_state.PJSIP_INV_STATE_INCOMING);

            foreach (Call call in sm.Calls.Values.ToList())
            {
                Console.WriteLine("State: " + call.State + " : " + call.LastState);
            }

            Refresh();
        }

        void sm_AccountStateChange(object sender, AccountStateEventArgs e)
        {
            isRegistered = e.State == pjsip_status_code.PJSIP_SC_OK;
            /*Console.WriteLine("Account State:");
            Console.WriteLine(e.State.ToString());
            Console.WriteLine(e.Reason);*/

            Refresh();
        }

        private void Refresh()
        {
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                (ThreadStart)delegate()
                {
                    if (sm.DefaultAccount != null)
                        RegisteredLabel.Content = sm.DefaultAccount.getInfo().regStatusText;

                    TotalCallsLabel.Text = sm.Calls.Count + " Calls";

                    CallList.Items.Clear();
                    foreach (Call call in sm.Calls.Values.ToList())
                    {
                        PJSIP.CallInfo info = call.getInfo();


                        PJSIP.AudioMedia am = call.GetAudioMedia();
                        int count = 0;
                        if (am != null)
                        {
                            PJSIP.ConfPortInfo pi = am.getPortInfo();

                            PJSIP.IntVector iv = pi.listeners;
                            count = iv.Count;
                        }


                        ListBoxItem lbi = new ListBoxItem();
                        lbi.Content = count + " - " + call.CallerDisplay + " " + (call.IsHeld ? "HOLDING" : (call.IsBridged ? "CONFERENCE" : info.stateText)) + " " + call.GetCallDurationString();
                        lbi.Tag = call;
                        CallList.Items.Add(lbi);
                    }

                    CallLogList.Items.Clear();
                    foreach (CallLog log in sm.CallLogManager.CallLogs)
                    {
                        ListBoxItem lbi = new ListBoxItem();
                        lbi.Content = log.LogType + " " + log.Name + " " + log.Number + " " + log.DurationString;
                        lbi.Tag = log;
                        CallLogList.Items.Add(lbi);
                    }

                }
            );
        }

        private void MakeCallButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                (ThreadStart) delegate
                {
                    if (sm.Endpoint.libIsThreadRegistered() == false)
                    {
                        sm.Endpoint.libRegisterThread(App.Current.Dispatcher.Thread.Name);
                    }
                    sm.DefaultAccount.MakeCall(PhoneNumberTextBox.Text);
                }
            );
        }

        private void AnswerButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                (ThreadStart) delegate()
                {
                    ((Call) ((ListBoxItem) CallList.SelectedItems[0]).Tag).Answer();
                });
        }

        private void HangupButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                (ThreadStart) delegate()
                {
                    foreach (ListBoxItem lbi in CallList.SelectedItems)
                    {
                        ((Call) lbi.Tag).hangup();
                    }
                });
        }

        private void BridgeButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                (ThreadStart) async delegate()
                {
                    foreach (ListBoxItem lbi in CallList.SelectedItems)
                    {
                        foreach (ListBoxItem lbi2 in CallList.SelectedItems)
                        {
                            await ((Call)lbi.Tag).SafeBridge((Call)lbi2.Tag);
                        }
                    }
                }
            );
        }

        private void DialDTMF(string digit)
        {
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                (ThreadStart) delegate()
                {
                    foreach (ListBoxItem lbi in CallList.SelectedItems)
                    {
                        foreach (ListBoxItem lbi2 in CallList.SelectedItems)
                        {
                            ((Call) lbi.Tag).DialDtmf(digit, false);
                        }
                    }
                }
            );
        }

        private void DTMFButton_Click(object sender, RoutedEventArgs e)
        {
            DialDTMF((string) ((Button) sender).Content);
        }

        private void HoldButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                (ThreadStart) delegate()
                {
                    foreach (ListBoxItem lbi in CallList.SelectedItems)
                    {
                        foreach (ListBoxItem lbi2 in CallList.SelectedItems)
                        {
                            Call call = (Call) lbi.Tag;
                            if (!call.IsHeld)
                                call.Hold();
                            else
                                call.Retrieve();
                        }
                    }
                }
          );
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                (ThreadStart) delegate()
                {
                    sm.addAccount(UsernameTextBox.Text, PasswordTextBox.Text, HostTextBox.Text);
                });
        }

        private void StopInOutButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                (ThreadStart) delegate()
                {
                    foreach (ListBoxItem lbi in CallList.SelectedItems)
                    {
                        foreach (ListBoxItem lbi2 in CallList.SelectedItems)
                        {
                            ((Call) lbi.Tag).StopAll();
                        }
                    }
                });
        }

        private void StopInputButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                (ThreadStart) delegate()
                {
                    foreach (ListBoxItem lbi in CallList.SelectedItems)
                    {
                        foreach (ListBoxItem lbi2 in CallList.SelectedItems)
                        {
                            ((Call) lbi.Tag).StopTransmit();
                        }
                    }
                });
        }

        private void StopOutputButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                (ThreadStart) delegate()
                {
                    foreach (ListBoxItem lbi in CallList.SelectedItems)
                    {
                        foreach (ListBoxItem lbi2 in CallList.SelectedItems)
                        {
                            ((Call) lbi.Tag).StopRecieve();
                        }
                    }
                });
        }

        private void StartInOutButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                (ThreadStart) delegate()
                {
                    foreach (ListBoxItem lbi in CallList.SelectedItems)
                    {
                        foreach (ListBoxItem lbi2 in CallList.SelectedItems)
                        {
                            ((Call) lbi.Tag).StartAll();
                        }
                    }
                });
        }

        private void StartInputButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                (ThreadStart) delegate()
                {
                    foreach (ListBoxItem lbi in CallList.SelectedItems)
                    {
                        foreach (ListBoxItem lbi2 in CallList.SelectedItems)
                        {
                            ((Call) lbi.Tag).StartTransmit();
                        }
                    }
                });
        }

        private void StartOutputButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                (ThreadStart) delegate()
                {
                    foreach (ListBoxItem lbi in CallList.SelectedItems)
                    {
                        foreach (ListBoxItem lbi2 in CallList.SelectedItems)
                        {
                            ((Call) lbi.Tag).StartRecieve();
                        }
                    }
                });
        }

        private void TransferButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                (ThreadStart) delegate()
                {
                    foreach (ListBoxItem lbi in CallList.SelectedItems)
                    {
                        ((Call) lbi.Tag).Transfer(PhoneNumberTextBox.Text);
                    }
                });
        }

        private void TransferAttendedButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                (ThreadStart) delegate()
                {
                    foreach (ListBoxItem lbi in CallList.SelectedItems)
                    {
                        ((Call) lbi.Tag).transferAttended(PhoneNumberTextBox.Text);
                    }
                });
        }

        private void BoostRxButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                (ThreadStart) delegate()
                {
                    foreach (ListBoxItem lbi in CallList.SelectedItems)
                    {
                        PJSIP.AudioMedia am = ((Call) lbi.Tag).GetAudioMedia();
                        am.adjustRxLevel(float.Parse(RxTextBox.Text));
                    }
                });
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            timer.Stop();

            sm.AccountStateChange -= sm_AccountStateChange;
            sm.CallStateChange -= sm_CallStateChange;
            sm.CallMediaStateChange -= sm_CallMediaStateChange;
            sm.IncomingCall -= sm_IncomingCall;

            Application.Current.Dispatcher.Invoke(
                DispatcherPriority.Normal,
                (ThreadStart)delegate ()
                {
                    sm.Endpoint.Dispose();
                }
            );
        }
    }
}
