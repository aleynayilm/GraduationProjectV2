using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Entities.DataTransferObjects
{
    public class FavoriteDetailDto
    {
        public string FavoriteId { get; set; } = "";
        public string ProductPlatformId { get; set; } = "";
        public string ProductUrl { get; set; } = "";
        public decimal Price { get; set; }
        public string Currency { get; set; } = "TRY";
        public string? Category { get; set; }        
        public DateTime? LastPriceCheckedAt { get; set; }  
        public DateTime CreatedDate { get; set; }
    }
}
