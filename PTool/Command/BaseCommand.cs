using System;
using System.Collections.Generic;
using SerialDevice;

namespace PTool
{
    public class BaseCommand : EventArgs
    {
        public event EventHandler<EventArgs> HandleResponse;//回调函数
        public event EventHandler<EventArgs> HandleTimeOut; //超时处理

        protected byte   m_MessageID            = 0;        //8位MessageID
        protected byte   m_PayloadLength        = 0;        //8位数据长度
        protected byte   m_Checksum             = 0;        //8位检验和

        #region 属性
       
        /// <summary>
        /// 命令ID
        /// </summary>
        public byte MessageID
        {
            get { return m_MessageID; }
            set { m_MessageID = value; }
        }

        /// <summary>
        /// 命令消息长度
        /// </summary>
        public byte PayloadLength
        {
            get { return m_PayloadLength; }
            set { m_PayloadLength = value; }
        }

        /// <summary>
        /// 8位检验码
        /// </summary>
        public byte Checksum
        {
            get { return m_Checksum; }
            set { m_Checksum = value; }
        }
  
        #endregion

        public BaseCommand()
        {
        }

        public BaseCommand(byte messageID)
        {
            m_MessageID = messageID;
        }

        
        public BaseCommand(byte messageID, byte payloadLength, byte checksum)
        {
            m_MessageID     = messageID;
            m_Checksum      = checksum;
            m_PayloadLength = payloadLength;
        }

        /// <summary>
        /// 更新命令数据体长度
        /// </summary>
        /// <param name="length"></param>
        public void UpdatePayloadLength(byte length)
        {
            m_PayloadLength = length;
        }

        /// <summary>
        /// 取Direction到m_PayloadLengthReverse length之间的字节
        /// </summary>
        /// <returns></returns>
        public virtual List<byte> GetBytes()
        {
            List<byte> buffer = new List<byte>();
            buffer.Add(m_MessageID);
            buffer.Add((byte)(m_PayloadLength & 0xFF));
            return buffer;
        }

        public virtual void SetBytes(byte[] payloadData)
        {}

        /// <summary>
        /// 复制命令
        /// </summary>
        /// <param name="other"></param>
        public virtual void Copy(BaseCommand other)
        {
            m_MessageID            = other.m_MessageID;
            m_PayloadLength        = other.m_PayloadLength;
            m_Checksum             = other.m_Checksum;
        }

        /// <summary>
        /// 当接受到泵端的响应数据时，调用回调函数
        /// </summary>
        public virtual void InvokeResponse()
        {
            if (HandleResponse != null)
            {
                HandleResponse(this, this);
            }
        }
        
        /// <summary>
        /// 当超时后调用回调函数
        /// </summary>
        public virtual void InvokeTimeOut()
        {
            if (HandleTimeOut != null)
            {
                HandleTimeOut(this, this);
            }
        }
        
    }
}
