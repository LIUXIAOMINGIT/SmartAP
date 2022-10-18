using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PTool
{
    /// <summary>
    /// 读取传感器电压命令
    /// </summary>
    public class CmdGetVoltage : BaseCommand
    {
        protected byte[] mBytesVoltage = new byte[3] { 0, 0, 0 };     //低中高对应的索引为0,1,2
        protected ScaleValue m_Scale = ScaleValue.None;
        protected float mfVoltage = 0f;

        /// <summary>
        /// 
        /// </summary>
        public CmdGetVoltage() : base(0x01)
        { }

        public float GetVoltage()
        {
            return mfVoltage;
        }

        public void SetVoltage(decimal vol)
        {
            mfVoltage = (float)vol;
            decimal intPart = decimal.Truncate(vol);
            int intVoltage = decimal.ToInt32(intPart); //整数部分
            decimal decimalRate = vol - intVoltage;    //小数部分
            int decimalRateLength = decimalRate.ToString().Length - 2;

            switch (decimalRateLength)
            {
                case 0:
                case 1:
                    intVoltage *= 10;
                    intVoltage += (int)(decimalRate * 10);
                    m_Scale = ScaleValue.Ten;
                    break;
                case 2:
                    intVoltage *= 100;
                    intVoltage += (int)(decimalRate * 100);
                    m_Scale = ScaleValue.Hundred;
                    break;
                case 3:
                    intVoltage *= 1000;
                    intVoltage += (int)(decimalRate * 1000);
                    m_Scale = ScaleValue.Thousand;
                    break;
                case 4:
                    intVoltage *= 10000;
                    intVoltage += (int)(decimalRate * 10000);
                    m_Scale = ScaleValue.TenThousand;
                    break;
                default:
                    intVoltage *= 10;
                    intVoltage += (int)(decimalRate * 10);
                    m_Scale = ScaleValue.Ten;
                    break;
            }
            mBytesVoltage[0] = (byte)(intVoltage & 0x000000FF);
            mBytesVoltage[1] = (byte)(intVoltage >> 8 & 0x000000FF);
            mBytesVoltage[2] = (byte)(intVoltage >> 16 & 0x000000FF);
        }

        /// <summary>
        /// 将要发送的命令变成字节数组
        /// </summary>
        /// <returns></returns>
        public override List<byte> GetBytes()
        {
            //先计算Payload长度
            UpdatePayloadLength(0);
            //命令头部（payload length（含）之前）
            List<byte> basebuffer = base.GetBytes();
            //取checksum字节
            byte checksum = CRC32.CalcCRC8Partial(basebuffer);
            basebuffer.Add(checksum);
            return basebuffer;
        }

        public override void SetBytes(byte[] payloadData)
        {
            if (payloadData.Length == 0)
            {
                Logger.Instance().Error("报警信息数据包有误,数据包长度为0！");
                return;
            }
            if (payloadData.Length != 4)
            {
                Logger.Instance().Error("报警信息数据包有误,数据包长度不为4！");
                return;
            }
            switch (payloadData[0])
            {
                case 0: m_Scale = ScaleValue.None; break;
                case 1: m_Scale = ScaleValue.Ten; break;
                case 2: m_Scale = ScaleValue.Hundred; break;
                case 3: m_Scale = ScaleValue.Thousand; break;
                case 4: m_Scale = ScaleValue.TenThousand; break;
                default: m_Scale = ScaleValue.None; break;
            }
            mBytesVoltage[0] = payloadData[0];
            mBytesVoltage[1] = payloadData[1];
            mBytesVoltage[2] = payloadData[2];
            mfVoltage = (float)(mBytesVoltage[2] << 16 + mBytesVoltage[1] << 8 + mBytesVoltage[0])/(float)m_Scale;
        }

        /// <summary>
        /// 复制命令
        /// </summary>
        /// <param name="other"></param>
        public override void Copy(BaseCommand other)
        {
            base.Copy(other);
        }

        public override void InvokeResponse()
        {
            base.InvokeResponse();
        }
        /// <summary>
        /// 当超时后调用回调函数
        /// </summary>
        public override void InvokeTimeOut()
        {
            base.InvokeTimeOut();
        }
    }

}