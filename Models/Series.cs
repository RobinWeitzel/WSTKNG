using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace wstk_b.Models
{
    public class Series {
        public int ID { get; set; }
        public string Name { get; set; }
        public string AuthorName { get; set; }
        public string CoverImageUrl { get; set; }
        public string TocUrl { get; set; }
        public string TocSelector { get; set; }
        public string TitleSelector { get; set; }
        public string ContentSelector { get; set; }
        public string Status { get; set; }

        public Template Template { get; set; }
        public ICollection<Chapter> Chapters { get; set; }
    }
}