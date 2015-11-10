using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.IO;
using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace mbed_Deploy_Tool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private SerialPort mbedPort = null;

        private TextBox debugLogTextBox;

        public MainWindow()
        {
            InitializeComponent();
            initDebugLog();
            reloadCOMPorts();
            reloadDrives();
        }

        [Conditional("DEBUG")]
        private void initDebugLog()
        {
            debugLogTextBox = new TextBox();
            debugLogTextBox.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            debugLogTextBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            debugLogTextBox.IsReadOnly = true;
            debugLogTextBox.FontFamily = new System.Windows.Media.FontFamily("Courier New");
            debugLogTextBox.FontSize = 10.667;

            RowDefinition rowDef = new RowDefinition();
            rowDef.Height = new GridLength(80);
            // current count is new index ==> (current max-index) + 1 ==> (count-1)+1 ==> count
            int rowIndex = mainGrid.RowDefinitions.Count;
            mainGrid.RowDefinitions.Add(rowDef);

            GroupBox debugGroupBox = new GroupBox();
            debugGroupBox.SetValue(Grid.RowProperty, rowIndex);
            debugGroupBox.Header = "Debug Log";
            debugGroupBox.Content = debugLogTextBox;

            mainGrid.Children.Add(debugGroupBox);
        }

        private void ToggleButton_CheckedUnChecked(object sender, RoutedEventArgs e)
        {
            if(advancedSettingsButton.IsChecked ?? false)
            {
                advancedSettings.Visibility = Visibility.Visible;
            } else
            {
                advancedSettings.Visibility = Visibility.Collapsed;
            }
        }

        void enableConnectButton(bool enable)
        {
            connectButton.IsEnabled = enable;
        }

        void enableDisconnectAndMoreButtons(bool enable)
        {
            disconnectButton.IsEnabled = enable;
            resetButton.IsEnabled = enable;
            outputTextBox.IsEnabled = enable;
        }

        private void connectButton_Click(object sender, RoutedEventArgs e)
        {
            connectSerialPort();
        }

        private void connectSerialPort()
        {
            if (serialPortComboBox.SelectedIndex == -1 ||
                BaudRateComboBox.SelectedIndex == -1 ||
                dataBitsUpDown.Text.Length == 0 ||
                stopBitsComboBox.SelectedIndex == -1 ||
                parityComboBox.SelectedIndex == -1 ||
                flowControlComboBox.SelectedIndex == -1)
            {
                // settings not complete
                log("Settings not complete");
                MessageBox.Show("Empty settings detected! Please make sure every settings-field has a valid value.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            enableConnectButton(false);

            mbedPort = new SerialPort();
            mbedPort.PinChanged += MbedPort_PinChanged;
            mbedPort.ErrorReceived += MbedPort_ErrorReceived;
            mbedPort.DataReceived += MbedPort_DataReceived;
            mbedPort.PortName = serialPortComboBox.Text;
            mbedPort.BaudRate = Convert.ToInt32(BaudRateComboBox.Text);
            mbedPort.DataBits = Convert.ToInt32(dataBitsUpDown.Text);
            mbedPort.StopBits = getSelectedStopBits();
            mbedPort.Parity = getSelectedParity();
            mbedPort.Handshake = getSelectedFlowControl();
            mbedPort.Open();
            if (!mbedPort.IsOpen)
            {
                log("Failed to connect");
                MessageBox.Show(String.Format("Unable to connect to {0}!", serialPortComboBox.Text), Title, MessageBoxButton.OK, MessageBoxImage.Error);
                disconnectSerialPort();
                return;
            }

            enableDisconnectAndMoreButtons(true);
        }

        private StopBits getSelectedStopBits()
        {
            switch(stopBitsComboBox.SelectedIndex)
            {
                case 0: return StopBits.None; // "0"
                case 1: return StopBits.One; // "1"
                case 2: return StopBits.OnePointFive; // "1.5"
                case 3: return StopBits.Two; // "2"
                default: log("getSelectedStopBits: this souldn't happen");  return StopBits.None; // have to return something otherwise this shit fails...
            }
        }
        private Parity getSelectedParity()
        {
            switch (parityComboBox.SelectedIndex)
            {
                case 0: return Parity.None; // "None"
                case 1: return Parity.Odd; // "odd"
                case 2: return Parity.Even; // "Even"
                case 3: return Parity.Mark; // "Mark"
                case 4: return Parity.Space; // "Space"
                default: log("getSelectedParity: this souldn't happen"); return Parity.None; // have to return something otherwise this shit fails...
            }
        }

        private Handshake getSelectedFlowControl()
        {
            switch (flowControlComboBox.SelectedIndex)
            {
                case 0: return Handshake.None; // "None"
                case 1: return Handshake.XOnXOff; // "XON/XOFF (software)"
                case 2: return Handshake.RequestToSend; // "Request To Send (hardware)"
                case 3: return Handshake.RequestToSendXOnXOff; // "Both"
                default: log("getSelectedFlowControl: this souldn't happen"); return Handshake.None; // have to return something otherwise this shit fails...
            }
        }

        private void disconnectButton_Click(object sender, RoutedEventArgs e)
        {
            disconnectSerialPort();
        }

        private void disconnectSerialPort()
        {
            enableDisconnectAndMoreButtons(false);
            cleanUpMbedPort();
            enableConnectButton(true);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            cleanUpMbedPort();
        }

        private void cleanUpMbedPort()
        {
            if(mbedPort == null) { return; }
            mbedPort.Close();
            mbedPort.Dispose();
            mbedPort = null;
        }

        private void dataBitsUpDown_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            int val = Convert.ToInt32(dataBitsUpDown.Text);
            val += e.Delta / 120;
            if (val < 0)
            {
                val = 0;
            }
            dataBitsUpDown.Text = val.ToString();
        }

        private void resetButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            mbedPort.BreakState = true;
        }

        private void resetButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            mbedPort.BreakState = false;
        }

        private void clearOutputButton_Click(object sender, RoutedEventArgs e)
        {
            outputTextBox.Clear();
        }

        private void MbedPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            log("MbedPort_DataReceived: " + e.EventType);
            switch(e.EventType)
            {
                case SerialData.Chars:
                    appentToOutout(mbedPort.ReadExisting());
                    break;
                case SerialData.Eof:
                    log("eof received, lets close connection");
                    MessageBox.Show("Connection closed by mbed", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                    disconnectSerialPort();
                    break;
            }
        }

        private void MbedPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            log("MbedPort_ErrorReceived: " + e.EventType);
            MessageBox.Show(String.Format("Error detected of type: {0}", e.EventType), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            // TODO handle error???
        }

        private void MbedPort_PinChanged(object sender, SerialPinChangedEventArgs e)
        {
            log("MbedPort_PinChanged: " + e.EventType);
            // TODO maybe do something with those???
        }

        private void appentToOutout(string str)
        {
            if(outputTextBox.Dispatcher.CheckAccess())
            {
                outputTextBox.AppendText(str);
                outputTextBox.ScrollToEnd();
            } else
            {
                outputTextBox.Dispatcher.Invoke(() =>
                {
                    appentToOutout(str);
                });
            }
        }

        [Conditional("DEBUG")]
        private void log(string message)
        {
            if (debugLogTextBox.Dispatcher.CheckAccess())
            {
                debugLogTextBox.AppendText(message + "\r\n");
                debugLogTextBox.ScrollToEnd();
            }
            else
            {
                debugLogTextBox.Dispatcher.Invoke(() =>
                {
                    log(message);
                });
            }
        }

        private static Regex numbersOnlyRegex = new Regex(@"^[0-9]+$");
        private void numbersOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !numbersOnlyRegex.IsMatch(e.Text);
        }

        private void numbersOnly_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(String)))
            {
                String text = (String)e.DataObject.GetData(typeof(String));
                if (!numbersOnlyRegex.IsMatch(text))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        private void reloadButton_Click(object sender, RoutedEventArgs e)
        {
            reloadCOMPorts();
        }

        private void reloadCOMPorts()
        {
            serialPortComboBox.Items.Clear();
            foreach(string name in SerialPort.GetPortNames())
            {
                serialPortComboBox.Items.Add(name);
            }
        }

        private void reloadDrivesButton_Click(object sender, RoutedEventArgs e)
        {
            reloadDrives();
        }

        List<string> drives = new List<string>();

        private void reloadDrives()
        {
            DriveInfo[] allDrives = DriveInfo.GetDrives();
            driveComboBox.Items.Clear();
            drives.Clear();
            int possibleMbedDrive = -1;
            foreach (DriveInfo d in allDrives)
            {
                if (d.IsReady == true && d.DriveType == DriveType.Removable)
                {
                    driveComboBox.Items.Add(d.VolumeLabel + " (" + d.Name.Substring(0,2) + ")");
                    drives.Add(d.Name);
                    if(d.VolumeLabel == "MBED")
                    {
                        possibleMbedDrive = driveComboBox.Items.Count - 1;
                    }
                }
            }

            driveComboBox.SelectedIndex = possibleMbedDrive;
        }

        private void browseButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.DefaultExt = ".bin";
            dlg.Filter = "mbed Binary (*.bin)|*.bin";
            dlg.FileName = binFilePath.Text;

            bool? result = dlg.ShowDialog(this);

            if(result == true)
            {
                binFilePath.Text = dlg.FileName;
            }
        }

        private async void deployButton_Click(object sender, RoutedEventArgs e)
        {
            string srcFile = binFilePath.Text;
            if (driveComboBox.SelectedIndex == -1 || !File.Exists(srcFile))
            {
                log("Cancel deploy: empty values detected, or bin-file does not exist");
                MessageBox.Show("Binary file and/or mbed-drive does not exist.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string drive = drives[driveComboBox.SelectedIndex];
            string fileName = Path.GetFileName(srcFile);
            string destFile = Path.Combine(drive, fileName);
            log(String.Format("Deploying: {0} to {1}", srcFile, destFile));

            IsEnabled = false;
            deployProgressBar.Visibility = Visibility.Visible;
            await Task.Run(() => File.Copy(srcFile, destFile, true));
            deployProgressBar.Visibility = Visibility.Hidden;
            IsEnabled = true;

            log("Deploying done");
        }
    }
}
