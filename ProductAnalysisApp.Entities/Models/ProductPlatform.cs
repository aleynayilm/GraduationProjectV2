using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Entities.Models
{
    public class ProductPlatform
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ProductPlatformId { get; set; }
        [BsonRepresentation(BsonType.ObjectId)]
        public string ProductId { get; set; }
        [BsonRepresentation(BsonType.ObjectId)]
        public string PlatformId { get; set; }
        public string ProductUrl { get; set; }
        [BsonRepresentation(BsonType.Decimal128)]
        public decimal Price { get; set; }
        public string Currency { get; set; }
        public List<string> ProductReviewIds { get; set; } = new List<string>();
        public List<string> FavoriteIds { get; set; } = new List<string>();
        [BsonElement("lastPriceCheckedAt")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? LastPriceCheckedAt { get; set; }

    }
}
