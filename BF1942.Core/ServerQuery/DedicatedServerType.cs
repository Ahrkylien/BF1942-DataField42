using System.ComponentModel;

public enum DedicatedServerType
{
    [Description("Non-Dedicated")]
    NonDedicated = 0,

    [Description("Linux Server")]
    Linux = 1,

    [Description("Windows Server")]
    Windows = 2
}
