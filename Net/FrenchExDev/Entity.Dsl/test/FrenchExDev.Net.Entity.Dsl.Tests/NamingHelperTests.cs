using FrenchExDev.Net.Entity.Dsl.SourceGenerator.Lib;
using Xunit;

namespace FrenchExDev.Net.Entity.Dsl.Tests;

public class NamingHelperTests
{
    [Theory]
    [InlineData("Order", "Orders")]
    [InlineData("Customer", "Customers")]
    [InlineData("Address", "Addresses")]
    [InlineData("Category", "Categories")]
    [InlineData("Bus", "Buses")]
    [InlineData("Box", "Boxes")]
    [InlineData("Quiz", "Quizes")]
    [InlineData("Church", "Churches")]
    [InlineData("Dish", "Dishes")]
    [InlineData("Day", "Days")]
    [InlineData("Key", "Keys")]
    public void Pluralize(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.Pluralize(input));
    }

    [Theory]
    [InlineData("OrderNumber", "order_number")]
    [InlineData("ID", "id")]
    [InlineData("Id", "id")]
    [InlineData("CreatedAt", "created_at")]
    [InlineData("HTMLParser", "html_parser")]
    public void ToSnakeCase(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.ToSnakeCase(input));
    }

    [Theory]
    [InlineData("OrderNumber", "orderNumber")]
    [InlineData("Id", "id")]
    [InlineData("Name", "name")]
    public void ToCamelCase(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.ToCamelCase(input));
    }
}
