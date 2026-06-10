using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

namespace DF.Settings
{
    /// <summary>
    /// Provides a list of settings and their kind.
    /// Writing to the provided settings will be reflected into the underlying data source.
    /// Changes in the underlying data source will also be reflected in the settings in the list.
    /// Each element in the list can be used as view model as well.
    /// </summary>
    public class SettingsProvider<T> where T : class
    {
        public List<ISetting> Settings { get; } = new List<ISetting>();

        public SettingsProvider(T settingsObject, IPropertyInfoSettingSelector propertyInfoSettingSelector)
        {
            var settingsType = settingsObject.GetType();
            PropertyInfo[] properties = settingsType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                ISetting setting = null;

                var value = property.GetValue(settingsObject);

                var type = property.PropertyType;
                var attrs = property.GetCustomAttributes(true);
                var display = attrs.OfType<DisplayAttribute>().FirstOrDefault();
                var range = attrs.OfType<RangeAttribute>().FirstOrDefault();

                var name = display?.Name ?? property.Name;
                var description = display?.Description ?? "";

                if (propertyInfoSettingSelector.TryGetSettingFromProperty(settingsObject, property, out setting))
                {
                    if (setting == null)
                        continue;
                }
                else if (_numericTypeInfo.TryGetValue(type, out var info))
                {
                    // Apply RangeAttribute if present
                    var min = range != null ? Convert.ToDecimal(range.Minimum) : info.min;
                    var max = range != null ? Convert.ToDecimal(range.Maximum) : info.max;

                    setting = new NumericSetting(
                        name,
                        () => Convert.ToDecimal(property.GetValue(settingsObject)),
                        x => property.SetValue(settingsObject, Convert.ChangeType(x, type))
                    )
                    {
                        Description = description,
                        MaxDecimals = info.decimals,
                        MinimumValue = min,
                        MaximumValue = max
                    };
                }
                else if (value is Enum enumValue)
                    setting = SingleItemFromList.CreateFromEnum(name, type, () => property.GetValue(settingsObject), x => property.SetValue(settingsObject, x));
                else if (value is string)
                    setting = new StringSetting(name, () => (string)property.GetValue(settingsObject), x => property.SetValue(settingsObject, x));
                else if (value is bool)
                    setting = new BoolSetting(name, () => (bool)property.GetValue(settingsObject), x => property.SetValue(settingsObject, x));
                else if (value is IEnumerable<string>)
                    setting = new StringCollectionSetting(name, () => ((IEnumerable<string>)property.GetValue(settingsObject)).ToList(), x => property.SetValue(settingsObject, x));
                else
                    throw new NotSupportedException($"{settingsType}.{name} has type {type} which is not supported");

                Settings.Add(setting);
            }
        }

        private static readonly Dictionary<Type, (decimal min, decimal max, ulong decimals)> _numericTypeInfo = new Dictionary<Type, (decimal, decimal, ulong)>()
        {
            { typeof(sbyte),   (sbyte.MinValue,   sbyte.MaxValue,   0) },
            { typeof(short),   (short.MinValue,   short.MaxValue,   0) },
            { typeof(int),     (int.MinValue,     int.MaxValue,     0) },
            { typeof(long),    (long.MinValue,    long.MaxValue,    0) },
            { typeof(byte),    (byte.MinValue,    byte.MaxValue,    0) },
            { typeof(ushort),  (ushort.MinValue,  ushort.MaxValue,  0) },
            { typeof(uint),    (uint.MinValue,    uint.MaxValue,    0) },
            { typeof(ulong),   (ulong.MinValue,   ulong.MaxValue,   0) },
            { typeof(float),   (decimal.MinValue, decimal.MaxValue, 6) },
            { typeof(double),  (decimal.MinValue, decimal.MaxValue, 10) },
            { typeof(decimal), (decimal.MinValue, decimal.MaxValue, 28) }
        };
    }
}
