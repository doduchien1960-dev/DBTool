using System.Collections.Immutable;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using ClosedXML.Excel;
using DBTool.Application;
using DBTool.Domain;

namespace DBTool.Infrastructure.Excel;

public enum SyncBucket
{
    BaseD10,BaseDOver10,WallD10,WallDOver10,RoofD10,RoofDOver10,
    NeckD10,NeckDOver10,CatchBasinD10,CatchBasinDOver10,
    PanelD10,PanelDOver10,SteelAngle
}

public sealed record SyncSummaryValue
{
    public SyncSummaryValue(SyncBucket bucket, double? massKg, CalculationStatus status)
    {
        Bucket=ContractGuard.EnumValue(bucket);
        Status=ContractGuard.EnumValue(status);
        if (status == CalculationStatus.Calculated ? massKg is null || !double.IsFinite(massKg.Value) || massKg.Value < 0 : massKg is not null)
            throw new ArgumentException("Calculated values need nonnegative mass; uncalculated values stay null.");
        MassKg=massKg;
    }
    public SyncBucket Bucket { get; }
    public double? MassKg { get; }
    public CalculationStatus Status { get; }
}

public sealed record SyncColumnContract
{
    private SyncColumnContract(IEnumerable<KeyValuePair<SyncBucket,int>> columns)
    { Columns=columns.ToImmutableDictionary(); }
    public ImmutableDictionary<SyncBucket,int> Columns { get; }
    public string RuleVersion => "T34-Sync-v1";
    public string Provenance => "VHGpro 3.2/Thep!A2:P3; three-profile header inspection";
    public static SyncColumnContract Vhg32() => new(Enum.GetValues<SyncBucket>()
        .Select((bucket,index) => new KeyValuePair<SyncBucket,int>(bucket,index+3)));
}

public sealed record SourceSyncRow
{
    public SourceSyncRow(string manholeId,string sourceRowId,Profile profile,string displayName,
        CalculationScope scope,IEnumerable<SyncSummaryValue> values)
    {
        ManholeId=ContractGuard.Text(manholeId,nameof(manholeId));
        SourceRowId=ContractGuard.Text(sourceRowId,nameof(sourceRowId));
        Profile=ContractGuard.EnumValue(profile);
        DisplayName=ContractGuard.Text(displayName,nameof(displayName));
        Scope=ContractGuard.EnumValue(scope);
        Values=ContractGuard.Copy(values);
        if (Values.Select(v=>v.Bucket).Distinct().Count()!=Values.Length)
            throw new ArgumentException("Duplicate sync bucket.");
    }
    public string ManholeId { get; }
    public string SourceRowId { get; }
    public Profile Profile { get; }
    public string DisplayName { get; }
    public CalculationScope Scope { get; }
    public ImmutableArray<SyncSummaryValue> Values { get; }
}

public sealed record SourceSyncRequest
{
    public SourceSyncRequest(string sourcePath,string destinationDirectory,IEnumerable<string> selectedIds,
        IEnumerable<SourceSyncRow> rows,SyncColumnContract columnContract,CalculationScope scope,
        Profile profile,string runId)
    {
        SourcePath=ContractGuard.Text(sourcePath,nameof(sourcePath));
        DestinationDirectory=ContractGuard.Text(destinationDirectory,nameof(destinationDirectory));
        SelectedIds=ContractGuard.Copy(selectedIds);
        Rows=ContractGuard.Copy(rows);
        ColumnContract=ContractGuard.Required(columnContract);
        Scope=ContractGuard.EnumValue(scope);
        Profile=ContractGuard.EnumValue(profile);
        RunId=ContractGuard.Text(runId,nameof(runId));
    }
    public string SourcePath { get; }
    public string DestinationDirectory { get; }
    public ImmutableArray<string> SelectedIds { get; }
    public ImmutableArray<SourceSyncRow> Rows { get; }
    public SyncColumnContract ColumnContract { get; }
    public CalculationScope Scope { get; }
    public Profile Profile { get; }
    public string RunId { get; }
}

public enum SourceSyncMode { PreserveWithExplicitStatus, RecalculateAggregate }

