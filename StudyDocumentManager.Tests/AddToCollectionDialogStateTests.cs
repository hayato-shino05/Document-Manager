using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.ViewModels.Items;
using Xunit;

namespace StudyDocumentManager.Tests;

public class AddToCollectionDialogStateTests
{
    [Fact]
    public void ApplyFilter_UpdatesVisibleItemsAndCountText()
    {
        var items = new[]
        {
            new SelectableDocumentItem(new StudyDocument { Id = 1, Ten = "Toán cao cấp", MonHoc = "Học tập", Loai = "PDF" }),
            new SelectableDocumentItem(new StudyDocument { Id = 2, Ten = "Báo cáo tài chính", MonHoc = "Công việc", Loai = "Excel" })
        };

        var state = new AddToCollectionDialogState(items);

        state.ApplyFilter("toán");

        Assert.Single(state.VisibleItems);
        Assert.Equal("1 / 2 tài liệu", state.CountText);
    }

    [Fact]
    public void ToggleSelectAllVisible_SelectsVisibleItemsAndUpdatesFooter()
    {
        var items = new[]
        {
            new SelectableDocumentItem(new StudyDocument { Id = 1, Ten = "A" }),
            new SelectableDocumentItem(new StudyDocument { Id = 2, Ten = "B" })
        };

        var state = new AddToCollectionDialogState(items);
        state.ApplyFilter(string.Empty);

        state.ToggleSelectAllVisible();

        Assert.All(state.VisibleItems, item => Assert.True(item.IsSelected));
        Assert.Equal("2", state.SelectedCountText);
        Assert.True(state.CanConfirm);
        Assert.Equal("Bỏ chọn tất cả", state.SelectAllButtonText);
        Assert.True(state.HeaderCheckState);
    }
}
