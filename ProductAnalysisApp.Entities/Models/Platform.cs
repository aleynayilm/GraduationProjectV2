using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Entities.Models
{
    public class Platform
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string PlatformId { get; set; }
        public string Name { get; set; }
        public string BaseUrl { get; set; }
        public List<string> ProductPlatformIds { get; set; } = new List<string>();
    }
}
