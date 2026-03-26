using Eventizo.Data;
using Eventizo.Extensions;
using Eventizo.Models;
using Eventizo.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Net.payOS;
using Net.payOS.Types;

namespace Eventizo.Controllers
{
    [Authorize]
    public class ShoppingCartController : Controller
    {
        private readonly IProductRepository _productRepository;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;

        public ShoppingCartController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IProductRepository productRepository, IConfiguration configuration)
        {
            _productRepository = productRepository;
            _context = context;
            _userManager = userManager;
            _configuration = configuration;
        }

        public async Task<IActionResult> AddToCart(int productId, int quantity)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            var cart = _context.Carts.Include(c => c.Items).FirstOrDefault(c => c.UserId == user.Id);

            if (cart == null)
            {
                cart = new Cart { UserId = user.Id, Items = new List<CartItem>() };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            var existingItem = cart.Items.FirstOrDefault(ci => ci.ProductId == productId);

            int currentCartQuantity = existingItem?.Quantity ?? 0;
            if (currentCartQuantity + quantity > product.Quantity)
            {
                TempData["Error"] = "Không đủ sản phẩm trong kho.";
                return RedirectToAction("Index");
            }

            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                cart.Items.Add(new CartItem
                {
                    ProductId = productId,
                    Quantity = quantity,
                    Price = product.PriceReduced ?? product.Price
                });
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var cart = await _context.Carts
                .Include(c => c.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(c => c.UserId == user.Id);

            var items = cart?.Items.ToList() ?? new List<CartItem>();

            // Kiểm tra số lượng còn lại so với giỏ hàng
            foreach (var item in items)
            {
                if (item.Quantity > item.Product.Quantity)
                {
                    TempData[$"Error_{item.ProductId}"] = $"Sản phẩm '{item.Product.Name}' chỉ còn lại {item.Product.Quantity}.";
                }
            }

            var shoppingCart = new ShoppingCart
            {
                Items = items
            };

            return View(shoppingCart);
        }

        public async Task<IActionResult> RemoveFromCart(int productId)
        {
            var user = await _userManager.GetUserAsync(User);
            var cart = await _context.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == user.Id);

            if (cart != null)
            {
                var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
                if (item != null)
                {
                    _context.CartItems.Remove(item);
                    await _context.SaveChangesAsync();
                }
            }

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Index2(int? id, int? quantity)
        {
            var user = await _userManager.GetUserAsync(User);

            if (id.HasValue && quantity.HasValue)
            {
                var product = await _context.Products.FindAsync(id.Value);
                if (product == null) return NotFound();

                if (quantity > product.Quantity)
                {
                    TempData["Error"] = "Số lượng mua vượt quá số lượng tồn";
                    return RedirectToAction("ProductDisplay", "Home", new { id = id.Value });
                }

                var cart = new ShoppingCart
                {
                    Items = new List<CartItem>
                    {
                        new CartItem
                        {
                            ProductId = product.Id,
                            Quantity = quantity.Value,
                            Price = product.PriceReduced ?? product.Price,
                            Product = product
                        }
                    }
                };

                return View("Index2", cart);
            }

            var cartFromDb = await _context.Carts
                .Include(c => c.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(c => c.UserId == user.Id);

            var shoppingCart = new ShoppingCart
            {
                Items = cartFromDb?.Items.ToList() ?? new List<CartItem>()
            };

            return View("Index2", shoppingCart);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmOrder([Bind("Address,Phone,ProductId,Quantity")] Payment2 payment)
        {
            var product = await _context.Products.FindAsync(payment.ProductId);
            if (product == null) return NotFound();

            if (payment.Quantity > product.Quantity)
            {
                TempData["Error"] = "Số lượng mua vượt quá số lượng tồn";
                return RedirectToAction("Index2", new { id = product.Id, quantity = payment.Quantity });
            }

            var user = await _userManager.GetUserAsync(User);
            long orderCode = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); // <-- tạo trước

            payment.UserId = user.Id;
            payment.Product = product;
            payment.TotalAmount = payment.Quantity * (product.PriceReduced ?? product.Price);
            payment.CreatedAt = DateTime.Now;
            payment.OrderCode = orderCode;       // <-- gán OrderCode
            payment.OrderStatus = "PENDING";     // <-- khởi tạo PENDING

            product.Quantity -= payment.Quantity;
            if (product.Quantity <= 0)
                product.Status = "Hết vé";

            _context.Payment2s.Add(payment);
            await _context.SaveChangesAsync();

            // --- Gửi PayOS ---
            var payOS = new PayOS(_configuration["PayOS:ClientId"], _configuration["PayOS:ApiKey"], _configuration["PayOS:ChecksumKey"]);
            var items = new List<ItemData> { new ItemData(product.Name, payment.Quantity, (int)Math.Round(payment.TotalAmount)) };
            var paymentData = new PaymentData(
                orderCode: orderCode,
                amount: (int)Math.Round(payment.TotalAmount),
                description: $"{product.Name} - {payment.Quantity} sản phẩm",
                items: items,
                returnUrl: $"{_configuration["PayOS:ReturnUrl"]}?productId={product.Id}&orderCode={orderCode}",
                cancelUrl: $"{_configuration["PayOS:CancelUrl"]}?productId={product.Id}&orderCode={orderCode}"
            );

            var response = await payOS.createPaymentLink(paymentData);
            return Redirect(response.checkoutUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmOrderMultiple(List<CartItem> Items, string Address, string Phone, bool isFromCart)
        {
            var user = await _userManager.GetUserAsync(User);
            long orderCode = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); // <-- 1 orderCode cho cả giỏ
            decimal totalAmount = 0;
            var itemsData = new List<ItemData>();

            foreach (var item in Items)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product == null || item.Quantity > product.Quantity) continue;

                decimal productAmount = item.Quantity * (product.PriceReduced ?? product.Price);
                totalAmount += productAmount;

                itemsData.Add(new ItemData(product.Name, item.Quantity, (int)Math.Round(productAmount)));

                var payment = new Payment2
                {
                    UserId = user.Id,
                    ProductId = product.Id,
                    Quantity = item.Quantity,
                    TotalAmount = productAmount,
                    OrderStatus = "PENDING",
                    Address = Address,
                    Phone = Phone,
                    CreatedAt = DateTime.Now,
                    OrderCode = orderCode   // <-- dùng chung orderCode
                };

                _context.Payment2s.Add(payment);

                product.Quantity -= item.Quantity;
                if (product.Quantity <= 0)
                    product.Status = "Hết vé";
            }

            if (isFromCart)
            {
                var cart = await _context.Carts.FirstOrDefaultAsync(c => c.UserId == user.Id);
                if (cart != null)
                {
                    var cartItems = await _context.CartItems.Where(ci => ci.CartId == cart.Id).ToListAsync();
                    if (cartItems.Any())
                        _context.CartItems.RemoveRange(cartItems);
                }
            }

            await _context.SaveChangesAsync();

            // --- Gửi PayOS ---
            var payOS = new PayOS(_configuration["PayOS:ClientId"], _configuration["PayOS:ApiKey"], _configuration["PayOS:ChecksumKey"]);
            var paymentData = new PaymentData(
                orderCode: orderCode,
                amount: (int)Math.Round(totalAmount),
                description: "Thanh toán giỏ hàng",
                items: itemsData,
                returnUrl: $"{_configuration["PayOS:ReturnUrl"]}?orderCode={orderCode}",
                cancelUrl: $"{_configuration["PayOS:CancelUrl"]}?orderCode={orderCode}"
            );

            var response = await payOS.createPaymentLink(paymentData);
            return Redirect(response.checkoutUrl);
        }

        public async Task<IActionResult> MyOrders()
        {
            var user = await _userManager.GetUserAsync(User);
            var orders = await _context.Payment2s
                .Include(p => p.Product)
                .Where(p => p.UserId == user.Id)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        public IActionResult Success()
        {
            return View();
        }
    }
}
