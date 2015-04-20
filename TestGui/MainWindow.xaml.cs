using pjsipDotNetSDK;
using System;
using System.Collections;
using System.Collections.Generic;
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

namespace TestGui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        SipManager sm;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                sm = new SipManager(Application.Current.Dispatcher.Thread);

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

            System.Timers.Timer timer = new System.Timers.Timer(250);
            timer.Elapsed += (a, b) => 
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
                    PJSIP.AudioMedia ep = call.getAudioMedia(); //PJSIP.Endpoint.instance().audDevManager().getCaptureDevMedia();
                    if(ep != null)
                        Console.WriteLine("RX:" + ep.getRxLevel() + " TX:" + ep.getTxLevel());
                }
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

            foreach (Call call in sm.Calls.Values.ToList())
            {
                Console.WriteLine("State: " + call.State + " : " + call.LastState);
            }

            Refresh();
        }

        void sm_AccountStateChange(object sender, AccountStateEventArgs e)
        {
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


                        PJSIP.AudioMedia am = call.getAudioMedia();
                        int count = 0;
                        if (am != null)
                        {
                            PJSIP.ConfPortInfo pi = am.getPortInfo();

                            PJSIP.IntVector iv = pi.listeners;
                            count = iv.Count;
                        }


                        ListBoxItem lbi = new ListBoxItem();
                        lbi.Content = count + " - " + call.getCallerDisplay() + " " + (call.IsHeld ? "HOLDING" : (call.IsBridged ? "CONFERENCE" : info.stateText)) + " " + call.getCallDurationString();
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
            sm.DefaultAccount.makeCall(PhoneNumberTextBox.Text);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            sm.Dispose();
        }

        private void AnswerButton_Click(object sender, RoutedEventArgs e)
        {
            ((Call)((ListBoxItem)CallList.SelectedItems[0]).Tag).answer();
        }

        private void HangupButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (ListBoxItem lbi in CallList.SelectedItems)
            {
                ((Call)lbi.Tag).hangup();
            }
        }

        private void BridgeButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (ListBoxItem lbi in CallList.SelectedItems)
            {
                foreach (ListBoxItem lbi2 in CallList.SelectedItems)
                {
                    ((Call)lbi.Tag).bridge((Call)lbi2.Tag);
                }
            }
        }

        private void DialDTMF(string digit)
        {
            foreach (ListBoxItem lbi in CallList.SelectedItems)
            {
                foreach (ListBoxItem lbi2 in CallList.SelectedItems)
                {
                    ((Call)lbi.Tag).dialDtmf(digit);
                }
            }
        }

        private void DTMFButton_Click(object sender, RoutedEventArgs e)
        {
            DialDTMF((string)((Button)sender).Content);
        }

        private void HoldButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (ListBoxItem lbi in CallList.SelectedItems)
            {
                foreach (ListBoxItem lbi2 in CallList.SelectedItems)
                {
                    Call call = (Call)lbi.Tag;
                    if (!call.IsHeld)
                        call.hold();
                    else
                        call.retrieve();
                }
            }
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            sm.addAccount(UsernameTextBox.Text, PasswordTextBox.Text, HostTextBox.Text);
        }

        private void StopInOutButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (ListBoxItem lbi in CallList.SelectedItems)
            {
                foreach (ListBoxItem lbi2 in CallList.SelectedItems)
                {
                    ((Call)lbi.Tag).stopAll();
                }
            }
        }

        private void StopInputButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (ListBoxItem lbi in CallList.SelectedItems)
            {
                foreach (ListBoxItem lbi2 in CallList.SelectedItems)
                {
                    ((Call)lbi.Tag).stopTransmit();
                }
            }
        }

        private void StopOutputButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (ListBoxItem lbi in CallList.SelectedItems)
            {
                foreach (ListBoxItem lbi2 in CallList.SelectedItems)
                {
                    ((Call)lbi.Tag).stopRecieve();
                }
            }
        }

        private void StartInOutButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (ListBoxItem lbi in CallList.SelectedItems)
            {
                foreach (ListBoxItem lbi2 in CallList.SelectedItems)
                {
                    ((Call)lbi.Tag).startAll();
                }
            }
        }

        private void StartInputButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (ListBoxItem lbi in CallList.SelectedItems)
            {
                foreach (ListBoxItem lbi2 in CallList.SelectedItems)
                {
                    ((Call)lbi.Tag).startTransmit();
                }
            }
        }

        private void StartOutputButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (ListBoxItem lbi in CallList.SelectedItems)
            {
                foreach (ListBoxItem lbi2 in CallList.SelectedItems)
                {
                    ((Call)lbi.Tag).startRecieve();
                }
            }
        }

        private void TransferButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (ListBoxItem lbi in CallList.SelectedItems)
            {
                ((Call)lbi.Tag).transfer(PhoneNumberTextBox.Text);
            }
        }

        private void TransferAttendedButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (ListBoxItem lbi in CallList.SelectedItems)
            {
                ((Call)lbi.Tag).transferAttended(PhoneNumberTextBox.Text);
            }
        }

        private void BoostRxButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (ListBoxItem lbi in CallList.SelectedItems)
            {
                PJSIP.AudioMedia am = ((Call)lbi.Tag).getAudioMedia();
                am.adjustRxLevel(float.Parse(RxTextBox.Text));
            }
        }
    }
}
