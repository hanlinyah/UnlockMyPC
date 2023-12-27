using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.Usb;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Security.Authentication.Identity.Provider;
using Windows.Security.Cryptography;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Newtonsoft.Json;
using UnLockMyPCLib;

// 空白頁項目範本已記錄在 https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x404

namespace UnlockMyPC
{
    /// <summary>
    /// 可以在本身使用或巡覽至框架內的空白頁面。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
        bool taskRegistered = false;
        static string myBGTaskName = "UnlockMyPC";
        static string myBGTaskEntryPoint = "BackGroundTask.UnlockPCTask";
        ObservableCollection<UnLockType> unLockTypes = new ObservableCollection<UnLockType>();
        public MainPage()
        {
            this.InitializeComponent();
            if (localSettings.Values["UnLockDeviceData"]==null) {
                string json = JsonConvert.SerializeObject(new UnLockDeviceData());
                localSettings.Values["UnLockDeviceData"] = json;
            }
            UnLockTypesSwitch.SelectionChanged += UnLockTypesSwitch_SelectionChanged;
            RefreshDeviceList();
            RefreshServiceRegStatus();
        }

        private void RefreshUnlokTypesList(ObservableCollection<UnLockType> unLockTypes)
        {
            unLockTypes.Clear();
            Object value = localSettings.Values["UnLockDeviceData"];
            UnLockDeviceData unLockDeviceData = JsonConvert.DeserializeObject<UnLockDeviceData>(value.ToString());
            if (!unLockDeviceData.BlueToothDevice.isBinding && !unLockDeviceData.USBDevice.isBinding)
            {
                unLockTypes.Add(new UnLockType(UnLockTypeCode.None));
            }
            if (unLockDeviceData.BlueToothDevice.isBinding) { 
                unLockTypes.Add(new UnLockType(UnLockTypeCode.BlueTooth));
            }
            if (unLockDeviceData.USBDevice.isBinding) { 
                unLockTypes.Add(new UnLockType(UnLockTypeCode.Usb));
            }
            if (unLockDeviceData.BlueToothDevice.isBinding && unLockDeviceData.USBDevice.isBinding) {
                unLockTypes.Add(new UnLockType(UnLockTypeCode.BlueToothOrUsb));
                unLockTypes.Add(new UnLockType(UnLockTypeCode.BlueToothAndUsb));
            }
            RefreshUnLockTypesSwitchStatus();
        }


        async void RefreshDeviceList()
        {
            IReadOnlyList<SecondaryAuthenticationFactorInfo> deviceList = await SecondaryAuthenticationFactorRegistration.FindAllRegisteredDeviceInfoAsync(
                SecondaryAuthenticationFactorDeviceFindScope.AllUsers);

            RegBlueToothDeviceList.Items.Clear();
            RegUSBDeviceList.Items.Clear();

            for (int index = 0; index < deviceList.Count; ++index)
            {
                SecondaryAuthenticationFactorInfo deviceInfo = deviceList.ElementAt(index);

                Object value = localSettings.Values["UnLockDeviceData"];
                UnLockDeviceData unLockDeviceData = JsonConvert.DeserializeObject<UnLockDeviceData>(value.ToString());

                if (unLockDeviceData.BlueToothDevice.isBinding) {
                    if (deviceInfo.DeviceId.Equals(unLockDeviceData.BlueToothDevice.GUID)) {
                        RegBlueToothDeviceList.Items.Add(deviceInfo.DeviceFriendlyName);
                    }
                }
                if (unLockDeviceData.USBDevice.isBinding) {
                    if (deviceInfo.DeviceId.Equals(unLockDeviceData.USBDevice.GUID))
                    {
                        RegUSBDeviceList.Items.Add(deviceInfo.DeviceFriendlyName);
                    }
                }
            }
            RefreshUnlokTypesList(unLockTypes);
        }
       
