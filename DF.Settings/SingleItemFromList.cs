using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DF.Settings
{
    public class SingleItemFromList : Setting<string>
    {
        private static readonly Regex _camelSplit = new Regex(@"(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])");

        public IEnumerable<string> Values { get; set; } = new List<string>();

        public SingleItemFromList(string name, Func<string> getter, Action<string> setter) : base(name, getter, setter) { }

        /// <summary>
        /// Creates a SingleItemFromList for an enum property.
        /// <paramref name="serialize"/> converts an enum name to a display string (default: insert spaces before capitals).
        /// <paramref name="deserialize"/> converts a display string back to an enum name (default: remove spaces).
        /// </summary>
        public static SingleItemFromList CreateFromEnum(
            string name,
            Type type,
            Func<object> getter,
            Action<object> setter,
            Func<string, string> serialize = null,
            Func<string, string> deserialize = null)
        {
            if (serialize == null) serialize = s => _camelSplit.Replace(s, " ");
            if (deserialize == null) deserialize = s => s.Replace(" ", string.Empty);

            return new SingleItemFromList(
                name,
                () => serialize(getter().ToString()),
                x => setter(Enum.Parse(type, deserialize(x))))
            {
                Values = Enum.GetNames(type).Select(serialize)
            };
        }
    }
}
