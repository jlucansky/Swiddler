using Swiddler.Common;
using System;
using System.Xml.Serialization;

namespace Swiddler.SocketSettings
{
    public abstract class SettingsBase : BindableBase, ICloneable
    {
        string _ImageName;
        [XmlIgnore] public string ImageName { get => _ImageName; set => SetProperty(ref _ImageName, value); }

        string _Caption;
        [XmlIgnore] public string Caption { get => _Caption; set => SetProperty(ref _Caption, value); }

        public object Clone()
        {
            var obj = Activator.CreateInstance(GetType());

            foreach (var prop in GetType().GetProperties())
            {
                if (prop.SetMethod?.IsPublic == true) // copy public properties
                {
                    prop.SetValue(obj, prop.GetValue(this));
                }
            }

            return obj;
        }

        public static bool Equals(SettingsBase obj1, SettingsBase obj2)
        {
            if (obj1.GetType() == obj2.GetType())
            {
                foreach (var prop in obj1.GetType().GetProperties())
                {
                    if (prop.SetMethod?.IsPublic == true) // compare public properties
                    {
                        var val1 = prop.GetValue(obj1);
                        var val2 = prop.GetValue(obj2);

                        if (val1 == null && val2 == null)
                            continue;

                        if (val1?.Equals(val2) != true)
                            return false;
                    }
                }
            }
            return false;
        }
    }
}
