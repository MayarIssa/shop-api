using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopApi.Data;
using ShopApi.Dtos;
using ShopApi.Models;

namespace ShopApi.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly ShopDbContext _db;

    public CategoriesController(ShopDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetAll() =>
        Ok(await _db.Categories
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Description, c.Products.Count(p => p.IsActive)))
            .ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoryDto>> GetById(int id)
    {
        var dto = await _db.Categories
            .Where(c => c.Id == id)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Description, c.Products.Count(p => p.IsActive)))
            .SingleOrDefaultAsync();
        return dto is null ? NotFound() : Ok(dto);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create(CategoryRequest request)
    {
        if (await _db.Categories.AnyAsync(c => c.Name == request.Name))
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "A category with this name already exists.");

        var category = new Category { Name = request.Name.Trim(), Description = request.Description };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = category.Id },
            new CategoryDto(category.Id, category.Name, category.Description, 0));
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CategoryRequest request)
    {
        var category = await _db.Categories.FindAsync(id);
        if (category is null) return NotFound();

        if (await _db.Categories.AnyAsync(c => c.Name == request.Name && c.Id != id))
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "A category with this name already exists.");

        category.Name = request.Name.Trim();
        category.Description = request.Description;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories.FindAsync(id);
        if (category is null) return NotFound();

        if (await _db.Products.AnyAsync(p => p.CategoryId == id))
            return Problem(statusCode: StatusCodes.Status409Conflict,
                title: "This category still has products. Move or delete them first.");

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
