using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PTool
{
    /// <summary>
    /// 启停控制
    /// </summary>
    public class CmdReadParameter : BaseCommand
    {
        public static ScaleValue m_Scale = ScaleValue.Hundred;
        protected int m_IntScale = 1;
        protected decimal a = 0;
        protected decimal b = 0;
        protected decimal c = 0;

        /// <summary>
        /// 
        /// </summary>
        public CmdReadParameter() : base(0x05)
        { }

        public void SetParameter(decimal a, decimal b, decimal c)
        {
            this.a = a; this.b = b; this.c = c;
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
            if (payloadData.Length != 12)
            {
                Logger.Instance().Error("报警信息数据包有误,数据包长度不为12！");
                return;
            }
            switch (payloadData[0])
            {
                case 0: m_Scale = ScaleValue.None;
                    m_IntScale = 1;
                    break;
                case 1: m_Scale = ScaleValue.Ten;
                    m_IntScale = 10;
                    break;
                case 2: m_Scale = ScaleValue.Hundred; 
                    m_IntScale = 100;
                    break;
                case 3: m_Scale = ScaleValue.Thousand;
                    m_IntScale = 1000;
                    break;
                case 4: m_Scale = ScaleValue.TenThousand;
                    m_IntScale = 10000;
                    break;
                default: m_Scale = ScaleValue.None;
                    m_IntScale = 1;
                    break;
            }

            decimal A = payloadData[1] + payloadData[2] << 8 + payloadData[3] << 16;
            a = A / m_IntScale;

            switch (payloadData[4])
            {
                case 0:
                    m_IntScale = 1;
                    break;
                case 1:
                    m_IntScale = 10;
                    break;
                case 2:
                    m_IntScale = 100;
                    break;
                case 3:
                    m_IntScale = 1000;
                    break;
                case 4:
                    m_IntScale = 10000;
                    break;
                default:
                    m_Scale = ScaleValue.None;
                    m_IntScale = 1;
                    break;
            }

            decimal B = payloadData[5] + payloadData[6] << 8 + payloadData[7] << 16;
            b = B / m_IntScale;

            switch (payloadData[8])
            {
                case 0:
                    m_IntScale = 1;
                    break;
                case 1:
                    m_IntScale = 10;
                    break;
                case 2:
                    m_IntScale = 100;
                    break;
                case 3:
                    m_IntScale = 1000;
                    break;
                case 4:
                    m_IntScale = 10000;
                    break;
                default:
                    m_Scale = ScaleValue.None;
                    m_IntScale = 1;
                    break;
            }
            decimal C = payloadData[9] + payloadData[10] << 8 + payloadData[11] << 16;
            c = C / m_IntScale;
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