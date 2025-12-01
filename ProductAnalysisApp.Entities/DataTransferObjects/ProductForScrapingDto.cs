using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Entities.DataTransferObjects
{
    public class ProductForScrapingDto
    {
        public string? PlatformName { get; init; }
        public string? ProductName { get; init; }
        public string? Description { get; init; }
        public string? ProductUrl { get; init; }
        public string? Price { get; init; }
        public string? Currency { get; init; }
        public string? ImageUrl { get; init; }
        public DateTime? ScrapedDate { get; init; } = DateTime.Now;
    }
}
