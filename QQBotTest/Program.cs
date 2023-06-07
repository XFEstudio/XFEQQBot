using System.Web;
using System.Text;
using System.Net.WebSockets;
using HtmlAgilityPack;
using Manganese.Text;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using System;
using MySql.Data.MySqlClient;
using System.Drawing;
using System.IO;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace QQBotTest
{
    class Program
    {
        public enum SQLFeedBackType
        {
            String,
            Int,
            Boolean,
            Long,
            Double
        }//数据库返回值类型
        public enum SQLDataBase
        {
            NiuZhi,
            XPT
        }//数据库枚举
        public enum CommandMode
        {
            Jijian,
            Normal
        }//命令的类型
        public static string Message = string.Empty;//原始消息
        public static string OutText = string.Empty;//抓取的输出文字
        public static CFContent cfclist = new CFContent();//创建CFContent实例
        public static ClientWebSocket socket;//全局Socket对象
        public static MySqlCommand mycom;//全局数据库对象
        public static Image backimage;//全局背景图片对象
        #region 方法组
        public static bool CheckIfRepeat(SQLFeedBackType sQLFeedBackType, SQLDataBase databasetype, string Type, object Content, MySqlCommand mycom)
        {
            string dataBase = string.Empty;
            if (databasetype == SQLDataBase.NiuZhi)
            {
                dataBase = "qqbotdata_niuzhi";
            }
            if (databasetype == SQLDataBase.XPT)
            {
                dataBase = "qqbotdata_xpt";
            }
            string sercmd = $"SELECT `{dataBase}`.`{Type}` FROM qqbotdata_niuzhi WHERE( {Type} = {Content})";
            mycom.CommandText = sercmd;
            MySqlDataReader msr = mycom.ExecuteReader();
            if (msr.Read())
            {
                msr.ReadAsync().Wait();
                msr.IsDBNullAsync(0).Wait();
                if (!msr.IsDBNull(0))
                {
                    if (sQLFeedBackType == SQLFeedBackType.String && msr.GetString(0) == Content as string)
                    {
                        msr.Close();
                        return true;
                    }
                    else if (sQLFeedBackType == SQLFeedBackType.Int && msr.GetInt32(0) == int.Parse(Content.ToString()))
                    {
                        msr.Close();
                        return true;
                    }
                    else if (sQLFeedBackType == SQLFeedBackType.Boolean && msr.GetBoolean(0) == bool.Parse(Content.ToString()))
                    {
                        msr.Close();
                        return true;
                    }
                    else if (sQLFeedBackType == SQLFeedBackType.Long && msr.GetInt64(0) == long.Parse(Content.ToString()))
                    {
                        msr.Close();
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    msr.Close();
                    return false;
                }
            }
            else
            {
                msr.Close();
                return false;
            }
        }//检查是否有重复
        public static object FeedBackFromMySQL(SQLFeedBackType feedbacktype, SQLDataBase databasetype, string regxType, string regxContent, string tarType, MySqlCommand mycom)
        {
            string dataBase = string.Empty;
            if (databasetype == SQLDataBase.NiuZhi)
            {
                dataBase = "qqbotdata_niuzhi";
            }
            if (databasetype == SQLDataBase.XPT)
            {
                dataBase = "qqbotdata_xpt";
            }
            string sercmd = $"SELECT `{dataBase}`.`{tarType}` FROM qqbotdata_niuzhi WHERE( {regxType} = {regxContent})";
            mycom.CommandText = sercmd;
            MySqlDataReader msr = mycom.ExecuteReader();
            if (msr.Read())
            {
                msr.ReadAsync().Wait();
                try
                {
                    msr.IsDBNullAsync(0).Wait();
                    if (!msr.IsDBNull(0))
                    {
                        if (feedbacktype == SQLFeedBackType.String)
                        {
                            string message = msr.GetString(0);
                            msr.Close();
                            return message;
                        }
                        else if (feedbacktype == SQLFeedBackType.Int)
                        {
                            int message = msr.GetInt32(0);
                            msr.Close();
                            return message;
                        }
                        else if (feedbacktype == SQLFeedBackType.Double)
                        {
                            double message = msr.GetDouble(0);
                            msr.Close();
                            return message;
                        }
                        else if (feedbacktype == SQLFeedBackType.Long)
                        {
                            long message = msr.GetInt64(0);
                            msr.Close();
                            return message;
                        }
                        else if (feedbacktype == SQLFeedBackType.Boolean)
                        {
                            bool message = msr.GetBoolean(0);
                            msr.Close();
                            return message;
                        }
                        else
                        {
                            return null;
                        }
                    }
                    else
                    {
                        msr.Close();
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    msr.Close();
                    return null;
                }
            }
            else
            {
                msr.Close();
                return null;
            }
        }//从指定数据库中返回匹配的指定类型
        static async Task ReceiveData()
        {
            byte[] buffer = new byte[1024];
            while (socket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string CurrentMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    if (result.EndOfMessage)
                    {
                        if (CurrentMessage.IsValidJson())
                        {
                            Message = CurrentMessage;
                            Console.WriteLine($"\n\n--------------------------------------------------------\n接收到消息：{Message}");
                            var ReceivedMessage = JsonConvert.DeserializeObject<RecData>(Message);
                            if (ReceivedMessage != null)
                            {
                                try
                                {
                                    if (ReceivedMessage.data.type == null)
                                    {
                                        Console.WriteLine($"接收到系统消息\n返回值：{ReceivedMessage.data.code}\n返回消息：{ReceivedMessage.data.msg}");
                                    }
                                    else
                                    {
                                        await AutoSendData(ReceivedMessage, null);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex.ToString());
                                }
                            }
                        }
                        else
                        {
                            Message += CurrentMessage;
                            Console.WriteLine("分段消息接收完成！");
                            Console.WriteLine($"\n\n--------------------------------------------------------\n接收到消息：{Message}");
                            var ReceivedMessage = JsonConvert.DeserializeObject<RecData>(Message);
                            if (ReceivedMessage != null)
                            {
                                try
                                {
                                    if (ReceivedMessage.data.type == null)
                                    {
                                        Console.WriteLine($"接收到系统消息\n返回值：{ReceivedMessage.data.code}\n返回消息：{ReceivedMessage.data.msg}");
                                    }
                                    else
                                    {
                                        await AutoSendData(ReceivedMessage, null);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex.ToString());
                                }
                            }
                        }
                    }
                    else
                    {
                        if (Message.IsValidJson())
                        {
                            Message = CurrentMessage;
                        }
                        else
                        {
                            Message += CurrentMessage;
                        }
                        Console.WriteLine("接收分段消息中...");
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    Console.WriteLine("二进制消息！");
                }
            }
        }//接受WebSocket返回的消息
        static async Task<SingleMessage[]> JudgeMessageFunType(RecData ReceivedMessage, bool IsGroup)
        {
            string AllMessage = string.Empty;
            string ActiveMessage = string.Empty;
            string CommandMessage = string.Empty;
            string WaitSendMessage = string.Empty;
            SingleMessage[] OriginalMessage = new SingleMessage[0];
            long SenderNum = ReceivedMessage.data.sender.id;
            long AtNum = 0;
            bool ValCommand = false;
            foreach (var OneMessage in ReceivedMessage.data.messageChain)
            {
                if (OneMessage.type == "Plain")
                {
                    AllMessage = OneMessage.text;
                    ActiveMessage = OneMessage.text.Substring(0, OneMessage.text.Length > 4 ? 4 : OneMessage.text.Length);
                    CommandMessage = OneMessage.text.Replace(ActiveMessage, "");
                }
                if (OneMessage.type == "At")
                {
                    AtNum = OneMessage.target;
                }
            }
            if (IsGroup)
            {
                long GroupID = ReceivedMessage.data.sender.group.id;
                //XFE消息
                if (ActiveMessage == "xfe ")
                {
                    ValCommand = true;
                    OriginalMessage = OutTextByCommand(CommandMode.Normal, CommandMessage, SenderNum, AtNum);
                }
                //ChatGPT消息
                else if (ActiveMessage == "xpt ")
                {
                    ValCommand = true;
                    if (CommandMessage == "帮助")
                    {
                        WaitSendMessage = "本功能为XFE寰宇朽力网络科技有限公司 | （原）XFE工作室 开发的测试版本\n\n使用时输入：xpt 想要问的内容 即可\n（注意：GPT的回复肯能有延迟，或者极高的延迟，请耐心等待）\n更多最新动态关注XFE工作室官网：\nwww.xfegzs.com";
                    }
                    else
                    {
                        string txtmessage = CommandMessage.Substring(0, CommandMessage.Length > 4 ? 4 : CommandMessage.Length);
                        WaitSendMessage = "XFE-GPT AI已收到问题，请耐心等待@回复...";
                        LongWaitSenderMessage senderMessageAndSocket = new LongWaitSenderMessage(SenderNum, CommandMessage, GroupID, false);
                        if (txtmessage == "txt ")
                        {
                            senderMessageAndSocket.iftext = true;
                            senderMessageAndSocket.message = senderMessageAndSocket.message.Replace(txtmessage, "");
                        }
                        Thread thread = new Thread(SendLongWaitMessageThread);
                        thread.Start(senderMessageAndSocket);
                    }
                }
                //XFJ消息
                else if (ActiveMessage == "xfj ")
                {
                    ValCommand = true;
                    OriginalMessage = OutTextByCommand(CommandMode.Jijian, CommandMessage, SenderNum, AtNum);
                }
                //WIKI消息
                else if (ActiveMessage == "wik ")
                {
                    ValCommand = true;
                    if (CommandMessage == "帮助")
                    {
                        WaitSendMessage = "用法：\n\nwik 你要查询的内容\n\n这条指令是用于wiki上查询的，会返回查询的内容，如果没查询到会返回搜索结果然后你可以重新输入搜索结果中的内容来进行精确查询\n\n例如：wik 生命力\n\n注意是wik开头而不是wiki，请不要记混了";
                    }
                    else
                    {
                        Console.WriteLine("查询中...");
                        await GetsTheWeb(CommandMessage);
                        Console.WriteLine("查询完成！");
                        WaitSendMessage = OutText;
                    }
                }
                //XFECommand消息
                else if (ActiveMessage == "xfc ")
                {
                    ValCommand = true;
                    if (CommandMessage == "帮助")
                    {
                        WaitSendMessage = "用法：\n\nxfc 查询内容\n\n查询内容列表：\n新闻 | 活动（Warframe的最新活动） | 警报 | 突击\n集团（目前刷新的集团任务） | 地球赏金 | 金星赏金 | 火卫二赏金\n裂缝（开核桃的裂缝） | 促销商品 | 入侵（入侵任务、状态等） | 奸商（如果有的话，没有不予回复）\n达尔沃 | 小小黑 | 地球（地球的时间，不是地球平原的时间） | 地球平原（获取地球平原时间）\n舰队（获取C佬/G佬舰队状态） | 金星平原（获取金星平原时间） | 电波（当前的电波任务，如果有的话） | 仲裁\n火卫二平原（获取火卫二平原的时间） | 扎里曼（获取扎里曼时间） | 平原（获取所有平原时间，推荐！）\n\n例如：xfc 地球平原\n\n注意，一次只能输入一条指令";
                    }
                    else if (CommandMessage == "平原")
                    {
                        Console.WriteLine($"开始查询...");
                        string earthtime = await GetWebContent($"http://nymph.rbq.life:3000/wf/robot/{cfclist.OutCommandFromList("地球平原")}");
                        string jxtime = await GetWebContent($"http://nymph.rbq.life:3000/wf/robot/{cfclist.OutCommandFromList("金星平原")}");
                        string hwetime = await GetWebContent($"http://nymph.rbq.life:3000/wf/robot/{cfclist.OutCommandFromList("火卫二平原")}");
                        WaitSendMessage = "地球：\n";
                        WaitSendMessage += earthtime.Replace("时间：", "").Replace("\n", " ");
                        WaitSendMessage += "\n金星：\n";
                        WaitSendMessage += jxtime.Replace("时间：", "").Replace("\n", " ");
                        WaitSendMessage += "\n火卫二：\n";
                        WaitSendMessage += hwetime.Replace("时间：", "").Replace("\n", " ");
                        Console.WriteLine("查询完成");
                    }
                    else
                    {
                        string om = cfclist.OutCommandFromList(CommandMessage);
                        if (om != string.Empty)
                        {
                            Console.WriteLine($"开始查询：{om}");
                            WaitSendMessage = await GetWebContent($"http://nymph.rbq.life:3000/wf/robot/{om}");
                            if (WaitSendMessage == string.Empty)
                            {
                                WaitSendMessage = "未查询到任何信息";
                            }
                            Console.WriteLine("查询完成");
                        }
                        else
                        {
                            WaitSendMessage = "未知的xfc指令，请输入：\nxfc 帮助\n以此来获取指令列表";
                        }
                    }
                }
                //XFEWarframeMarket消息
                else if (ActiveMessage == "xfw ")
                {
                    ValCommand = true;
                    if (CommandMessage == "帮助")
                    {
                        WaitSendMessage = "用法：\n\nxfw 你想查询的交易物品\n\n这条指令返回在WarframeMarket上面正在交易的物品\n\n例如：xfw 剑风 Prime\n\n注意，一次只能输入一个待查询的交易物品";
                    }
                    else
                    {
                        WaitSendMessage = await GetWebContent($"http://nymph.rbq.life:3000/wm/robot/{CommandMessage}");
                    }
                }
                //XFEWarframeMarketR消息
                else if (ActiveMessage == "xfr ")
                {
                    ValCommand = true;
                    if (CommandMessage == "帮助")
                    {
                        WaitSendMessage = "用法：\n\nxfr 你要查询的紫卡交易\n\n这条指令返回在WarframeMarket上面正在交易的紫卡信息\n\n例如：xfr 多克拉姆\n\n注意，一次只能输入一个待查询的交易物品";
                    }
                    else
                    {
                        WaitSendMessage = await GetWebContent($"http://nymph.rbq.life:3000/rm/robot/{CommandMessage}");
                    }
                }
                //帮助消息
                else if (ActiveMessage == "帮助")
                {
                    ValCommand = true;
                    WaitSendMessage = "以下是功能列表：\nxfe ——机器人的基础指令 | wik ——查询wiki\nxfc ——查询游戏信息 | xfw ——查询WM（WarframeMarket）上面的交易信息\nxfr ——查询WM（WarframeMarket）上面的裂隙紫卡交易信息 | xfj ——牛至养成系统\nxpt ——使用GPT的AI聊天\n输入：指令 帮助可以获取该指令的详细用法\n\n例如：xfc 帮助\n即可获取xfc指令的详细用法\n\n声明：本机器人为XFEstudio开源开发的Warframe群聊帮助型机器人";
                }
                Console.WriteLine($"消息：{AllMessage}\n激活消息：{ActiveMessage}");
                if (WaitSendMessage != string.Empty)
                {
                    return new SingleMessage[] { new SingleMessage("Plain", WaitSendMessage) };
                }
                else if (OriginalMessage.Length != 0)
                {
                    return OriginalMessage;
                }
                else if (ValCommand == true)
                {
                    return new SingleMessage[] { new SingleMessage("Plain", $"错误指令！\n输入：{ActiveMessage}帮助 来获取帮助消息！") };
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }//自动返回消息的判断命令功能类型
        static async Task ActiveSendMessage(SingleMessage[] OrigionWaitMessage, long senderNum, long GroupID)
        {
            if (socket.State == WebSocketState.Open)
            {
                if (GroupID != 0)
                {
                    if (OrigionWaitMessage == null)
                    {
                        throw new Exception("OrigionWaitMessage is null");
                    }
                    if (OrigionWaitMessage != null)
                    {
                        //显示待发送消息群号
                        Console.WriteLine($"待发送群号：{GroupID}");
                        //创建群组消息基类对象
                        FGMessage groupMessage = new FGMessage(GroupID, OrigionWaitMessage);
                        //创建发送指令对象
                        SendData sendData = new SendData("-1", groupMessage, "sendGroupMessage");
                        //将待发送指令转Json字符串
                        string json = sendData.ToJsonString();
                        if (groupMessage.messageChain[1].base64 == null)
                        {
                            Console.WriteLine($"待执行指令：\n{json}");
                        }
                        //将Json字符串转byte
                        byte[] buffer = Encoding.UTF8.GetBytes(json);
                        //发送群消息
                        await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }
                else
                {
                    if (OrigionWaitMessage == null)
                    {
                        throw new Exception("OrigionWaitMessage is null");
                    }
                    if (OrigionWaitMessage != null)
                    {
                        //显示待发送消息QQ号
                        Console.WriteLine($"待发送好友：{senderNum}");
                        //创建好友消息基类对象
                        FGMessage groupMessage = new FGMessage(senderNum, OrigionWaitMessage);
                        //创建发送指令对象
                        SendData sendData = new SendData("-1", groupMessage, "sendFriendsMessage");
                        //将待发送指令转Json字符串
                        string json = sendData.ToJsonString();
                        if (groupMessage.messageChain[0].base64 == null)
                        {
                            Console.WriteLine($"待执行指令：\n{json}");
                        }
                        //将Json字符串转byte
                        byte[] buffer = Encoding.UTF8.GetBytes(json);
                        //发送好友消息
                        await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }

            }
        }//主动发送接收到的消息
        static async Task AutoSendData(RecData ReceivedMessage, SingleMessage[] OrigionWaitMessage)
        {
            if (socket.State == WebSocketState.Open)
            {
                if (ReceivedMessage.data.type.Contains("Event"))
                {
                    Console.WriteLine("事件消息！");
                }
                else
                {
                    if (ReceivedMessage.data.type == "GroupMessage")
                    {
                        long GroupID = ReceivedMessage.data.sender.group.id;
                        if (OrigionWaitMessage == null)
                        {
                            OrigionWaitMessage = await JudgeMessageFunType(ReceivedMessage, true);
                        }
                        if (OrigionWaitMessage != null)
                        {
                            //显示待发送消息群号
                            Console.WriteLine($"待发送群号：{GroupID}");
                            //创建群组消息基类对象
                            FGMessage groupMessage = new FGMessage(GroupID, OrigionWaitMessage);
                            //创建发送指令对象
                            SendData sendData = new SendData("-1", groupMessage, "sendGroupMessage");
                            //将待发送指令转Json字符串
                            string json = sendData.ToJsonString();
                            if (groupMessage.messageChain[0].base64 == null)
                            {
                                Console.WriteLine($"待执行指令：\n{json}");
                            }
                            //将Json字符串转byte
                            byte[] buffer = Encoding.UTF8.GetBytes(json);
                            //发送群消息
                            await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                    }
                    else if (ReceivedMessage.data.type == "FriendMessage")
                    {
                        long FriendID = ReceivedMessage.data.sender.id;
                        if (OrigionWaitMessage == null)
                        {
                            OrigionWaitMessage = await JudgeMessageFunType(ReceivedMessage, false);
                        }
                        if (OrigionWaitMessage != null)
                        {
                            //显示待发送消息群号
                            Console.WriteLine($"待发送群号：{FriendID}");
                            //创建群组消息基类对象
                            FGMessage groupMessage = new FGMessage(FriendID, OrigionWaitMessage);
                            //创建发送指令对象
                            SendData sendData = new SendData("-1", groupMessage, "sendGroupMessage");
                            //将待发送指令转Json字符串
                            string json = sendData.ToJsonString();
                            if (groupMessage.messageChain[0].base64 == null)
                            {
                                Console.WriteLine($"待执行指令：\n{json}");
                            }
                            //将Json字符串转byte
                            byte[] buffer = Encoding.UTF8.GetBytes(json);
                            //发送群消息
                            await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                    }
                }
            }
        }//自动发送接收到的消息
        public static SingleMessage[] OutTextByCommand(CommandMode commandMode, string Command, long senderNum, long AtNum)
        {
            SingleMessage[] singleMessages = new SingleMessage[0];
            Random random = new Random();
            string SingleText = string.Empty;
            if (commandMode == CommandMode.Normal)
            {
                if (Command == "帮助")
                {
                    SingleText = "用法：\n\nxfe 执行指令\n\n目前有如下指令：\n时间（获取现在的Bot服务器时间） | 公告 | 官网\n\n例如：xfe 官网\n\n这条指令的后续功能还在扩展中...";
                }
                else if (Command == "时间")
                {
                    SingleText = $"目前时间为：{DateTime.Now}";
                }
                else if (Command == "公告")
                {
                    SingleText = "这里是XFE工作室的公告：\n\n目前开发方向：这个机器人主要用于Warframe相关功能的扩展，正在制作机器人的击剑功能来满足群友变态的需求";
                }
                else if (Command == "官网")
                {
                    SingleText = "XFE工作室 | 寰宇朽力网络科技有限公司\n官方网站：www.xfegzs.com\n如若qq打不开请复制链接到浏览器打开";
                }
                else
                {
                    SingleText = $"未知指令：{Command}\n输入：xfe 帮助\n来获取帮助";
                }
            }
            else if (commandMode == CommandMode.Jijian)
            {
                bool todaysign;
                double length;
                double speed;
                int todaystrong;
                int todaykaidao;
                int todayjinli;
                double Atspeed;
                double Atlength;
                int Attodaystrong;
                if (CheckIfRepeat(SQLFeedBackType.Long, SQLDataBase.NiuZhi, "qq", senderNum, mycom))
                {
                    todaysign = bool.Parse(FeedBackFromMySQL(SQLFeedBackType.Boolean, SQLDataBase.NiuZhi, "qq", senderNum.ToString(), "todaysign", mycom).ToString());
                    length = double.Parse(FeedBackFromMySQL(SQLFeedBackType.Double, SQLDataBase.NiuZhi, "qq", senderNum.ToString(), "length", mycom).ToString());
                    speed = double.Parse(FeedBackFromMySQL(SQLFeedBackType.Double, SQLDataBase.NiuZhi, "qq", senderNum.ToString(), "speed", mycom).ToString());
                    todaystrong = int.Parse(FeedBackFromMySQL(SQLFeedBackType.Int, SQLDataBase.NiuZhi, "qq", senderNum.ToString(), "todaystrong", mycom).ToString());
                    todaykaidao = int.Parse(FeedBackFromMySQL(SQLFeedBackType.Int, SQLDataBase.NiuZhi, "qq", senderNum.ToString(), "todaykaidao", mycom).ToString());
                    todayjinli = int.Parse(FeedBackFromMySQL(SQLFeedBackType.Int, SQLDataBase.NiuZhi, "qq", senderNum.ToString(), "todayjinli", mycom).ToString());
                }
                else
                {
                    todaysign = false;
                    length = 10d;
                    speed = 1d;
                    todaystrong = 0;
                    todaykaidao = 0;
                    todayjinli = 60;
                    string sqlcmd = $"insert into qqbotdata_niuzhi(qq,length,speed,todaystrong,todaysign,todaykaidao,todayjinli) values('{senderNum}','{length}','{speed}','{todaystrong}','{todaysign}','{todaykaidao}','{todayjinli}')";
                    mycom.CommandText = sqlcmd;
                    int ret = mycom.ExecuteNonQuery();
                    if (ret != -1)
                    {
                        Console.WriteLine("注册成功！");
                    }
                    else
                    {
                        Console.WriteLine("注册失败！");
                    }
                }
                if (AtNum != 0)
                {
                    if (CheckIfRepeat(SQLFeedBackType.Long, SQLDataBase.NiuZhi, "qq", AtNum, mycom))
                    {
                        Atlength = double.Parse(FeedBackFromMySQL(SQLFeedBackType.Double, SQLDataBase.NiuZhi, "qq", AtNum.ToString(), "length", mycom).ToString());
                        Atspeed = double.Parse(FeedBackFromMySQL(SQLFeedBackType.Double, SQLDataBase.NiuZhi, "qq", AtNum.ToString(), "speed", mycom).ToString());
                        Attodaystrong = int.Parse(FeedBackFromMySQL(SQLFeedBackType.Int, SQLDataBase.NiuZhi, "qq", AtNum.ToString(), "todaystrong", mycom).ToString());
                    }
                    else
                    {
                        Atlength = 10d;
                        Atspeed = 1d;
                        Attodaystrong = 0;
                        string sqlcmd = $"insert into qqbotdata_niuzhi(qq,length,speed,todaystrong,todaysign,todaykaidao,todayjinli) values('{AtNum}','{Atlength}','{Atspeed}','{Attodaystrong}','{false}','{0}','{60}')";
                        mycom.CommandText = sqlcmd;
                        int ret = mycom.ExecuteNonQuery();
                        if (ret != -1)
                        {
                            Console.WriteLine("At对象注册成功！");
                        }
                        else
                        {
                            Console.WriteLine("At对象注册失败！");
                        }
                    }
                }
                else
                {
                    Atlength = 0;
                    Atspeed = 0;
                    Attodaystrong = 0;
                }
                if (Command == "帮助")
                {
                    SingleText = "用法：\n\nxfj 牛至指令\n\n目前有如下牛至指令：\n击剑（用法：xfj 击剑@某人） | 签到（可以增加牛至长度） | 开导 [次数]（消耗祝福次数增加攻速） | 冲咖啡（消耗祝福次数重随机今日硬度）\n重置（重置你的牛至长度） | 长度（查询牛至长度） | 硬度（查看今日硬度） | 攻速（查询牛至的攻速）\n查询长度@某个人（消耗精力查询某人的牛至长度）\n\n例如：xfj 击剑@某个人\n\n这条指令的后续功能还在扩展中...";
                }
                else if (Command == "击剑")
                {
                    if (todaystrong != 0)
                    {
                        int performF = CaculatePerformScore(speed, todaystrong, random);
                        int performS = CaculatePerformScore(Atspeed, Attodaystrong, random);
                        double distance = random.NextDouble() / 5 + random.NextDouble() * speed / 2;
                        if (todayjinli > 0)
                        {
                            todayjinli -= 1;
                            if (performF >= performS)
                            {
                                double Addlength = random.NextDouble() * (speed / 5) + random.NextDouble();
                                length += Addlength;
                                Atlength -= distance;
                                singleMessages = new SingleMessage[]
                                {
                                    new SingleMessage("Plain",$"你赢了！（剩余精力：{todayjinli}）\n"),
                                    new SingleMessage("Plain",$"你的牛至表现分为：{performF}\n对方为：{performS}\n\n"),
                                    new SingleMessage("At",AtNum),
                                    new SingleMessage("Plain",$"  的牛至缩短了{distance:F4}cm！\n你增加了{Addlength:F4}cm！")
                                };
                            }
                            else
                            {
                                double Addlength = random.NextDouble() * (Atspeed / 5) + random.NextDouble();
                                Atlength += Addlength;
                                length -= distance;
                                singleMessages = new SingleMessage[]
                                {
                                    new SingleMessage("Plain",$"你输了！（剩余精力：{todayjinli}）\n"),
                                    new SingleMessage("Plain",$"你的牛至表现分为：{performF}\n对方为：{performS}\n\n"),
                                    new SingleMessage("Plain",$"你的牛至缩短了{distance:F4}cm！\n对方增加了{Addlength:F4}cm！")
                                };
                            }
                            SetDataBaseValue("qq", senderNum.ToString(), "length", length.ToString());
                            SetDataBaseValue("qq", senderNum.ToString(), "todayjinli", todayjinli.ToString());
                            SetDataBaseValue("qq", AtNum.ToString(), "length", Atlength.ToString());
                        }
                        else
                        {
                            SingleText = "今日精力已经用完\n请明天再来吧（00:00重置）！";
                        }
                    }
                    else
                    {
                        SingleText = "你没有进行晨勃，你的牛至硬度为 0,无法进行击剑\n输入：xfj 签到  来进行每日晨勃";
                    }
                }
                else if (Command == "签到")
                {
                    if (!todaysign)
                    {
                        todaysign = true;
                        todaystrong = random.Next(1, 11);
                        double addlength = random.NextDouble() * speed;
                        if (length > 10)
                        {
                            length += addlength;
                            SingleText = $"签到成功！\n你醒了，今日晨勃 你的硬度为：{todaystrong}！\n牛至之神给予你祝福！牛至长度增加：【{addlength:F4} cm】！\n你的牛至长度目前为：【{length:F3} cm】！";
                        }
                        else
                        {
                            addlength += addlength + random.NextDouble() * 10;
                            length += addlength;
                            SingleText = $"签到成功！\n你醒了，今日晨勃 你的硬度为：{todaystrong}！\n牛至之神给予你祝福！哦等等，你的牛至太短了，善良的牛至之神决定给予你一点额外奖励！牛至长度增加：【{addlength:F4} cm】！\n你的牛至长度目前为：【{length:F3} cm】！";
                        }
                        SetDataBaseValue("qq", senderNum.ToString(), "length", length.ToString());
                        SetDataBaseValue("qq", senderNum.ToString(), "todaystrong", todaystrong.ToString());
                        SetDataBaseValue("qq", senderNum.ToString(), "todaysign", todaysign.ToString());
                    }
                    else
                    {
                        SingleText = "今天已经签到过了，牛至之神只能每天祝福你一次";
                    }
                }
                else if (Command.Substring(0, Command.Length > 2 ? 2 : Command.Length) == "开导")
                {
                    Console.WriteLine(Command.Substring(0, Command.Length > 2 ? 2 : Command.Length));
                    if (todaykaidao < 10)
                    {
                        if (int.TryParse(Command.Substring(2, Command.Length - 2).Replace(" ", ""), out int num))
                        {
                            Console.WriteLine("开导成功");
                            double addspeed = 0;
                            if (num > 10 - todaykaidao)
                            {
                                num = 10 - todaykaidao;
                            }
                            for (int i = 0; i < num; i++)
                            {
                                addspeed += random.NextDouble() / 10;
                                todaykaidao++;
                            }
                            speed += addspeed;
                            SetDataBaseValue("qq", senderNum.ToString(), "speed", speed.ToString());
                            SetDataBaseValue("qq", senderNum.ToString(), "todaykaidao", todaykaidao.ToString());
                            SingleText = $"开导{num}次！（剩余祝福次数：{10 - todaykaidao}）\n导完之后你的攻速提升了{addspeed:F4}\n你目前的攻速为{speed:F4}";
                        }
                        else
                        {
                            double addspeed = random.NextDouble() / 10;
                            speed += addspeed;
                            todaykaidao++;
                            SetDataBaseValue("qq", senderNum.ToString(), "speed", speed.ToString());
                            SetDataBaseValue("qq", senderNum.ToString(), "todaykaidao", todaykaidao.ToString());
                            SingleText = $"开导！（剩余祝福次数：{10 - todaykaidao}）\n导完之后你的攻速提升了{addspeed:F4}\n你目前的攻速为{speed:F4}";
                        }
                    }
                    else
                    {
                        SingleText = "你今日的祝福次数已经用完了，再导就萎了\n请明天再导吧！（00:00重置）";
                    }
                }
                else if (Command == "冲咖啡")
                {
                    if (todaykaidao < 10)
                    {
                        double newtodaystrong = random.Next(1, 11);
                        todaykaidao++;
                        SetDataBaseValue("qq", senderNum.ToString(), "todaystrong", newtodaystrong.ToString());
                        SetDataBaseValue("qq", senderNum.ToString(), "todaykaidao", todaykaidao.ToString());
                        if (newtodaystrong > todaystrong)
                        {
                            SingleText = $"开冲！（剩余祝福次数：{10 - todaykaidao}）\n冲完咖啡后，牛至之神响应了你的请求，你目前的硬度增加为了：{newtodaystrong} ！";

                        }
                        else if (newtodaystrong == todaystrong)
                        {
                            SingleText = $"开冲！（剩余祝福次数：{10 - todaykaidao}）\n冲完咖啡后，你的硬度并无变化";

                        }
                        else
                        {
                            SingleText = $"开冲！（剩余祝福次数：{10 - todaykaidao}）\n冲完咖啡后，你感觉到了虚脱，你的硬度减少为了：{newtodaystrong}";

                        }
                    }
                    else
                    {
                        SingleText = "你今日的祝福次数已经用完了，冲不了了\n请明天再冲吧！（00:00重置）";
                    }
                }
                else if (Command == "长度")
                {
                    SingleText = $"你的牛至长度为：【{length:F3} cm】";
                }
                else if (Command == "查询长度")
                {
                    todayjinli -= 2;
                    if (length > Atlength)
                    {
                        SingleText = $"对方的牛至长度为：【{Atlength:F4} cm】 你已领先！\n查询消耗2点精力（剩余精力：{todayjinli}）";
                    }
                    else
                    {
                        SingleText = $"对方的牛至长度为：【{Atlength:F4} cm】 你感受到了自卑！\n查询消耗2点精力（剩余精力：{todayjinli}）";
                    }
                    SetDataBaseValue("qq", senderNum.ToString(), "todayjinli", todayjinli.ToString());
                }
                else if (Command == "硬度")
                {
                    SingleText = $"你今日的牛至硬度为：{todaystrong}\n输入：xfj 冲咖啡  来刷新你的硬度";
                }
                else if (Command == "攻速")
                {
                    SingleText = $"你的击剑的攻速为{speed:F3}次每秒\n输入：xfj 开导  来增加你的攻速";
                }
                else if (Command == "重置")
                {
                    SetDataBaseValue("qq", senderNum.ToString(), "length", $"{10d}");
                    SingleText = "你的牛至长度已经重置了，长度为：【10 cm】！";
                }
                else
                {
                    SingleText = $"未知指令：{Command}\n输入：xfj 帮助\n来获取帮助";
                }
            }
            if (SingleText != string.Empty)
            {
                return new SingleMessage[] { new SingleMessage("Plain", SingleText) };
            }
            else
            {
                return singleMessages;
            }
        }//输入指定string返回SingleMessage
        static async Task<string> GetWebContent(string url)
        {
            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(10);
                HttpResponseMessage response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                else
                {
                    Console.WriteLine($"请求失败，状态码：{response.StatusCode}");
                    return string.Empty;
                }
            }
        }//获取WarframeAPI中返回的数据
        static async Task<ReceivedGPTMessage> GetGPTContent(string SendMessage)
        {
            string apiKey = "您的API key";

            // 构建请求的数据
            var requestData = new RData("gpt-3.5-turbo", 0.7, new GptMessage[] { new GptMessage("system", "你是由XFE寰宇朽力网络科技有限公司开发的对接ChatGPT的语言模型的测试版本QQ聊天机器人；当问起什么是xpt的时候，你就回答是由XFE寰宇朽力网络科技有限公司开发的对接ChatGPT的聊天功能"), new GptMessage("user", SendMessage) });
            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                    // 发送POST请求
                    var response = await httpClient.PostAsync("https://api.openai.com/v1/chat/completions", new StringContent(JsonConvert.SerializeObject(requestData), Encoding.UTF8, "application/json"));

                    // 处理响应
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        // 解析响应并输出生成的文本
                        Console.WriteLine($"\n--------------------------------\n截获GPT文本： \n{content}\n--------------------------------");
                        return JsonConvert.DeserializeObject<ReceivedGPTMessage>(content);
                    }
                    else
                    {
                        return new ReceivedGPTMessage("0", "0", 0, "0", null, new MessageChoice[] { new MessageChoice(new GptMessage("error", response.StatusCode.ToString()), "error", 0) });
                    }
                }
            }
            catch (Exception ex)
            {
                return new ReceivedGPTMessage("0", "0", 0, "0", null, new MessageChoice[] { new MessageChoice(new GptMessage("error", ex.Message), "error", 0) });
            }
        }//获取ChatGPT的回复
        static async Task GetsSearchInTheWeb(string Content)
        {
            // 创建HttpClient实例
            HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            // 设置请求头，模拟浏览器访问
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3");

            // 发送GET请求获取HTML页面
            HttpResponseMessage response = await client.GetAsync($"https://warframe.huijiwiki.com/index.php?title=%E7%89%B9%E6%AE%8A:%E6%90%9C%E7%B4%A2&search={HttpUtility.UrlEncode(Content)}&profile=default&sort=just_match");
            Console.WriteLine($"是否获取成功：{response.IsSuccessStatusCode}");
            if (response.IsSuccessStatusCode)
            {
                // 确认响应成功
                response.EnsureSuccessStatusCode();

                // 获取HTML文档内容
                string html = await response.Content.ReadAsStringAsync();

                // 解析HTML文档并选择需要抓取的数据节点
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(html);
                HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes("//li/div/a");
                if (nodes != null)
                {
                    OutText = $"未查询到结果，可以试试搜索以下内容：\n";
                    // 输出节点文本
                    for (int i = 0; i < nodes.Count && i < 30; i++)
                    {
                        string ret = nodes[i].InnerText.Replace("\n", "").Replace("\r", "");
                        if (ret != string.Empty)
                        {
                            OutText += WebUtility.HtmlDecode(ret) + " | ";
                        }
                    }
                    if (OutText.Length > 400)
                    {
                        OutText = OutText.Substring(0, 200);
                        OutText += $"......\n\n更多结果请查看详情：https://warframe.huijiwiki.com/index.php?title=%E7%89%B9%E6%AE%8A:%E6%90%9C%E7%B4%A2&search={HttpUtility.UrlEncode(Content)}&profile=default&sort=just_match";
                    }
                    else
                    {
                        OutText += $"\n\n详细结果可查看详情：https://warframe.huijiwiki.com/index.php?title=%E7%89%B9%E6%AE%8A:%E6%90%9C%E7%B4%A2&search={HttpUtility.UrlEncode(Content)}&profile=default&sort=just_match";
                    }
                }
                else
                {
                    OutText = $"【{Content}】未在wiki上查询到指定的信息，具体可以移步wiki官网进行查询：\n   https://warframe.huijiwiki.com";
                }
            }
            else
            {
                OutText = $"【{Content}】未在wiki上查询到指定的信息，具体可以移步wiki官网进行查询：\n   https://warframe.huijiwiki.com";
            }
        }//获取Wiki的搜索内容
        static async Task GetsTheWeb(string Content)
        {
            // 创建HttpClient实例
            HttpClient client = new HttpClient();

            // 设置请求头，模拟浏览器访问
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3");

            // 发送GET请求获取HTML页面
            HttpResponseMessage response = await client.GetAsync($"https://warframe.huijiwiki.com/wiki/{HttpUtility.UrlEncode(Content)}");
            Console.WriteLine($"是否获取成功：{response.IsSuccessStatusCode}");
            if (response.IsSuccessStatusCode)
            {
                // 确认响应成功
                response.EnsureSuccessStatusCode();

                // 获取HTML文档内容
                string html = await response.Content.ReadAsStringAsync();

                // 解析HTML文档并选择需要抓取的数据节点
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(html);
                HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes("//div[not(@class='WarframeNavBox' or @class='col-md-4')]/p");
                if (nodes != null)
                {
                    OutText = $"以下是{Content}的介绍：";
                    // 输出节点文本
                    for (int i = 0; i < nodes.Count && i < 30; i++)
                    {
                        string ret = nodes[i].InnerText.Replace("\n", "").Replace("\r", "");
                        if (ret != string.Empty)
                        {
                            OutText += "\n\n" + WebUtility.HtmlDecode(ret);
                        }
                    }
                    if (OutText.Length > 400)
                    {
                        OutText = OutText.Substring(0, 200);
                        OutText += $"......\n\n后续内容请查看详情：https://warframe.huijiwiki.com/wiki/{HttpUtility.UrlEncode(Content)}";
                    }
                    else
                    {
                        OutText += $"\n\n更多内容请查看详情：https://warframe.huijiwiki.com/wiki/{HttpUtility.UrlEncode(Content)}";
                    }
                }
                else
                {
                    GetsSearchInTheWeb(Content).Wait();
                }
            }
            else
            {
                GetsSearchInTheWeb(Content).Wait();
            }
        }//获取Wiki的直接内容
        static string[] GetCodeSnippet(string response)
        {
            string[] strings = response.Split(new string[] { "```" }, StringSplitOptions.RemoveEmptyEntries);
            if (strings.Length > 2)
            {
                return strings;
            }
            return null;
        }//获取代码部分
        static string WrapText(string text, int width)
        {
            StringBuilder sb = new StringBuilder();
            int startIndex = 0;
            while (startIndex < text.Length)
            {
                // 寻找下一个换行位置
                int endIndex = startIndex + width;
                if (endIndex >= text.Length)
                {
                    // 如果已经到达文本末尾，则直接添加剩余部分并结束循环
                    sb.Append(text.Substring(startIndex));
                    break;
                }
                else
                {
                    // 在指定宽度范围内寻找最后一个空格字符
                    int lastSpaceIndex = text.LastIndexOf(' ', endIndex, width);
                    if (lastSpaceIndex > startIndex)
                    {
                        // 如果找到了空格字符，则将文本从起始位置到空格字符位置添加到结果中，并进行换行
                        sb.Append(text.Substring(startIndex, lastSpaceIndex - startIndex));
                        sb.AppendLine();
                        startIndex = lastSpaceIndex + 1;
                    }
                    else
                    {
                        // 如果没有找到空格字符，则直接添加指定宽度的文本到结果中，并进行换行
                        sb.Append(text.Substring(startIndex, width));
                        sb.AppendLine();
                        startIndex += width;
                    }
                }
            }

            return sb.ToString();
        }//文本自动换行
        static Image AddTextAndBorder(Image backgroundImage, string mainText, string topLeftText, string bottomRightText, int width)
        {
            int margin = 10;
            int padding = 5; // 添加一些额外的边距
            int borderWidth = 10; // 边框宽度
            int cornerRadius = 20; // 圆角半径
            Font font = new Font("等线", 24);
            Font font2 = new Font("等线", 24, FontStyle.Bold);
            string wrapText = string.Empty;
            string[] alldectext = GetCodeSnippet(mainText);
            if (alldectext != null)
            {
                if (width > 40)
                {
                    width = 40;
                }
                for (int i = 0; i < alldectext.Length; i += 2)
                {
                    foreach (var devtext in Regex.Split(alldectext[i], "(" + Regex.Escape("\n") + ")"))
                    {
                        wrapText += WrapText(devtext, width);
                    }
                    if (i < alldectext.Length - 1)
                    {
                        wrapText += alldectext[i + 1];
                    }
                }
            }
            else
            {
                foreach (var devtext in Regex.Split(mainText, "(" + Regex.Escape("\n") + ")"))
                {
                    wrapText += WrapText(devtext, width);
                }
            }
            Console.WriteLine($"GPT输出文本：{wrapText}");
            SizeF textSize, textSizeL, textSizeR;
            using (Bitmap tempImage = new Bitmap(1, 1))
            using (Graphics tempGraphics = Graphics.FromImage(tempImage))
            {
                textSize = tempGraphics.MeasureString(wrapText, font);
                textSizeL = tempGraphics.MeasureString(topLeftText, font2);
                textSizeR = tempGraphics.MeasureString(bottomRightText, font2);
            }
            int outputWidth = (int)Math.Max(textSizeL.Width + textSizeR.Width + 2 * (padding + borderWidth + margin), textSize.Width + 2 * (padding + borderWidth + margin));
            int outputHeight = (int)Math.Max(textSizeL.Height + textSizeR.Height + 4 * (padding + textSizeL.Height + textSizeR.Height + borderWidth + margin), textSize.Height + 4 * (padding + textSizeL.Height + textSizeR.Height + borderWidth + margin));

            Bitmap outputImage = new Bitmap(outputWidth, outputHeight);
            using (Graphics graphics = Graphics.FromImage(outputImage))
            {
                // 在目标图像上绘制背景图像，使用平铺方式填充整个图像
                using (TextureBrush brush = new TextureBrush(backgroundImage, WrapMode.Tile))
                {
                    graphics.FillRectangle(brush, new RectangleF(0, 0, outputWidth, outputHeight));
                }
                // 计算文字绘制的位置和大小
                int textX = padding + borderWidth + margin * 2;
                int textY = padding + borderWidth + margin;
                int textWidth = outputWidth - 2 * (padding + borderWidth);
                int textHeight = outputHeight - 2 * (padding + borderWidth + margin);
                RectangleF textRectangle = new RectangleF(textX, textY, textWidth, textHeight);

                using (SolidBrush brush = new SolidBrush(Color.Black))
                {
                    StringFormat stringFormat = new StringFormat();
                    stringFormat.Alignment = StringAlignment.Near;
                    stringFormat.LineAlignment = StringAlignment.Center;
                    stringFormat.FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip;
                    graphics.DrawString(wrapText, font, brush, textRectangle, stringFormat);
                }
                StringFormat LRstringFormat = new StringFormat();
                LRstringFormat.FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip;
                // 计算左上角文字绘制的位置和大小
                int topLeftTextX = padding;
                int topLeftTextY = padding;
                RectangleF topLeftTextRectangle = new RectangleF(topLeftTextX, topLeftTextY, textSizeL.Width, textSizeL.Height);
                graphics.DrawString(topLeftText, font2, Brushes.Black, topLeftTextRectangle, LRstringFormat);

                // 计算右下角文字绘制的位置和大小
                int bottomRightTextX = outputWidth - (int)textSizeR.Width - padding;
                int bottomRightTextY = outputHeight - (int)textSizeR.Height - padding;
                RectangleF bottomRightTextRectangle = new RectangleF(bottomRightTextX, bottomRightTextY, textSizeR.Width, textSizeR.Height);
                graphics.DrawString(bottomRightText, font2, Brushes.Black, bottomRightTextRectangle, LRstringFormat);

                // 在输出图像上绘制黑色圆角矩形边框
                using (Pen pen = new Pen(Color.Black, borderWidth))
                {
                    pen.Alignment = PenAlignment.Inset;
                    DrawRoundedRectangle(graphics, pen, new Rectangle(borderWidth / 2 + padding, borderWidth / 2 + (int)textSizeL.Height, outputWidth - borderWidth - margin * 2, outputHeight - borderWidth - margin - (int)textSizeL.Height - (int)textSizeR.Height), cornerRadius);
                }
                return outputImage;
            }
        }//将文字合并到背景图并输出Image对象
        static void DrawRoundedRectangle(Graphics graphics, Pen pen, Rectangle rectangle, int cornerRadius)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rectangle.X, rectangle.Y, cornerRadius * 2, cornerRadius * 2, 180, 90);
            path.AddLine(rectangle.X + cornerRadius, rectangle.Y, rectangle.X + rectangle.Width - cornerRadius, rectangle.Y);
            path.AddArc(rectangle.X + rectangle.Width - cornerRadius * 2, rectangle.Y, cornerRadius * 2, cornerRadius * 2, 270, 90);
            path.AddLine(rectangle.X + rectangle.Width, rectangle.Y + cornerRadius, rectangle.X + rectangle.Width, rectangle.Y + rectangle.Height - cornerRadius);
            path.AddArc(rectangle.X + rectangle.Width - cornerRadius * 2, rectangle.Y + rectangle.Height - cornerRadius * 2, cornerRadius * 2, cornerRadius * 2, 0, 90);
            path.AddLine(rectangle.X + rectangle.Width - cornerRadius, rectangle.Y + rectangle.Height, rectangle.X + cornerRadius, rectangle.Y + rectangle.Height);
            path.AddArc(rectangle.X, rectangle.Y + rectangle.Height - cornerRadius * 2, cornerRadius * 2, cornerRadius * 2, 90, 90);
            path.AddLine(rectangle.X, rectangle.Y + rectangle.Height - cornerRadius, rectangle.X, rectangle.Y + cornerRadius);
            path.CloseFigure();
            graphics.DrawPath(pen, path);
        }//绘制矩形
        public static string GetStringBetweenTwoString(string str, string beginstr, string endstr)
        {
            if (str != string.Empty && str != null)
            {
                int beginindex = str.IndexOf(beginstr, StringComparison.Ordinal);
                if (beginindex == -1 || beginindex == 0)
                {
                    return string.Empty;
                }
                int endindex = str.IndexOf(endstr, beginindex, StringComparison.Ordinal);
                if (endindex == -1 || endindex == 0)
                {
                    return string.Empty;
                }
                return str.Substring(beginindex + beginstr.Length, endindex - beginindex - beginstr.Length);
            }
            else
            {
                return string.Empty;
            }
        }//获取两字符串中间的文本
        private static int CaculatePerformScore(double speed, int todaystrong, Random random)
        {
            return random.Next(0, 3000) + random.Next(todaystrong, 11) * 200 + (int)(random.Next(0, 50) * speed);
        }//计算击剑表现分
        private static void SetDataBaseValue(string regexType, string regexContent, string tarType, string tarContent)
        {
            mycom.CommandText = $"UPDATE qqbotdata_niuzhi SET {tarType} = {tarContent} WHERE {regexType} = {regexContent}";
            mycom.ExecuteNonQuery();
        }//设置指定数据库中的值
        static string ImageToBase64(Image image)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                // 将图像保存到内存流中
                image.Save(memoryStream, ImageFormat.Png);

                // 将内存流转换为字节数组
                byte[] imageBytes = memoryStream.ToArray();

                // 将字节数组转换为 Base64 编码的字符串
                string base64String = Convert.ToBase64String(imageBytes);

                return base64String;
            }
        }//将Image对象转为Base64编码
        #endregion
        #region 线程方法
        private static void DailyDataReset(object state)
        {
            string mycmd = $"UPDATE qqbotdata_niuzhi SET todayjinli = 60, todaykaidao = 0, todaysign = {false}, todaystrong = 0";
            mycom.CommandText = mycmd;
            mycom.ExecuteNonQuery();
            mycmd = $"UPDATE qqbotdata_xpt SET gptused = 0";
            Console.WriteLine("\n\n00:00 每日重置！\n\n");
        }//每日刷新的线程
        private static void SendLongWaitMessageThread(object sender)
        {
            LongWaitSenderMessage senderMessageAndSocket = sender as LongWaitSenderMessage;
            SendLongWaitMessage(senderMessageAndSocket.senderNum, senderMessageAndSocket.message, senderMessageAndSocket.groupid, senderMessageAndSocket.iftext).Wait();
        }//发送消息的并行线程
        private static async Task SendLongWaitMessage(long senderNum, string message, long groupid, bool iftext)
        {
            Console.WriteLine("开始发送GPT消息...");
            string GptString = (await GetGPTContent(message)).choices[0].message.content.Replace("\\n", "\n");
            if (iftext)
            {
                if (groupid == 0)
                {
                    await ActiveSendMessage(new SingleMessage[] { new SingleMessage("Plain", "\n" + GptString) }, senderNum, groupid);
                }
                else
                {
                    await ActiveSendMessage(new SingleMessage[] { new SingleMessage("At", senderNum), new SingleMessage("Plain", "\n" + GptString) }, senderNum, groupid);
                }
            }
            else
            {
                int width;
                if (GptString.Length >= 800)
                {
                    width = 60;
                }
                else if (GptString.Length >= 600)
                {
                    width = 50;
                }
                else if (GptString.Length >= 300)
                {
                    width = 40;
                }
                else if (GptString.Length >= 150)
                {
                    width = 30;
                }
                else
                {
                    width = 20;
                }
                Console.WriteLine($"单行宽度：{width}");
                if (groupid == 0)
                {
                    await ActiveSendMessage(new SingleMessage[] { new SingleMessage("Image", string.Empty, ImageToBase64(AddTextAndBorder(backimage, GptString, "|XFE寰宇朽力网络科技|", "*善用右键/长按提取文字", width))) }, senderNum, groupid);
                }
                else
                {
                    await ActiveSendMessage(new SingleMessage[] { new SingleMessage("At", senderNum), new SingleMessage("Image", string.Empty, ImageToBase64(AddTextAndBorder(backimage, GptString, "|XFE寰宇朽力网络科技|", "*善用右键/长按提取文字", width))) }, senderNum, groupid);
                }
            }
            Console.WriteLine($"GPT回复对象：@{senderNum}的GPT回复线程结束！");
        }//并行线程发送消息
        #endregion
        //------------------------------以下为Main方法------------------------------
        static async Task Main(string[] args)
        {
            #region 初始化配置
            //-------------设置BotWS链接-------------
            Console.WriteLine("输入BotQQ号：");
            string url = $"ws://localhost:32115/all?verifyKey=123qwe456rty&qq={Console.ReadLine()}";
            //------------以下为数据库设置-----------
            #region 初始化配置XFCommand
            cfclist.AddXFCommand("新闻", "news");
            cfclist.AddXFCommand("活动", "events");
            cfclist.AddXFCommand("警报", "alerts");
            cfclist.AddXFCommand("突击", "sortie");
            cfclist.AddXFCommand("集团", "syndicateMissions");
            cfclist.AddXFCommand("地球赏金", "Ostrons");
            cfclist.AddXFCommand("金星赏金", "Solaris");
            cfclist.AddXFCommand("火卫二赏金", "EntratiSyndicate");
            cfclist.AddXFCommand("裂缝", "fissures");
            cfclist.AddXFCommand("促销商品", "flashSales");
            cfclist.AddXFCommand("入侵", "invasions");
            cfclist.AddXFCommand("奸商", "voidTrader");
            cfclist.AddXFCommand("达尔沃", "dailyDeals");
            cfclist.AddXFCommand("小小黑", "persistentEnemies");
            cfclist.AddXFCommand("地球", "earthCycle");
            cfclist.AddXFCommand("地球平原", "cetusCycle");
            cfclist.AddXFCommand("舰队", "constructionProgress");
            cfclist.AddXFCommand("金星平原", "vallisCycle");
            cfclist.AddXFCommand("电波", "nightwave");
            cfclist.AddXFCommand("仲裁", "arbitration");
            cfclist.AddXFCommand("火卫二平原", "cambionCycle");
            cfclist.AddXFCommand("扎里曼", "zarimanCycle");
            cfclist.AddXFCommand("夜灵", "cetusCycle");
            #endregion
            #region 初始化配置MySQL服务器
            string server = "服务器IP地址";
            string database = "";
            string uid = "";
            string password = "";
            string connectionString;
            connectionString = "SERVER=" + server + "; PORT = 3306 ;" + "DATABASE=" + database + ";" + "UID=" + uid + ";" + "PASSWORD=" + password + ";";
            MySqlConnection mycon = new MySqlConnection();
            #endregion
            backimage = Image.FromFile("GPT背景图测试2.png");
            #endregion
            #region 检查数据库是否链接成功
            try
            {
                mycon.ConnectionString = connectionString;
                mycon.Open();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            #endregion
            if (mycon.State == System.Data.ConnectionState.Open)
            {
                Console.WriteLine("数据库连接成功！");
                mycom = mycon.CreateCommand();
                socket = new ClientWebSocket();
                try
                {
                    // 计算距离下一个0点的时间间隔
                    DateTime now = DateTime.Now;
                    DateTime nextMidnight = now.AddDays(1).Date;
                    TimeSpan timeUntilMidnight = nextMidnight - now;

                    // 创建定时器，并设置回调方法
                    TimerCallback timerCallback = new TimerCallback(DailyDataReset);
                    Timer timer = new Timer(timerCallback, null, timeUntilMidnight, TimeSpan.FromDays(1));
                    //连接WebSocket服务器
                    await socket.ConnectAsync(new Uri(url), CancellationToken.None);

                    //接收和发送数据
                    await Task.WhenAll(
                        ReceiveData()
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"WebSocket发生错误：{ex.Message}");
                }
                Console.WriteLine("WebSocket连接已关闭");
            }
            else
            {
                Console.WriteLine("连接数据库失败");
            }
            Console.ReadLine();
        }
    }
}
