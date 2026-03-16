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
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        [BsonElement("firebaseUid")]
        public string FirebaseUid { get; set; }
        [BsonElement("firstName")]
        public string FirstName { get; set; }

        [BsonElement("lastName")]
        public string LastName { get; set; }
        [BsonElement("email")]
        public string Email { get; set; }

        [BsonElement("priceAlertEnabled")]
        public bool PriceAlertEnabled { get; set; }

        [BsonElement("priceRange")]
        public decimal PriceRange { get; set; }

        [BsonElement("profileImageUrl")]
        public string? ProfileImageUrl { get; set; }
        [BsonElement("pushToken")]                      
        public string? PushToken { get; set; }

        [BsonElement("priceCheckIntervalHours")]        
        [BsonDefaultValue(24)]                           
        public int PriceCheckIntervalHours { get; set; } = 24;
        [BsonElement("createdDate")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [BsonElement("favoriteIds")]
        public List<string> FavoriteIds { get; set; } = new List<string>();

        [BsonElement("searchHistoryIds")]
        public List<string> SearchHistoryIds { get; set; } = new List<string>();
    }
}
