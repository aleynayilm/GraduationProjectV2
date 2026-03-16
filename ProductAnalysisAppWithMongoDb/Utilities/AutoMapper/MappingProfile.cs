using AutoMapper;
using ProductAnalysisApp.Entities.DataTransferObjects;
using ProductAnalysisApp.Entities.Models;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ProductAnalysisAppWithMongoDb.Utilities.AutoMapper
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<ProductForScrapingDto, Product>();
            CreateMap<FavoriteDtoForCreate, Favorite>();
            CreateMap<ProductDtoForCreate, Product>();
        }
    }
}
