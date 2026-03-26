using Eventizo.Controllers;
using Eventizo.Models;
using Eventizo.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Newtonsoft.Json;
using Eventizo.Data;

namespace Eventizo.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Role_Admin)]
    public class ProductController : Controller
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HomeController> _logger;

        public ProductController(ILogger<HomeController> logger, IProductRepository productRepository, ICategoryRepository categoryRepository, ApplicationDbContext context)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _logger = logger;
            _context = context;
        }

        // Hiển thị danh sách sản phẩm
        public async Task<IActionResult> Index()
        {
            await UpdateStatusAsync(); // Cập nhật trạng thái trước khi hiển thị danh sách
            var products = await _productRepository.GetAllAsync();
            return View(products);
        }

        private async Task UpdateStatusAsync()
        {
            var products = await _productRepository.GetAllAsync();

            foreach (var product in products)
            {
                string status = product.Quantity > 0 ? "Còn hàng" : "Hết hàng";

                if (product.Status != status)
                {
                    product.Status = status;
                    await _productRepository.UpdateStatusAsync(product.Id, status);
                }
            }
        }

        // Hiển thị form thêm sản phẩm mới
        public async Task<IActionResult> Add()
        {
            var categories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name");

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Add(Product product, IFormFile imageUrl, List<IFormFile> images, List<string> ticketlist)
        {
            if (ModelState.IsValid)
            {
                
                // ✅ Xử lý giá giảm (PriceReduced)
                if (product.PriceReduced == null || product.PriceReduced <= 0 || product.PriceReduced >= product.Price)
                {
                    product.PriceReduced = null; // Không hiển thị giá giảm nếu không hợp lệ
                }

                // ✅ Xử lý ảnh chính (Product Image)
                if (imageUrl != null)
                {
                    product.ImageUrl = await SaveImage(imageUrl);
                }

                // ✅ Xử lý danh sách ảnh bổ sung (Additional Images)
                if (images != null && images.Count > 0)
                {
                    product.Images = new List<ProductImage>();
                    foreach (var image in images)
                    {
                        var imageUrlPath = await SaveImage(image);
                        product.Images.Add(new ProductImage { Url = imageUrlPath });
                    }
                }

                await _productRepository.AddAsync(product);
                return RedirectToAction(nameof(Index));
            }

            var categories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name");
            return View(product);
        }

        private async Task<string> SaveImage(IFormFile image)
        {
            //Thay đổi đường dẫn theo cấu hình của bạn
            var savePath = Path.Combine("wwwroot/images", image.FileName);
            using (var fileStream = new FileStream(savePath, FileMode.Create))
            {
                await image.CopyToAsync(fileStream);
            }
            return "/images/" + image.FileName; // Trả về đường dẫn tương đối
        }

        // Hiển thị thông tin chi tiết sản phẩm
        public async Task<IActionResult> Display(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }
            return View(product);
        }

        // Hiển thị form cập nhật sản phẩm
        public async Task<IActionResult> Update(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }
            var categories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        [HttpPost]
        public async Task<IActionResult> Update(int id, Product product, IFormFile imageUrl)
        {

            ModelState.Remove("ImageUrl"); // Loại bỏ xác thực ModelState cho ImageUrl

            if (id != product.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var existingProduct = await _productRepository.GetByIdAsync(id);
                if (existingProduct == null)
                {
                    return NotFound();
                }

                // ✅ Cập nhật ảnh nếu có
                if (imageUrl != null && imageUrl.Length > 0)
                {
                    var savedImageUrl = await SaveImage(imageUrl);
                    if (!string.IsNullOrEmpty(savedImageUrl))
                    {
                        existingProduct.ImageUrl = savedImageUrl;
                    }
                    else
                    {
                        ModelState.AddModelError("ImageUrl", "Lỗi khi lưu ảnh.");
                        return View(product);
                    }
                }

                // ✅ Cập nhật thông tin sản phẩm
                existingProduct.Name = product.Name;
                existingProduct.Price = product.Price;
                existingProduct.PriceReduced = product.PriceReduced;
                existingProduct.Description = product.Description;
                existingProduct.CategoryId = product.CategoryId;
                existingProduct.Quantity = product.Quantity;
                existingProduct.Status = product.Status;

                // ✅ Ghi log để kiểm tra dữ liệu trước khi lưu
                Console.WriteLine($"Updating Product ID: {existingProduct.Id}");
                
                // ✅ Lưu thay đổi vào database
                await _productRepository.UpdateAsync(existingProduct);

                return RedirectToAction(nameof(Index));
            }

            // ✅ Nếu có lỗi, hiển thị danh sách danh mục
            var categories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name");
            return View(product);
        }


        // Hiển thị form xác nhận xóa sản phẩm
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }
            return View(product);
        }

        // Xử lý xóa sản phẩm
        [HttpPost, ActionName("DeleteConfirmed")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _productRepository.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }


        public async Task<IActionResult> Details(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }
            return View(product);
        }

        public async Task<IActionResult> Sold()
        {
            var soldItems = await _context.Payment2s
                .Include(p => p.Product)
                .Include(p => p.User)
                .Where(p => p.Product != null && p.User != null) 
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(soldItems);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, string status)
        {
            var order = await _context.Payment2s.FindAsync(orderId);
            if (order != null)
            {
                order.OrderStatus = status;
                _context.Payment2s.Update(order);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Sold");
        }

    }
}
