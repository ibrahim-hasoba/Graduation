using AutoMapper;
using Graduation.BLL.DTOs.Cart;
using Graduation.BLL.DTOs.Category;
using Graduation.BLL.DTOs.Product;
using Graduation.DAL.Entities;

namespace Graduation.BLL
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<CartItem, CartItemDto>();
            CreateMap<CartItemVariant, CartItemVariantDto>();
            CreateMap<Product, ProductListDto>();
            CreateMap<ProductImage, ProductImageDto>();
            CreateMap<ProductVariant, ProductVariantDto>();
            CreateMap<Category, CategoryDto>();
            CreateMap<Category, CategoryHierarchyDto>();
        }
    }
}
