using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopApi.Data;
using ShopApi.Dtos;
using ShopApi.Models;

namespace ShopApi.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly ShopDbContext _db;

    public ProductsController(ShopDbContext db) => _db = db;

    /// <summary>Browse the catalog with search, filters, sorting and paging.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetAll([FromQuery] ProductQuery q)
    {
        var query = _db.Products.AsNoTracking().Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            // LIKE is case-insensitive on SQLite and on SQL Server's default collation.
            var pattern = $"%{q.Search.Trim()}%";
            query = query.Where(p => EF.Functions.Like(p.Name, pattern)
                || (p.Description != null && EF.Functions.Like(p.Description, pattern)));
        }
        if (q.CategoryId is int categoryId) query = query.Where(p => p.CategoryId == categoryId);
        // SQLite has no native decimal type, so price comparisons and sorting convert to double.
        // On SQL Server the cast is harmless.
        if (q.MinPrice is decimal min) { var m = (double)min; query = query.Where(p => (double)p.Price >= m); }
        if (q.MaxPrice is decimal max) { var m = (double)max; query = query.Where(p => (double)p.Price <= m); }

        query = q.SortBy?.ToLowerInvariant() switch
        {
            "name" => query.OrderBy(p => p.Name),
            "price" => query.OrderBy(p => (double)p.Price),
            "price_desc" => query.OrderByDescending(p => (double)p.Price),
            _ => query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id),
        };

        var total = await query.CountAsync();
        var items = await query
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .Select(p => new ProductDto(p.Id, p.Name, p.Description, p.Price, p.Stock, p.ImageUrl,
                p.IsActive, p.CategoryId, p.Category!.Name))
            .ToListAsync();

        return Ok(new PagedResult<ProductDto>(items, q.Page, q.PageSize, total));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductDto>> GetById(int id)
    {
        var dto = await _db.Products.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProductDto(p.Id, p.Name, p.Description, p.Price, p.Stock, p.ImageUrl,
                p.IsActive, p.CategoryId, p.Category!.Name))
            .SingleOrDefaultAsync();
        return dto is null ? NotFound() : Ok(dto);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(ProductRequest request)
    {
        var category = await _db.Categories.FindAsync(request.CategoryId);
        if (category is null)
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(request.CategoryId)] = new[] { "Category does not exist." }
            }));

        var product = new Product
        {
            Name = request.Name.Trim(),
            Description = request.Description,
            Price = request.Price,
            Stock = request.Stock,
            ImageUrl = request.ImageUrl,
            IsActive = request.IsActive,
            CategoryId = category.Id,
        };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = product.Id },
            new ProductDto(product.Id, product.Name, product.Description, product.Price, product.Stock,
                product.ImageUrl, product.IsActive, category.Id, category.Name));
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ProductRequest request)
    {
        var product = await _db.Products.FindAsync(id);
        if (product is null) return NotFound();

        if (!await _db.Categories.AnyAsync(c => c.Id == request.CategoryId))
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(request.CategoryId)] = new[] { "Category does not exist." }
            }));

        product.Name = request.Name.Trim();
        product.Description = request.Description;
        product.Price = request.Price;
        product.Stock = request.Stock;
        product.ImageUrl = request.ImageUrl;
        product.IsActive = request.IsActive;
        product.CategoryId = request.CategoryId;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Products that appear in past orders are deactivated instead of deleted,
    /// so order history keeps pointing at a real row.
    /// </summary>
    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product is null) return NotFound();

        if (await _db.OrderItems.AnyAsync(oi => oi.ProductId == id))
        {
            product.IsActive = false;
        }
        else
        {
            _db.CartItems.RemoveRange(_db.CartItems.Where(ci => ci.ProductId == id));
            _db.Products.Remove(product);
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }
}
