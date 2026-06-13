using System;

namespace DF.Settings
{
    public class NumericSetting : Setting<decimal>
    {
        public decimal MinimumValue { get; set; } = decimal.MinValue;

        public decimal MaximumValue { get; set; } = decimal.MaxValue;

        public ulong MaxDecimals { get; set; } = ulong.MaxValue;

        public NumericSetting(string name, Func<decimal> getter, Action<decimal> setter) : base(name, getter, setter) { }

        protected override void SetValue(decimal value)
        {
            Validate(value);
            base.SetValue(value);
        }

        private void Validate(decimal value)
        {
            if (value < MinimumValue || value > MaximumValue)
                throw new ArgumentException($"{value} is out of range ({MinimumValue} -> {MaximumValue}).");
        }
    }
}
