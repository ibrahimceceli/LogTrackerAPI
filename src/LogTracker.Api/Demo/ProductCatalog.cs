namespace LogTracker.Api.Demo;

/// <summary>A product of the demo shop.</summary>
/// <param name="Id">Product id.</param>
/// <param name="Name">Display name.</param>
/// <param name="Category">Category name.</param>
/// <param name="Price">Unit price in euros.</param>
public sealed record Product(int Id, string Name, string Category, decimal Price);

/// <summary>Fixed list of sample products; the demo endpoints only exist to generate traffic.</summary>
public sealed class ProductCatalog
{
    private static readonly Product[] Products =
    [
        new(1, "Mechanical Keyboard", "Peripherals", 89.90m),
        new(2, "Wireless Mouse", "Peripherals", 34.50m),
        new(3, "27\" Monitor", "Displays", 249.00m),
        new(4, "USB-C Hub", "Accessories", 45.00m),
        new(5, "Laptop Stand", "Accessories", 39.90m),
        new(6, "Noise-Cancelling Headphones", "Audio", 199.00m),
        new(7, "Webcam 1080p", "Peripherals", 59.90m),
        new(8, "External SSD 1 TB", "Storage", 109.00m),
        new(9, "Desk Lamp", "Office", 29.90m),
        new(10, "Ergonomic Chair", "Office", 329.00m),
        new(11, "Standing Desk", "Office", 499.00m),
        new(12, "USB Microphone", "Audio", 119.00m),
        new(13, "Portable Speaker", "Audio", 79.00m),
        new(14, "Graphics Tablet", "Peripherals", 149.00m),
        new(15, "HDMI Cable 2 m", "Accessories", 12.90m),
        new(16, "Wi-Fi Router", "Networking", 139.00m),
        new(17, "Network Switch 8-Port", "Networking", 49.90m),
        new(18, "Memory Card 128 GB", "Storage", 24.90m),
        new(19, "Power Bank 20000 mAh", "Accessories", 44.90m),
        new(20, "Smart Watch", "Wearables", 229.00m),
    ];

    public IReadOnlyList<Product> GetAll() => Products;

    public Product? Find(int id) => Array.Find(Products, product => product.Id == id);
}
