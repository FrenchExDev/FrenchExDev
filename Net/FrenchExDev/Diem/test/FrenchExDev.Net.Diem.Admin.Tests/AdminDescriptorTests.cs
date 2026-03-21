using FrenchExDev.Net.Diem.Admin.Actions;
using FrenchExDev.Net.Diem.Admin.Forms;
using FrenchExDev.Net.Diem.Admin.Lists;
using Xunit;

namespace FrenchExDev.Net.Diem.Admin.Tests;

public class AdminDescriptorTests
{
    [Fact]
    public void AdminModuleDescriptor_CanBeCreated()
    {
        var descriptor = new AdminModuleDescriptor
        {
            Name = "Products",
            AggregateType = typeof(string),
            Icon = "box",
            Group = "Content",
            PageSize = 50,
        };

        Assert.Equal("Products", descriptor.Name);
        Assert.Equal(typeof(string), descriptor.AggregateType);
        Assert.Equal("box", descriptor.Icon);
        Assert.Equal("Content", descriptor.Group);
        Assert.Equal(50, descriptor.PageSize);
    }

    [Fact]
    public void AdminModuleDescriptor_DefaultPageSizeIs25()
    {
        var descriptor = new AdminModuleDescriptor
        {
            Name = "Users",
            AggregateType = typeof(object),
        };

        Assert.Equal(25, descriptor.PageSize);
        Assert.Empty(descriptor.Filters);
    }

    [Fact]
    public void AdminFieldDescriptor_CanBeCreated()
    {
        var descriptor = new AdminFieldDescriptor
        {
            Name = "Title",
            DisplayType = "TextInput",
            DisplayName = "Article Title",
            ReadOnly = true,
            HideInList = false,
            HideInForm = true,
            Order = 3,
        };

        Assert.Equal("Title", descriptor.Name);
        Assert.Equal("TextInput", descriptor.DisplayType);
        Assert.Equal("Article Title", descriptor.DisplayName);
        Assert.True(descriptor.ReadOnly);
        Assert.False(descriptor.HideInList);
        Assert.True(descriptor.HideInForm);
        Assert.Equal(3, descriptor.Order);
    }

    [Fact]
    public void AdminActionDescriptor_CanBeCreated()
    {
        var descriptor = new AdminActionDescriptor
        {
            Name = "Publish",
            Command = "PublishArticleCommand",
            Icon = "send",
            ConfirmationMessage = "Are you sure?",
            RequiresRole = "Editor",
        };

        Assert.Equal("Publish", descriptor.Name);
        Assert.Equal("PublishArticleCommand", descriptor.Command);
        Assert.Equal("send", descriptor.Icon);
        Assert.Equal("Are you sure?", descriptor.ConfirmationMessage);
        Assert.Equal("Editor", descriptor.RequiresRole);
    }
}
