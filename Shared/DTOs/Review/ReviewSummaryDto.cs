namespace Shared.DTOs.Review
{
    
        public class ReviewSummaryDto
        {
            public int ProductId { get; set; }
            public double AverageRating { get; set; }
            public int TotalReviews { get; set; }
            public Dictionary<int, int> RatingDistribution { get; set; } = new();
        }

}
