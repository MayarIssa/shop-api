using System.Net;
using System.Net.Http.Json;
using ShopApi.Dtos;
using ShopApi.Models;

namespace ShopApi.Tests;

public class AuthTests : IClassFixture<ShopApiFactory>
{
    private readonly ShopApiFactory _factory;
    public AuthTests(ShopApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_then_login_returns_a_token()
    {
        var client = _factory.CreateClient();
        var email = $"new-{Guid.NewGuid():N}@test.local";

        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "New User", "Password123"));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options);
        Assert.False(string.IsNullOrWhiteSpace(auth!.Token));
        Assert.Equal(Roles.Customer, auth.User.Role);
    }

    [Fact]
    public async Task Duplicate_email_is_rejected()
    {
        var client = _factory.CreateClient();
        var email = $"dup-{Guid.NewGuid():N}@test.local";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "First", "Password123"));

        var second = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Second", "Password123"));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_returns_401()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(ShopApiFactory.AdminEmail, "wrong-password"));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Short_password_fails_validation()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("short@test.local", "Shorty", "123"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}

public class CatalogTests : IClassFixture<ShopApiFactory>
{
    private readonly ShopApiFactory _factory;
    public CatalogTests(ShopApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Anyone_can_browse_seeded_products()
    {
        var client = _factory.CreateClient();
        var page = await client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/products?pageSize=50", TestJson.Options);
        Assert.NotNull(page);
        Assert.True(page!.TotalCount >= 6);
    }

    [Fact]
    public async Task Search_and_price_filter_narrow_results()
    {
        var client = _factory.CreateClient();
        var page = await client.GetFromJsonAsync<PagedResult<ProductDto>>(
            "/api/products?search=keyboard&minPrice=50&sortBy=price", TestJson.Options);
        Assert.All(page!.Items, p => Assert.True(p.Price >= 50));
        Assert.Contains(page.Items, p => p.Name.Contains("Keyboard"));
    }

    [Fact]
    public async Task Customer_cannot_create_products()
    {
        var client = await _factory.CreateCustomerClientAsync();
        var res = await client.PostAsJsonAsync("/api/products",
            new ProductRequest("Hacked", null, 1m, 1, null, 1));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Anonymous_user_cannot_create_products()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/products",
            new ProductRequest("Hacked", null, 1m, 1, null, 1));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Admin_can_create_update_and_delete_a_product()
    {
        var admin = await _factory.CreateAdminClientAsync();

        var created = await admin.PostAsJsonAsync("/api/products",
            new ProductRequest("Desk Lamp", "LED, dimmable", 24.5m, 10, null, 1));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var product = await created.Content.ReadFromJsonAsync<ProductDto>(TestJson.Options);

        var updated = await admin.PutAsJsonAsync($"/api/products/{product!.Id}",
            new ProductRequest("Desk Lamp Pro", "LED, dimmable", 29m, 8, null, 1));
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);

        var fetched = await admin.GetFromJsonAsync<ProductDto>($"/api/products/{product.Id}", TestJson.Options);
        Assert.Equal("Desk Lamp Pro", fetched!.Name);
        Assert.Equal(29m, fetched.Price);

        var deleted = await admin.DeleteAsync($"/api/products/{product.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/products/{product.Id}")).StatusCode);
    }

    [Fact]
    public async Task Creating_a_product_in_a_missing_category_fails()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var res = await admin.PostAsJsonAsync("/api/products",
            new ProductRequest("Orphan", null, 5m, 1, null, 9999));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}

public class CheckoutTests : IClassFixture<ShopApiFactory>
{
    private readonly ShopApiFactory _factory;
    public CheckoutTests(ShopApiFactory factory) => _factory = factory;

