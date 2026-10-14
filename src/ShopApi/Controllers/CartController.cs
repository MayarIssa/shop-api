using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopApi.Data;
using ShopApi.Dtos;
using ShopApi.Extensions;
using ShopApi.Models;

namespace ShopApi.Controllers;

[ApiController]
[Authorize]
[Route("api/cart")]
public class CartController : ControllerBase
{
    private readonly ShopDbContext _db;

    public CartController(ShopDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<CartDto>> Get() => Ok(await LoadCartAsync(User.GetUserId()));

    /// <summary>Add a product to the cart, or increase its quantity if it's already there.</summary>
    [HttpPost("items")]
    public async Task<ActionResult<CartDto>> AddItem(AddToCartRequest request)
    {
        var userId = User.GetUserId();
        var product = await _db.Products.FindAsync(request.ProductId);
        if (product is null || !product.IsActive) return NotFound(new { title = "Product not found." });

        var item = await _db.CartItems.SingleOrDefaultAsync(ci => ci.UserId == userId && ci.ProductId == product.Id);
        var newQuantity = (item?.Quantity ?? 0) + request.Quantity;
        if (newQuantity > product.Stock)
            return Problem(statusCode: StatusCodes.Status409Conflict,
                title: $"Only {product.Stock} of '{product.Name}' left in stock.");

        if (item is null)
            _db.CartItems.Add(new CartItem { UserId = userId, ProductId = product.Id, Quantity = request.Quantity });
        else
            item.Quantity = newQuantity;

        await _db.SaveChangesAsync();
        return Ok(await LoadCartAsync(userId));
    }

    [HttpPut("items/{productId:int}")]
    public async Task<ActionResult<CartDto>> UpdateItem(int productId, UpdateCartItemRequest request)
    {
        var userId = User.GetUserId();
        var item = await _db.CartItems.Include(ci => ci.Product)
            .SingleOrDefaultAsync(ci => ci.UserId == userId && ci.ProductId == productId);
        if (item is null) return NotFound();

        if (request.Quantity > item.Product!.Stock)
            return Problem(statusCode: StatusCodes.Status409Conflict,
                title: $"Only {item.Product.Stock} of '{item.Product.Name}' left in stock.");

        item.Quantity = request.Quantity;
        await _db.SaveChangesAsync();
        return Ok(await LoadCartAsync(userId));
    }

    [HttpDelete("items/{productId:int}")]
    public async Task<ActionResult<CartDto>> RemoveItem(int productId)
    {
        var userId = User.GetUserId();
        var item = await _db.CartItems.SingleOrDefaultAsync(ci => ci.UserId == userId && ci.ProductId == productId);
        if (item is null) return NotFound();

        _db.CartItems.Remove(item);
        await _db.SaveChangesAsync();
        return Ok(await LoadCartAsync(userId));
    }

    [HttpDelete]
    public async Task<IActionResult> Clear()
    {
        var userId = User.GetUserId();
        _db.CartItems.RemoveRange(_db.CartItems.Where(ci => ci.UserId == userId));
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<CartDto> LoadCartAsync(int userId)
    {
        var rows = await _db.CartItems.AsNoTracking()
            .Where(ci => ci.UserId == userId)
            .OrderBy(ci => ci.Id)
            .Select(ci => new { ci.ProductId, ci.Product!.Name, ci.Product.Price, ci.Quantity })
            .ToListAsync();

        // Totals are calculated in C#, not SQL, so decimal math stays exact on every database.
        var items = rows
            .Select(r => new CartItemDto(r.ProductId, r.Name, r.Price, r.Quantity, r.Price * r.Quantity))
            .ToList();
        return new CartDto(items, items.Sum(i => i.LineTotal));
    }
}
