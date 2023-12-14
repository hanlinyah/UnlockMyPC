using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Data.Xml.Dom;
using Windows.Security.Authentication.Identity.Provider;
using Windows.Security.Cryptography.Core;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using Windows.UI.Notifications;
using Windows.Devices.Bluetooth;

namespace BackGroundTask
{
    public sealed class UnlockPCTask : IBackgroundTask
    {
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
            await SecondaryAuthenticationFactorAuthentication.ShowNotificationMessageAsync(
                    "藍芽裝置",
                    SecondaryAuthenticationFactorAuthenticationMessage.LookingForDevice);

            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            String m_selectedDeviceId = localSettings.Values["SelectedDevice"] as String;

            SecondaryAuthenticationFactorAuthenticationStageInfo authStageInfo = await SecondaryAuthenticationFactorAuthentication.GetAuthenticationStageInfoAsync();

            if (authStageInfo.Stage != SecondaryAuthenticationFactorAuthenticationStage.CollectingCredential)
            {
                throw new Exception("Unexpected!");
            }

            IReadOnlyList<SecondaryAuthenticationFactorInfo> deviceList = await SecondaryAuthenticationFactorRegistration.FindAllRegisteredDeviceInfoAsync(
                    SecondaryAuthenticationFactorDeviceFindScope.AllUsers);

            if (deviceList.Count == 0)
            {
                throw new Exception("Unexpected exception, device list = 0");
            }

            int connectDeviceIndex = -1;

            for (int index = 0; index < deviceList.Count; ++index)
            {
                m_selectedDeviceId = deviceList.ElementAt(index).DeviceId;
                var bluedevice = await BluetoothDevice.FromIdAsync("Bluetooth#Bluetooth" + m_selectedDeviceId.ToString());
                if (bluedevice.ConnectionStatus == BluetoothConnectionStatus.Connected) {
                    connectDeviceIndex = index;
                    break;
                }
            }
            if (connectDeviceIndex == -1)
            {
                await SecondaryAuthenticationFactorAuthentication.ShowNotificationMessageAsync(
                    "藍芽裝置",
                    SecondaryAuthenticationFactorAuthenticationMessage.DeviceUnavailable);
                return;
            }

            SecondaryAuthenticationFactorInfo deviceInfo = deviceList.ElementAt(0);
            m_selectedDeviceId = deviceInfo.DeviceId;

            IBuffer svcNonce = CryptographicBuffer.GenerateRandom(32);  //Generate a nonce and do a HMAC operation with the nonce

            SecondaryAuthenticationFactorAuthenticationResult authResult = await SecondaryAuthenticationFactorAuthentication.StartAuthenticationAsync(
                    m_selectedDeviceId, svcNonce);

            if (authResult.Status != SecondaryAuthenticationFactorAuthenticationStatus.Started)
            {
                throw new Exception("Unexpected! Could not start authentication!");
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
                throw new Exception("Unable to complete authentication!");
            }
        }
        async void OnStageChanged(Object sender, SecondaryAuthenticationFactorAuthenticationStageChangedEventArgs args)
        {
            if (args.StageInfo.Stage == SecondaryAuthenticationFactorAuthenticationStage.WaitingForUserConfirmation)
            {
                String deviceName = "藍芽裝置";
                await SecondaryAuthenticationFactorAuthentication.ShowNotificationMessageAsync(
                    deviceName,
                    SecondaryAuthenticationFactorAuthenticationMessage.SwipeUpWelcome);
            }
            else if (args.StageInfo.Stage == SecondaryAuthenticationFactorAuthenticationStage.CollectingCredential)
            {
                PerformAuthentication();
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
