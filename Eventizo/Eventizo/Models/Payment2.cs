using System;
using System.ComponentModel.DataAnnotations;

namespace Eventizo.Models
{
    public class Payment2
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        public string Address { get; set; }
        public string Phone { get; set; }
        public int Quantity { get; set; }
        public decimal TotalAmount { get; set; }
        public string OrderStatus { get; set; } = "PENDING";
        public DateTime CreatedAt { get; set; }

        public long OrderCode { get; set; }
        public int ProductId { get; set; }
        public Product Product { get; set; }
        public DateTime? PaymentDate { get; set; }
    }
}