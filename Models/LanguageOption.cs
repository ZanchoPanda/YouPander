using System;
using System.Collections.Generic;
using System.Text;

namespace YouPander.Models
{
    public class LanguageOption
    {
        public string Code { get; set; }
        public string DisplayName { get; set; }

        public LanguageOption(string code, string displayName)
        {
            Code = code;
            DisplayName = displayName;
        }
    }
}
