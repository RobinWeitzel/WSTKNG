using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace wstk_b.Models
{
    public class Chapter
    {
        public int ID { get; set; }
        public string Title { get; set; }
        public string URL { get; set; }
        public string Content { get; set; }
        public bool Crawled { get; set; }
        public bool Sent { get; set; }
        public DateTime Published { get; set; }
        public int SeriesID { get; set; }

        public Series Series { get; set; }
    }
}