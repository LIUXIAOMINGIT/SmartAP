using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PTool
{
    /// <summary>
    /// 启停控制
    /// </summary>
    public class CmdGetStatus : BaseCommand
    {
        protected byte mStatus = 0x00;

        /// <summary>
        /// 
        /// </summary>
        public CmdGetStatus() : base(0x03)
        { }

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
            if (payloadData.Length != 1)
            {
                Logger.Instance().Error("报警信息数据包有误,数据包长度不为1！");
                return;
            }

            mStatus = payloadData[0];
        }

        /// <summary>
        /// 1: 运行中， 2：已停止
        /// </summary>
        /// <returns></returns>
        public byte GetStatus()
        {
            return mStatus;
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