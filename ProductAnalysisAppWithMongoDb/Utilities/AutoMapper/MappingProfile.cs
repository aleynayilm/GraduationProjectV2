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
            // ProductForScrapingDto → Product
            CreateMap<ProductForScrapingDto, Product>()
                .ForMember(dest => dest.Name,
                    opt => opt.MapFrom(src => src.ProductName))
                .ForMember(dest => dest.ImageUrl,
                    opt => opt.MapFrom(src => src.ImageUrl))
                .ForMember(dest => dest.ProductPlatformIds,
                    opt => opt.Ignore())
                .ForMember(dest => dest.ProductId,
                    opt => opt.Ignore())
                .ForMember(dest => dest.CategoryId,
                    opt => opt.Ignore());

            // FavoriteDtoForCreate → Favorite
            CreateMap<FavoriteDtoForCreate, Favorite>()
                .ForMember(dest => dest.UserId,
                    opt => opt.MapFrom(src => src.FirebaseUid))
                .ForMember(dest => dest.FavoriteId,
                    opt => opt.Ignore())
                .ForMember(dest => dest.CreatedDate,
                    opt => opt.Ignore())
                .ForMember(dest => dest.Category,
                    opt => opt.Ignore());

            // ProductDtoForCreate → Product
            CreateMap<ProductDtoForCreate, Product>()
                .ForMember(dest => dest.ProductId,
                    opt => opt.Ignore())
                .ForMember(dest => dest.ProductPlatformIds,
                    opt => opt.Ignore());
        }
    }
}
