using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Graduation.DAL.Entities
{
    public class ProductVariant
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }

        [ForeignKey(nameof(ProductId))]
        public Product Product { get; set; } = null!;


        [Required]
        [MaxLength(50)]
        public string TypeName { get; set; } = string.Empty;


        [Required]
        [MaxLength(100)]
        public string Value { get; set; } = string.Empty;


        [MaxLength(10)]
        public string? ColorHex { get; set; }


        [Column(TypeName = "decimal(18,2)")]
        public decimal PriceAdjustment { get; set; } = 0;


        public int? StockQuantity { get; set; }


        public int DisplayOrder { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
