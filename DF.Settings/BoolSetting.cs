using System;

namespace DF.Settings
{
    public class BoolSetting : Setting<bool>
    {
        public BoolSetting(string name, Func<bool> getter, Action<bool> setter) : base(name, getter, setter) { }
    }
}
