using System.Collections.Generic;
using System.Linq;

namespace Eventizo.Models
{
    public class ShoppingCart
    {
        public List<CartItem> Items { get; set; } = new List<CartItem>();

        public void AddItem(CartItem item)
        {
            var existingItem = Items.FirstOrDefault(i => i.ProductId == item.ProductId);
            if (existingItem != null)
            {
                existingItem.Quantity += item.Quantity;
            }
            else
            {
                Items.Add(item);
            }
        }

        public void RemoveItem(int productId)
        {
            Items.RemoveAll(i => i.ProductId == productId);
        }

        public void Clear()
        {
            Items.Clear();
        }

        public int TotalQuantity => Items.Sum(i => i.Quantity);

        public decimal TotalAmount => Items.Sum(i => i.Price * i.Quantity);
    }
}
