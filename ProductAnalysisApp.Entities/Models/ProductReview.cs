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
    public class ProductReview
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ReviewId { get; set; }
        [BsonRepresentation(BsonType.ObjectId)]
        public string ProductPlatformId { get; set; }
        public decimal? Rating { get; set; }
        public string? ReviewText { get; set; }
        public DateTime? ReviewDate { get; set; }
        public string? SentimentLabel { get; set; }
        public decimal? SentimentConfidence { get; set; }
        public string? KeyPhrases { get; set; }
        public DateTime ScrapedDate { get; set; } = DateTime.Now;

    }
}
