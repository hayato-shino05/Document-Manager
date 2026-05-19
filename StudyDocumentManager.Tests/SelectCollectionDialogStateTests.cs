using StudyDocumentManager.ViewModels.Items;
using Xunit;

namespace StudyDocumentManager.Tests;

public class SelectCollectionDialogStateTests
{
    [Fact]
    public void Select_UpdatesSelectedIdLabelAndConfirmState()
    {
        var state = new SelectCollectionDialogState(
        [
            (1, "Môn Toán", 3),
            (2, "Báo cáo", 0)
        ]);

        state.Select(2);

        Assert.Equal(2, state.SelectedId);
        Assert.Equal("Đã chọn: Báo cáo", state.SelectedLabel);
        Assert.True(state.CanConfirm);
    }

    [Fact]
    public void BuildChipLabel_AppendsCountOnlyWhenPositive()
    {
        Assert.Equal("Môn Toán  (3)", SelectCollectionDialogState.BuildChipLabel("Môn Toán", 3));
        Assert.Equal("Báo cáo", SelectCollectionDialogState.BuildChipLabel("Báo cáo", 0));
    }
}
