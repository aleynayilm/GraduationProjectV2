using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Entities.DataTransferObjects
{
    public class ProductDtoForCreate
    {
        public string CategoryId { get; init; }
        public string Name { get; init; }
        public string Description { get; init; }
        public string ImageUrl { get; init; }
        public DateTime ScrapedDate { get; init; } = DateTime.Now;
    }
}
