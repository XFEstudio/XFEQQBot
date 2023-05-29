using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace QQBotTest
{
    #region 用于发送Mirai消息的类
    /// <summary>
    /// 发送指令的类
    /// </summary>
    class SendData
    {
        public string syncId { get; private set; }
        public string command { get; private set; }
        public FGMessage content { get; private set; }
        public SendData(string syncId, FGMessage content, string command)
        {
            this.syncId = syncId;
            this.content = content;
            this.command = command;
        }
    }
    class QQBackCode
    {
        public int code;
        public string session;
    }
    class RecEventData
    {
        public string syncId;
        public QQBackCode data;
    }
    /// <summary>
    /// 接受到的消息
    /// </summary>
    class RecData
    {
        public string syncId;
        public FGMessage data;
        [JsonConstructor]
        public RecData(string syncId, FGMessage data)
        {
            this.syncId = syncId;
            this.data = data;
        }
        public RecData(long GroupID, long sender, long at)
        {
            this.data = new FGMessage(0, new QQSender(sender, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, 0, 0, 0, new QQGroup(GroupID, string.Empty, string.Empty)), new SingleMessage[] { new SingleMessage("At", at) });
        }
        public RecData(long sender, long at)
        {
            this.data = new FGMessage(0, new QQSender(sender, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, 0, 0, 0, null), new SingleMessage[] { new SingleMessage("At", at) });
        }
    }
    class QQGroup
    {
        public long id;
        public string name;
        public string permission;
        [JsonConstructor]
        public QQGroup(long id, string name, string permission)
        {
            this.name = name;
            this.permission = permission;
            this.id = id;
        }
        public QQGroup() { }
    }
    class QQSender
    {
        public long id;
        public string memberName;
        public string nickname;
        public string remark;
        public string specialTitle;
        public string permission;
        public long joinTimestamp;
        public long lastSpeakTimestamp;
        public long muteTimeRemaining;
        public QQGroup group;
        [JsonConstructor]
        public QQSender(long id, string memberName, string nickname, string remark, string specialTitle, string permission, long joinTimestamp, long lastSpeakTimestamp, long muteTimeRemaining, QQGroup group)
        {
            this.id = id;
            this.memberName = memberName;
            this.nickname = nickname;
            this.remark = remark;
            this.specialTitle = specialTitle;
            this.permission = permission;
            this.joinTimestamp = joinTimestamp;
            this.lastSpeakTimestamp = lastSpeakTimestamp;
            this.muteTimeRemaining = muteTimeRemaining;
            this.group = group;
        }
        public QQSender() { }
    }
    /// <summary>
    /// 所有消息的基类
    /// </summary>
    class FGMessage
    {
        public string session;
        public string type;
        public long target;
        public int code;
        public string msg;
        public int messageId;
        public SingleMessage[] messageChain;
        public QQSender sender;
        [JsonConstructor]
        public FGMessage(string session, string type, long target, int code, string msg, int messageId, SingleMessage[] messageChain, QQSender sender)
        {
            this.session = session;
            this.type = type;
            this.target = target;
            this.code = code;
            this.msg = msg;
            this.messageId = messageId;
            this.messageChain = messageChain;
            this.sender = sender;
        }
        public FGMessage(long target, SingleMessage[] messageChain)
        {
            this.target = target;
            this.messageChain = messageChain;
        }
        public FGMessage(long target, QQSender sender, SingleMessage[] messageChain)
        {
            this.target = target;
            this.sender = sender;
            this.messageChain = messageChain;
        }
    }
    /// <summary>
    /// 单个的具体消息
    /// </summary>
    class SingleMessage
    {
        public string type { get; private set; }
        public string text { get; private set; }
        public string base64 { get; private set; }
        public string imageId { get; private set; }
        public string url { get; private set; }
        public long target { get; private set; }
        public long id { get; private set; }
        [JsonConstructor]
        public SingleMessage(string type, string text, string base64, string imageId, string url, long target, long id) : this(type, text, base64)
        {
            this.imageId = imageId;
            this.url = url;
            this.target = target;
            this.id = id;
        }

        public SingleMessage(string type, string text)
        {
            this.type = type;
            this.text = text;
        }
        public SingleMessage(string type, long target)
        {
            this.type = type;
            this.target = target;
        }
        public SingleMessage(string type, string text, string base64)
        {
            this.type = type;
            this.text = text;
            this.base64 = base64;
        }
    }
    #endregion
    #region WarframeAPI命令类
    /// <summary>
    /// CFContent的基类
    /// </summary>
    abstract class CFContentBase
    {
        public abstract void AddXFCommand(XFCommand xfc);
    }
    /// <summary>
    /// 存储XFCommand并用于XFCommand的查找和输出的类
    /// </summary>
    class CFContent : CFContentBase
    {
        public List<XFCommand> XFCList { get; private set; }
        public string OutCommandFromList(string zhCommand)
        {
            XFCommand OutCommand = XFCList.Find(new Predicate<XFCommand>(tar => tar.ZhCommand == zhCommand));
            if (OutCommand != null)
            {
                return OutCommand.OuCommand;
            }
            else
            {
                return string.Empty;
            }
        }
        public override void AddXFCommand(XFCommand xfc)
        {
            XFCList.Add(xfc);
        }
        public void AddXFCommand(string zhcommand, string oucommand)
        {
            XFCList.Add(new XFCommand(zhcommand, oucommand));
        }
        public CFContent(List<XFCommand> xFCList)
        {
            XFCList = xFCList;
        }
        public CFContent()
        {
            XFCList = new List<XFCommand>();
        }
    }
    /// <summary>
    /// 输入文本与输出命令对应关系的类
    /// </summary>
    class XFCommand
    {
        public string ZhCommand { get; private set; }
        public string OuCommand { get; private set; }
        public XFCommand(string zhCommand, string ouCommand)
        {
            ZhCommand = zhCommand;
            OuCommand = ouCommand;
        }
    }
    #endregion
    #region GPT类
    class ReceivedGPTMessage
    {
        public string id { get; private set; }
        public string Object { get; private set; }
        public long created { get; private set; }
        public string model { get; private set; }
        public TokenUsage usage { get; private set; }
        public MessageChoice[] choices { get; private set; }
        public ReceivedGPTMessage(string id, string @object, long created, string model, TokenUsage usage, MessageChoice[] choices)
        {
            this.id = id;
            Object = @object;
            this.created = created;
            this.model = model;
            this.usage = usage;
            this.choices = choices;
        }
    }
    class MessageChoice
    {
        public GptMessage message { get; private set; }
        public string finish_reason { get; private set; }
        public int index { get; private set; }
        public MessageChoice(GptMessage message, string finish_reason, int index)
        {
            this.message = message;
            this.finish_reason = finish_reason;
            this.index = index;
        }
    }
    class TokenUsage
    {
        public int prompt_tokens { get; private set; }
        public int completion_tokens { get; private set; }
        public int total_tokens { get; private set; }
        public TokenUsage(int prompt_tokens, int completion_tokens, int total_tokens)
        {
            this.prompt_tokens = prompt_tokens;
            this.completion_tokens = completion_tokens;
            this.total_tokens = total_tokens;
        }
    }
    class RData
    {
        public string model { get; private set; }
        public double temperature { get; private set; }
        public GptMessage[] messages { get; private set; }
        public RData(string model, double temperature, GptMessage[] messages)
        {
            this.model = model;
            this.temperature = temperature;
            this.messages = messages;
        }
    }
    class GptMessage
    {
        public string role;
        public string content;
        public GptMessage(string role, string content)
        {
            this.role = role;
            this.content = content;
        }
    }
    class LongWaitSenderMessage
    {
        public long senderNum { get; private set; }
        public long groupid { get; private set; }
        public string message { get; set; }
        public bool iftext { get; set; }
        public LongWaitSenderMessage(long senderNum, string message, long groupid, bool iftext)
        {
            this.senderNum = senderNum;
            this.groupid = groupid;
            this.message = message;
            this.iftext = iftext;
        }
    }
    #endregion
}
