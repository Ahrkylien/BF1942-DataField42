using System.Reflection;

namespace DF.Settings
{
    public interface IPropertyInfoSettingSelector
    {
        /// <summary>
        /// Override the default <see cref="ISetting"/> instance for the property type.
        /// Return False when this <see cref="IPropertyInfoSettingSelector"/> leaves selecting the settings instance to another selector.
        /// Return True with a null setting when this property should be skipped.
        /// </summary>
        bool TryGetSettingFromProperty(PropertyInfo propertyInfo, out ISetting setting);
    }
}
