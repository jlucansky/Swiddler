using System;
using System.Reflection;

namespace Swiddler.Utils
{
    public static class AssemblyExtensions
    {
        public static string GetLocalPath(this Assembly assembly)
        {
            return new Uri(Assembly.GetEntryAssembly().GetName().CodeBase).LocalPath;
        }
    }
}
