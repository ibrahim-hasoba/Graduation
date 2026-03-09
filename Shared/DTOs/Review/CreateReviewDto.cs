using System;
using System.Collections.Generic;

namespace Shared.DTOs.Review
{
    
        public class CreateReviewDto
        {
            public int ProductId { get; set; }
            public int Rating { get; set; }       
            public string? Comment { get; set; }
        }
}
