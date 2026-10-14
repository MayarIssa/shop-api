using System.ComponentModel.DataAnnotations;
using ShopApi.Models;

namespace ShopApi.Dtos;

// ---------- Auth ----------

public record RegisterRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(2), MaxLength(100)] string FullName,
    [Required, MinLength(8), MaxLength(100)] string Password);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record AuthResponse(string Token, DateTime ExpiresAt, UserDto User);

public record UserDto(int Id, string Email, string FullName, string Role);

// ---------- Catalog ----------

public record CategoryDto(int Id, string Name, string? Description, int ProductCount);

public record CategoryRequest(
    [Required, MinLength(2), MaxLength(100)] string Name,
    [MaxLength(500)] string? Description);

public record ProductDto(
    int Id, string Name, string? Description, decimal Price, int Stock,
    string? ImageUrl, bool IsActive, int CategoryId, string CategoryName);

public record ProductRequest(
    [Required, MinLength(2), MaxLength(200)] string Name,
    [MaxLength(2000)] string? Description,
    [Range(0.01, 1_000_000)] decimal Price,
    [Range(0, 1_000_000)] int Stock,
    [Url] string? ImageUrl,
    [Range(1, int.MaxValue)] int CategoryId,
    bool IsActive = true);

public class ProductQuery
{
    /// <summary>Matches product name or description.</summary>
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }

    /// <summary>name, price, price_desc or newest (default).</summary>
    public string? SortBy { get; set; }

    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

// ---------- Cart ----------

public record AddToCartRequest(
    [Range(1, int.MaxValue)] int ProductId,
    [Range(1, 100)] int Quantity);

public record UpdateCartItemRequest([Range(1, 100)] int Quantity);

public record CartItemDto(int ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal);

public record CartDto(IReadOnlyList<CartItemDto> Items, decimal Total);

// ---------- Orders ----------

public record CheckoutRequest([Required, MinLength(5), MaxLength(500)] string ShippingAddress);

public record OrderItemDto(int ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal);

public record OrderDto(
    int Id, DateTime CreatedAt, string Status, decimal Total,
    string ShippingAddress, IReadOnlyList<OrderItemDto> Items);

public record UpdateOrderStatusRequest([Required] OrderStatus Status);
