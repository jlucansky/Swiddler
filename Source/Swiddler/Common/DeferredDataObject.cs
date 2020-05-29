using System;
using System.Windows;

namespace Swiddler.Common
{
    public class DeferredDataObject : IDataObject
    {
        readonly DataObject _innerDataObject = new DataObject();

        public object GetData(string format, bool autoConvert)
        {
            var data = _innerDataObject.GetData(format, autoConvert);

            if (data is Func<object> func)
                data = func();

            return data;
        }

        public void SetData(string format, Func<object> data) => _innerDataObject.SetData(format, data);
        public void SetData(string format, Func<object> data, bool autoConvert) => _innerDataObject.SetData(format, data, autoConvert);



        public object GetData(string format) => _innerDataObject.GetData(format);
        public object GetData(Type format) => _innerDataObject.GetData(format);
        public bool GetDataPresent(string format) => _innerDataObject.GetDataPresent(format);
        public bool GetDataPresent(Type format) => _innerDataObject.GetDataPresent(format);
        public bool GetDataPresent(string format, bool autoConvert) => _innerDataObject.GetDataPresent(format, autoConvert);
        public string[] GetFormats() => _innerDataObject.GetFormats();
        public string[] GetFormats(bool autoConvert) => _innerDataObject.GetFormats(autoConvert);
        public void SetData(object data) => _innerDataObject.SetData(data);
        public void SetData(string format, object data) => _innerDataObject.SetData(format, data);
        public void SetData(Type format, object data) => _innerDataObject.SetData(format, data);
        public void SetData(string format, object data, bool autoConvert) => _innerDataObject.SetData(format, data, autoConvert);
    }
}