        private void RefreshUnLockTypesSwitchStatus()
        {
            Object value = localSettings.Values["UnLockDeviceData"];
            UnLockDeviceData unLockDeviceData = JsonConvert.DeserializeObject<UnLockDeviceData>(value.ToString());
            int matchCount = 0;
            foreach (UnLockType unit in unLockTypes)
            {
                if (unit.TypeCode == unLockDeviceData.unLockTypeCode)
                {
                    UnLockTypesSwitch.SelectedItem = unit;
                    matchCount = 1;
                }
            }
            if (matchCount==0) {
                UnLockTypesSwitch.SelectedIndex = 0;
            }

        }
        void RefreshServiceRegStatus()
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

        
        private void UnLockTypesSwitch_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UnLockTypesSwitch.SelectedItem != null) {
                Object value = localSettings.Values["UnLockDeviceData"];
                UnLockDeviceData unLockDeviceData = JsonConvert.DeserializeObject<UnLockDeviceData>(value.ToString());
                UnLockType selectedUnLockType = (UnLockType)UnLockTypesSwitch.SelectedItem;
                unLockDeviceData.unLockTypeCode = selectedUnLockType.TypeCode;
                unLockDeviceData.RefreshUnLockType();
                string json = JsonConvert.SerializeObject(unLockDeviceData);
                localSettings.Values["UnLockDeviceData"] = json;
                RefreshUnLockTypesSwitchStatus();
            }
        }
        private async void RegBlueToothButton_Click(object sender, RoutedEventArgs e)
        {
            Object value = localSettings.Values["UnLockDeviceData"];
            UnLockDeviceData unLockDeviceData = JsonConvert.DeserializeObject<UnLockDeviceData>(value.ToString()); ;
            if (unLockDeviceData.BlueToothDevice.isBinding)
            {
                var messageDialog = new MessageDialog("僅能綁定一個裝置，請先解除已綁定裝置");
                await messageDialog.ShowAsync();
                return;
            }
            DevicePicker picker = new DevicePicker();
            picker.Filter.SupportedDeviceSelectors.Add(
                    BluetoothDevice.GetDeviceSelectorFromPairingState(false)
                );
            picker.Filter.SupportedDeviceSelectors.Add(
                    BluetoothDevice.GetDeviceSelectorFromPairingState(true)
                );
            DeviceInformation device = await picker.PickSingleDeviceAsync(new Rect());
            if (device != null)
            {
                if (!device.Pairing.IsPaired)
                {
                    DevicePairingResult dpr = await device.Pairing.PairAsync();
                    if (dpr.Status != DevicePairingResultStatus.Paired)
                    {
                        return;
                    }
                }
                picker.SetDisplayStatus(device,
                    "Device.Id:" + device.Id 
                    , DevicePickerDisplayStatusOptions.None);
                DeviceRegistration(device, UnLockTypeCode.BlueTooth);
            }
        }
        
        private async void RegUSBButton_Click(object sender, RoutedEventArgs e)
        {
            Object value = localSettings.Values["UnLockDeviceData"];
            UnLockDeviceData unLockDeviceData = JsonConvert.DeserializeObject<UnLockDeviceData>(value.ToString());
            if (unLockDeviceData.USBDevice.isBinding)
            {
                var messageDialog = new MessageDialog("僅能綁定一個裝置，請先解除已綁定裝置");
                await messageDialog.ShowAsync();
                return;
            }
            DevicePicker picker = new DevicePicker();
            picker.Filter.SupportedDeviceSelectors.Add("System.Devices.DeviceInstanceId:~=USB\\VID  AND System.Devices.InterfaceEnabled:=System.StructuredQueryType.Boolean#True");
            DeviceInformation device = await picker.PickSingleDeviceAsync(new Rect());
            
            if (device != null)
            {
                picker.SetDisplayStatus(device,
                    "Device.Id:" + device.Id + "\n" +
                    "Devices.ContainerId:" + device.Properties["System.Devices.ContainerId"] + "\n"
                    , DevicePickerDisplayStatusOptions.None);
                DeviceRegistration(device, UnLockTypeCode.Usb);
            }
        }
        
        public async void DeviceRegistration(DeviceInformation device, UnLockTypeCode unLockType)
        {
            Object value = localSettings.Values["UnLockDeviceData"];
            UnLockDeviceData unLockDeviceData = JsonConvert.DeserializeObject<UnLockDeviceData>(value.ToString());

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
            String deviceId = "";
            String deviceName = device.Name;
            if (unLockType == UnLockTypeCode.BlueTooth)
            {
                deviceId = unLockDeviceData.BlueToothDevice.newGUID();
                unLockDeviceData.BlueToothDevice.DeviceId = device.Id;
                unLockDeviceData.BlueToothDevice.isBinding = true;
            }
            else
            {
                deviceId = unLockDeviceData.USBDevice.newGUID();
                unLockDeviceData.USBDevice.DeviceId = device.Id;
                unLockDeviceData.USBDevice.ContainerId = device.Properties["System.Devices.ContainerId"].ToString();
                unLockDeviceData.USBDevice.isBinding = true;
            }
            try
            {
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
                await registrationResult.Registration.FinishRegisteringDeviceAsync(deviceConfigData);
                string json = JsonConvert.SerializeObject(unLockDeviceData);
                localSettings.Values["UnLockDeviceData"] = json;
                RefreshDeviceList();
            }
            catch (Exception e) {
                Debug.WriteLine("Exception:"+e.StackTrace);
            }
        }
        private void UnRegBlueToothButton_Click(object sender, RoutedEventArgs e)
        {
            if (RegBlueToothDeviceList.Items.Count > 0)
            {
                DeviceUnRegistration(UnLockTypeCode.BlueTooth);
            }
        }
        private void UnRegUSBButton_Click(object sender, RoutedEventArgs e)
        {
            if (RegUSBDeviceList.Items.Count > 0)
            {
                DeviceUnRegistration(UnLockTypeCode.Usb);
            }
        }

        private async void DeviceUnRegistration(UnLockTypeCode unLockType)
        {
            Object value = localSettings.Values["UnLockDeviceData"];
            UnLockDeviceData unLockDeviceData = JsonConvert.DeserializeObject<UnLockDeviceData>(value.ToString());
            DeviceInformation device;
            String deviceId = "";
            if (unLockType==UnLockTypeCode.BlueTooth) {
                deviceId = unLockDeviceData.BlueToothDevice.GUID;
                device = await DeviceInformation.CreateFromIdAsync(unLockDeviceData.BlueToothDevice.DeviceId);
                unLockDeviceData.BlueToothDevice.isBinding = false;
                if (device.Pairing.IsPaired)
                {
                    await device.Pairing.UnpairAsync();
                }
            }
            else {
                deviceId = unLockDeviceData.USBDevice.GUID;
                device = await DeviceInformation.CreateFromIdAsync(unLockDeviceData.USBDevice.DeviceId);
                unLockDeviceData.USBDevice.isBinding = false;
            }
            unLockDeviceData.RefreshUnLockType();
            await SecondaryAuthenticationFactorRegistration.UnregisterDeviceAsync(deviceId);
            string json = JsonConvert.SerializeObject(unLockDeviceData);
            localSettings.Values["UnLockDeviceData"] = json;
            RefreshDeviceList();
            if (!unLockDeviceData.BlueToothDevice.isBinding && !unLockDeviceData.USBDevice.isBinding) {
                UnRegBGService();
            }
        }

        
        private async void RePairingButton_Click(object sender, RoutedEventArgs e)
        {
            if (RegBlueToothDeviceList.Items.Count > 0)
            {
                Object value = localSettings.Values["UnLockDeviceData"];
                UnLockDeviceData unLockDeviceData = JsonConvert.DeserializeObject<UnLockDeviceData>(value.ToString());
                DeviceInformation device = await DeviceInformation.CreateFromIdAsync(unLockDeviceData.BlueToothDevice.DeviceId);
                if (device.Pairing.IsPaired)
                {
                    await device.Pairing.UnpairAsync();
                    await device.Pairing.PairAsync();
                }
                else {
                    await device.Pairing.PairAsync();
                }
            }
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
