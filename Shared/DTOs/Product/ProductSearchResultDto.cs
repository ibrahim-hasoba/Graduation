using System;
using System.Collections.Generic;
using System.Text;

namespace Shared.DTOs.Product
{
    public class ProductSearchResultDto
    {
        public List<ProductListDto> Products { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasPreviousPage { get; set; }
        public bool HasNextPage { get; set; }
    }
}
