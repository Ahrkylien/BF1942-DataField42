using System.ComponentModel;

public enum DedicatedServerType
{
    [Description("Non-Dedicated")]
    NonDedicated = 0,

    [Description("Windows Server")]
    Windows = 1,

    [Description("Linux Server")]
    Linux = 2
}
