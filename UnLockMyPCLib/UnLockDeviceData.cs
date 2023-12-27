using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnLockMyPCLib
{
    public class UnLockDeviceData
    {
        public UnLockDevice BlueToothDevice;
        public UnLockDevice USBDevice;
        public UnLockTypeCode unLockTypeCode;

        public UnLockDeviceData()
        {
            this.BlueToothDevice = new UnLockDevice(UnLockTypeCode.BlueTooth);
            this.USBDevice = new UnLockDevice(UnLockTypeCode.Usb);
            this.unLockTypeCode = UnLockTypeCode.None;
        }
        public void RefreshUnLockType()
        {
            if (this.BlueToothDevice.isBinding && !this.USBDevice.isBinding)
            {
                this.unLockTypeCode = UnLockTypeCode.BlueTooth;
            }
            if (!this.BlueToothDevice.isBinding && this.USBDevice.isBinding)
            {
                this.unLockTypeCode = UnLockTypeCode.Usb;
            }
            if (!this.BlueToothDevice.isBinding && !this.USBDevice.isBinding)
            {
                this.unLockTypeCode = UnLockTypeCode.None;
            }
            if (this.BlueToothDevice.isBinding && this.USBDevice.isBinding)
            {
                if (this.unLockTypeCode == null || this.unLockTypeCode == UnLockTypeCode.None) {
                    this.unLockTypeCode= UnLockTypeCode.BlueTooth;
                }
            }
        }
    }
    public class UnLockDevice
    {
        public bool isBinding = false;
        public UnLockTypeCode deviceType;
        public String GUID;
        public String DeviceId;
        public String ContainerId;
        public UnLockDevice(UnLockTypeCode deviceType)
        {
            this.deviceType = deviceType;
        }
        public String newGUID()
        {
            this.GUID = System.Guid.NewGuid().ToString();
            return this.GUID;
        }
    }
    public class UnLockType
    {

        public static Dictionary<UnLockTypeCode, String> UnLockTypeCodeDescribeMap;
        static UnLockType()
        {
            UnLockTypeCodeDescribeMap = new Dictionary<UnLockTypeCode, String>();
            UnLockTypeCodeDescribeMap.Add(UnLockTypeCode.None, "無");
            UnLockTypeCodeDescribeMap.Add(UnLockTypeCode.BlueTooth, "僅綁定之藍芽裝置(單驗證)");
            UnLockTypeCodeDescribeMap.Add(UnLockTypeCode.Usb, "僅綁定之USB裝置(單驗證)");
            UnLockTypeCodeDescribeMap.Add(UnLockTypeCode.BlueToothOrUsb, "綁定之藍芽或USB裝置(單驗證)");
            UnLockTypeCodeDescribeMap.Add(UnLockTypeCode.BlueToothAndUsb, "綁定之藍芽及USB裝置(雙驗證)");
        }
        public UnLockTypeCode TypeCode;
        public String TypeDescribe;
        public UnLockType(UnLockTypeCode typecode)
        {
            this.TypeCode = typecode;
            this.TypeDescribe = UnLockTypeCodeDescribeMap[typecode];
        }

        public override string ToString()
        {
            return this.TypeDescribe;
        }
    }
    public enum UnLockTypeCode
    {
        None = 0,
        BlueTooth = 1,
        Usb = 2,
        BlueToothOrUsb = 3,
        BlueToothAndUsb = 4
    }
}
