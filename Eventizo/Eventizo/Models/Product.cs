using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eventizo.Models
{
    public class Product
    {
        public int Id { get; set; }
        [Required, StringLength(100)]
        public string Name { get; set; }
        [Range(1000, 100000000, ErrorMessage = "Giá phải từ 1,000 VND đến 100,000,000 VND")]
        public decimal Price { get; set; }
        [Range(1000, 100000000, ErrorMessage = "Giá phải từ 1,000 VND đến 100,000,000 VND")]
        [BindProperty]
        public decimal? PriceReduced { get; set; }
        public string? Status { get; set; }
        public int Quantity { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public List<ProductImage>? Images { get; set; }
        public int CategoryId { get; set; }
        public Category? Category { get; set; }
        
        public decimal DiscountPercentage
        {
            get
            {
                if (Price > 0 && PriceReduced.HasValue)
                {
                    return Math.Floor(((Price - PriceReduced.Value) / Price) * 100);
                }
                return 0;
            }
        }

        public List<Payment2> Payments { get; set; } = new List<Payment2>();
    }

}
