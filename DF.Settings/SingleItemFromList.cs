using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace DF.Settings
{
    public class SingleItemFromList : Setting<string>
    {
        public IEnumerable<string> Values { get; set; } = new List<string>();

        public SingleItemFromList(string name, Func<string> getter, Action<string> setter) : base(name, getter, setter) { }

        public static SingleItemFromList CreateFromEnum(string name, Type type, Func<object> getter, Action<object> setter)
        {
            var enumValues = Enum.GetValues(type);
            return new SingleItemFromList(name, () => getter().ToString(), x => setter(Enum.Parse(type, x)))
            {
                Values = Enum.GetNames(type)
            };
        }
    }
}
