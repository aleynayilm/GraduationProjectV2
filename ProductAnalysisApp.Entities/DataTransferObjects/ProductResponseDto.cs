using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Entities.DataTransferObjects
{
    public class ProductResponseDto
    {
        public string ProductId { get; init; }
        public string Name { get; init; }
        public string Description { get; init; }
        public string ImageUrl { get; init; }
        public List<ProductPlatformResponseDto> Platforms { get; set; } = new List<ProductPlatformResponseDto>();
    }
}
