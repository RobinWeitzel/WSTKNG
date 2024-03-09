using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace wstk_b.Models
{
    public class Setting {
        public int ID { get; set; }
        public string SMTPHost { get; set; }
        public int SMTPPort { get; set; }
        public string SMTPUser { get; set; }
        public string SMTPPassword { get; set; }
        public string EmailFrom { get; set; }
        public string KindleEmail { get; set; }
    }
}