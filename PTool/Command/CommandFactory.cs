/*
 * System.Reflection 命名空间
 * Type type = Type.GetType("类的完全限定名"); 
 * object obj = type.Assembly.CreateInstance(type); 
 * 可以根据类对象的限定名来反射创建实例，所以不用担心无法访问派生类对象
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PTool
{
    /// <summary>
    /// 命令工厂，用户不用关心命令是怎么创建的。
    /// </summary>
    public class CommandFactory
    {
        public static BaseCommand CreateCommand(byte messageID)
        {
            BaseCommand cmd = null;
            switch (messageID)
            {
                case 0x01:
                    cmd = new CmdGetVoltage();
                    break;
                case 0x02:
                    cmd = new CmdStartStopAP();
                    break;
                case 0x03:
                    cmd = new CmdGetStatus();
                    break;
                case 0x04:
                    cmd = new CmdWriteParameter();
                    break;
                case 0x05:
                    cmd = new CmdReadParameter();
                    break;
                default:
                    cmd = null;
                    break;
            }
            return cmd;
        }
    }
}
