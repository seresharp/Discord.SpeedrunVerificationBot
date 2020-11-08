using System;
using System.Collections.Generic;
using System.Text;

namespace VerificationBot.SpeedrunCom
{
    public class Variable
    {
        public string Title { get; }
        public string Value { get; }
        public bool IsSubcategory { get; }

        internal Variable(string title, string value, bool isSubCategory)
        {
            Title = title;
            Value = value;
            IsSubcategory = isSubCategory;
        }
    }
}
