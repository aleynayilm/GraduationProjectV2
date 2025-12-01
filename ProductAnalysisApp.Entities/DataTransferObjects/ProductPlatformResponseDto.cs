using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Entities.DataTransferObjects
{
    public class ProductPlatformResponseDto
    {
        public string PlatformId { get; init; }
        public string PlatformName { get; init; }
        public string ProductUrl { get; init; }
        public decimal Price { get; init; }
        public string Currency { get; init; }
    }
}
