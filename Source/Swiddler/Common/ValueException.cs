using System;

namespace Swiddler.Common
{
    public class ValueException : Exception
    {
        public ValueException(string propertyName, string message) : base(message) 
        {
            PropertyName = propertyName;
        }

        public string PropertyName { get; set; }
    }
}
