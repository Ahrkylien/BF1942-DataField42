using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace DF.Settings
{
    public abstract class Setting<T> : ISetting
    {
        private readonly Func<T> _getter;
        private readonly Action<T> _setter;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Name { get; }

        public string Description { get; set; } = string.Empty;

        public T Value
        {
            get => _getter();
            set => SetValue(value);
        }

        public Setting(string name, Func<T> getter, Action<T> setter)
        {
            Name = name;
            _getter = getter;
            _setter = setter;
        }

        protected virtual void SetValue(T value)
        {
            if (!EqualityComparer<T>.Default.Equals(value, _getter()))
            {
                _setter(value);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }
}
