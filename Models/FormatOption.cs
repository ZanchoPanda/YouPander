using System;
using System.Collections.Generic;
using System.Text;

namespace YouPander.Models
{
    public class FormatOption
    {
        public string FormatId { get; set; } = string.Empty;   
        public string Label { get; set; } = string.Empty;      
        public string Extension { get; set; } = string.Empty;  
        public string Resolution { get; set; } = string.Empty;
        public int ResolutionInt { get; set; } 
        public int Fps { get; set; }
        public double Tbr { get; set; }
        public double Abr { get; set; }
        public long FilesizeApprox { get; set; }
        public bool IsVideo { get; set; }
    }
}
