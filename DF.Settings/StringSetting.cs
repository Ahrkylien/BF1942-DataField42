using System;

namespace DF.Settings
{
    public class StringSetting : Setting<string>
    {
        public ulong MinimumLength { get; set; } = 0;

        public ulong MaximumLength { get; set; } = ulong.MaxValue;

        public StringSetting(string name, Func<string> getter, Action<string> setter) : base(name, getter, setter) { }
    }
}
