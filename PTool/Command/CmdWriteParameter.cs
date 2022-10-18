using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PTool
{
    /// <summary>
    /// 启停控制
    /// </summary>
    public class CmdWriteParameter : BaseCommand
    {
        public static ScaleValue scaleValue = ScaleValue.Hundred;
        protected decimal a = 0;
        protected decimal b = 0;
        protected decimal c = 0;

        /// <summary>
        /// 
        /// </summary>
        public CmdWriteParameter() : base(0x04)
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
            UpdatePayloadLength(0x0C);
            //命令头部（payload length（含）之前）
            List<byte> basebuffer = base.GetBytes();
            basebuffer.Add(100);

            int A = ((int)decimal.Truncate(a * 1000));
            int B = ((int)decimal.Truncate(b * 1000));
            int C = ((int)decimal.Truncate(c * 1000));

            basebuffer.Add(0x03);
            basebuffer.Add((byte)(A & 0x000000FF));
            basebuffer.Add((byte)(A & 0x0000FF00 >> 8));
            basebuffer.Add((byte)(A & 0x00FF0000 >> 16));

            basebuffer.Add(0x03);
            basebuffer.Add((byte)(B & 0x000000FF));
            basebuffer.Add((byte)(B & 0x0000FF00 >> 8));
            basebuffer.Add((byte)(B & 0x00FF0000 >> 16));

            basebuffer.Add(0x03);
            basebuffer.Add((byte)(C & 0x000000FF));
            basebuffer.Add((byte)(C & 0x0000FF00 >> 8));
            basebuffer.Add((byte)(C & 0x00FF0000 >> 16));

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