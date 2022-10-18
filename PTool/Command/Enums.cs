using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PTool
{
   
    /// <summary>
    /// ACK
    /// </summary>
    public enum ACKID : byte
    {
        CommandUndefine = 0x01, //命令未定义产品不支持命令
        CommandErrorFormat = 0x02, //命令格式错误,命令中包含的字段不完整
        CommandErrorParameter = 0x03, //字段参数格式错误,字段中参数不符合要求
        CommandErrorPackage = 0x04, //包格式错误,包的格式不符合要求
        Unknown = 0xFF,
    }

    /// <summary>
    /// Scale Value
    /// </summary>
    public enum ScaleValue : byte
    {
        None = 0x00,
        Ten = 0x01,
        Hundred = 0x02,
        Thousand = 0x03,
        TenThousand = 0x04,
    }


}
