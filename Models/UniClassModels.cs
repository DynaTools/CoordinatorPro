using System.Collections.Generic;

namespace CoordinatorPro.Models
{
    public class UniClassItem
    {
        public string Code { get; set; }
        public string Title { get; set; }
        public string Parent { get; set; }
     public string Description { get; set; }
    }
    
    public class ClassificationResult
    {
        public string Code { get; set; }
        public int Confidence { get; set; }
        public string Source { get; set; }
        public List<string> Alternatives { get; set; } = new List<string>();
    }
}
