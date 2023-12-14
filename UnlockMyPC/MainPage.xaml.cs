using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.Background;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Security.Authentication.Identity.Provider;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// 空白頁項目範本已記錄在 https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x404

namespace UnlockMyPC
{
    /// <summary>
    /// 可以在本身使用或巡覽至框架內的空白頁面。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        String m_selectedDeviceId = String.Empty;
        bool taskRegistered = false;
        static string myBGTaskName = "UnlockMyPC";
        static string myBGTaskEntryPoint = "BackGroundTask.UnlockPCTask";
        public MainPage()
        {
            this.InitializeComponent();
            RegDeviceList.SelectionChanged += RegDeviceList_SelectionChanged;
            RefreshDeviceList();
            RefreshServiceRegStatus();
        }

        private async void RegButton_Click(object sender, RoutedEventArgs e)
        {
            DevicePicker picker = new DevicePicker();
            picker.Filter.SupportedDeviceSelectors.Add(
                    BluetoothDevice.GetDeviceSelectorFromPairingState(true)
                );
            Debug.WriteLine("before Reg");
            DeviceInformation device = await picker.PickSingleDeviceAsync(new Rect());
            if (device != null)
            {
                Debug.WriteLine("device:" + device.Name);

                DeviceRegistration(device);
            }
            Debug.WriteLine("after Reg");
        }
        public async void DeviceRegistration(DeviceInformation device)
        {

            IBuffer deviceKey = CryptographicBuffer.GenerateRandom(32);
            IBuffer authKey = CryptographicBuffer.GenerateRandom(32);

            byte[] deviceKeyArray = { 0 };
            CryptographicBuffer.CopyToByteArray(deviceKey, out deviceKeyArray);

            byte[] authKeyArray = { 0 };
            CryptographicBuffer.CopyToByteArray(authKey, out authKeyArray);

            int combinedDataArraySize = deviceKeyArray.Length + authKeyArray.Length;
            byte[] combinedDataArray = new byte[combinedDataArraySize];
            for (int index = 0; index < deviceKeyArray.Length; index++)
            {
                combinedDataArray[index] = deviceKeyArray[index];
            }
            for (int index = 0; index < authKeyArray.Length; index++)
            {
                combinedDataArray[deviceKeyArray.Length + index] = authKeyArray[index];
            }

            IBuffer deviceConfigData = CryptographicBuffer.CreateFromByteArray(combinedDataArray);


            String deviceId = device.Id.Replace("Bluetooth#Bluetooth", "");
            String deviceName = device.Name;



            Debug.WriteLine("Device Id:" + deviceId);
            Debug.WriteLine("Device Name:" + deviceName);
            Debug.WriteLine("deviceKey:" + CryptographicBuffer.EncodeToHexString(deviceKey));
            Debug.WriteLine("authKey:" + CryptographicBuffer.EncodeToHexString(authKey));
            SecondaryAuthenticationFactorRegistrationResult registrationResult =
                await SecondaryAuthenticationFactorRegistration.RequestStartRegisteringDeviceAsync(
                deviceId,
                SecondaryAuthenticationFactorDeviceCapabilities.SupportSecureUserPresenceCheck,
                deviceName,
                "MyDevice",
                deviceKey,
                authKey);

            if (registrationResult.Status != SecondaryAuthenticationFactorRegistrationStatus.Started)
            {
                MessageDialog myDlg = null;

                if (registrationResult.Status == SecondaryAuthenticationFactorRegistrationStatus.DisabledByPolicy)
                {
                    //For DisaledByPolicy Exception:Ensure secondary auth is enabled.
                    //Use GPEdit.msc to update group policy to allow secondary auth
                    //Local Computer Policy\Computer Configuration\Administrative Templates\Windows Components\Microsoft Secondary Authentication Factor\Allow Companion device for secondary authentication
                    myDlg = new MessageDialog("Disabled by Policy.  Please update the policy and try again.");
                }

                if (registrationResult.Status == SecondaryAuthenticationFactorRegistrationStatus.PinSetupRequired)
                {
                    //For PinSetupRequired Exception:Ensure PIN is setup on the device
                    //Either use gpedit.msc or set reg key
                    //This setting can be enabled by creating the AllowDomainPINLogon REG_DWORD value under the HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System Registry key and setting it to 1.
                    myDlg = new MessageDialog("Please setup PIN for your device and try again.");
                }

                if (myDlg != null)
                {
                    await myDlg.ShowAsync();
                    return;
                }
            }
            System.Diagnostics.Debug.WriteLine("Device Registration Started!");
            await registrationResult.Registration.FinishRegisteringDeviceAsync(deviceConfigData);

            RegDeviceList.Items.Add(deviceId);
            System.Diagnostics.Debug.WriteLine("Device Registration is Complete!");

            IReadOnlyList<SecondaryAuthenticationFactorInfo> deviceList = await SecondaryAuthenticationFactorRegistration.FindAllRegisteredDeviceInfoAsync(
                SecondaryAuthenticationFactorDeviceFindScope.AllUsers);
            for (int index = 0; index < deviceList.Count; ++index)
            {
                SecondaryAuthenticationFactorInfo deviceInfo = deviceList.ElementAt(index);
                Debug.WriteLine("  >>>> deviceInfo[" + index + "]:" + deviceInfo.DeviceFriendlyName);
                Debug.WriteLine("  >>>> DeviceId[" + index + "]:" + deviceInfo.DeviceId);
            }
            RefreshDeviceList();
        }
        async void RefreshDeviceList()
        {
            IReadOnlyList<SecondaryAuthenticationFactorInfo> deviceList = await SecondaryAuthenticationFactorRegistration.FindAllRegisteredDeviceInfoAsync(
                SecondaryAuthenticationFactorDeviceFindScope.AllUsers);

            RegDeviceList.Items.Clear();

            for (int index = 0; index < deviceList.Count; ++index)
            {
                SecondaryAuthenticationFactorInfo deviceInfo = deviceList.ElementAt(index);
                Debug.WriteLine("  >>>> deviceInfo[" + index + "]:" + deviceInfo.DeviceFriendlyName);
                Debug.WriteLine("  >>>> DeviceId[" + index + "]:" + deviceInfo.DeviceId);
                RegDeviceList.Items.Add(deviceInfo.DeviceId);
            }
        }
        async void RefreshServiceRegStatus()
        {
            foreach (var task in BackgroundTaskRegistration.AllTasks)
            {
                if (task.Value.Name == myBGTaskName)
                {
                    taskRegistered = true;
                }
            }
            RegBGServiceButton.IsEnabled = !taskRegistered;
            UnRegBGServiceButton.IsEnabled = taskRegistered;
            if (taskRegistered)
            {
                RegBGServiceStatus.Text = "已啟用";
                RegBGServiceStatus.Foreground = new SolidColorBrush(Colors.Green);
            }
            else {
                RegBGServiceStatus.Text = "未啟用";
                RegBGServiceStatus.Foreground = new SolidColorBrush(Colors.Red);
            }
        }
        private void RegDeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RegDeviceList.Items.Count > 0)
            {
                m_selectedDeviceId = RegDeviceList.SelectedItem.ToString();
            }
            else
            {
                m_selectedDeviceId = String.Empty;
            }
            System.Diagnostics.Debug.WriteLine("The device " + m_selectedDeviceId + " is selected.");

            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            localSettings.Values["SelectedDevice"] = m_selectedDeviceId;

        }

        private async void UnRegButton_Click(object sender, RoutedEventArgs e)
        {
            if (m_selectedDeviceId == String.Empty)
            {
                return;
            }

            await SecondaryAuthenticationFactorRegistration.UnregisterDeviceAsync(m_selectedDeviceId);

            RefreshDeviceList();
        }

        private void RegBGServiceButton_Click(object sender, RoutedEventArgs e)
        {
            RegBGService();
        }

        async void RegBGService()
        {
            System.Diagnostics.Debug.WriteLine("Register the background task.");

            BackgroundExecutionManager.RemoveAccess();
            var access = await BackgroundExecutionManager.RequestAccessAsync();

            foreach (var task in BackgroundTaskRegistration.AllTasks)
            {
                Debug.WriteLine("  >>>> task.Value.Name:" + task.Value.Name);
                if (task.Value.Name == myBGTaskName)
                {
                    taskRegistered = true;
                    break;
                }
            }

            if (!taskRegistered)
            {

                if (access == BackgroundAccessStatus.AllowedSubjectToSystemPolicy)
                {
                    BackgroundTaskBuilder taskBuilder = new BackgroundTaskBuilder();
                    taskBuilder.Name = myBGTaskName;
                    SecondaryAuthenticationFactorAuthenticationTrigger myTrigger = new SecondaryAuthenticationFactorAuthenticationTrigger();

                    taskBuilder.TaskEntryPoint = myBGTaskEntryPoint;
                    taskBuilder.SetTrigger(myTrigger);
                    BackgroundTaskRegistration taskReg = taskBuilder.Register();

                    String taskRegName = taskReg.Name;
                    System.Diagnostics.Debug.WriteLine("Background task registration is completed.");
                    taskRegistered = true;
                }
            }
            RefreshServiceRegStatus();
        }

        private void UnRegBGServiceButton_Click(object sender, RoutedEventArgs e)
        {
            UnRegBGService();
        }
        async void UnRegBGService()
        {
            System.Diagnostics.Debug.WriteLine("UnRegister the background task.");

            BackgroundExecutionManager.RemoveAccess();
            var access = await BackgroundExecutionManager.RequestAccessAsync();

            foreach (var task in BackgroundTaskRegistration.AllTasks)
            {
                Debug.WriteLine("  >>>> task.Value.Name:" + task.Value.Name);
                if (task.Value.Name == myBGTaskName)
                {
                    task.Value.Unregister(true);
                    taskRegistered = false;
                    break;
                }
            }
            RefreshServiceRegStatus();
        }
    }
}
