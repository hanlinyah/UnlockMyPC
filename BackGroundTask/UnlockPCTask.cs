using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Windows.ApplicationModel.Background;
using Windows.Security.Authentication.Identity.Provider;
using Windows.Security.Cryptography.Core;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using Windows.Devices.Bluetooth;
using Windows.Storage;
using UnLockMyPCLib;
using Newtonsoft.Json;
using Windows.Devices.Enumeration;
using System.Diagnostics;


namespace BackGroundTask
{
    public sealed class UnlockPCTask : IBackgroundTask
    {
        ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
        ManualResetEvent opCompletedEvent = null;
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            var deferral = taskInstance.GetDeferral();
            opCompletedEvent = new ManualResetEvent(false);
            SecondaryAuthenticationFactorAuthentication.AuthenticationStageChanged += OnStageChanged;
            opCompletedEvent.WaitOne();

            deferral.Complete();
        }
        async void PerformAuthentication()
        {
            Object value = localSettings.Values["UnLockDeviceData"];
            UnLockDeviceData unLockDeviceData = JsonConvert.DeserializeObject<UnLockDeviceData>(value.ToString());

            UnLockType unLockType =new UnLockType(unLockDeviceData.unLockTypeCode);
            String notifyString= unLockType.TypeDescribe;

            BluetoothDevice bluetoothdevice=null;
            DeviceInformation usbdevice=null;
            String bluetoothdeviceGUID = "";
            String usbdeviceGUID = "";


            switch (unLockType.TypeCode)
            {
                case UnLockTypeCode.BlueTooth:
                    bluetoothdeviceGUID = unLockDeviceData.BlueToothDevice.GUID;
                    bluetoothdevice = await BluetoothDevice.FromIdAsync(unLockDeviceData.BlueToothDevice.DeviceId);
                    break;
                case UnLockTypeCode.Usb:
                    usbdeviceGUID = unLockDeviceData.USBDevice.GUID;
                    usbdevice = await DeviceInformation.CreateFromIdAsync(unLockDeviceData.USBDevice.DeviceId);
                    break;
                case UnLockTypeCode.BlueToothOrUsb:
                case UnLockTypeCode.BlueToothAndUsb:
                    bluetoothdeviceGUID = unLockDeviceData.BlueToothDevice.GUID;
                    usbdeviceGUID = unLockDeviceData.USBDevice.GUID;
                    bluetoothdevice = await BluetoothDevice.FromIdAsync(unLockDeviceData.BlueToothDevice.DeviceId);
                    usbdevice = await DeviceInformation.CreateFromIdAsync(unLockDeviceData.USBDevice.DeviceId);
                    break;
                default:
                    return;
            }
            await SecondaryAuthenticationFactorAuthentication.ShowNotificationMessageAsync(
                    notifyString,
                    SecondaryAuthenticationFactorAuthenticationMessage.LookingForDevice);

            SecondaryAuthenticationFactorAuthenticationStageInfo authStageInfo = await SecondaryAuthenticationFactorAuthentication.GetAuthenticationStageInfoAsync();

            if (authStageInfo.Stage != SecondaryAuthenticationFactorAuthenticationStage.CollectingCredential)
            {
                return;
            }


            IReadOnlyList<SecondaryAuthenticationFactorInfo> deviceList = await SecondaryAuthenticationFactorRegistration.FindAllRegisteredDeviceInfoAsync(
                    SecondaryAuthenticationFactorDeviceFindScope.AllUsers);

            if (deviceList.Count == 0)
            {
                return;
            }
            bool isbluetoothdeviceReg = false;
            bool isbluetoothdeviceConnect = false;
            bool isusbdeviceReg = false;
            bool isusbdeviceConnect = false;
            bool isbluetoothReadyUnlock = false;
            bool isusbReadyUnlock = false;
            bool isCanUnlock = false;
            String unlockdeviceGUID = "";

            for (int index = 0; index < deviceList.Count; ++index)
            {
                
                if (deviceList.ElementAt(index).DeviceId.Equals(usbdeviceGUID))
                {
                    isusbdeviceReg = usbdevice.Properties["System.Devices.ContainerId"].ToString().Equals(unLockDeviceData.USBDevice.ContainerId);
                    isusbdeviceConnect = (bool)usbdevice.Properties["System.Devices.InterfaceEnabled"];
                }
                if (deviceList.ElementAt(index).DeviceId.Equals(bluetoothdeviceGUID))
                {
                    isbluetoothdeviceReg = true;
                    isbluetoothdeviceConnect = bluetoothdevice.ConnectionStatus == BluetoothConnectionStatus.Connected;
                }
            }
            isbluetoothReadyUnlock = (isbluetoothdeviceReg && isbluetoothdeviceConnect);
            isusbReadyUnlock = (isusbdeviceReg && isusbdeviceConnect);


            switch (unLockType.TypeCode)
            {
                case UnLockTypeCode.BlueTooth:
                    isCanUnlock = isbluetoothReadyUnlock;
                    unlockdeviceGUID = bluetoothdeviceGUID;
                    break;
                case UnLockTypeCode.Usb:
                    isCanUnlock = isusbReadyUnlock;
                    unlockdeviceGUID = usbdeviceGUID;
                    break;
                case UnLockTypeCode.BlueToothOrUsb:
                    isCanUnlock = (isbluetoothReadyUnlock || isusbReadyUnlock);
                    if (isbluetoothReadyUnlock) {
                        unlockdeviceGUID = bluetoothdeviceGUID;
                    }
                    else {
                        unlockdeviceGUID = usbdeviceGUID;
                    }
                    break;
                case UnLockTypeCode.BlueToothAndUsb:
                    isCanUnlock = (isbluetoothReadyUnlock && isusbReadyUnlock);
                    unlockdeviceGUID = bluetoothdeviceGUID;
                    break;
                default:
                    return;
            }

            if (!isCanUnlock)
            {
                await SecondaryAuthenticationFactorAuthentication.ShowNotificationMessageAsync(
                   notifyString,
                    SecondaryAuthenticationFactorAuthenticationMessage.DeviceUnavailable);
                return;
            }

            IBuffer svcNonce = CryptographicBuffer.GenerateRandom(32);  //Generate a nonce and do a HMAC operation with the nonce

            SecondaryAuthenticationFactorAuthenticationResult authResult = await SecondaryAuthenticationFactorAuthentication.StartAuthenticationAsync(
                    unlockdeviceGUID, svcNonce);

            if (authResult.Status != SecondaryAuthenticationFactorAuthenticationStatus.Started)
            {
                return;
            }

            //
            // WARNING: Test code
            // The HAMC calculation SHOULD be done on companion device
            //
            byte[] combinedDataArray;
            CryptographicBuffer.CopyToByteArray(authResult.Authentication.DeviceConfigurationData, out combinedDataArray);

            byte[] deviceKeyArray = new byte[32];
            byte[] authKeyArray = new byte[32];
            for (int index = 0; index < deviceKeyArray.Length; index++)
            {
                deviceKeyArray[index] = combinedDataArray[index];
            }
            for (int index = 0; index < authKeyArray.Length; index++)
            {
                authKeyArray[index] = combinedDataArray[deviceKeyArray.Length + index];
            }
            // Create device key and authentication key
            IBuffer deviceKey = CryptographicBuffer.CreateFromByteArray(deviceKeyArray);
            IBuffer authKey = CryptographicBuffer.CreateFromByteArray(authKeyArray);

            // Calculate the HMAC
            MacAlgorithmProvider hMACSha256Provider = MacAlgorithmProvider.OpenAlgorithm(MacAlgorithmNames.HmacSha256);

            CryptographicKey deviceHmacKey = hMACSha256Provider.CreateKey(deviceKey);
            IBuffer deviceHmac = CryptographicEngine.Sign(deviceHmacKey, authResult.Authentication.DeviceNonce);

            // sessionHmac = HMAC(authKey, deviceHmac || sessionNonce)
            IBuffer sessionHmac;
            byte[] deviceHmacArray = { 0 };
            CryptographicBuffer.CopyToByteArray(deviceHmac, out deviceHmacArray);

            byte[] sessionNonceArray = { 0 };
            CryptographicBuffer.CopyToByteArray(authResult.Authentication.SessionNonce, out sessionNonceArray);

            combinedDataArray = new byte[deviceHmacArray.Length + sessionNonceArray.Length];
            for (int index = 0; index < deviceHmacArray.Length; index++)
            {
                combinedDataArray[index] = deviceHmacArray[index];
            }
            for (int index = 0; index < sessionNonceArray.Length; index++)
            {
                combinedDataArray[deviceHmacArray.Length + index] = sessionNonceArray[index];
            }

            // Get a Ibuffer from combinedDataArray
            IBuffer sessionMessage = CryptographicBuffer.CreateFromByteArray(combinedDataArray);

            // Calculate sessionHmac
            CryptographicKey authHmacKey = hMACSha256Provider.CreateKey(authKey);
            sessionHmac = CryptographicEngine.Sign(authHmacKey, sessionMessage);

            SecondaryAuthenticationFactorFinishAuthenticationStatus authStatus = await authResult.Authentication.FinishAuthenticationAsync(deviceHmac,
                sessionHmac);

            if (authStatus != SecondaryAuthenticationFactorFinishAuthenticationStatus.Completed)
            {
                return;
            }
        }
        async void OnStageChanged(Object sender, SecondaryAuthenticationFactorAuthenticationStageChangedEventArgs args)
        {
            Object value = localSettings.Values["UnLockDeviceData"];
            UnLockDeviceData unLockDeviceData = JsonConvert.DeserializeObject<UnLockDeviceData>(value.ToString());

            UnLockType unLockType = new UnLockType(unLockDeviceData.unLockTypeCode);
            String notifyString = unLockType.TypeDescribe;
            if (args.StageInfo.Stage == SecondaryAuthenticationFactorAuthenticationStage.WaitingForUserConfirmation)
            {
                await SecondaryAuthenticationFactorAuthentication.ShowNotificationMessageAsync(
                    notifyString,
                    SecondaryAuthenticationFactorAuthenticationMessage.SwipeUpWelcome);
            }
            else if (args.StageInfo.Stage == SecondaryAuthenticationFactorAuthenticationStage.CollectingCredential)
            {
                try {
                    PerformAuthentication();
                }catch(Exception e) {
                    Debug.WriteLine("Exception:"+e.StackTrace);
                }
            }
            else
            {
                if (args.StageInfo.Stage == SecondaryAuthenticationFactorAuthenticationStage.StoppingAuthentication)
                {
                    SecondaryAuthenticationFactorAuthentication.AuthenticationStageChanged -= OnStageChanged;
                    opCompletedEvent.Set();
                }

                SecondaryAuthenticationFactorAuthenticationStage stage = args.StageInfo.Stage;
            }
        }
    }
}
