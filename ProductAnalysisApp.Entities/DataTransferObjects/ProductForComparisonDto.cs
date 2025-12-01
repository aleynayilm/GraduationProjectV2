using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Entities.DataTransferObjects
{
    public class ProductForComparisonDto
    {
        [JsonPropertyName("name")]
        public string Name { get; init; }
        [JsonPropertyName("price")]
        public string Price { get; init; }
        [JsonPropertyName("description")]
        public string Description { get; init; }
        [JsonPropertyName("platform")]
        public string Platform { get; init; }
    }
}
