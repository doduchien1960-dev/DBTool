using DBTool.Domain;

namespace DBTool.Infrastructure.Excel;

public sealed record OutputNameRequest
{
    public OutputNameRequest(string outputDirectory, string displayName, string extension, string runId)
    { OutputDirectory=outputDirectory; DisplayName=displayName; Extension=extension; RunId=runId; }
    public string OutputDirectory { get; }
    public string DisplayName { get; }
    public string Extension { get; }
    public string RunId { get; }
}

public sealed record FileReservation
{
    public FileReservation(string path, string reservationPath, string runId)
    { Path=path; ReservationPath=reservationPath; RunId=runId; }
    public string Path { get; }
    public string ReservationPath { get; }
    public string RunId { get; }
}

public static class OutputFileNamer
{
    public static OperationResult<FileReservation> ReserveNewFile(OutputNameRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.OutputDirectory) ||
            string.IsNullOrWhiteSpace(request.RunId) || string.IsNullOrWhiteSpace(request.Extension) ||
            request.Extension[0] != '.' || request.Extension.Skip(1).Any(ch => !char.IsLetterOrDigit(ch)))
            return Block("OUTPUT_NAME_INVALID", "Output directory, extension or run ID is invalid.");
        try
        {
            string directory = System.IO.Path.GetFullPath(request.OutputDirectory);
            Directory.CreateDirectory(directory);
            string stem = Sanitize(request.DisplayName);
            string run = Sanitize(request.RunId);
            if (stem.Length > 80) stem = stem[..80];
            if (run.Length > 40) run = run[..40];
            string extension = request.Extension.ToLowerInvariant();
            for (int n = 1; n <= 9999; n++)
            {
                string suffix = n == 1 ? "" : $"-r{n:000}";
                string candidate = System.IO.Path.GetFullPath(System.IO.Path.Combine(directory,$"{stem}-{run}{suffix}{extension}"));
                if (!IsInside(candidate,directory)) return Block("OUTPUT_PATH_ESCAPE", "Output path escapes directory.");
                string reservationPath = candidate + ".reserve";
                if (File.Exists(candidate) || File.Exists(reservationPath)) continue;
                try
                {
                    using var stream = new FileStream(reservationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    using var writer = new StreamWriter(stream);
                    writer.Write(request.RunId);
                    return new(true,new FileReservation(candidate,reservationPath,request.RunId),[]);
                }
                catch (IOException) { continue; }
            }
            return Block("OUTPUT_NAME_EXHAUSTED", "No free output name is available.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return Block("OUTPUT_RESERVATION_FAILED", ex.Message); }
    }

    public static OperationResult<FileReservation> FinalizeReservation(FileReservation reservation, string stagingPath)
    {
        if (reservation is null || string.IsNullOrWhiteSpace(stagingPath) || !File.Exists(reservation.ReservationPath))
            return Block("OUTPUT_RESERVATION_MISSING", "Reservation or staging file is missing.");
        try
        {
            if (!OwnsReservation(reservation))
                return Block("OUTPUT_RESERVATION_OWNER_MISMATCH", "Reservation belongs to another run.");
            string destination = System.IO.Path.GetFullPath(reservation.Path);
            string directory = System.IO.Path.GetDirectoryName(destination)!;
            string staging = System.IO.Path.GetFullPath(stagingPath);
            if (!IsInside(staging,directory) || !IsInside(destination,directory) ||
                string.Equals(staging,destination,StringComparison.OrdinalIgnoreCase))
                return Block("OUTPUT_PATH_ESCAPE", "Staging and final paths must be distinct within output directory.");
            File.Move(staging,destination,overwrite:false);
            File.Delete(reservation.ReservationPath);
            return new(true,reservation,[]);
        }
        catch (IOException ex) { return Block("OUTPUT_COLLISION", ex.Message); }
        catch (UnauthorizedAccessException ex) { return Block("OUTPUT_WRITE_FAILED", ex.Message); }
    }

    public static OperationResult<FileReservation> ReleaseReservation(FileReservation reservation)
    {
        if (reservation is null || !File.Exists(reservation.ReservationPath))
            return Block("OUTPUT_RESERVATION_MISSING", "Reservation is missing.");
        try
        {
            if (!OwnsReservation(reservation))
                return Block("OUTPUT_RESERVATION_OWNER_MISMATCH", "Reservation belongs to another run.");
            File.Delete(reservation.ReservationPath);
            return new(true,reservation,[]);
        }
        catch (IOException ex) { return Block("OUTPUT_RELEASE_FAILED",ex.Message); }
        catch (UnauthorizedAccessException ex) { return Block("OUTPUT_RELEASE_FAILED",ex.Message); }
    }

    private static bool OwnsReservation(FileReservation reservation) =>
        File.ReadAllText(reservation.ReservationPath) == reservation.RunId;

    private static bool IsInside(string path,string directory) =>
        path.StartsWith(directory.TrimEnd(System.IO.Path.DirectorySeparatorChar)+System.IO.Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);

    private static string Sanitize(string? value)
    {
        var chars=(value ?? "").Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_').ToArray();
        string result = new string(chars).Trim('_','.', ' ');
        return result.Length == 0 ? "Output" : result;
    }

    private static OperationResult<FileReservation> Block(string code,string message) =>
        new(false,null,[new Diagnostic(code,DiagnosticSeverity.Block,DiagnosticScope.Global,null,null,null,null,null,message)]);
}
