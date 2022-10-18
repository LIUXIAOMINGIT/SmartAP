using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PTool
{
    /// <summary>
    /// 启停控制
    /// </summary>
    public class CmdStartStopAP : BaseCommand
    {
        protected byte mAction = 0x01;

        /// <summary>
        /// 
        /// </summary>
        public CmdStartStopAP() : base(0x02)
        { }

        /// <summary>
        /// 用这个命令去启动泵
        /// </summary>
        /// <returns></returns>
        public List<byte> GetStartCommandBytes()
        {
            SetStartCommand();
            return GetBytes();
        }

        /// <summary>
        /// 用这个命令去停止泵
        /// </summary>
        /// <returns></returns>
        public List<byte> GetStopCommandBytes()
        {
            SetStopCommand();
            return GetBytes();
        }

        /// <summary>
        /// 
        /// </summary>
        public void SetStartCommand()
        {
            mAction = 0x01;
        }

        /// <summary>
        /// 
        /// </summary>
        public void SetStopCommand()
        {
            mAction = 0x00;
        }

        /// <summary>
        /// 将要发送的命令变成字节数组
        /// </summary>
        /// <returns></returns>
        public override List<byte> GetBytes()
        {
            //先计算Payload长度
            UpdatePayloadLength(1);
            //命令头部（payload length（含）之前）
            List<byte> basebuffer = base.GetBytes();
            basebuffer.Add((byte)mAction);
            //取checksum字节
            byte checksum = CRC32.CalcCRC8Partial(basebuffer);
            basebuffer.Add(checksum);
            return basebuffer;
        }

        public override void SetBytes(byte[] payloadData)
        {
            //不需要填充数据，泵没有数据返回
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