public sealed record SourceWorkbookSyncPackage
{
    public SourceWorkbookSyncPackage(string manholeId, Profile profile, string displayName,
        CalculationPackage package, IEnumerable<SyncSummaryValue> values)
    {
        ManholeId = ContractGuard.Text(manholeId, nameof(manholeId));
        Profile = ContractGuard.EnumValue(profile);
        DisplayName = ContractGuard.Text(displayName, nameof(displayName));
        Package = ContractGuard.Required(package);
        Values = ContractGuard.Copy(values);
        if (Values.Select(value => value.Bucket).Distinct().Count() != Values.Length)
            throw new ArgumentException("Duplicate sync bucket.");
    }
    public string ManholeId { get; }
    public Profile Profile { get; }
    public string DisplayName { get; }
    public CalculationPackage Package { get; }
    public ImmutableArray<SyncSummaryValue> Values { get; }
}

public sealed record SourceWorkbookCopyRequest
{
    public SourceWorkbookCopyRequest(string sourcePath, string expectedSourceHash, IEnumerable<string> selectedIds,
        string outputDirectory, string runId, SourceSyncMode syncMode, IEnumerable<SourceWorkbookSyncPackage> packages)
    {
        SourcePath = ContractGuard.Text(sourcePath, nameof(sourcePath));
        ExpectedSourceHash = ContractGuard.Hash(expectedSourceHash);
        SelectedIds = ContractGuard.Copy(selectedIds);
        OutputDirectory = ContractGuard.Text(outputDirectory, nameof(outputDirectory));
        RunId = ContractGuard.Text(runId, nameof(runId));
        SyncMode = ContractGuard.EnumValue(syncMode);
        Packages = ContractGuard.Copy(packages);
    }
    public string SourcePath { get; }
    public string ExpectedSourceHash { get; }
    public ImmutableArray<string> SelectedIds { get; }
    public string OutputDirectory { get; }
    public string RunId { get; }
    public SourceSyncMode SyncMode { get; }
    public ImmutableArray<SourceWorkbookSyncPackage> Packages { get; }
}

public sealed record SourceSyncStatusRow
{
    public SourceSyncStatusRow(string sourceRowId, string? manholeId, string status)
    {
        SourceRowId = ContractGuard.Text(sourceRowId, nameof(sourceRowId));
        ManholeId = manholeId;
        Status = ContractGuard.Text(status, nameof(status));
    }
    public string SourceRowId { get; }
    public string? ManholeId { get; }
    public string Status { get; }
}

public sealed record SourceWorkbookSyncResult
{
    public SourceWorkbookSyncResult(ExportArtifact artifact, IEnumerable<string> updatedIds,
        IEnumerable<SourceSyncStatusRow> excludedOrStale)
    {
        Artifact = ContractGuard.Required(artifact);
        UpdatedIds = ContractGuard.Copy(updatedIds);
        ExcludedOrStale = ContractGuard.Copy(excludedOrStale);
    }
    public ExportArtifact Artifact { get; }
    public ImmutableArray<string> UpdatedIds { get; }
    public ImmutableArray<SourceSyncStatusRow> ExcludedOrStale { get; }
}

public static class SourceWorkbookSynchronizer
{
    private const string StatusSheet = "DBTool.SyncStatus";
    private static readonly string[] Groups = ["Đáy","Tường","Trần","Cổ","Cửa thu","Nắp"];

    public static OperationResult<SourceWorkbookSyncResult> SyncToCopy(SourceWorkbookCopyRequest request) =>
        SyncToCopy(request, CancellationToken.None);

