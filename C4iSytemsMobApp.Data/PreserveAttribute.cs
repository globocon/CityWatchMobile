using System;

namespace C4iSytemsMobApp.Data
{
    [AttributeUsage(AttributeTargets.All)]
    public sealed class PreserveAttribute : Attribute
    {
        public bool AllMembers;
        public bool Conditional;
    }
}