    private async Task<ProductDto> CreateProductAsync(int stock, decimal price)
    {
        var admin = await _factory.CreateAdminClientAsync();
        var res = await admin.PostAsJsonAsync("/api/products",
            new ProductRequest($"Test item {Guid.NewGuid():N}", null, price, stock, null, 1));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ProductDto>(TestJson.Options))!;
    }

    [Fact]
    public async Task Checkout_creates_an_order_reduces_stock_and_empties_cart()
    {
        var product = await CreateProductAsync(stock: 5, price: 10m);
        var customer = await _factory.CreateCustomerClientAsync();

        var add = await customer.PostAsJsonAsync("/api/cart/items", new AddToCartRequest(product.Id, 3));
        var cart = await add.Content.ReadFromJsonAsync<CartDto>(TestJson.Options);
        Assert.Equal(30m, cart!.Total);

        var checkout = await customer.PostAsJsonAsync("/api/orders/checkout", new CheckoutRequest("12 Fouad St, Alexandria"));
        Assert.Equal(HttpStatusCode.Created, checkout.StatusCode);
        var order = await checkout.Content.ReadFromJsonAsync<OrderDto>(TestJson.Options);
        Assert.Equal(30m, order!.Total);
        Assert.Equal("Pending", order.Status);

        var after = await customer.GetFromJsonAsync<ProductDto>($"/api/products/{product.Id}", TestJson.Options);
        Assert.Equal(2, after!.Stock);

        var emptyCart = await customer.GetFromJsonAsync<CartDto>("/api/cart", TestJson.Options);
        Assert.Empty(emptyCart!.Items);
    }

    [Fact]
    public async Task Cannot_add_more_than_stock_to_cart()
    {
        var product = await CreateProductAsync(stock: 2, price: 5m);
        var customer = await _factory.CreateCustomerClientAsync();

        var res = await customer.PostAsJsonAsync("/api/cart/items", new AddToCartRequest(product.Id, 3));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Checkout_with_empty_cart_fails()
    {
        var customer = await _factory.CreateCustomerClientAsync();
        var res = await customer.PostAsJsonAsync("/api/orders/checkout", new CheckoutRequest("12 Fouad St, Alexandria"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Cancelling_an_order_restores_stock()
    {
        var product = await CreateProductAsync(stock: 4, price: 8m);
        var customer = await _factory.CreateCustomerClientAsync();
        await customer.PostAsJsonAsync("/api/cart/items", new AddToCartRequest(product.Id, 4));
        var order = await (await customer.PostAsJsonAsync("/api/orders/checkout", new CheckoutRequest("12 Fouad St, Alexandria")))
            .Content.ReadFromJsonAsync<OrderDto>(TestJson.Options);

        var cancel = await customer.PostAsync($"/api/orders/{order!.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        var restored = await customer.GetFromJsonAsync<ProductDto>($"/api/products/{product.Id}", TestJson.Options);
        Assert.Equal(4, restored!.Stock);
    }

    [Fact]
    public async Task Customers_cannot_see_each_others_orders()
    {
        var product = await CreateProductAsync(stock: 3, price: 12m);
        var alice = await _factory.CreateCustomerClientAsync();
        await alice.PostAsJsonAsync("/api/cart/items", new AddToCartRequest(product.Id, 1));
        var order = await (await alice.PostAsJsonAsync("/api/orders/checkout", new CheckoutRequest("1 Alice Rd, Cairo")))
            .Content.ReadFromJsonAsync<OrderDto>(TestJson.Options);

        var bob = await _factory.CreateCustomerClientAsync();
        var res = await bob.GetAsync($"/api/orders/{order!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Admin_can_move_an_order_to_shipped()
    {
        var product = await CreateProductAsync(stock: 3, price: 15m);
        var customer = await _factory.CreateCustomerClientAsync();
        await customer.PostAsJsonAsync("/api/cart/items", new AddToCartRequest(product.Id, 1));
        var order = await (await customer.PostAsJsonAsync("/api/orders/checkout", new CheckoutRequest("5 Main St, Giza")))
            .Content.ReadFromJsonAsync<OrderDto>(TestJson.Options);

        var admin = await _factory.CreateAdminClientAsync();
        var res = await admin.PatchAsJsonAsync($"/api/orders/{order!.Id}/status",
            new UpdateOrderStatusRequest(OrderStatus.Shipped), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var updated = await res.Content.ReadFromJsonAsync<OrderDto>(TestJson.Options);
        Assert.Equal("Shipped", updated!.Status);
    }
}