    public static OperationResult<SourceWorkbookSyncResult> SyncToCopy(SourceWorkbookCopyRequest request, CancellationToken token)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(request);
            token.ThrowIfCancellationRequested();
            if (request.SelectedIds.IsEmpty || request.SelectedIds.Distinct(StringComparer.Ordinal).Count() != request.SelectedIds.Length)
                return BlockFull("SYNC_ID_AMBIGUOUS", "Selected canonical IDs must be unique.");
            if (request.Packages.IsEmpty || request.Packages.Select(item => item.ManholeId).Distinct(StringComparer.Ordinal).Count() != request.Packages.Length)
                return BlockFull("SYNC_ID_AMBIGUOUS", "Full-scope package IDs must be unique.");
            if (!request.SelectedIds.ToHashSet(StringComparer.Ordinal).SetEquals(request.Packages.Select(item => item.ManholeId)))
                return BlockFull("SYNC_ID_AMBIGUOUS", "Each selected canonical ID needs exactly one package.");
            if (request.Packages.Any(item => item.Package.Scope != CalculationScope.Full ||
                item.Package.ConcreteStatus != CalculationStatus.Calculated ||
                item.Package.FormworkStatus != CalculationStatus.Calculated ||
                item.Package.ConcreteLines is null || item.Package.FormworkLines is null))
                return BlockFull("SOURCE_SYNC_SCOPE_INCOMPLETE", "Source sync requires full-scope packages with all quantity statuses calculated.");
            if (!File.Exists(request.SourcePath)) return BlockFull("SOURCE_WORKBOOK_MISSING", "Source workbook is missing.");
            var actualSourceHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(request.SourcePath)));
            if (!string.Equals(actualSourceHash, request.ExpectedSourceHash, StringComparison.OrdinalIgnoreCase))
                return BlockFull("SOURCE_HASH_MISMATCH", "Source bytes differ from the expected snapshot.");
            using (var archive = ZipFile.OpenRead(request.SourcePath))
                if (archive.GetEntry("[Content_Types].xml") is null)
                    return BlockFull("SYNC_SCHEMA_UNVERIFIED", "Source is not an OOXML workbook.");
            using var book = new XLWorkbook(request.SourcePath);
            if (!book.TryGetWorksheet("Thep", out var sheet) || !VerifiedHeader(sheet))
                return BlockFull("SYNC_SCHEMA_UNVERIFIED", "Thep A2:P3 header does not match the verified three-profile schema.");

            var sourceRows = ReadSourceRows(sheet);
            var status = ReadStatus(book);
            var mapping = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var item in request.Packages)
            {
                var matches = status.ById.Where(pair => pair.Value.Profile == item.Profile.ToString() &&
                        pair.Value.Name == item.DisplayName && sourceRows.ContainsKey(pair.Key) &&
                        string.Equals(pair.Value.ManholeId, item.ManholeId, StringComparison.Ordinal))
                    .Select(pair => pair.Key).ToArray();
                if (matches.Length == 0)
                    matches = sourceRows.Where(pair => string.Equals(pair.Value, item.DisplayName, StringComparison.Ordinal))
                        .Select(pair => pair.Key).ToArray();
                if (matches.Length != 1 || mapping.Values.Contains(matches[0]))
                    return BlockFull("SYNC_ID_AMBIGUOUS", $"Identity mapping is not unique: {item.ManholeId}");
                mapping[item.ManholeId] = matches[0];
                if (item.Values.Length != SyncColumnContract.Vhg32().Columns.Count ||
                    item.Values.Any(value => value.Status != CalculationStatus.Calculated) ||
                    !item.Values.Select(value => value.Bucket).ToHashSet().SetEquals(SyncColumnContract.Vhg32().Columns.Keys))
                    return BlockFull("SOURCE_SYNC_SCOPE_INCOMPLETE", $"Package summary is incomplete: {item.ManholeId}");
            }

            var selectedRows = mapping.Values.ToHashSet();
            var unselected = new List<SourceSyncStatusRow>();
            var allUnselectedKnown = true;
            foreach (var item in request.Packages)
            {
                var rowNumber = mapping[item.ManholeId];
                foreach (var value in item.Values)
                    sheet.Cell(rowNumber, SyncColumnContract.Vhg32().Columns[value.Bucket]).Value = value.MassKg!.Value;
                sheet.Cell(rowNumber, 16).Value = item.Values.Sum(value => value.MassKg!.Value);
            }
            foreach (var pair in sourceRows)
            {
                if (selectedRows.Contains(pair.Key)) continue;
                var previous = status.ByRow.GetValueOrDefault(pair.Key);
                var known = previous is null
                    ? RowHasFiniteValues(sheet, pair.Key)
                    : previous.Status is "KnownValid" or "Calculated";
                var statusText = request.SyncMode == SourceSyncMode.PreserveWithExplicitStatus
                    ? "StaleNotRecalculated"
                    : known ? "KnownValid" : "Review";
                if (statusText != "KnownValid") allUnselectedKnown = false;
                unselected.Add(new SourceSyncStatusRow(pair.Key.ToString(CultureInfo.InvariantCulture),
                    previous?.ManholeId, statusText));
            }
            var aggregateStatus = request.SyncMode == SourceSyncMode.RecalculateAggregate && allUnselectedKnown
                ? "AggregateVerified" : "AggregateUnavailable";
            double? aggregateTotal = null;
            if (aggregateStatus == "AggregateVerified")
            {
                aggregateTotal = sourceRows.Keys.Sum(row => Enumerable.Range(3, SyncColumnContract.Vhg32().Columns.Count).Sum(column => NumericValue(sheet.Cell(row, column))));
            }

            if (book.TryGetWorksheet(StatusSheet, out var oldStatus)) book.Worksheets.Delete(oldStatus.Name);
            var statusSheet = book.AddWorksheet(StatusSheet);
            statusSheet.Cell("A1").Value = "OverallTotalsNotValidated";
            statusSheet.Cell("A2").Value = "AggregateStatus"; statusSheet.Cell("B2").Value = aggregateStatus;
            statusSheet.Cell("A3").Value = "AggregateTotal"; statusSheet.Cell("B3").Value = aggregateTotal;
            statusSheet.Cell("A5").Value = "SourceRow"; statusSheet.Cell("B5").Value = "ManholeId";
            statusSheet.Cell("C5").Value = "Profile"; statusSheet.Cell("D5").Value = "Name";
            statusSheet.Cell("E5").Value = "Status"; statusSheet.Cell("F5").Value = "SourceRowId";
            foreach (var pair in sourceRows.OrderBy(pair => pair.Key))
            {
                var selected = request.Packages.SingleOrDefault(item => mapping[item.ManholeId] == pair.Key);
                var previous = status.ByRow.GetValueOrDefault(pair.Key);
                var row = pair.Key + 1;
                statusSheet.Cell(row, 1).Value = pair.Key;
                statusSheet.Cell(row, 2).Value = selected?.ManholeId ?? previous?.ManholeId ?? "";
                statusSheet.Cell(row, 3).Value = selected?.Profile.ToString() ?? previous?.Profile.ToString() ?? "";
                statusSheet.Cell(row, 4).Value = pair.Value;
                statusSheet.Cell(row, 5).Value = selected is not null ? "Calculated" :
                    unselected.Single(item => item.SourceRowId == pair.Key.ToString(CultureInfo.InvariantCulture)).Status;
                statusSheet.Cell(row, 6).Value = selected is not null
                    ? pair.Key.ToString(CultureInfo.InvariantCulture)
                    : previous?.SourceRow ?? "";
            }
            token.ThrowIfCancellationRequested();
            var reserved = OutputFileNamer.ReserveNewFile(new OutputNameRequest(request.OutputDirectory,
                Path.GetFileNameWithoutExtension(request.SourcePath) + "-sync", Path.GetExtension(request.SourcePath), request.RunId));
            if (!reserved.Success) return new(false, null, reserved.Diagnostics);
            var reservation = reserved.Value!;
            var staging = Path.Combine(Path.GetDirectoryName(reservation.Path)!,
                $".{Path.GetFileNameWithoutExtension(reservation.Path)}.{Guid.NewGuid():N}.staging{Path.GetExtension(reservation.Path)}");
            try
            {
                token.ThrowIfCancellationRequested();
                book.SaveAs(staging);
                token.ThrowIfCancellationRequested();
                var finalized = OutputFileNamer.FinalizeReservation(reservation, staging);
                if (!finalized.Success) return new(false, null, finalized.Diagnostics);
                var artifact = new ExportArtifact(reservation.Path, actualSourceHash, "T34-Sync-v2", CalculationScope.Full, RecalculationStatus.NotRun);
                return new(true, new SourceWorkbookSyncResult(artifact, request.Packages.Select(item => item.ManholeId), unselected), []);
            }
            catch (OperationCanceledException ex) { return BlockFull("OPERATION_CANCELLED", ex.Message); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
            { return BlockFull("SOURCE_SYNC_FAILED", ex.Message); }
            finally
            {
                if (File.Exists(staging)) File.Delete(staging);
                if (File.Exists(reservation.ReservationPath)) OutputFileNamer.ReleaseReservation(reservation);
            }
        }
        catch (OperationCanceledException) { return BlockFull("OPERATION_CANCELLED", "Sync cancelled before finalize."); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException)
        { return BlockFull("SOURCE_SYNC_FAILED", ex.Message); }
    }

    public static OperationResult<ExportArtifact> SyncToCopy(SourceSyncRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Scope != CalculationScope.Full || request.Rows.Any(r => r.Scope != CalculationScope.Full))
            return Block("SOURCE_SYNC_SCOPE_INCOMPLETE", "Only a fully calculated row may update all Thep summary buckets.");
        if (Path.GetFullPath(request.SourcePath).Equals(Path.GetFullPath(request.DestinationDirectory),StringComparison.OrdinalIgnoreCase))
            return Block("SOURCE_EQUALS_DESTINATION", "Source file and destination must differ.");
        if (!File.Exists(request.SourcePath)) return Block("SOURCE_WORKBOOK_MISSING", "Source workbook is missing.");
        if (request.SelectedIds.IsEmpty || request.SelectedIds.Distinct(StringComparer.Ordinal).Count()!=request.SelectedIds.Length ||
            request.Rows.Select(r=>r.ManholeId).Distinct(StringComparer.Ordinal).Count()!=request.Rows.Length)
            return Block("SYNC_ID_AMBIGUOUS", "Selected and supplied IDs must be unique.");
        var selected=request.SelectedIds.ToHashSet(StringComparer.Ordinal);
        if (!selected.SetEquals(request.Rows.Select(r=>r.ManholeId)))
            return Block("SYNC_ID_AMBIGUOUS", "Each selected ID needs exactly one summary row.");
        foreach (var row in request.Rows)
            if (row.Profile != request.Profile || row.Values.Length != request.ColumnContract.Columns.Count ||
                row.Values.Any(v=>v.Status != CalculationStatus.Calculated) ||
                !row.Values.Select(v=>v.Bucket).ToHashSet().SetEquals(request.ColumnContract.Columns.Keys))
                return Block("SOURCE_SYNC_SCOPE_INCOMPLETE", "Selected row has uncalculated or missing groups.");

        try
        {
            string sourceHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(request.SourcePath)));
            // Reject non-OOXML input before ClosedXML opens it; malformed archives may hold a handle on failure.
            using (var archive=ZipFile.OpenRead(request.SourcePath))
                if (archive.GetEntry("[Content_Types].xml") is null)
                    return Block("SYNC_SCHEMA_UNVERIFIED", "Source is not an OOXML workbook.");
            using var book=new XLWorkbook(request.SourcePath);
            if (!book.TryGetWorksheet("Thep",out var sheet) || !VerifiedHeader(sheet))
                return Block("SYNC_SCHEMA_UNVERIFIED", "Thep A2:P3 header does not match the verified three-profile schema.");
            var sourceRows=new Dictionary<int,string>();
            int last=sheet.LastRowUsed(XLCellsUsedOptions.Contents)?.RowNumber() ?? 4;
            for(int number=5;number<=last;number++)
            {
                var cell=sheet.Cell(number,2);
                string name=cell.HasFormula ? cell.CachedValue.ToString() : cell.GetString();
                if (!string.IsNullOrWhiteSpace(name)) sourceRows[number]=name.Trim();
            }
            var existingIds=new Dictionary<int,string>();
            if (book.TryGetWorksheet(StatusSheet,out var previous))
                for(int metadataRow=3;metadataRow<=(previous.LastRowUsed(XLCellsUsedOptions.Contents)?.RowNumber() ?? 2);metadataRow++)
                {
                    if (!int.TryParse(previous.Cell(metadataRow,1).GetString(),out int number) ||
                        !sourceRows.TryGetValue(number,out string? currentName) ||
                        !string.Equals(previous.Cell(metadataRow,3).GetString(),request.Profile.ToString(),StringComparison.Ordinal) ||
                        !string.Equals(previous.Cell(metadataRow,4).GetString(),currentName,StringComparison.Ordinal))
                        continue;
                    string id=previous.Cell(metadataRow,2).GetString();
                    if (!string.IsNullOrWhiteSpace(id)) existingIds[number]=id;
                }
            var mapping=new Dictionary<string,int>(StringComparer.Ordinal);
            foreach(var row in request.Rows)
            {
                int[] matches=existingIds.Where(x=>x.Value==row.ManholeId).Select(x=>x.Key).ToArray();
                if(matches.Length==0) matches=sourceRows.Where(x=>x.Value==row.DisplayName).Select(x=>x.Key).ToArray();
                if(matches.Length!=1 || mapping.Values.Contains(matches[0]))
                    return Block("SYNC_ID_AMBIGUOUS", $"ID/name mapping is not unique: {row.ManholeId}");
                mapping[row.ManholeId]=matches[0];
            }
            var reserved=OutputFileNamer.ReserveNewFile(new OutputNameRequest(request.DestinationDirectory,
                Path.GetFileNameWithoutExtension(request.SourcePath)+"-sync",Path.GetExtension(request.SourcePath),request.RunId));
            if (!reserved.Success) return new(false,null,reserved.Diagnostics);
            var reservation=reserved.Value!;
            string staging=Path.Combine(Path.GetDirectoryName(reservation.Path)!,
                Path.GetFileNameWithoutExtension(reservation.Path)+".staging"+Path.GetExtension(reservation.Path));
            try
            {
                foreach(var row in request.Rows)
                {
                    int number=mapping[row.ManholeId];
                    foreach(var value in row.Values)
                        sheet.Cell(number,request.ColumnContract.Columns[value.Bucket]).Value=value.MassKg!.Value;
                    sheet.Cell(number,16).Value=row.Values.Sum(v=>v.MassKg!.Value);
                }
                if(book.TryGetWorksheet(StatusSheet,out var oldStatus)) book.Worksheets.Delete(oldStatus.Name);
                var status=book.AddWorksheet(StatusSheet);
                status.Cell("A1").Value="OverallTotalsNotValidated";
                status.Cell("A2").Value="SourceRow"; status.Cell("B2").Value="ManholeId";
                status.Cell("C2").Value="Profile"; status.Cell("D2").Value="Name";
                status.Cell("E2").Value="Status"; status.Cell("F2").Value="SourceRowId";
                foreach(var (number,name) in sourceRows.OrderBy(x=>x.Key))
                {
                    int metadataRow=number-2;
                    var selectedRow=request.Rows.SingleOrDefault(r=>mapping[r.ManholeId]==number);
                    status.Cell(metadataRow,1).Value=number;
                    status.Cell(metadataRow,2).Value=selectedRow?.ManholeId ?? existingIds.GetValueOrDefault(number) ?? "";
                    status.Cell(metadataRow,3).Value=request.Profile.ToString();
                    status.Cell(metadataRow,4).Value=name;
                    status.Cell(metadataRow,5).Value=selectedRow is null ? "StaleNotRecalculated" : "Calculated";
                    status.Cell(metadataRow,6).Value=selectedRow?.SourceRowId ?? "";
                }
                book.SaveAs(staging);
                var finalized=OutputFileNamer.FinalizeReservation(reservation,staging);
                if (!finalized.Success) return new(false,null,finalized.Diagnostics);
                return new(true,new ExportArtifact(reservation.Path,sourceHash,request.ColumnContract.RuleVersion,
                    request.Scope,RecalculationStatus.NotRun),[]);
            }
            finally
            {
                if(File.Exists(staging)) File.Delete(staging);
                if(File.Exists(reservation.ReservationPath)) OutputFileNamer.ReleaseReservation(reservation);
            }
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException)
        { return Block("SOURCE_SYNC_FAILED",ex.Message); }
    }

    private static bool VerifiedHeader(IXLWorksheet sheet)
    {
        if(sheet.Cell("A2").GetString()!="TT" || sheet.Cell("B2").GetString()!="Hố ga" ||
            sheet.Cell("O2").GetString()!="Thép hình" || sheet.Cell("P2").GetString()!="Tổng") return false;
        for(int i=0;i<6;i++)
        {
            int column=3+2*i;
            if(sheet.Cell(2,column).GetString()!=Groups[i] ||
                sheet.Cell(3,column).GetString()!="D ≤ 10" ||
                sheet.Cell(3,column+1).GetString()!="D > 10") return false;
        }
        return true;
    }

    private static Dictionary<int, string> ReadSourceRows(IXLWorksheet sheet)
    {
        var rows = new Dictionary<int, string>();
        var last = sheet.LastRowUsed(XLCellsUsedOptions.Contents)?.RowNumber() ?? 4;
        for (var row = 5; row <= last; row++)
        {
            var cell = sheet.Cell(row, 2);
            var name = cell.HasFormula ? cell.CachedValue.ToString() : cell.GetString();
            if (!string.IsNullOrWhiteSpace(name)) rows[row] = name.Trim();
        }
        return rows;
    }

    private static StatusSnapshot ReadStatus(XLWorkbook book)
    {
        var snapshot = new StatusSnapshot();
        if (!book.TryGetWorksheet(StatusSheet, out var sheet)) return snapshot;
        var last = sheet.LastRowUsed(XLCellsUsedOptions.Contents)?.RowNumber() ?? 0;
        for (var row = 1; row <= last; row++)
        {
            if (!int.TryParse(sheet.Cell(row, 1).GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var sourceRowId))
                continue;
            var manholeId = sheet.Cell(row, 2).GetString().Trim();
            var profile = sheet.Cell(row, 3).GetString().Trim();
            var name = sheet.Cell(row, 4).GetString().Trim();
            var status = sheet.Cell(row, 5).GetString().Trim();
            var sourceRow = sheet.Cell(row, 6).GetString().Trim();
            if (string.IsNullOrWhiteSpace(sourceRow)) sourceRow = sourceRowId.ToString(CultureInfo.InvariantCulture);
            var entry = new StatusEntry(sourceRowId, manholeId, profile, name, status, sourceRow);
            snapshot.ByRow[sourceRowId] = entry;
            if (!string.IsNullOrWhiteSpace(manholeId)) snapshot.ById[sourceRowId] = entry;
        }
        return snapshot;
    }

    private static bool RowHasFiniteValues(IXLWorksheet sheet, int row)
    {
        for (var column = 3; column <= 2 + SyncColumnContract.Vhg32().Columns.Count; column++)
        {
            var cell = sheet.Cell(row, column);
            if (cell.IsEmpty()) continue;
            if (!cell.TryGetValue<double>(out var value) || !double.IsFinite(value)) return false;
        }
        return true;
    }

    private static double NumericValue(IXLCell cell) =>
        cell.IsEmpty() ? 0 : cell.TryGetValue<double>(out var value) && double.IsFinite(value) ? value : 0;

    private sealed class StatusSnapshot
    {
        public Dictionary<int, StatusEntry> ByRow { get; } = new();
        public Dictionary<int, StatusEntry> ById { get; } = new();
    }

    private sealed record StatusEntry(int SourceRowId, string ManholeId, string Profile, string Name, string Status, string SourceRow);

    private static OperationResult<SourceWorkbookSyncResult> BlockFull(string code, string message) =>
        new(false, null, [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global, null, null, null, null, null, message)]);

    private static OperationResult<ExportArtifact> Block(string code,string message) =>
        new(false,null,[new Diagnostic(code,DiagnosticSeverity.Block,DiagnosticScope.Global,null,null,null,null,null,message)]);
}
