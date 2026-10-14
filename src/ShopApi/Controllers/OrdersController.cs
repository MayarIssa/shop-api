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
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly ShopDbContext _db;

    public OrdersController(ShopDbContext db) => _db = db;

    /// <summary>
    /// Turn the cart into an order. Stock is checked and reduced, and the cart is emptied,
    /// inside one transaction: either all of it happens or none of it does.
    /// </summary>
    [HttpPost("checkout")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> Checkout(CheckoutRequest request)
    {
        var userId = User.GetUserId();

        await using var tx = await _db.Database.BeginTransactionAsync();

        var cart = await _db.CartItems.Include(ci => ci.Product)
            .Where(ci => ci.UserId == userId)
            .ToListAsync();
        if (cart.Count == 0)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Your cart is empty.");

        var problems = cart
            .Where(ci => !ci.Product!.IsActive || ci.Quantity > ci.Product.Stock)
            .Select(ci => ci.Product!.IsActive
                ? $"Only {ci.Product.Stock} of '{ci.Product.Name}' left in stock."
                : $"'{ci.Product.Name}' is no longer available.")
            .ToList();
        if (problems.Count > 0)
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Some items can't be ordered.",
                detail: string.Join(" ", problems));

        var order = new Order { UserId = userId, ShippingAddress = request.ShippingAddress.Trim() };
        foreach (var ci in cart)
        {
            ci.Product!.Stock -= ci.Quantity;
            order.Items.Add(new OrderItem
            {
                ProductId = ci.ProductId,
                ProductName = ci.Product.Name,
                UnitPrice = ci.Product.Price,
                Quantity = ci.Quantity,
            });
        }
        order.Total = order.Items.Sum(i => i.UnitPrice * i.Quantity);

        _db.Orders.Add(order);
        _db.CartItems.RemoveRange(cart);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, ToDto(order));
    }

    /// <summary>The signed-in user's orders, newest first.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetMine()
    {
        var userId = User.GetUserId();
        var orders = await _db.Orders.AsNoTracking().Include(o => o.Items)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
            .ToListAsync();
        return Ok(orders.Select(ToDto));
    }

    /// <summary>One order. Customers can only see their own; admins can see any.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDto>> GetById(int id)
    {
        var order = await _db.Orders.AsNoTracking().Include(o => o.Items).SingleOrDefaultAsync(o => o.Id == id);
        if (order is null || (order.UserId != User.GetUserId() && !User.IsInRole(Roles.Admin)))
            return NotFound();
        return Ok(ToDto(order));
    }

    /// <summary>Cancel a pending order and put its items back in stock.</summary>
    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<OrderDto>> Cancel(int id)
    {
        var order = await _db.Orders.Include(o => o.Items).ThenInclude(i => i.Product)
            .SingleOrDefaultAsync(o => o.Id == id);
        if (order is null || (order.UserId != User.GetUserId() && !User.IsInRole(Roles.Admin)))
            return NotFound();
        if (order.Status != OrderStatus.Pending)
            return Problem(statusCode: StatusCodes.Status409Conflict,
                title: $"Only pending orders can be cancelled. This one is {order.Status}.");

        foreach (var item in order.Items) item.Product!.Stock += item.Quantity;
        order.Status = OrderStatus.Cancelled;
        await _db.SaveChangesAsync();
        return Ok(ToDto(order));
    }

    // ---------- Admin ----------

    [Authorize(Roles = Roles.Admin)]
    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetAll([FromQuery] OrderStatus? status)
    {
        var query = _db.Orders.AsNoTracking().Include(o => o.Items).AsQueryable();
        if (status is OrderStatus s) query = query.Where(o => o.Status == s);
        var orders = await query.OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id).ToListAsync();
        return Ok(orders.Select(ToDto));
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<OrderDto>> UpdateStatus(int id, UpdateOrderStatusRequest request)
    {
        var order = await _db.Orders.Include(o => o.Items).SingleOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();

        if (order.Status == OrderStatus.Cancelled)
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "A cancelled order can't change status.");
        if (request.Status == OrderStatus.Cancelled)
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Use POST /api/orders/{id}/cancel so stock is restored.");

        order.Status = request.Status;
        await _db.SaveChangesAsync();
        return Ok(ToDto(order));
    }

    private static OrderDto ToDto(Order o) => new(
        o.Id, o.CreatedAt, o.Status.ToString(), o.Total, o.ShippingAddress,
        o.Items.Select(i => new OrderItemDto(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity,
            i.UnitPrice * i.Quantity)).ToList());
}
