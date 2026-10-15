using DBTool.Core;
using DBTool.IO;
using DBTool.UI;
using Xunit;

namespace DBTool.UI.Tests;

public sealed class PaletteSelectionTests
{
    // Isolate button gating from workbook parsing and the CAD host.
    private static MainViewModel Ready(params string[] names)
    {
        var vm = new MainViewModel();
        var specs = names.Select(name => new NormalizedManholeSpec { Name = name }).ToList();
        var result = new ExcelWorkbookReader.ReadResult(specs, [], new(), new(), new(), ManholeType.GaThamLienKhoi);
        typeof(MainViewModel).GetProperty(nameof(MainViewModel.CurrentReadResult))!.SetValue(vm, result);
        foreach (var spec in specs) vm.Manholes.Add(new ManholeItemViewModel(spec));
        return vm;
    }

    [Fact]
    public void Multiple_checked_manholes_enable_generation() => Assert.True(Ready("G1", "G2").CanGenerate);

    [Fact]
    public void Unchecked_manholes_disable_generation()
    {
        var vm = Ready("G1"); vm.SelectAll(false);
        Assert.False(vm.CanGenerate);
    }

    [Fact]
    public void Blocking_error_on_unselected_manholes_does_not_lock_valid_selection()
    {
        var vm = Ready("G1", "G2"); vm.Manholes[1].IsSelected = false;
        vm.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Block, "G2",
            new SourceRef("fixture.xlsx", "GaThamLienKhoi", "B5", "G2"), "Invalid G2"));
        Assert.True(vm.CanGenerate);
    }

    [Fact]
    public void Global_block_and_selected_block_still_disable_generation()
    {
        var vm = Ready("G1");
        vm.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Block, "G1", null, "Invalid G1"));
        Assert.False(vm.CanGenerate);
        vm.Diagnostics.Clear();
        vm.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Block, "", null, "Missing catalog"));
        Assert.False(vm.CanGenerate);
    }

    [Fact]
    public void Insertion_point_changes_do_not_invalidate_workbook_snapshot()
    {
        var vm = Ready("G1"); vm.BaseX = 123; vm.BaseY = -456;
        Assert.True(vm.CanGenerate);
    }

    [Fact]
    public void Busy_and_changed_profile_disable_generation()
    {
        var vm = Ready("G1"); vm.IsBusy = true;
        Assert.False(vm.CanGenerate);
        vm.IsBusy = false; Assert.True(vm.CanGenerate);
        vm.SelectedProfile = "GaThamNapRoi";
        Assert.False(vm.CanGenerate);
    }

    [Fact]
    public void Added_items_notify_button_when_their_selection_changes()
    {
        var vm = Ready("G1");
        var notifications = new List<string?>();
        vm.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
        vm.Manholes[0].IsSelected = false;
        Assert.Contains(nameof(MainViewModel.CanGenerate), notifications);
        Assert.Contains(nameof(MainViewModel.SelectedManholeIds), notifications);
    }

    [Fact]
    public void Highlighted_row_is_used_when_no_checkmarks_remain()
    {
        var vm = Ready("G1", "G2"); vm.SelectAll(false);
        vm.ActiveManhole = vm.Manholes[1];
        Assert.True(vm.CanGenerate);
        Assert.Equal("G2", Assert.Single(vm.GetSelectedSpecs()).Name);
        vm.Manholes[0].IsSelected = true;
        Assert.Equal("G1", Assert.Single(vm.GetSelectedSpecs()).Name);
    }
}
