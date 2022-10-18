using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using SerialDevice;

namespace PTool
{
    public class SmartAP : DeviceBase
    {
        private List<byte> m_ReadBuffer = new List<byte>(); //存放数据缓存，如果数据到达数量少于指定长度，等待下次接受

        public SmartAP()
        {
            this._deviceType = DeviceType.AP;
            Init(115200, 8, StopBits.One, Parity.None, "");
        }

        private string BufferToString(List<byte> buffer)
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte b in buffer)
            {
                sb.Append(b.ToString("X2"));
                sb.Append(b.ToString(" "));
            }
            sb.Append("\r\n");
            return sb.ToString();
        }

        private string BufferToString(byte[] buffer)
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte b in buffer)
            {
                sb.Append(b.ToString("X2"));
                sb.Append(b.ToString(" "));
            }
            return sb.ToString();
        }

        /// <summary>
        /// 接收到的字符转成
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        private int Char2Hex(byte[] inBuffer, List<byte> outCharBuffer)
        {
            outCharBuffer.Clear();
            List<byte> charBuffer = new List<byte>(inBuffer);
            int headIndex = charBuffer.IndexOf(0x02);
            int tailIndex = charBuffer.IndexOf(0x03);
          
            if (headIndex < 0)
            {
                //如果连包头都不存在，那么整个包都删除，
                string str = BufferToString(charBuffer);
                Logger.Instance().ErrorFormat("Char2Hex()->找不到包头字节0x02, charBuffer长度为{0},缓冲区数据={1}, 清空!", charBuffer.Count, str);
                return -1;
            }
            else
            {
                if (tailIndex < 0)
                {
                    //如果包尾不存在，继续
                    Logger.Instance().ErrorFormat("Char2Hex()->找不到包尾字节0x03,此包不完整，等待下一次数据到来，charBuffer长度为{0}", charBuffer.Count);
                    return 1;
                }
            }
            //0x02和0x03之间的数据必须是偶数个的
            if ((tailIndex - headIndex - 1) % 2 != 0)
            {
                Logger.Instance().ErrorFormat("Char2Hex()->包头与包尾之间字节数不为偶数或数量小于10，从缓冲区中移动这段数据，并保留余下数据,headIndex={0},tailIndex={1}", headIndex, tailIndex);
                return -4;
            }
            byte[] temp = new byte[tailIndex - headIndex - 1];
            charBuffer.CopyTo(headIndex + 1, temp, 0, temp.Length);
            charBuffer.RemoveRange(headIndex, tailIndex - headIndex + 1);
            int length = temp.Length;
            int iLoop = 0;
            byte byteHigh = 0x00;
            byte byteLow = 0x00;
            while (iLoop + 1 < length)
            {
                if (temp[iLoop] >= 0x30 && temp[iLoop] <= 0x39)
                    byteHigh = (byte)((temp[iLoop] - 0x30) << 4);
                else if (temp[iLoop] >= 0x41 && temp[iLoop] <= 0x46)
                    byteHigh = (byte)((temp[iLoop] - 0x37) << 4);
                else if (temp[iLoop] >= 0x61 && temp[iLoop] <= 0x66)
                    byteHigh = (byte)((temp[iLoop] - 0x57) << 4);
                else
                {
                    Logger.Instance().ErrorFormat("Char2Hex 错误，出现0~9，A~F以外的字符 temp[iLoop]={0}， iLoop={1}", temp[iLoop], iLoop);

                    return -2;
                }

                if (temp[iLoop + 1] >= 0x30 && temp[iLoop + 1] <= 0x39)
                    byteLow = (byte)(temp[iLoop + 1] - 0x30);
                else if (temp[iLoop + 1] >= 0x41 && temp[iLoop + 1] <= 0x46)
                    byteLow = (byte)(temp[iLoop + 1] - 0x37);
                else if (temp[iLoop + 1] >= 0x61 && temp[iLoop + 1] <= 0x66)
                    byteLow = (byte)(temp[iLoop + 1] - 0x57);
                else
                {
                    Logger.Instance().ErrorFormat("Char2Hex 错误，出现0~9，A~F以外的字符 temp[iLoop]={0}， iLoop={1}", temp[iLoop], iLoop);

                    return -3;
                }
                byteHigh &= 0xF0;
                byteLow &= 0x0F;
                outCharBuffer.Add((byte)(byteHigh + byteLow));
                iLoop = iLoop + 2;
            }
            return 0;
        }

        private byte[] Hex2Char(byte[] buffer)
        {
            List<byte> charBuffer = new List<byte>();
            charBuffer.Add(0x02);
            byte byteHigh = 0x00;
            byte byteLow = 0x00;
            for (int iLoop = 0; iLoop < buffer.Length; iLoop++)
            {
                byteHigh = (byte)(buffer[iLoop] >> 4 & 0x0F);
                byteLow = (byte)(buffer[iLoop] & 0x0F);

                if (byteHigh >= 0x00 && byteHigh <= 0x09)
                    charBuffer.Add((byte)(byteHigh + 0x30));
                else if (byteHigh >= 0x0A && byteHigh <= 0x0F)
                    charBuffer.Add((byte)(byteHigh + 0x37));
                else
                    break;

                if (byteLow >= 0x00 && byteLow <= 0x09)
                    charBuffer.Add((byte)(byteLow + 0x30));
                else if (byteLow >= 0x0A && byteLow <= 0x0F)
                    charBuffer.Add((byte)(byteLow + 0x37));
                else
                    break;
            }
            charBuffer.Add(0x03);
            Logger.Instance().InfoFormat("Hex2Char() Raw bytes={0}", BufferToString(buffer));
            return charBuffer.ToArray();
        }

        public void SendCmdGetVoltage()
        {
            CmdGetVoltage cmd = new CmdGetVoltage();
            Send(cmd.GetBytes().ToArray());
        }

        public void SendCmdStartAP()
        {
            CmdStartStopAP cmd = new CmdStartStopAP();
            Send(cmd.GetStartCommandBytes().ToArray());
        }

        public void SendCmdStopAP()
        {
            CmdStartStopAP cmd = new CmdStartStopAP();
            Send(cmd.GetStopCommandBytes().ToArray());
        }

        public void SendCmdGetStatus()
        {
            CmdGetStatus cmd = new CmdGetStatus();
            Send(cmd.GetBytes().ToArray());
        }

        public void SendCmdWriteParameter(decimal a, decimal b, decimal c)
        {
            CmdWriteParameter cmd = new CmdWriteParameter();
            cmd.SetParameter(a,b,c);
            Send(cmd.GetBytes().ToArray());
        }

        public void SendCmdReadParameter()
        {
            CmdReadParameter cmd = new CmdReadParameter();
            Send(cmd.GetBytes().ToArray());
        }

        /// <summary>
        /// 测试串口号（读电压值）
        /// </summary>
        public override void Get()
        {
            SendCmdGetVoltage();
        }

        /// <summary>
        /// 发送拆分成字符，头尾各加02, 03
        /// </summary>
        /// <param name="buffer">原始字节</param>
        public void Send(byte[] buffer)
        {
            this._communicateDevice.SendData(Hex2Char(buffer));
        }

        private BaseCommand CreateCommand(List<byte> CommandBuffer)
        {
            if(CommandBuffer.Count < 3)
                return null;
            byte[] cmdBytes = null;
            cmdBytes = new byte[CommandBuffer.Count];
            CommandBuffer.CopyTo(cmdBytes);
            byte messageId = cmdBytes[0];
            BaseCommand cmd = null;
            cmd = CommandFactory.CreateCommand(messageId);
            if (cmd == null)
            {
                string msg = string.Format("CreateCommand() Error! MessageID={0}", messageId);
                Logger.Instance().Error(msg);
                return null;
            }
            cmd.PayloadLength = cmdBytes[1];
            //分离出最后1字节的Checksum
            cmd.Checksum = cmdBytes[cmdBytes.Length - 1];
            //重新计算泵端传入的数据checksum，如果对应不上，记录日志，由上层应用酌情处理，但不能作超时处理
            byte checksum = CRC32.CalcCRC8Partial(cmdBytes, cmdBytes.Length-1);
            if( (cmd.Checksum^checksum) != 0 )
            {
                string buffer2String = BufferToString(cmdBytes);
                Logger.Instance().ErrorFormat("ProtocolEngine::CreateCommand() Checksum error, buffer={0}", buffer2String);
            }

            //将PayloadData提取出来，方便生成字段
            byte[] arrFieldsBuf = new byte[cmd.PayloadLength];
            int index = cmdBytes.Length - arrFieldsBuf.Length - 1; //这里的1指是Checksum四个字节
            if (index <= 0)
            {
                string msg = string.Format("CreateCommand() Error! index<=0 cmdBytes.Length={0} arrFieldsBuf.Length={1}", cmdBytes.Length, arrFieldsBuf.Length);
                Logger.Instance().Error(msg);
                return null;
            }
            Array.Copy(cmdBytes, index, arrFieldsBuf, 0, arrFieldsBuf.Length);
            //将字段填进命令
            cmd.SetBytes(arrFieldsBuf);
            return cmd;
        }

        public override void ReceiveData(object sender, EventArgs args)
        {
            if (args is DataTransmissionEventArgs)
            {
                DataTransmissionEventArgs data = args as DataTransmissionEventArgs;
                List<byte> commandBuffer = new List<byte>();
                try
                {
                    lock (m_ReadBuffer)
                    {
                        m_ReadBuffer.AddRange(data.EventData);
                    }
                    int ret = Char2Hex(m_ReadBuffer.ToArray(), commandBuffer);
                    if (ret < 0)
                    {
                        //数据不正确，或不完整，等待下一波数据
                        return;
                    }
                    BaseCommand cmd = CreateCommand(commandBuffer);
                    if (cmd != null)
                        base.ReceiveData(sender, cmd);
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }
    }
}
