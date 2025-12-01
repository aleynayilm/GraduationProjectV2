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
    public class Category
    {

        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string CategoryId { get; set; }
        public string ParentCategory { get; set; }
        public string CategoryName { get; set; }
        public int RefreshInterval { get; set; }
        public List<string> ProductIds { get; set; } = new List<string>();
    }
}
