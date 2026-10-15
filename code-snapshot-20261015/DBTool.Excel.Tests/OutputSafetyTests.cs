using DBTool.Infrastructure.Excel;
using Xunit;

namespace DBTool.Excel.Tests;

public sealed class OutputSafetyTests
{
    [Fact]
    public void SanitizedCollisionsReservedNamesAndLongNamesRemainUnique()
    {
        var requests = new[] {
            new SheetNameRequest("A", "a/b"), new SheetNameRequest("B", "a_b"),
            new SheetNameRequest("C", "TH"), new SheetNameRequest("D", "Temp"),
            new SheetNameRequest("G", "Steel"),
            new SheetNameRequest("E", new string('X',40)), new SheetNameRequest("F", "O'Brien") };
        var result = SheetNameAllocator.AllocateSheetNames(requests, new[] { "Summary" });
        Assert.True(result.Success);
        var map = result.Value!;
        Assert.Equal("a_b", map.Names["A"]);
        Assert.Equal("a_b__2", map.Names["B"]);
        Assert.All(map.Names.Values, name => Assert.InRange(name.Length, 1, 31));
        Assert.DoesNotContain(map.Names.Values, name => name.Equals("TH", StringComparison.OrdinalIgnoreCase) || name.Equals("Temp", StringComparison.OrdinalIgnoreCase) || name.Equals("Steel", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("'O''Brien'!A1", SheetNameAllocator.FormulaReference(map.Names["F"], "A1"));
    }

    [Fact]
    public void ReservationNeverOverwritesExistingFinalOrRaceWinner()
    {
        string dir = Path.Combine(Path.GetTempPath(),"dbtool-t34-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var request = new OutputNameRequest(dir,"Result",".xlsx","run1");
            var first = OutputFileNamer.ReserveNewFile(request).Value!;
            Assert.False(File.Exists(first.Path));
            Assert.True(File.Exists(first.ReservationPath));
            File.WriteAllBytes(first.Path,"ABC"u8.ToArray());
            var staging = Path.Combine(dir,"staging.xlsx");
            File.WriteAllBytes(staging,"NEW"u8.ToArray());
            var collision = OutputFileNamer.FinalizeReservation(first, staging);
            Assert.False(collision.Success);
            Assert.Null(collision.Value);
            Assert.Equal("ABC", File.ReadAllText(first.Path));
            var second = OutputFileNamer.ReserveNewFile(request).Value!;
            Assert.Contains("-r002", second.Path);
            var saved = OutputFileNamer.FinalizeReservation(second, staging);
            Assert.True(saved.Success);
            Assert.Equal("ABC", File.ReadAllText(first.Path));
            Assert.Equal("NEW", File.ReadAllText(second.Path));
        }
        finally { Directory.Delete(dir,true); }
    }

    [Fact]
    public void UnsafeExtensionAndBlankNameDoNotEscapeOutputDirectory()
    {
        string dir = Path.Combine(Path.GetTempPath(),"dbtool-t34-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var result = OutputFileNamer.ReserveNewFile(new OutputNameRequest(dir,"../../", ".xlsx","run"));
            Assert.True(result.Success);
            Assert.StartsWith(Path.GetFullPath(dir)+Path.DirectorySeparatorChar,
                Path.GetFullPath(result.Value!.Path),StringComparison.OrdinalIgnoreCase);
            Assert.False(OutputFileNamer.ReserveNewFile(new OutputNameRequest(dir,"x","../xlsx","run")).Success);
        }
        finally { Directory.Delete(dir,true); }
    }

    [Fact]
    public void ReservationFromAnotherRunCannotFinalizeOrRelease()
    {
        string dir=Path.Combine(Path.GetTempPath(),"dbtool-t34-owner-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var owned=OutputFileNamer.ReserveNewFile(new OutputNameRequest(dir,"Result",".xlsx","owner")).Value!;
            var foreign=new FileReservation(owned.Path,owned.ReservationPath,"other-run");
            string staging=Path.Combine(dir,"stage.xlsx");
            File.WriteAllBytes(staging,"NEW"u8.ToArray());
            var finalize=OutputFileNamer.FinalizeReservation(foreign,staging);
            Assert.False(finalize.Success); Assert.Null(finalize.Value);
            Assert.Equal("OUTPUT_RESERVATION_OWNER_MISMATCH",Assert.Single(finalize.Diagnostics).Code);
            Assert.False(File.Exists(owned.Path)); Assert.True(File.Exists(staging));
            var release=OutputFileNamer.ReleaseReservation(foreign);
            Assert.False(release.Success); Assert.True(File.Exists(owned.ReservationPath));
            Assert.True(OutputFileNamer.ReleaseReservation(owned).Success);
        }
        finally { Directory.Delete(dir,true); }
    }
}
