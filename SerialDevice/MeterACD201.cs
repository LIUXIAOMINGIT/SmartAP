using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;

namespace SerialDevice
{
    /// <summary>
    /// 压力表类:目前只定义安森 ACD-201
    /// 默认单位Kpa，数据保留一位小数，默认4个字节整型 * 0.1
    /// </summary>
    public class MeterACD201 : DeviceBase
    {
        private List<byte> m_ReadBuffer = new List<byte>(); //存放数据缓存，如果数据到达数量少于指定长度，等待下次接受

        public MeterACD201()
        {
            this._deviceType = DeviceType.ACD_201;
            _detectByteLength = 9;                                          //ACD-201的数据长度为9个字节
            SetDetectBytes(new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x02, 0xC4, 0x0B});   //设置检测命令
            Init(9600, 8, StopBits.One, Parity.None, "");
        }

        public override void Get()
        {
            this._communicateDevice.SendData(this._detectCommandBytes);
        }

        public override void Set(byte[] buffer)
        {
        }

        /// <summary>
        /// 用于正式接收串口设备的数据
        /// 整型数据格式：01 03 04 00 00 00 15 3B FC ，其中下标3~6四个字节就是想要的数据,也可表达成负数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        public override void ReceiveData(object sender, EventArgs args)
        {
            if(args is DataTransmissionEventArgs)
            {
                DataTransmissionEventArgs data = args as DataTransmissionEventArgs;
                byte[] temp = new byte[_detectByteLength];
                try
                {
                    lock (m_ReadBuffer)
                    {
                        m_ReadBuffer.AddRange(data.EventData);
                    }
                    PressureMeterArgs para = Analyze(m_ReadBuffer);
                    if (para != null)
                        base.ReceiveData(sender, para);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    throw ex;
                }
            }
        }

        /// <summary>
        /// 对压力表数据进行解析
        /// </summary>
        /// <param name="eventData"></param>
        /// <returns></returns>
        private PressureMeterArgs Analyze(List<byte> eventData)
        {
            PressureMeterArgs args = null;
            if (eventData != null && eventData.Count < _detectByteLength)
                return null;
            byte[] buffer = new byte[_detectByteLength];
            bool bFind = false;
            lock (m_ReadBuffer)
            {
                while (eventData.Count >= _detectByteLength)
                {
                    if (eventData[0] != 0x01)
                    {
                        eventData.RemoveAt(0);
                        continue;
                    }
                    else
                    {
                        bFind = true;
                        eventData.CopyTo(0, buffer, 0, _detectByteLength);
                        eventData.RemoveRange(0, _detectByteLength);
                    }
                }
            }

            if (bFind)
            {
                int D4 = buffer[3] << 24;
                int D3 = buffer[4] << 16;
                int D2 = buffer[5] << 8;
                int D1 = buffer[6];
                int total = D1 + D2 + D3 + D4;
                var sum = total * 0.1;
                args = new PressureMeterArgs(PressureUnit.KPa, (float)sum);
                return args;
            }
            else
            {
                return null;
            }
        }

    }
}
