using System.ComponentModel.DataAnnotations;

namespace WSTKNG.Models
{
    public class Template
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public string TocSelector { get; set; }
        public string TitleSelector { get; set; }
        public string ContentSelector { get; set; }

        public ICollection<Series> Series { get; set; }
    }
}